﻿
using System;
using System.Text;

namespace ORM_1_21_.Utils
{
    internal class UtilsCreateTableSqLite
    {
        public static string Create<T>(ProviderName providerName)
        {
            StringBuilder builder = new StringBuilder();

            var tableName = AttributesOfClass<T>.TableName(providerName);
            builder.AppendLine($"CREATE TABLE IF NOT EXISTS {tableName} (");
            var pk = AttributesOfClass<T>.PkAttribute(providerName);
            if (pk.Generator == Generator.Native|| pk.Generator == Generator.NativeNotReturningId)
            {
                var typePk = $"  INTEGER PRIMARY KEY AUTOINCREMENT";
                if (pk.TypeString!=null)
                    typePk = pk.TypeString;
                var defValue = "";
                if (pk.DefaultValue != null)
                {
                    defValue = pk.DefaultValue;
                }
                builder.AppendLine($"{pk.GetColumnName(providerName)} {typePk} {defValue},");
            }
            else
            {
                var typePk = $"BLOB PRIMARY KEY";
                if (pk.TypeString != null)
                    typePk = pk.TypeString;
                var defValue = "";
                if (pk.DefaultValue != null)
                {
                    defValue = pk.DefaultValue;
                }
                builder.AppendLine($"{pk.GetColumnName(providerName)} {typePk} {defValue},");
            }

            foreach (MapColumnAttribute map in AttributesOfClass<T>.CurrentTableAttributeDal(providerName))
            {
                var ee = map.PropertyName;
                builder.AppendLine(
                    $" {map.GetColumnName(providerName)} {GetTypeColumn(map.PropertyType)} {FactoryCreatorTable.GetDefaultValue(map.DefaultValue, map.PropertyType)},");
            }

            string str2 = builder.ToString();
            str2 = str2.Substring(0, str2.LastIndexOf(','));
            builder.Clear();
            builder.Append(str2);
            builder.AppendLine(");");

            foreach (MapColumnAttribute map in AttributesOfClass<T>.CurrentTableAttributeDal(providerName))
            {
                if (map.IsIndex)
                {
                    var c = map.GetColumnNameRaw();
                    var t = UtilsCore.ClearTrim(tableName);
                    builder.AppendLine($"CREATE INDEX 'INDEX_{t}_{c}' ON '{t}' ('{c}');");
                }
            }

            return builder.ToString();
        }
        private static string GetTypeColumn(Type type)
        {
            if (type == typeof(Guid) || type == typeof(Guid?))
            {
                return "TEXT";
            }
            if (type == typeof(long) || type == typeof(long?))
            {
                return "NUMERIC";
            }

            if (type == typeof(int) || type.BaseType == typeof(Enum) || type == typeof(int?))
            {
                return "INTEGER";
            }

            if (type == typeof(UInt16) || type == typeof(UInt16?) || type == typeof(UInt32) ||
                type == typeof(UInt32?) ||
                type == typeof(UInt64) || type == typeof(UInt64?)||type==typeof(UInt16)||type==typeof(Int16?))
            {
                return "INTEGER";
            }

            if (type == typeof(SByte) || type == typeof(SByte?) || type == typeof(Byte) || type == typeof(Byte?))
            {
                return "INTEGER";
            }

            if (type == typeof(char) || type == typeof(char?))
            {
                return "TEXT";
            }
            if (type == typeof(short) || type.BaseType == typeof(short?))
            {
                return "INTEGER";
            }
            if (type == typeof(bool) || type == typeof(bool?))
            {
                return "INTEGER";
            }
            if (type == typeof(decimal) || type == typeof(decimal?))
            {
                return "REAL";
            }
            if (type == typeof(float) || type == typeof(float?))
            {
                return "REAL";
            }

            if (type == typeof(double) || type == typeof(double?))
            {
                return "REAL";
            }

            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                return "TEXT";
            }

            if (type.BaseType == typeof(Enum))
            {
                return "INT";
            }

            if (type == typeof(string))
            {
                return "TEXT";
            }

            if (type == typeof(byte[]))
            {
                return "BLOB";
            }

            return "TEXT";

        }

    }
}
