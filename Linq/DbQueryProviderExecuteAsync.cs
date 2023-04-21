﻿using ORM_1_21_.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ORM_1_21_.Linq
{
    internal partial class DbQueryProvider<T>
    {
        public async Task<object> ExecuteAsync<TS>(Expression expression, object[] param, CancellationToken cancellationToken)
        {
            var tt = typeof(TS);
            var tk = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration? registration = null;

            bool isCacheUsage = CacheState == CacheState.CacheUsage || CacheState == CacheState.CacheOver || CacheState == CacheState.CacheKey;
            var services = (IServiceSessions)Sessione;
            var re = TranslateE(expression);
            string sql = re.Sql;
            var paramJon = re.Param;
            List<OneComposite> listCore = re.Composites;
            listCore.AddRange(ListOuterOneComposites);

            /*usage cache*/

            var hashCode = -1;
            if (isCacheUsage)
            {
                var b = new StringBuilder(sql);
                foreach (var p in paramJon)
                {
                    b.Append($" {p.Key} - {p.Value} ");
                }

                if (_paramFree.Any())
                {
                    UtilsCore.AddParamsForCache(b, sql, _providerName, _paramFree);
                }

                foreach (var p in _paramFreeStoredPr)
                {
                    b.Append($" {p.Name} - {p.Value} -{p.Value} ");
                }

                var str = b.ToString();
                hashCode = str.GetHashCode();
                if (CacheState == CacheState.CacheKey)
                {

                    return await Task.FromResult(hashCode);
                }
                if (CacheState == CacheState.CacheOver)
                {
                    MyCache<T>.DeleteKey(hashCode);
                    MySqlLogger.Info($"Delete Cache key:{hashCode}");
                }
                var r = MyCache<T>.GetValue(hashCode);
                if (r != null)
                {
                    MySqlLogger.Info($"CACHE: {str}");
                    return await Task.FromResult(r);

                }


            }
            /*usage cache*/


            var com = services.CommandForLinq;

            var to = GetTimeout();
            if (to >= 0)
            {
                com.CommandTimeout = to;
            }


            if (_isStoredPr)
                com.CommandType = CommandType.StoredProcedure;

            com.CommandText = sql;
            var sb = new StringBuilder();
            if (_providerName == ProviderName.MsSql)
            {
                var mat = new Regex(@"TOP\s@p\d").Matches(com.CommandText);
                foreach (var variable in mat)
                {
                    var st = variable.ToString().Split(' ')[1];
                    var val = paramJon.FirstOrDefault(a => a.Key == st).Value;
                    com.CommandText = com.CommandText.Replace(variable.ToString(),
                        string.Format("{1} ({0})", val, StringConst.Top));
                    paramJon.Remove(st);
                }
            }

            com.Parameters.Clear();


            foreach (var p in paramJon)
            {
                sb.Append(string.Format(CultureInfo.CurrentCulture, "{0}-{1},", p.Key, p.Value));
                IDataParameter pr = com.CreateParameter();
                pr.ParameterName = p.Key;
                pr.Value = p.Value;
                com.Parameters.Add(pr);
            }

            if (_paramFree.Any())
            {
                UtilsCore.AddParam(com, _providerName, _paramFree.ToArray());
            }


            foreach (var p in _paramFreeStoredPr)
            {
                IDataParameter pr = com.CreateParameter();
                pr.Direction = p.Direction;
                pr.ParameterName = p.Name;
                pr.Value = p.Value;
                com.Parameters.Add(pr);
            }


            IDataReader dataReader = null;
            try
            {
                await _session.OpenConnectAndTransactionAsync(com);

                if (cancellationToken != default)
                {
                    registration = cancellationToken.Register(UtilsCore.CancelRegistr(com, cancellationToken, _session.Transactionale, _providerName));
                }

                if (PingCompositeE(Evolution.All, listCore))
                {
                    var reader = await com.ExecuteReaderAsync();
                    try
                    {
                        while (reader.Read())
                        {
                            var v1 = reader.GetInt32(0);
                            var v2 = reader.GetInt32(1);
                            var res = v1 == v2;
                            if (isCacheUsage)
                            {
                                MyCache<T>.Push(hashCode, res);
                            }
                            tk.SetResult(res);
                            return await tk.Task;
                        }
                    }
                    finally
                    {
                        reader.Dispose();
                    }

                }

                if (PingCompositeE(Evolution.LongCount, listCore))
                {
                    var ee = await com.ExecuteScalarAsync();
                    long res = Convert.ToInt64(ee, CultureInfo.CurrentCulture);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    tk.SetResult(res);
                    return await tk.Task;
                }

                if (PingCompositeE(Evolution.Count, listCore))
                {
                    var ee = await com.ExecuteScalarAsync();
                    var res = Convert.ToInt32(ee, CultureInfo.CurrentCulture);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    tk.SetResult(res);
                    return await tk.Task;
                }


                if (PingCompositeE(Evolution.Delete, listCore))
                {
                    var res = await com.ExecuteNonQueryAsync();
                    MyCache<T>.Clear();
                    tk.SetResult(res);
                    return await tk.Task;
                }

                if (PingCompositeE(Evolution.Update, listCore))
                {
                    var res = await com.ExecuteNonQueryAsync();
                    MyCache<T>.Clear();
                    tk.SetResult(res);
                    return await tk.Task;
                }

                if (PingCompositeE(Evolution.Any, listCore))
                {
                    var ee = await com.ExecuteScalarAsync();
                    var res = Convert.ToInt32(ee, CultureInfo.CurrentCulture) != 0;
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    tk.SetResult(res);
                    return await tk.Task;
                }



                #region dataReader

                if (PingCompositeE(Evolution.TableCreate, listCore))
                {
                    if (UtilsCore.IsValid<T>() == false)
                    {
                        throw new Exception(
                            $"I can't create a table from type {typeof(T)}, it doesn't have attrubute: MapTableAttribute");
                    }

                    com.CommandText = listCore.First(a => a.Operand == Evolution.TableCreate).Body;
                    int res = await com.ExecuteNonQueryAsync();
                    tk.SetResult(res);
                    return await tk.Task;

                }

                if (PingCompositeE(Evolution.DropTable, listCore))
                {
                    com.CommandText = listCore.First(a => a.Operand == Evolution.DropTable).Body;
                    int res = await com.ExecuteNonQueryAsync();
                    tk.SetResult(res);
                    return await tk.Task;

                }

                if (PingCompositeE(Evolution.DataTable, listCore))
                {
                    com.CommandText = listCore.First(a => a.Operand == Evolution.DataTable).Body;
                    var table = new DataTable();
                    var reader = await com.ExecuteReaderAsync();
                    table.BeginLoadData();
                    table.Load(reader);
                    table.EndLoadData();
                    tk.SetResult(table);
                    return await tk.Task;

                }

                if (PingCompositeE(Evolution.ExecuteNonQuery, listCore))
                {
                    com.CommandText = listCore.First(a => a.Operand == Evolution.ExecuteNonQuery).Body;
                    int res = await com.ExecuteNonQueryAsync();
                    tk.SetResult(res);
                    return await tk.Task;
                }


                if (PingCompositeE(Evolution.TruncateTable, listCore))
                {
                    com.CommandText = listCore.First(a => a.Operand == Evolution.TruncateTable).Body;
                    int res = await com.ExecuteNonQueryAsync();
                    tk.SetResult(res);
                    return await tk.Task;
                }

                if (PingCompositeE(Evolution.ExecuteScalar, listCore))
                {

                    com.CommandText = listCore.First(a => a.Operand == Evolution.ExecuteScalar).Body;
                    object res = await com.ExecuteScalarAsync();
                    tk.SetResult(res);
                    return await tk.Task;

                }


                if (PingCompositeE(Evolution.TableExists, listCore))
                {
                    com.CommandText = listCore.First(a => a.Operand == Evolution.TableExists).Body;
                    if (_providerName == ProviderName.PostgreSql)
                    {
                        var r = (long)await com.ExecuteScalarAsync();
                        var res = r != 0;
                        tk.SetResult(res);
                        return await tk.Task;
                    }

                    if (_providerName == ProviderName.MsSql)
                    {
                        var r = await com.ExecuteScalarAsync();
                        var res = !(r is DBNull);
                        tk.SetResult(res);
                        return await tk.Task;
                    }
                    else
                    {
                        try
                        {
                            await com.ExecuteNonQueryAsync();
                            tk.SetResult(true);
                            return await tk.Task;
                        }
                        catch (Exception)
                        {
                            tk.SetResult(false);
                            return await tk.Task;
                        }
                    }

                }

                if (PingCompositeE(Evolution.FreeSql, listCore) &&
                    UtilsCore.IsValid<TS>() == false)
                {
                    dataReader = await com.ExecuteReaderAsync();
                    var count = dataReader.FieldCount;
                    var list = new List<Type>();
                    for (var i = 0; i < count; i++) list.Add(dataReader.GetFieldType(i));
                    var resDis = new List<TS>();
                    if (UtilsCore.IsAnonymousType(typeof(TS)))
                    {
                        var ci = typeof(TS).GetConstructor(list.ToArray());
                        if (ci == null)
                        {
                            throw new Exception($"Can't find constructor for anonymous type: {typeof(TS).Name}");
                        }

                        while (dataReader.Read())
                        {
                            var par = new List<object>();
                            for (var i = 0; i < count; i++)
                            {
                                var val = Pizdaticus.MethodFreeIndex(_providerName, list[i], dataReader,i);
                                par.Add(val);
                            }
                            var e = ci.Invoke(par.ToArray());
                            resDis.Add((TS)e);
                        }

                    }
                    else if (UtilsCore.IsReceiverFreeSql<TS>())
                    {
                        var c = typeof(TS).GetConstructors();
                        if (c.Length == 0)
                        {
                            throw new Exception(
                                $"The type {typeof(TS)} is marked with an attribute but does not have a constructor");
                        }

                        if (c.Length > 1)
                        {
                            throw new Exception(
                                $"The type {typeof(TS)} is marked with an attribute. Must have only one constructor");
                        }

                        var ci = c[0];

                        if (ci.GetParameters().Length != list.Count)
                        {
                            throw new Exception(
                                $"The number of parameters of the constructor method:{ci.GetParameters().Length}  is not equal to the number of" +
                                $" fields retrieved from the database: {list.Count}, check sql the query");
                        }

                        while (dataReader.Read())
                        {
                            var par = new List<object>();
                            for (var i = 0; i < count; i++)
                            {
                                var val = Pizdaticus.MethodFreeIndex(_providerName, ci.GetParameters()[i].ParameterType,
                                    dataReader,i);
                                par.Add(val);
                            }

                            var e = ci.Invoke(par.ToArray());
                            resDis.Add((TS)e);
                        }

                    }

                    else if (count == 1 && typeof(TS).IsValueType || typeof(TS) == typeof(string) ||
                             typeof(TS).GetInterface("IEnumerable") != null || typeof(TS).IsGenericTypeDefinition)
                    {
                        while (dataReader.Read())
                            resDis.Add((TS)Pizdaticus.MethodFreeIndex(_providerName, typeof(TS), dataReader,0));
                    }
                    else
                    {
                        if (typeof(TS) != typeof(object) && typeof(TS).IsClass)
                        {
                            object employee;
                            var isLegalese = AttributesOfClass<TS>.IsUsageActivator(_providerName);
                            if (isLegalese)
                            {
                                employee = Activator.CreateInstance<TS>();
                            }
                            else
                            {
                                employee = (TS)FormatterServices.GetSafeUninitializedObject(typeof(TS));
                            }

                            while (dataReader.Read())
                            {
                                for (var i = 0; i < dataReader.FieldCount; i++)
                                    AttributesOfClass<TS>.SetValueFreeSqlE(_providerName, dataReader.GetName(i),
                                        (TS)employee,
                                        dataReader[i] == DBNull.Value ? null : dataReader[i]);
                                resDis.Add((TS)employee);
                            }
                        }
                        else
                        {
                            while (dataReader.Read())
                            {
                                dynamic employee = new ExpandoObject();
                                for (var i = 0; i < count; i++)
                                    ((IDictionary<string, object>)employee).Add(dataReader.GetName(i),
                                        dataReader[i] == DBNull.Value ? null : dataReader[i]);
                                resDis.Add((TS)employee);
                            }
                        }
                    }

                    dataReader.Dispose();
                    foreach (var par in com.Parameters)
                        if (((IDataParameter)par).Direction == ParameterDirection.InputOutput ||
                            ((IDataParameter)par).Direction == ParameterDirection.Output ||
                            ((IDataParameter)par).Direction == ParameterDirection.ReturnValue)
                            _parOut.Add(((IDataParameter)par).ParameterName, ((IDataParameter)par).Value);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, resDis);
                    }

                    tk.SetResult(resDis);
                    return await tk.Task;
                }

                if (listCore.Any(a => a.Operand == Evolution.Select && a.IsAggregate))
                {
                    dataReader = await com.ExecuteReaderAsync();
                    object rObj = null;
                    while (dataReader.Read())
                    {
                        rObj = dataReader[0];
                        break;
                    }


                    dataReader.Dispose();
                    var res = UtilsCore.Convertor<TS>(rObj);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    tk.SetResult(res);
                    return await tk.Task;
                }



                // if (PingCompositeE(Evolution.Join, listCore))
                // {
                //     dataReader = await _com.ExecuteReaderAsync();
                //     var res = new List<TS>();
                //     var ss = listCore.Single(a => a.Operand == Evolution.Join).NewConstructor;
                //     if (ss == null)
                //         while (dataReader.Read())
                //             res.Add((TS)dataReader[0]);
                //     else
                //         res = Pizdaticus.GetListAnonymousObj<TS>(dataReader, ss, _providerName);
                //
                //     if (isCacheUsage)
                //     {
                //         MyCache<T>.Push(hashCode, res);
                //     }
                //
                //     tk.SetResult(res);
                //     return await tk.Task;
                // }


                if (PingCompositeE(Evolution.Select, listCore) &&
                    !PingCompositeE(Evolution.SelectNew, listCore))
                {
                    var type = typeof(TS);
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var ttType = typeof(TS).GenericTypeArguments[0];
                        var lees = new List<object>();
                        dataReader = await com.ExecuteReaderAsync();
                        while (dataReader.Read()) lees.Add(UtilsCore.Convertor(dataReader[0], ttType));
                        dataReader.Dispose();

                        var listNativeInvoke = DbHelp.CastList(lees);
                        var devastatingly1 = Pizdaticus.SingleData(listCore, lees, out var active1);
                        var res = !active1 ? listNativeInvoke : devastatingly1;
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, res);
                        }

                        return res;

                    }
                    else
                    {
                        var lees = new List<TS>();
                        dataReader = await com.ExecuteReaderAsync();
                        while (dataReader.Read()) lees.Add((TS)UtilsCore.Convertor<TS>(dataReader[0]));
                        dataReader.Dispose();
                        var devastatingly1 = Pizdaticus.SingleData(listCore, lees, out var active1);
                        var res = !active1 ? (object)lees : devastatingly1;
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, res);
                        }
                        return res;
                    }

                }

                if (PingCompositeE(Evolution.ElementAt, listCore))
                {
                    dataReader = await com.ExecuteReaderAsync();
                    var r = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                    var enumerable = r as T[] ?? r.ToArray();
                    if (enumerable.Any())
                    {
                        var res = enumerable.First();
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, res);
                        }

                        tk.SetResult(res);
                        return await tk.Task;
                    }

                    throw new Exception("Element not in selection.");
                }

                if (PingCompositeE(Evolution.ElementAtOrDefault, listCore))
                {
                    dataReader = await com.ExecuteReaderAsync();
                    var r = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                    var enumerable = r as T[] ?? r.ToArray();

                    if (enumerable.Any())
                    {
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, enumerable.First());
                        }

                        var res = enumerable.First();
                        tk.SetResult(res);
                        return await tk.Task;
                    }

                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, null);
                    }


                    tk.SetResult(enumerable.FirstOrDefault());
                    return await tk.Task;

                }

                if (PingCompositeE(Evolution.DistinctCore, listCore))
                {

                    var sas = ListCastExpression.Single(a => a.TypeEvolution == Evolution.DistinctCore);
                    IList resT = sas.ListDistinct;
                    dataReader = await com.ExecuteReaderAsync();
                    if (PingCompositeE(Evolution.SelectNew, listCore))
                    {
                        var ss = listCore.Single(a => a.Operand == Evolution.SelectNew).NewConstructor;
                        Pizdaticus.GetListAnonymousObjDistinct(dataReader, ss, resT, _providerName);

                        tk.SetResult(resT);
                        return await tk.Task;

                    }
                    else
                    {
                        var resDis = resT;
                        while (dataReader.Read())
                        {
                            var val = Pizdaticus.MethodFree(_providerName, sas.TypeReturn, dataReader[0]);
                            resDis.Add(val);
                        }

                        dataReader.Dispose();
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, resDis);
                        }

                        tk.SetResult(resDis);
                        return await tk.Task;
                    }

                }

                if (PingCompositeE(Evolution.SelectNew, listCore))
                {
                    //todo ion100
                    var ss = listCore.Single(a => a.Operand == Evolution.SelectNew).NewConstructor;
                    com.CommandText = com.CommandText.Replace(",?p", "?p");
                    dataReader = com.ExecuteReader();
                    var type = typeof(TS);
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var ttType = typeof(TS).GenericTypeArguments[0];
                        if (UtilsCore.IsAnonymousType(ttType))
                        {
                            var lRes = Pizdaticus.GetListAnonymousObj<object>(dataReader, ss, _providerName);
                            var listNativeInvoke = DbHelp.CastList(lRes);

                            var dataSing1 = Pizdaticus.SingleData(listCore, lRes, out var isaActive1);

                            var res = !isaActive1 ? listNativeInvoke : dataSing1;
                            if (isCacheUsage)
                            {
                                MyCache<T>.Push(hashCode, res);
                            }
                            return res;
                        }
                        else
                        {
                            throw new Exception($"Method Select for IQueryable is not implemented, use method SelectCore or ...toList().Select()");

                        }
                    }
                    else
                    {
                        if (UtilsCore.IsAnonymousType(typeof(TS)))
                        {
                            var lRes = Pizdaticus.GetListAnonymousObj<TS>(dataReader, ss, _providerName);
                            var dataSing1 = Pizdaticus.SingleData(listCore, lRes, out var isaActive1);

                            var res = !isaActive1 ? (object)lRes : dataSing1;
                            if (isCacheUsage)
                            {
                                MyCache<T>.Push(hashCode, res);
                            }
                            return res;
                        }
                        else
                        {
                            throw new Exception($"Method Select for IQueryable is not implemented, use method SelectCore or ...toList().Select()");

                        }
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                dataReader = await com.ExecuteReaderAsync();
                if (registration.HasValue)
                {
                    registration.Value.Dispose();
                }



                var resd = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                var dataSing = Pizdaticus.SingleData(listCore, resd, out var isActive);
                var ress2 = !isActive ? (object)resd : dataSing;
                if (isCacheUsage)
                {
                    MyCache<T>.Push(hashCode, ress2);
                }
                tk.SetResult(ress2);
                return await tk.Task;

            }


            catch (Exception ex)
            {
                _session.Transactionale.isError = true;
                throw new Exception(ex.Message + Environment.NewLine + com.CommandText, ex);
            }

            finally
            {
                await _session.ComDisposableAsync(com);
                if (dataReader != null)
                {
                    await dataReader.DisposeAsync();
                }
            }

        }
    }
}