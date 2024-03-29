﻿using Newtonsoft.Json;
using ORM_1_21_.geo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ORM_1_21_.Utils
{
    internal class UtilsBulkPostgres
    {
        private readonly ProviderName _providerName;

        public UtilsBulkPostgres(ProviderName providerName)
        {
            _providerName = providerName;
        }

        public string GetSql<T>(IEnumerable<T> list, string fileCsv, string fieldTerminator)
        {
            if (fileCsv != null)
                return SqlFile(list, fileCsv, fieldTerminator);
            return SqlSimple(list);
        }
        public string GetSql<T>(IEnumerable<T> list)
        {
            return SqlSimple(list);
        }

        private string SqlFile<T>(IEnumerable<T> list, string fileCsv, string fieldterminator)
        {
            var sql = new StringBuilder();

            sql.Append($"COPY {AttributesOfClass<T>.TableName(_providerName)} FROM '{fileCsv}' DELIMITER '{fieldterminator}';");
            var builder = new StringBuilder();
            var isAddPk = AttributesOfClass<T>.PkAttribute(_providerName).Generator == Generator.Assigned;



            foreach (var ob in list)
            {
                var row = new StringBuilder();
                if (isAddPk)
                {
                    var o = AttributesOfClass<T>.GetValueE(_providerName, AttributesOfClass<T>.PkAttribute(_providerName).PropertyName, ob);
                    var type = AttributesOfClass<T>.PropertyInfoList
                        .Value[AttributesOfClass<T>.PkAttribute(_providerName).PropertyName].PropertyType;
                    row.Append(GetValueE(o, type)).Append($"{fieldterminator}");
                }

                foreach (var map in AttributesOfClass<T>.CurrentTableAttributeDal(_providerName))
                {
                    var o = AttributesOfClass<T>.GetValueE(_providerName, map.PropertyName, ob);
                    var type = AttributesOfClass<T>.PropertyInfoList.Value[map.PropertyName].PropertyType;
                    var str = GetValueE(o, type);
                    row.Append(str).Append($"{fieldterminator}");
                }

                var s = row.ToString()
                            .Substring(0, row.ToString().LastIndexOf(fieldterminator, StringComparison.Ordinal)) + "\n";
                builder.Append(s);
            }

            File.WriteAllText(fileCsv, builder.ToString());


            return sql.ToString();
        }

        private string SqlSimple<T>(IEnumerable<T> list)
        {
            var builder = new StringBuilder($"INSERT INTO {AttributesOfClass<T>.TableName(_providerName)}");
            builder.Append(" ( ");

            var isAddPk = AttributesOfClass<T>.PkAttribute(_providerName).Generator == Generator.Assigned;

            var rowHead = new StringBuilder();
            if (isAddPk)
                rowHead.Append($"\"{UtilsCore.ClearTrim(AttributesOfClass<T>.PkAttribute(_providerName).GetColumnName(_providerName))}\"").Append(',');
            foreach (var map in AttributesOfClass<T>.CurrentTableAttributeDal(_providerName))
            {
                //if ( map.PropertyType == typeof(byte[])) continue;
                rowHead.Append($"\"{UtilsCore.ClearTrim(map.GetColumnName(_providerName))}\"").Append(',');
            }

            builder.Append(rowHead.ToString()
                .Substring(0, rowHead.ToString().LastIndexOf(",", StringComparison.Ordinal))).Append(") VALUES");
            var rt = new UtilsBulkMySql(_providerName);
            foreach (var ob in list)
            {
                var row = new StringBuilder("(");
                if (isAddPk)
                {
                    var o = AttributesOfClass<T>.GetValueE(_providerName, AttributesOfClass<T>.PkAttribute(_providerName).PropertyName, ob);
                    var type = AttributesOfClass<T>.PropertyInfoList
                        .Value[AttributesOfClass<T>.PkAttribute(_providerName).PropertyName].PropertyType;
                    row.Append(rt.GetValue(o, type)).Append(',');
                }

                foreach (var map in AttributesOfClass<T>.CurrentTableAttributeDal(_providerName))
                {
                    if (map.IsJson)
                    {
                        var o = AttributesOfClass<T>.GetValueE(_providerName, map.PropertyName, ob);
                        if (o is string d)
                        {
                            d = UtilsCore.JsonStringReplace(d, _providerName);

                            row.Append($"CAST('{d}' AS JSON)").Append(',');
                        }
                        else
                        {
                            var json = JsonConvert.SerializeObject(o);
                            json=UtilsCore.JsonStringReplace(json,_providerName);
                            row.Append($"CAST('{json}' AS JSON)").Append(',');
                        }


                    }
                    else if (map.IsInheritIGeoShape)
                    {
                        var o = AttributesOfClass<T>.GetValueE(_providerName, map.PropertyName, ob);
                        if (o == null)
                        {
                            row.Append("null").Append(',');
                        }
                        else
                        {
                            IGeoShape shape = (IGeoShape)o;

                            string data = $"ST_GeomFromText('{shape.StAsText()}', {shape.StSrid()})";

                            row.Append(data).Append(',');
                        }

                    }
                    else
                    {
                        var o = AttributesOfClass<T>.GetValueE(_providerName, map.PropertyName, ob);
                        var type = AttributesOfClass<T>.PropertyInfoList.Value[map.PropertyName].PropertyType;
                        var str = new UtilsBulkMySql(_providerName).GetValue(o, type);
                        row.Append(str).Append(',');
                    }

                }

                builder.AppendLine(row.ToString().Substring(0, row.ToString().LastIndexOf(',')) + "),");
            }

            var res = builder.ToString().Substring(0, builder.ToString().LastIndexOf(','));
            return res;
        }

        public string GetValueE(object o, Type type)
        {
            if (o == null) return "null";

            type = UtilsCore.GetCoreType(type);

            if (type == typeof(int)
                || type == typeof(uint)
                || type == typeof(decimal)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(double)
                || type == typeof(float)
                || type == typeof(ushort)
                || type == typeof(sbyte)
                || type == typeof(short)
               )
                return o.ToString().Replace(",", ".");

            if (type == typeof(DateTime))
                return $"'{(DateTime)o:yyyy-MM-dd HH:mm:ss.fff}'";

            if (type.IsEnum) return Convert.ToInt32(o).ToString();

            if (type == typeof(bool))
            {
                if (_providerName == ProviderName.PostgreSql) return o.ToString();
                var v = Convert.ToBoolean(o);
                return v ? 0.ToString() : 1.ToString();
            }

            return $"{o}";
        }

        public static string InsertFile<T>(string fileCsv, string fieldterminator, ProviderName providerName)
        {
            return $"COPY {AttributesOfClass<T>.TableName(providerName)} FROM '{fileCsv}' DELIMITER '{fieldterminator}' CSV HEADER;";
        }
    }
}