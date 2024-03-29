﻿using ORM_1_21_.Utils;
using System;

namespace ORM_1_21_
{
    /// <summary>
    /// Base abstract class for attribute of the table and column of the table
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class BaseAttribute : Attribute
    {
        private string _columnName;

        /// <summary>
        /// Field name in the table  as sql
        /// </summary>
        public string GetColumnName(ProviderName providerName)
        {

            switch (providerName)
            {
                case ProviderName.MsSql:
                    return $"[{_columnName}]";

                case ProviderName.MySql:
                    return $"`{_columnName}`";
                case ProviderName.PostgreSql:
                    return $"\"{_columnName}\"";

                case ProviderName.SqLite:
                    return $"\"{_columnName}\"";
                default:
                    throw new ArgumentOutOfRangeException($"Database type is not defined:{providerName}");
            }


        }

        /// <summary>
        /// Field name in the table raw
        /// </summary>
        /// <returns></returns>
        public string GetColumnNameRaw()
        {
            return _columnName;
        }

        internal void SetColumnNameRaw(string name)
        {
            _columnName = name;
        }
        internal bool IsNotUpdateInsert { get; set; }

        internal Type PropertyType { get; set; }

        internal bool IsInheritIGeoShape { get; set; }

        internal bool IsJson { get; set; }

        internal string TypeString { get; set; }

        internal TypeReturning TypeReturning { get; set; }

        internal string PropertyName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnName"></param>
        protected BaseAttribute(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentException("column name zero");
            _columnName = UtilsCore.ClearTrim(columnName);
        }

        /// <summary>
        /// 
        /// </summary>
        protected BaseAttribute()
        {

        }

        internal string DefaultValue { get; set; }

        internal string TypeBase { get; set; }

        internal string ColumnNameAlias { get; set; }
        internal Type DeclareType { get; set; }

        internal bool IsBaseKey { get; set; }
        internal bool IsForeignKey { get; set; }


    }

}
