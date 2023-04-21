﻿using ORM_1_21_.Utils;
using System;

namespace ORM_1_21_
{
   
    internal class FactoryCreatorTable
    {
        public string SqlCreate<T>(ProviderName providerName)
        {
            switch (providerName)
            {
                case ProviderName.MySql:
                    return UtilsCreateTableMySql.Create<T>(providerName);
                case ProviderName.MsSql:
                    return UtilsCreateTableMsSql.Create<T>(providerName);
                case ProviderName.PostgreSql:
                    return UtilsCreateTablePostgres.Create<T>(providerName);
                case ProviderName.SqLite:
                    return UtilsCreateTableSqLite.Create<T>(providerName);
                default:
                    return null;
            }
        }

        public static string GetDefaultValue(string def, Type type)
        {
            if (def != null)
            {
                return def;
            }

            if (type == typeof(long))
            {
                return "NOT NULL DEFAULT '0'";
            }

            if (type == typeof(Nullable<>))
            {
                return "NULL";
            }
            if (type == typeof(int)
                || type.BaseType == typeof(Enum)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(short)
                || type == typeof(bool)
                || type == typeof(decimal)
                || type == typeof(float)
                || type == typeof(double))
            {
                return "NOT NULL DEFAULT '0'";
            }

            if (type == typeof(DateTime))
            {
                return "NULL";
            }
            if (type == typeof(char))
            {
                return $"NOT NULL DEFAULT  ' '";
            }

            if (type == typeof(Guid))
            {
                return $"NOT NULL DEFAULT '{Guid.Empty}'";
            }

            //var st = UtilsCore.GetSerializeType(type);
          
            return "NULL";

        }



    }
}