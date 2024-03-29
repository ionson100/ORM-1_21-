﻿using ORM_1_21_.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ORM_1_21_
{
    /// <summary>
    ///     Base interface
    /// </summary>
    public interface ISession : IDisposable
    {
        /// <summary>
        ///     Is call Dispose
        /// </summary>
        bool IsDispose { get; }

        /// <summary>
        ///     Session ID
        /// </summary>
        string IdSession { get; }


        /// <summary>
        ///     Getting the connection string for the current session
        /// </summary>
        string ConnectionString { get; }

        /// <summary>
        ///     Gets symbol of the parameter for sql request
        /// </summary>
        /// <returns></returns>
        string SymbolParam { get; }

        /// <summary>
        ///     Current session provider name
        /// </summary>
        /// <returns></returns>
        ProviderName ProviderName { get; }

        /// <summary>
        ///     Default timeout connection (second)
        /// </summary>
        int DefaultTimeOut { get; }


        /// <summary>
        ///     Request for selection from IDataReader
        /// </summary>
        /// <param name="reader">IDataReader</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>IEnumerable</returns>
        IEnumerable<T> GetListMonster<T>(IDataReader reader) where T : class;


        /// <summary>
        /// </summary>
        /// <param name="source"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        int Insert<TSource>(TSource source) where TSource : class;

        /// <summary>
        ///     Removing an object from the database, return the number of affected rows
        /// </summary>
        int Delete<TSource>(TSource source) where TSource : class;

        /// <summary>
        ///     Removing asynchronously an object from the database, return the number of affected rows
        /// </summary>
        Task<int> DeleteAsync<TSource>(TSource source, CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Getting ITransaction with the start of the transaction
        /// </summary>
        ITransaction BeginTransaction();

        /// <summary>
        ///     Getting ITransaction with the start of the transaction
        /// </summary>
        ITransaction BeginTransaction(IsolationLevel? value);

        /// <summary>
        ///     Create a table
        /// </summary>
        int TableCreate<TSource>() where TSource : class;

        /// <summary>
        ///     Getting DbCommand
        /// </summary>
        IDbCommand GeDbCommand();

        /// <summary>
        ///     Drop table
        /// </summary>
        int DropTable<TSource>() where TSource : class;

        /// <summary>
        ///     Drop table if exists
        /// </summary>
        int DropTableIfExists<TSource>();

        /// <summary>
        ///     Checking if a table exists in database
        /// </summary>
        bool TableExists<TSource>() where TSource : class;

        /// <summary>
        ///     Checking if a table exists in database
        /// </summary>
        Task<bool> TableExistsAsync<TSource>(CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Getting ExecuteReader
        /// </summary>
        /// <param name="sql">request string</param>
        /// <param name="params">parameters array( parameters name: mysql-?, PostgreSQL-@, msSql-@, SqLite-@)</param>
        IDataReader ExecuteReader(string sql, params object[] @params);

        /// <summary>
        ///     Getting ExecuteReader
        /// </summary>
        /// <param name="sql">request string</param>
        /// <param name="timeOut">timeout connection</param>
        /// <param name="param">parameters array (parameters name: mysql-?,PostgreSQL-@,msSql-@,SqLite-@)</param>
        IDataReader ExecuteReader(string sql, int timeOut = 30, params object[] param);

        /// <summary>
        ///     Getting DataTable
        /// </summary>
        /// <param name="sql">request string</param>
        /// <param name="timeout">timeout connection</param>
        DataTable GetDataTable(string sql, int timeout = 30);

        /// <summary>
        ///     Getting DataTable
        /// </summary>
        /// <param name="sql">sql text</param>
        /// <param name="timeout">timeout connection</param>
        /// <param name="param">parameters array (parameters name: mysql-?, PostgreSQL-@, msSql-@, SqLite-@)</param>
        DataTable GetDataTable(string sql, int timeout = 30, params object[] param);

        /// <summary>
        ///     Returns a list of table names from the current session database
        /// </summary>
        List<string> GetTableNames();

        /// <summary>
        ///     Database creation
        /// </summary>
        int CreateBase(string baseName);

        /// <summary>
        ///     Insert bulk from list (Attention: sql parameters are not used, Beware of sql injection)
        /// </summary>
        int InsertBulk<TSource>(IEnumerable<TSource> list, int timeOut = 30) where TSource : class;

        /// <summary>
        ///     Insert bulk from list (Attention: sql parameters are not used, Beware of sql injection)
        /// </summary>
        Task<int> InsertBulkAsync<TSource>(IEnumerable<TSource> list, int timeOut,
            CancellationToken cancellationToken = default) where TSource : class;


        /// <summary>
        ///     Insert bulk to database from file (Attention: sql parameters are not used)
        /// </summary>
        /// <param name="fileCsv">path to file</param>
        /// <param name="FIELDTERMINATOR">terminator, default - ;</param>
        /// <param name="timeOut">timeout connection</param>
        int InsertBulkFromFile<T>(string fileCsv, string FIELDTERMINATOR = ";", int timeOut = 30) where T : class;


        /// <summary>
        ///     Returns the first element of the request
        /// </summary>
        /// <param name="sql">sql text</param>
        /// <param name="param">parameters array (parameters name: mysql-?, PostgreSQL-@, msSql-@, SqLite-@)</param>
        /// <returns></returns>
        object ExecuteScalar(string sql, params object[] param);




        /// <summary>
        ///     Returns the first element of the request
        /// </summary>
        /// <param name="sql">sql text</param>
        /// <param name="timeOut">timeout connection</param>
        /// <param name="param">parameter array (parameters name: mysql-?, PostgreSQL-@, msSql-@, SqLite-@)</param>
        object ExecuteScalar(string sql, int timeOut = 30, params object[] param);


        /// <summary>
        ///     Recreating a table
        /// </summary>
        int TruncateTable<TSource>() where TSource : class;

        /// <summary>
        ///     Recreating a table
        /// </summary>
        Task<int> TruncateTableAsync<TSource>(CancellationToken cancellationToken = default) where TSource : class;


        /// <summary>
        ///     Main point  Linq to Sql
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        Query<TSource> Query<TSource>() where TSource : class;

        /// <summary>
        ///     Write to log file
        /// </summary>
        /// <param name="message">message</param>
        void WriteLogFile(string message);

        /// <summary>
        ///     Getting IDbCommand. Does not belong to the current session!
        /// </summary>
        IDbCommand GetCommand();

        /// <summary>
        ///     Getting IDbConnection. Does not belong to the current session!
        /// </summary>
        IDbConnection GetConnection();

        /// <summary>
        ///     Getting IDbDataAdapter. Dispose manual
        /// </summary>
        IDbDataAdapter GetDataAdapter();

        /// <summary>
        ///     Executes the query and returns the number of records affected
        /// </summary>
        /// <param name="sql">sql text</param>
        /// <param name="param">parameters array (parameters name: mysql-?, postgresql-@, msSql-@, SqLite-@)</param>
        int ExecuteNonQuery(string sql, params object[] param);


        /// <summary>
        ///     Executes the query and returns the number of records affected
        /// </summary>
        /// <param name="sql">sql text</param>
        /// <param name="timeOut">timeout connection</param>
        /// <param name="param">parameters array (parameters name: mysql-?, postgresql-@, msSql-@, SqLite-@)</param>
        int ExecuteNonQuery(string sql, int timeOut = 30, params object[] param);


        /// <summary>
        ///     Getting the name of the table to build an sql query.
        /// </summary>
        string TableName<TSource>() where TSource : class;

        /// <summary>
        ///     Getting the column name for a table
        /// </summary>
        string ColumnName<TSource>(Expression<Func<TSource, object>> property) where TSource : class;

        /// <summary>
        ///     Getting string SQL for insert command
        /// </summary>
        string GetSqlInsertCommand<TSource>(TSource source) where TSource : class;

        /// <summary>
        ///     Getting string SQL for delete command
        /// </summary>
        string GetSqlDeleteCommand<TSource>(TSource source) where TSource : class;


        /// <summary>
        ///     Getting string SQL for bulk insert command
        /// </summary>
        /// <param name="enumerable"></param>
        /// <typeparam name="TSource"></typeparam>
        string GetSqlForInsertBulk<TSource>(IEnumerable<TSource> enumerable) where TSource : class;


        /// <summary>
        ///     Write sql query directly to log file
        /// </summary>
        /// <exception cref="Exception"></exception>
        void WriteLogFile(IDbCommand command);

        /// <summary>
        ///     Gets a list of table fields
        /// </summary>
        /// <param name="tableName"></param>
        IEnumerable<TableColumn> GetTableColumns(string tableName);

        /// <summary>
        ///     Update table with additional condition where
        /// </summary>
        /// <param name="source">object for update</param>
        /// <param name="whereObjects">list condition</param>
        /// <returns>Query result: 1 - Aptly  0-Record not updated</returns>
        int Update<TSource>(TSource source, params AppenderWhere[] whereObjects) where TSource : class;

        /// <summary>
        ///     Delete table asynchronously
        /// </summary>
        Task<int> DropTableAsync<TSource>(CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Delete table if exists asynchronously
        /// </summary>
        Task<int> DropTableIfExistsAsync<TSource>(CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Create table asynchronously
        /// </summary>
        Task<int> TableCreateAsync<TSource>(CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Request ExecuteReader asynchronously
        /// </summary>
        Task<IDataReader> ExecuteReaderAsync(string sql, object[] param, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Get ExecuteReader asynchronously
        /// </summary>
        Task<IDataReader> ExecuteReaderAsync(string sql, int timeOut, object[] param,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Returns the first element of the request asynchronously
        /// </summary>
        Task<object> ExecuteScalarAsync(string sql, object[] param, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Returns the first element of the request asynchronously
        /// </summary>
        Task<object> ExecuteScalarAsync(string sql, int timeOut, object[] param,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Executes the query and returns the number of records affected asynchronously
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string sql, object[] param, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Getting DataTable  asynchronously
        /// </summary>
        Task<DataTable> GetDataTableAsync(string sql, int timeOut, object[] param,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Getting  DataTable
        /// </summary>
        DataTable GetDataTable(string sql, params object[] param);

        /// <summary>
        ///     Get DataTable  asynchronously
        /// </summary>
        Task<DataTable> GetDataTableAsync(string sql, object[] param, CancellationToken cancellationToken = default);


        /// <summary>
        ///     Asynchronously insert command
        /// </summary>
        /// <param name="source">Object for insertion</param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        Task<int> InsertAsync<TSource>(TSource source, CancellationToken cancellationToken = default)
            where TSource : class;

        /// <summary>
        ///     Update   asynchronously
        /// </summary>
        Task<int> UpdateAsync<TSource>(TSource source, AppenderWhere[] whereObjects,
            CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     Update   asynchronously
        /// </summary>
        Task<int> UpdateAsync<TSource>(TSource source,
            CancellationToken cancellationToken = default) where TSource : class;

        /// <summary>
        ///     This method begins a database transaction asynchronously
        /// </summary>
        /// <returns></returns>
        Task<ITransaction> BeginTransactionAsync();

        /// <summary>
        ///     This method begins a database transaction asynchronously
        /// </summary>
        /// <param name="value">Isolation Level</param>
        /// <returns></returns>
        Task<ITransaction> BeginTransactionAsync(IsolationLevel? value);

        /// <summary>
        ///     Approximate projection of a class (C#), from a database table
        /// </summary>
        /// <param name="tableName">Table name</param>
        string ParseTableToClass(string tableName);

        /// <summary>
        /// 
        /// </summary>
        bool IsBlobGuid { get; }

        /// <summary>
        /// Query field string that replaces the * character
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        string StarSql<TSource>();


    }
}