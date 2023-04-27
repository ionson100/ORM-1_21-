﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ORM_1_21_.Linq;

namespace ORM_1_21_.Utils
{
    internal static class UtilsCore
    {
        internal const string Bungalo = "____";

        private static readonly List<Evolution> EvolutionsList = new List<Evolution>
        {
            Evolution.FreeSql,
            Evolution.TableCreate,
            Evolution.TableExists,
            Evolution.ExecuteScalar,
            Evolution.TruncateTable,
            Evolution.ExecuteNonQuery,
            Evolution.DataTable,
            Evolution.DropTable
        };


        private static readonly HashSet<Type> Hlam = new HashSet<Type>
        {
            typeof(uint),
            typeof(ulong),
            typeof(ushort),
            typeof(bool),
            typeof(byte),
            typeof(char),
            typeof(DateTime),
            typeof(decimal),
            typeof(double),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(sbyte),
            typeof(float),
            typeof(string),
            typeof(uint?),
            typeof(ulong?),
            typeof(ushort?),
            typeof(bool?),
            typeof(byte?),
            typeof(char?),
            typeof(DateTime?),
            typeof(decimal?),
            typeof(double?),
            typeof(short?),
            typeof(int?),
            typeof(long?),
            typeof(sbyte?),
            typeof(float?),
            typeof(Guid?),
            typeof(Guid),
            typeof(Enum),
            typeof(byte[])
        };

        internal static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(decimal),
            typeof(double),
            typeof(float)
        };

        public static bool IsReceiverFreeSql<T>()
        {
            if (Hlam.Contains(typeof(T))) return false;
            if (typeof(T).IsGenericType) return false;
            return AttributesOfClass<T>.IsReceiverFreeSqlInner;
        }

        public static bool IsValid<T>()
        {
            if (Hlam.Contains(typeof(T))) return false;
            if (typeof(T).IsGenericType) return false;
            return AttributesOfClass<T>.IsValidInner;
        }

        public static Type GetCoreType(Type t)
        {
            var s = Nullable.GetUnderlyingType(t);
            if (s != null)
            {
                return s;
            }

            return t;
        }


        internal static bool IsNumericType(Type type)
        {
            return NumericTypes.Contains(type) ||
                   NumericTypes.Contains(Nullable.GetUnderlyingType(type));
        }

        internal static string PrefParam(ProviderName providerName)
        {
            switch (providerName)
            {
                case ProviderName.MsSql:
                    return "@";
                case ProviderName.MySql:
                    return "?";
                case ProviderName.PostgreSql:
                    return "@";
                case ProviderName.SqLite:
                    return "@";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        internal static bool IsAnonymousType(Type type)
        {
            return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
                   && type.IsGenericType && type.Name.Contains("AnonymousType")
                   && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) ||
                       type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase));
        }


        internal static byte[] ObjectToByteArray(object obj)
        {
            if (obj == null)
                return null;
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        internal static object ByteArrayToObject(byte[] arrBytes)
        {
            if (arrBytes == null) return null;
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
        }

        internal static string GetAsAlias(string table, string field)
        {
            return string.Concat(table.Trim(), Bungalo, field.Trim()).Replace("`", "").Replace("[", "").Replace("]", "")
                .Trim().ToLower();
        }

       // internal static bool IsPersistent(object obj)
       // {
       //     return TypeDescriptor.GetAttributes(obj).Contains(new PersistentAttribute());
       // }


       

        internal static object ConverterPrimaryKeyType(Type typeColumn, object val)
        {
            if (typeColumn == typeof(string))
                return val.ToString();
            if (typeColumn == typeof(Guid))

                return (Guid)val;
            if (typeColumn == typeof(decimal))
                return Convert.ToDecimal(val);
            if (typeColumn == typeof(short))
                return Convert.ToInt16(val);
            if (typeColumn == typeof(int))
                return Convert.ToInt32(val);
            if (typeColumn == typeof(long))
                return Convert.ToInt64(val);
            if (typeColumn == typeof(ushort))
                return Convert.ToUInt16(val);
            if (typeColumn == typeof(uint))
                return Convert.ToUInt32(val);
            if (typeColumn == typeof(ulong))
                return Convert.ToUInt64(val);

            if (typeColumn == typeof(double))
                return Convert.ToDouble(val);

            if (typeColumn == typeof(float))
                return (float)Convert.ToDouble(val);
            throw new Exception($"Can't find type to convert primary key {typeColumn} {val}");
        }

        internal static bool ColumnExists(IDataReader reader, string columnName)
        {
            for (var i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i) == columnName)
                    return true;

            return false;
        }


        public static string ClearTrim(string tableName)
        {
            return tableName.Trim(' ', '`', '\'', '[', ']', '"');
        }

        public static string GetStringSql(IDbCommand command)
        {
            var stringBuilder = new StringBuilder(command.CommandText);
            if (command.Parameters.Count > 0)
            {
                stringBuilder.Append(" params: ");
                foreach (DbParameter commandParameter in command.Parameters)
                    stringBuilder.Append($" {commandParameter.ParameterName} - {commandParameter.Value} ");
            }


            return stringBuilder.ToString();
        }


        public static string[] MySplit(string str)
        {
            var list = new List<string>();
            str = str.Trim(' ', ',');
            var builder = new StringBuilder();
            var stop = 0;
            foreach (var c in str)
            {
                if (c == '(') stop = ++stop;
                if (c == ')') stop = --stop;
                if (stop > 0)
                {
                    builder.Append(c);
                    continue;
                }

                if (c != ',')
                {
                    builder.Append(c);
                }
                else
                {
                    list.Add(builder.ToString());
                    builder.Clear();
                }
            }

            if (builder.Length > 0) list.Add(builder.ToString());

            return list.ToArray();
        }


        public static void AddParamsForCache(StringBuilder b, string sql, ProviderName providerName, List<object> param)
        {
            if (param == null) return;
            if (param.Count == 0) return;
            var regex = new Regex(@"\@([\w.$]+|""[^""]+""|'[^']+')");
            if (providerName == ProviderName.MySql) regex = new Regex(@"\?([\w.$]+|""[^""]+""|'[^']+')");
            var matches = regex.Matches(sql);
            for (var index = 0; index < matches.Count; index++) b.Append($" {matches[index].Value} - {param[index]} ");
        }

        public static void AddParam(IDbCommand com, ProviderName providerName, object[] param)
        {
            if (param == null) return;
            if (param.Length == 0) return;
            var sql = com.CommandText;

            var regex = new Regex(@"\@([\w.$]+|""[^""]+""|'[^']+')");
            if (providerName == ProviderName.MySql) regex = new Regex(@"\?([\w.$]+|""[^""]+""|'[^']+')");
            MatchCollection matches = regex.Matches(sql);

            for (var index = 0; index < matches.Count; index++)
                if (param[index] is IDbDataParameter)
                {
                    com.Parameters.Add((IDbDataParameter)param[index]);
                }
                else
                {
                    dynamic d = com.Parameters;
                    d.AddWithValue(matches[index].Value, param[index] ?? DBNull.Value);
                }
        }

        public static Action CancelRegistr(IDbCommand com, CancellationToken cancellationToken,
            Transactionale transactionale, ProviderName providerName)
        {
            return () =>
            {
                try
                {
                    if (providerName == ProviderName.MySql)
                    {
                        dynamic s = com;
                        s.ExecuteNonQuery(cancellationToken);
                    }
                    else
                    {
                        com.Cancel();
                    }
                }
                catch (Exception ex)
                {
                    MySqlLogger.Error("cancellationToken", ex);
                }
                finally
                {
                    transactionale.isError = true;
                    cancellationToken.ThrowIfCancellationRequested();
                }
            };
        }


        public static string CheckAny(List<OneComposite> listOne)
        {
            foreach (var evolution in EvolutionsList)
            {
                var ss = listOne.FirstOrDefault(a => a.Operand == evolution);
                if (ss != null)
                    return ss.Body;
            }

            return null;
        }

        public static string ColumnBuilder(string type, bool isPk = false)
        {
            switch (type.ToLower().Trim())
            {
                case "blob":
                {
                    if (isPk) return "Guid";
                    return "byte[]";
                }
                case "text":
                {
                    if (isPk) return "Guid";
                    return "string";
                }
                case "integer": return "int";
                case "real": return "Int16";
                case "numeric": return "decimal";
                case "uuid": return "Guid";
                case "bit": return "bool";
                case "bigint": return "long";
                case "smallint": return "Int16";
                case "money": return "decimal";
                case "smallmoney": return "decimal";
                case "mediumint": return "int";
                case "tinyint": return "bool";
                case "boolean": return "bool";
            }

            if (type.ToLower().Contains("text")) return "string";
            if (type.ToLower().Contains("double")) return "double";
            if (type.ToLower().Contains("decimal")) return "decimal";
            if (type.ToLower().Contains("character")) return "string";
            if (type.ToLower().Contains("timestamp")) return "DateTime";
            if (type.ToLower().Contains("uniqueidentifier")) return "Guid";
            if (type.ToLower().Contains("varchar")) return "string";
            if (type.ToLower().Contains("datetime")) return "DateTime";

            return type;
        }
        internal static object Convertor(object ob, Type type)
        {
            if (ob is DBNull)
                return null;
            type = UtilsCore.GetCoreType(type);
            if (type == typeof(Guid))
            {
                if (ob is Guid) return ob;
                var guid = Guid.Empty;
                if (ob is byte[] bytes)
                    return bytes.Length == 16 ? new Guid(bytes) : new Guid(Encoding.ASCII.GetString(bytes));

                return ob is string ? new Guid(ob.ToString()) : guid;
            }

            if (type.BaseType == typeof(Enum))
            {
                return Enum.Parse(type, Convert.ToInt32(ob).ToString());
            }

            if (type == typeof(uint)) return Convert.ToUInt32(ob);
            if (type == typeof(ulong)) return Convert.ToUInt64(ob);
            if (type == typeof(ushort)) return Convert.ToUInt16(ob);
            if (type == typeof(bool)) return Convert.ToBoolean(ob);
            if (type == typeof(byte)) return Convert.ToByte(ob);

            if (type == typeof(char)) return Convert.ToChar(ob);
            if (type == typeof(DateTime)) return Convert.ToDateTime(ob);
            if (type == typeof(decimal)) return Convert.ToDecimal(ob);
            if (type == typeof(double)) return Convert.ToDouble(ob);
            if (type == typeof(short)) return Convert.ToInt16(ob);
            if (type == typeof(int)) return Convert.ToInt32(ob);
            if (type == typeof(long)) return Convert.ToInt64(ob);
            if (type == typeof(sbyte)) return Convert.ToSByte(ob);
            if (type == typeof(float)) return Convert.ToSingle(ob);
            if (type == typeof(string)) return Convert.ToString(ob);
            if (type == typeof(byte[])) return (byte[])ob;
            
            
            

       
            var message = string.Format(CultureInfo.CurrentCulture, "Can't convert type {0} as {1}",
                ob.GetType().FullName, type);
            throw new Exception(message);
        }
    }

}