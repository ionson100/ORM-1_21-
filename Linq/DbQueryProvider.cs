﻿using ORM_1_21_.Linq.MsSql;
using ORM_1_21_.Linq.MySql;
using ORM_1_21_.Utils;
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
    internal partial class DbQueryProvider<T> : QueryProvider, ISqlComposite
    {
        private readonly List<object> _paramFree = new List<object>();
        private readonly List<ParameterStoredPr> _paramFreeStoredPr = new List<ParameterStoredPr>();
        //private CancellationToken _cancellationToken;


        private readonly Dictionary<string, object> _parOut = new Dictionary<string, object>();
        private readonly Sessione _sessione;
        private IDbCommand _com;
        private readonly ProviderName _providerName;




        private bool _isStoredPr;

        private Dictionary<string, object> _param;

        public Type GetSourceType()
        {
            return typeof(T);
        }

        public DbQueryProvider<T> GetDbQueryProvider()
        {
            return this;
        }
        public DbQueryProvider(Sessione ses)
        {
            _sessione = ses;
            Sessione = ses;
            _providerName = ses.MyProviderName;
        }

        public ISession Sessione { get; }
        public IDbTransaction Transaction { get; set; }

        public List<ContainerCastExpression> ListCastExpression { get; set; } = new List<ContainerCastExpression>();






        private bool PingCompositeE(Evolution eval, List<OneComposite> list)
        {
            return list.Any(a => a.Operand == eval);
        }




        public override string GetQueryText(Expression expression)
        {
            return TranslateString(expression);
        }

        private CacheState CacheState
        {
            get
            {
                if (ListCastExpression.Any(t => t.TypeRevalytion == Evolution.CacheUsage))
                {
                    return CacheState.CacheUsage;
                }
                if (ListCastExpression.Any(t => t.TypeRevalytion == Evolution.CacheOver))
                {
                    return CacheState.CacheOver;
                }
                if (ListCastExpression.Any(t => t.TypeRevalytion == Evolution.CacheKey))
                {
                    return CacheState.CacheKey;
                }

                return CacheState.NoCache;
            }
        }

        async Task<List<TResult>> ActionCoreGroupBy<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            List<TResult> resList = new List<TResult>();
            var res = await ExecuteAsync<TResult>(expression, null, cancellationToken);
            foreach (var o in (IEnumerable<object>)res)
            {
                resList.Add((TResult)o);
            }
            return resList;
        }

        public override object Execute(Expression expression)
        {
            return null;
        }
        public override object ExecuteSpp<TS>(Expression expression)
        {
            IDataReader dataReader = null;
            try
            {
                var sb = new StringBuilder();
                var services = (IServiceSessions)Sessione;
                _com = services.CommandForLinq;
                _com.CommandType = CommandType.StoredProcedure;
                var re = TranslateE(expression);
                _com.CommandText = re.Sql;
                if (_providerName == ProviderName.MsSql)
                {
                    var mat = new Regex(@"TOP\s@p\d").Matches(_com.CommandText);
                    foreach (var variable in mat)
                    {
                        var st = variable.ToString().Split(' ')[1];
                        var val = _param.FirstOrDefault(a => a.Key == st).Value;
                        _com.CommandText = _com.CommandText.Replace(variable.ToString(),
                            string.Format("{1} ({0})", val, StringConst.Top));
                        _param.Remove(st);
                    }
                }

                foreach (var p in _paramFreeStoredPr)
                {
                    sb.Append(string.Format(CultureInfo.CurrentCulture, "{0}-{1},", p.Name, p.Value));
                    IDataParameter pr = _com.CreateParameter();
                    pr.Direction = p.Direction;
                    pr.ParameterName = p.Name;
                    pr.Value = p.Value;
                    _com.Parameters.Add(pr);
                }

                _sessione.OpenConnectAndTransaction(_com);
                dataReader = _com.ExecuteReader();
                if (AttributesOfClass<TS>.IsValid)
                {
                    var lResult = AttributesOfClass<TS>.GetEnumerableObjects(dataReader, _providerName);
                    foreach (var par in _com.Parameters)
                        if (((IDataParameter)par).Direction == ParameterDirection.InputOutput ||
                            ((IDataParameter)par).Direction == ParameterDirection.Output ||
                            ((IDataParameter)par).Direction == ParameterDirection.ReturnValue)
                            _parOut.Add(((IDataParameter)par).ParameterName, ((IDataParameter)par).Value);
                    return lResult;
                }
                else
                {
                    #region

                    var countcol = dataReader.FieldCount;
                    var list = new List<Type>();
                    for (var i = 0; i < countcol; i++) list.Add(dataReader.GetFieldType(i));
                    var ci = typeof(TS).GetConstructor(list.ToArray());
                    var resDis = new List<TS>();
                    while (dataReader.Read())
                        if (ci != null)
                        {
                            var par = new List<object>();
                            for (var i = 0; i < countcol; i++)
                                par.Add(dataReader[i] == DBNull.Value ? null : dataReader[i]);
                            var e = ci.Invoke(par.ToArray());
                            resDis.Add((TS)e);
                        }
                        else
                        {
                            if (countcol == 1)
                            {
                                resDis.Add((TS)UtilsCore.Convertor<TS>(dataReader[0]));
                            }
                            else
                            {
                                dynamic employee = new ExpandoObject();
                                for (var i = 0; i < countcol; i++)
                                    ((IDictionary<string, object>)employee).Add(dataReader.GetName(i),
                                        dataReader[i] == DBNull.Value ? null : dataReader[i]);
                                resDis.Add((TS)employee);
                            }
                        }

                    dataReader.NextResult();
                    foreach (var par in _com.Parameters)
                        if (((IDataParameter)par).Direction == ParameterDirection.InputOutput ||
                            ((IDataParameter)par).Direction == ParameterDirection.Output ||
                            ((IDataParameter)par).Direction == ParameterDirection.ReturnValue)
                            _parOut.Add(((IDataParameter)par).ParameterName, ((IDataParameter)par).Value);

                    return resDis;

                    #endregion
                }
            }
            catch (Exception ex)
            {
                _sessione.Transactionale.isError = true;
                MySqlLogger.Error(_com.CommandText, ex);
                throw new Exception(ex.Message + Environment.NewLine + _com.CommandText, ex);

            }
            finally
            {
                _sessione.ComDisposable(_com);
                if (dataReader != null)
                {
                    dataReader.Close();
                    dataReader.Dispose();
                }
            }
        }


        private int GetTimeout()
        {
            foreach (var t in ListCastExpression)
            {
                if (t.TypeRevalytion == Evolution.Timeout && t.Timeout >= 0) { return t.Timeout; }
            }
            return 30;
        }

        public List<object> GetParamFree()
        {
            return _paramFree;
        }

        public List<OneComposite> ListOuterOneComposites { get; set; } = new List<OneComposite>();

        public TS ExecuteExtension<TS>(Expression expression, params object[] param)
        {
            if (param != null && param.Length > 0)
            {
                _paramFree.AddRange(param);
            }
            var res = Execute<TS>(expression);
            return (TS)res;
        }



        public override async Task<TS> ExecuteExtensionAsync<TS>(Expression expression, object[] param, CancellationToken cancellationToken)
        {
            var tk = new TaskCompletionSource<TS>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (param != null && param.Length > 0)
            {
                _paramFree.AddRange(param);
            }
            var r = await ExecuteAsync<TS>(expression, param, cancellationToken);
            tk.SetResult((TS)r);
            return await tk.Task;
        }

        public override object Execute<TS>(Expression expression)
        {
            //Thread.Sleep(5000);


            bool isCacheUsage = CacheState == CacheState.CacheUsage || CacheState == CacheState.CacheOver || CacheState == CacheState.CacheKey;
            var services = (IServiceSessions)Sessione;
            var re = TranslateE(expression);
            string sql = re.Sql;
            List<OneComposite> listCore = re.Composites;
            listCore.AddRange(ListOuterOneComposites);
            if (PingCompositeE(Evolution.GroupBy, listCore) && _isAsync)
            {
                _isAsync = false;
                throw new GroupException();
            }


            /*usage cache*/

            int hashCode = -1;
            if (isCacheUsage)
            {
                var b = new StringBuilder(sql);
                foreach (var p in _param)
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
                    return hashCode;
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
                    return r;
                }


            }
            /*usage cache*/


            _com = services.CommandForLinq;

            var to = GetTimeout();
            if (to >= 0)
            {
                _com.CommandTimeout = to;
            }


            if (_isStoredPr)
                _com.CommandType = CommandType.StoredProcedure;

            _com.CommandText = sql;
            var sb = new StringBuilder();
            if (_providerName == ProviderName.MsSql)
            {
                var mat = new Regex(@"TOP\s@p\d").Matches(_com.CommandText);
                foreach (var variable in mat)
                {
                    var st = variable.ToString().Split(' ')[1];
                    var val = _param.FirstOrDefault(a => a.Key == st).Value;
                    _com.CommandText = _com.CommandText.Replace(variable.ToString(),
                        string.Format("{1} ({0})", val, StringConst.Top));
                    _param.Remove(st);
                }
            }

            _com.Parameters.Clear();


            foreach (var p in _param)
            {
                sb.Append(string.Format(CultureInfo.CurrentCulture, "{0}-{1},", p.Key, p.Value));
                IDataParameter pr = _com.CreateParameter();
                pr.ParameterName = p.Key;
                pr.Value = p.Value;
                _com.Parameters.Add(pr);
            }

            if (_paramFree.Any())
            {
                UtilsCore.AddParam(_com, _providerName, _paramFree.ToArray());
            }


            foreach (var p in _paramFreeStoredPr)
            {
                IDataParameter pr = _com.CreateParameter();
                pr.Direction = p.Direction;
                pr.ParameterName = p.Name;
                pr.Value = p.Value;
                _com.Parameters.Add(pr);
            }


            IDataReader dataReader = null;
            try
            {
                _sessione.OpenConnectAndTransaction(_com);

                if (PingCompositeE(Evolution.All, listCore))
                {
                    var reader = _com.ExecuteReader();
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

                            return res;
                        }
                    }
                    finally
                    {
                        reader.Dispose();
                    }

                }

                if (PingCompositeE(Evolution.LongCount, listCore))
                {
                    var ee = _com.ExecuteScalar();
                    var res = Convert.ToInt64(ee, CultureInfo.CurrentCulture);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    return res;
                }

                if (PingCompositeE(Evolution.Count, listCore))
                {
                    var ee = _com.ExecuteScalar();
                    var res = Convert.ToInt32(ee, CultureInfo.CurrentCulture);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    return res;
                }


                if (PingCompositeE(Evolution.Delete, listCore))
                {
                    var ee = _com.ExecuteNonQuery();
                    MyCache<T>.Clear();
                    return ee;
                }

                if (PingCompositeE(Evolution.Update, listCore))
                {
                    var ee = _com.ExecuteNonQuery();
                    MyCache<T>.Clear();
                    return ee;
                }

                if (PingCompositeE(Evolution.Any, listCore))
                {
                    var ee = _com.ExecuteScalar();
                    var res = Convert.ToInt32(ee, CultureInfo.CurrentCulture) != 0;
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    return res;
                }



                #region dataReader

                if (PingCompositeE(Evolution.TableCreate, listCore))
                {
                    if (AttributesOfClass<T>.IsValid == false)
                    {
                        throw new Exception(
                            $"I can't create a table from type {typeof(T)}, it doesn't have attrubute: MapTableNameAttribute");
                    }

                    _com.CommandText = listCore.First(a => a.Operand == Evolution.TableCreate).Body;
                    int res = _com.ExecuteNonQuery();
                    return res;

                }

                if (PingCompositeE(Evolution.DropTable, listCore))
                {
                    _com.CommandText = listCore.First(a => a.Operand == Evolution.DropTable).Body;
                    int res = _com.ExecuteNonQuery();
                    return res;

                }

                if (PingCompositeE(Evolution.DataTable, listCore))
                {
                    _com.CommandText = listCore.First(a => a.Operand == Evolution.DataTable).Body;
                    var table = new DataTable();
                    var reader = _com.ExecuteReader();
                    table.BeginLoadData();
                    table.Load(reader);
                    table.EndLoadData();
                    return table;

                }

                if (PingCompositeE(Evolution.ExecuteNonQuery, listCore))
                {
                    _com.CommandText = listCore.First(a => a.Operand == Evolution.ExecuteNonQuery).Body;
                    int res = _com.ExecuteNonQuery();
                    return res;
                }


                if (PingCompositeE(Evolution.TruncateTable, listCore))
                {
                    _com.CommandText = listCore.First(a => a.Operand == Evolution.TruncateTable).Body;
                    int res = _com.ExecuteNonQuery();
                    return res;
                }

                if (PingCompositeE(Evolution.ExecuteScalar, listCore))
                {

                    _com.CommandText = listCore.First(a => a.Operand == Evolution.ExecuteScalar).Body;
                    object res = _com.ExecuteScalar();
                    return res;

                }


                if (PingCompositeE(Evolution.TableExists, listCore))
                {
                    _com.CommandText = listCore.First(a => a.Operand == Evolution.TableExists).Body;
                    if (_providerName == ProviderName.PostgreSql)
                    {
                        var r = (long)_com.ExecuteScalar();
                        return r != 0;
                    }

                    if (_providerName == ProviderName.MsSql)
                    {
                        var r = _com.ExecuteScalar();
                        return !(r is DBNull);
                    }
                    else
                    {
                        try
                        {
                            _com.ExecuteNonQuery();
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }

                }

                if (PingCompositeE(Evolution.FreeSql, listCore) &&
                    AttributesOfClass<TS>.IsValid == false)
                {
                    dataReader = _com.ExecuteReader();
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
                                var val = Pizdaticus.MethodFree(_providerName, list[i], dataReader[i]);
                                par.Add(dataReader[i] == DBNull.Value ? null : val);
                            }
                            var e = ci.Invoke(par.ToArray());
                            resDis.Add((TS)e);
                        }

                    }
                    else if (AttributesOfClass<TS>.IsReceiverFreeSql)
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
                                var val = Pizdaticus.MethodFree(_providerName, ci.GetParameters()[i].ParameterType,
                                    dataReader[i]);
                                par.Add(dataReader[i] == DBNull.Value ? null : val);
                            }

                            var e = ci.Invoke(par.ToArray());
                            resDis.Add((TS)e);
                        }

                    }

                    else if (count == 1 && typeof(TS).IsValueType || typeof(TS) == typeof(string) ||
                             typeof(TS).GetInterface("IEnumerable") != null || typeof(TS).IsGenericTypeDefinition)
                    {
                        while (dataReader.Read())
                            resDis.Add((TS)(dataReader[0] == DBNull.Value ? null : dataReader[0]));
                    }
                    else
                    {
                        if (typeof(TS) != typeof(object) && typeof(TS).IsClass)
                        {
                            object employee;
                            var isLegalese = AttributesOfClass<TS>.IsUssageActivator(_providerName);
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
                    foreach (var par in _com.Parameters)
                        if (((IDataParameter)par).Direction == ParameterDirection.InputOutput ||
                            ((IDataParameter)par).Direction == ParameterDirection.Output ||
                            ((IDataParameter)par).Direction == ParameterDirection.ReturnValue)
                            _parOut.Add(((IDataParameter)par).ParameterName, ((IDataParameter)par).Value);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, resDis);
                    }

                    return resDis;
                }

                if (listCore.Any(a => a.Operand == Evolution.Select && a.IsAggregate))
                {
                    dataReader = _com.ExecuteReader();
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

                    return res;
                }



                if (PingCompositeE(Evolution.Join, listCore))
                {
                    dataReader = _com.ExecuteReader();
                    var res = new List<TS>();
                    var ss = listCore.Single(a => a.Operand == Evolution.Join).NewConstructor;
                    if (ss == null)
                        while (dataReader.Read())
                            res.Add((TS)dataReader[0]);
                    else
                        res = Pizdaticus.GetListAnonymousObj<TS>(dataReader, ss, _providerName);

                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    return res;
                }


                if (PingCompositeE(Evolution.Select, listCore) &&
                    !PingCompositeE(Evolution.SelectNew, listCore))
                {
                    var lres = new List<TS>();
                    dataReader = _com.ExecuteReader();
                    while (dataReader.Read()) lres.Add((TS)UtilsCore.Convertor<TS>(dataReader[0]));
                    dataReader.Dispose();
                    var devastatingly1 = Pizdaticus.SingleData(listCore, lres, out var isactive1);
                    var res = !isactive1 ? (object)lres : devastatingly1;
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, res);
                    }

                    return res;
                }

                if (PingCompositeE(Evolution.ElementAt, listCore))
                {
                    dataReader = _com.ExecuteReader();
                    var r = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                    var enumerable = r as T[] ?? r.ToArray();
                    if (enumerable.Any())
                    {
                        var res = enumerable.First();
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, res);
                        }

                        return res;
                    }

                    throw new Exception("Element not in selection.");
                }

                if (PingCompositeE(Evolution.ElementAtOrDefault, listCore))
                {
                    dataReader = _com.ExecuteReader();
                    var r = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                    var enumerable = r as T[] ?? r.ToArray();

                    if (enumerable.Any())
                    {
                        if (isCacheUsage)
                        {
                            MyCache<T>.Push(hashCode, enumerable.First());
                        }

                        return enumerable.First();
                    }

                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, null);
                    }

                    return enumerable.FirstOrDefault();

                }

                if (PingCompositeE(Evolution.DistinctCore, listCore))
                {

                    var sas = ListCastExpression.Single(a => a.TypeRevalytion == Evolution.DistinctCore);
                    IList resT = sas.ListDistinct;
                    dataReader = _com.ExecuteReader();
                    if (PingCompositeE(Evolution.SelectNew, listCore))
                    {
                        var ss = listCore.Single(a => a.Operand == Evolution.SelectNew).NewConstructor;
                        Pizdaticus.GetListAnonymousObjDistinct(dataReader, ss, resT, _providerName);
                        return resT;

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

                        return resDis;
                    }

                }

                if (PingCompositeE(Evolution.SelectNew, listCore))
                {
                    //todo ion100
                    var ss = listCore.Single(a => a.Operand == Evolution.SelectNew).NewConstructor;
                    _com.CommandText = _com.CommandText.Replace(",?p", "?p");
                    dataReader = _com.ExecuteReader();
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
                        if (listCore.Any(a => a.Operand == Evolution.GroupBy && a.ExpressionDelegate != null))
                        {
                            var lRes = Pizdaticus.GetListAnonymousObj<object>(dataReader, ss, _providerName);
                            var dataSing1 = Pizdaticus.SingleData(listCore, lRes, out var isActive1);
                            var res = !isActive1 ? lRes : dataSing1;
                            if (isCacheUsage)
                            {
                                MyCache<T>.Push(hashCode, res);
                            }

                            return res;
                        }

                        throw new Exception("Not implemented");
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                dataReader = _com.ExecuteReader();

                if (listCore.Any(a => a.Operand == Evolution.GroupBy && a.ExpressionDelegate != null))
                {
                    var lResult = AttributesOfClass<TS>.GetEnumerableObjectsGroupBy<T>(dataReader,
                        listCore.First(a => a.Operand == Evolution.GroupBy).ExpressionDelegate, _providerName);
                    if (isCacheUsage)
                    {
                        MyCache<T>.Push(hashCode, lResult);
                    }

                    return lResult;
                }

                var resd = AttributesOfClass<T>.GetEnumerableObjects(dataReader, _providerName);
                var dataSingl = Pizdaticus.SingleData(listCore, resd, out var isActive);
                var ress2 = !isActive ? (object)resd : dataSingl;
                if (isCacheUsage)
                {
                    MyCache<T>.Push(hashCode, ress2);
                }

                return ress2;
            }
            catch (GroupException)
            {
                throw;
            }

            catch (Exception ex)
            {
                _sessione.Transactionale.isError = true;
                throw new Exception(ex.Message + Environment.NewLine + _com.CommandText, ex);
            }

            finally
            {
                _sessione.ComDisposable(_com);
                if (dataReader != null)
                {
                    dataReader.Close();
                    dataReader.Dispose();
                }
            }
        }

        private bool _isAsync;
        public override async Task<List<TS>> ExecuteToListAsync<TS>(Expression expression, CancellationToken cancellationToken)
        {
            _isAsync = true;
            var tk = new TaskCompletionSource<List<TS>>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {


                object rr = await ExecuteAsync<TS>(expression, null, cancellationToken);
                tk.SetResult((List<TS>)rr);
                return await tk.Task;

            }
            catch (GroupException)
            {
                var res = await ActionCoreGroupBy<TS>(expression, cancellationToken);
                tk.SetResult(res);
                return await tk.Task;
            }


        }




        private MyTuple TranslateE(Expression expression)
        {
            //QueryTranslatorMsSql
            ITranslate sq = new QueryTranslator<T>(_providerName);

            _param = sq.Param;

            ListCastExpression.ForEach(a => sq.Translate(a.CustomExpression, a.TypeRevalytion, a.ParamList));
            string res = sq.Translate(expression, out _);
            var sdd = sq.GetListOne();
            Thread.MemoryBarrier();


            return new MyTuple { Sql = res, Composites = sdd };

        }

        private string TranslateString(Expression expression)
        {
            ITranslate sq = new QueryTranslator<T>(_providerName);
            _param = sq.Param;
            ListCastExpression.ForEach(a => sq.Translate(a.CustomExpression, a.TypeRevalytion, a.ParamList));
            string res = sq.Translate(expression, out _);


            return res;
        }



        public IEnumerable<TS> ExecuteCall<TS>(Expression callExpr)
        {
            _isStoredPr = true;
            return (IEnumerable<TS>)Execute<TS>(callExpr);
        }

        public IEnumerable<TS> ExecuteCallParam<TS>(Expression callExpr, params ParameterStoredPr[] par)
        {
            _isStoredPr = true;
            if (par != null) _paramFreeStoredPr.AddRange(par);
            var res = (IEnumerable<TS>)ExecuteSpp<TS>(callExpr);

            foreach (var re in _parOut)
            {
                if (par == null) continue;
                var p = par.FirstOrDefault(a => a.Name == re.Key);
                if (p != null) p.Value = re.Value;
            }

            return res;
        }

        public Type GetTypeGetTypeGeneric()
        {
            return typeof(T);
        }
    }

    class MyTuple
    {
        public string Sql { get; set; }
        public List<OneComposite> Composites { get; set; } = new List<OneComposite>();
    }
}