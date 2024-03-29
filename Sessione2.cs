﻿using ORM_1_21_.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ORM_1_21_
{
    internal sealed partial class Session
    {
        private readonly IDbConnection _connect;
        private readonly Guid _id;
        private readonly IOtherDataBaseFactory _factoryOtherBase;
        private bool _isDispose;
        internal readonly Transactionale Transactionale = new Transactionale();
        private readonly string _connectionString;

        internal IDbTransaction Transaction
        {
            get => Transactionale.Transaction;
            set => Transactionale.Transaction = value;
        }

        internal ProviderName MyProviderName
        {
            get
            {
                if (_factoryOtherBase == null)
                {
                    return Configure.Provider;
                }

                return _factoryOtherBase.GetProviderName();
            }
        }



        public Session(string connectionString)
        {
            _connectionString = connectionString;
            _id = Guid.NewGuid();
            Check.NotEmpty(connectionString, "connectionString");
            _connect = ProviderFactories.GetConnect(null);
            _connect.ConnectionString = connectionString;
        }
        /// <summary>
        /// Constructor for connecting to another database
        /// </summary>
        /// <param name="factoryOtherBase">Object implementing IOtherDataBaseFactory</param>
        public Session(IOtherDataBaseFactory factoryOtherBase)
        {
            _factoryOtherBase = factoryOtherBase;
            _connect = factoryOtherBase.GetDbProviderFactories().CreateConnection();
            if (_connect == null) throw new Exception("Can't connect to another database");
            _connect.ConnectionString = factoryOtherBase.GetConnectionString();
            _connectionString = factoryOtherBase.GetConnectionString();
            if (factoryOtherBase.GetProviderName() == ProviderName.PostgreSql)
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
            }

        }





        private static void NotificAfter<TSource>(TSource source, ActionMode mode) where TSource : class
        {
            if (mode == ActionMode.None) return;
            if (!(source is IMapAction<TSource> actionDal)) return;
            switch (mode)
            {
                case ActionMode.Insert:
                    actionDal.ActionCommand(source, CommandMode.AfterInsert);
                    break;
                case ActionMode.Update:
                    actionDal.ActionCommand(source, CommandMode.AfterUpdate);
                    break;
                case ActionMode.Delete:
                    actionDal.ActionCommand(source, CommandMode.AfterDelete);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static void NotificBefore<TSource>(TSource source, ActionMode mode) where TSource : class
        {


            if (mode == ActionMode.None) return;
            if (!(source is IMapAction<TSource> actionDal)) return;
            switch (mode)
            {
                case ActionMode.Insert:
                    actionDal.ActionCommand(source, CommandMode.BeforeInsert);
                    break;
                case ActionMode.Update:
                    actionDal.ActionCommand(source, CommandMode.BeforeUpdate);
                    break;
                case ActionMode.Delete:
                    actionDal.ActionCommand(source, CommandMode.BeforeDelete);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

        }

        ITransaction ISession.BeginTransaction()
        {
            if (Transactionale.MyStateTransaction == StateTransaction.Begin)
            {
                Transactionale.isError = true;
                throw new Exception("Transaction opened earlier");
            }
            Transactionale.MyStateTransaction = StateTransaction.Begin;
            Transactionale.IsolationLevel = null;
            Transactionale.Connection = _connect;
            Transactionale.isError = false;
            return Transactionale;
        }
        Task<ITransaction> ISession.BeginTransactionAsync()
        {
            var tk = new TaskCompletionSource<ITransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (Transactionale.MyStateTransaction == StateTransaction.Begin)
            {
                Transactionale.isError = true;
                throw new Exception("Transaction opened earlier");
            }
            Transactionale.MyStateTransaction = StateTransaction.Begin;
            Transactionale.IsolationLevel = null;
            Transactionale.Connection = _connect;
            Transactionale.isError = false;
            tk.SetResult(Transactionale);
            return tk.Task;
        }


        ITransaction ISession.BeginTransaction(IsolationLevel? value)
        {
            if (Transactionale.MyStateTransaction == StateTransaction.Begin)
            {
                Transactionale.isError = true;
                throw new Exception("Transaction opened earlier");
            }
            Transactionale.MyStateTransaction = StateTransaction.Begin;
            Transactionale.Connection = _connect;
            Transactionale.IsolationLevel = value;
            Transactionale.isError = false;
            return Transactionale;
        }

        Task<ITransaction> ISession.BeginTransactionAsync(IsolationLevel? value)
        {
            var tk = new TaskCompletionSource<ITransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (Transactionale.MyStateTransaction == StateTransaction.Begin)
            {
                Transactionale.isError = true;
                throw new Exception("Transaction opened earlier");
            }
            Transactionale.MyStateTransaction = StateTransaction.Begin;
            Transactionale.Connection = _connect;
            Transactionale.IsolationLevel = value;
            Transactionale.isError = false;
            tk.SetResult(Transactionale);
            return tk.Task;
        }



        public string ParseTableToClass(string tableName)
        {
            var res = ((ISession)this).GetTableColumns(tableName);
            if (!res.Any()) throw new Exception($"Table: {tableName} not found in database.");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"[MapTable(\"{UtilsCore.ClearTrim(tableName)}\")]");
            builder.AppendLine($"class {UtilsCore.ClearTrim(tableName)}");
            builder.AppendLine("{");
            int ic = 0;
            foreach (TableColumn tableColumn in res.OrderByDescending(a => a.IsPk))
            {
                if (ic == 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"   [MapPrimaryKey(\"{tableColumn.ColumnName}\", Generator.Native)]");
                    builder.AppendLine($"   public {UtilsCore.ColumnBuilder(tableColumn.ColumnType, tableColumn.IsPk)} {tableColumn.ColumnName} {{get;set;}}");
                }
                else
                {
                    builder.AppendLine();
                    builder.AppendLine($"   [MapColumn(\"{tableColumn.ColumnName}\")]");
                    builder.AppendLine($"   public {UtilsCore.ColumnBuilder(tableColumn.ColumnType)} {tableColumn.ColumnName} {{get;set;}}");
                }
                ic++;
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        ProviderName ISession.ProviderName => MyProviderName;

        string ISession.SymbolParam
        {
            get
            {
                switch (MyProviderName)
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
                        throw new ArgumentOutOfRangeException($"Database type is not defined:{MyProviderName}");
                }
            }
        }

        int ISession.DefaultTimeOut
        {
            get
            {
                switch (MyProviderName)
                {
                    case ProviderName.MsSql:
                        return 30;
                    case ProviderName.MySql:
                        return 30;
                    case ProviderName.PostgreSql:
                        return 30;
                    case ProviderName.SqLite:
                        return 30;
                    default:
                        throw new ArgumentOutOfRangeException($"Database type is not defined:{MyProviderName}");
                }
            }
        }
        string ISession.IdSession => _id.ToString();

        ///<summary>
        /// Disposing
        ///</summary>
        public void Dispose()
        {
            InnerDispose();
        }
        public async Task DisposeAsync()
        {
            await InnerDisposeAsync();
        }


        internal async Task OpenConnectAndTransactionAsync(IDbCommand com)
        {
            if (com.Connection.State == ConnectionState.Closed)
            {
                await com.Connection.OpenAsync();
                if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                {

                    if (Transactionale.IsolationLevel == null)
                    {
                        Transaction = await _connect.BeginTransactionAsync();
                    }
                    else
                    {
                        Transaction = await _connect.BeginTransactionAsync(Transactionale.IsolationLevel.Value);
                    }
                    Transactionale.ListDispose.Add(com);
                    com.Transaction = Transaction;
                }
            }
            else
            {
                if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                {
                    com.Transaction = Transaction;
                }
            }
        }

        internal void OpenConnectAndTransaction(IDbCommand com)
        {
            if (com.Connection.State == ConnectionState.Closed)
            {
                com.Connection.Open();
                if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                {

                    if (Transactionale.IsolationLevel == null)
                    {
                        Transaction = _connect.BeginTransaction();
                    }
                    else
                    {
                        Transaction = _connect.BeginTransaction(Transactionale.IsolationLevel.Value);
                    }
                    Transactionale.ListDispose.Add(com);
                    com.Transaction = Transaction;
                }
            }
            else
            {
                if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                {
                    com.Transaction = Transaction;
                }
            }
        }

        bool ISession.IsDispose => _isDispose;


        ~Session()
        {
            InnerDispose(true);
        }

        void InnerDispose(bool isFinalize = false)
        {
            if (_isDispose) return;
            try
            {
                if (Transactionale.Transaction != null)
                {
                    if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                    {
                        Transactionale.Transaction.Rollback();
                    }
                }
                Transactionale?.ListDispose.ForEach(a => a.Dispose());
                if (_connect != null)
                    _connect.Dispose();

                _isDispose = true;
                foreach (var dbCommand in _dbCommands)
                {
                    dbCommand.Dispose();
                }
                _dbCommands.Clear();
                if (isFinalize == false)
                    GC.SuppressFinalize(this);
            }
            catch (Exception)
            {
                //ignored
            }
        }

        async Task InnerDisposeAsync(bool isFinalize = false)
        {
            if (_isDispose) return;
            try
            {
                if (Transactionale.Transaction != null)
                {
                    if (Transactionale.MyStateTransaction == StateTransaction.Begin)
                    {
                        Transactionale.Transaction.Rollback();
                    }
                }
                Transactionale?.ListDispose.ForEach(a => a.Dispose());
                if (_connect != null)
                    await _connect.DisposeAsync();

                _isDispose = true;
                foreach (var dbCommand in _dbCommands)
                {
                    await dbCommand.DisposeAsync();
                }
                _dbCommands.Clear();
                if (isFinalize == false)
                    GC.SuppressFinalize(this);
            }
            catch (Exception)
            {
                //ignored
            }
        }

        void ISession.WriteLogFile(string message)
        {
            InnerWriteLogFile($"WriteLogFile: {message}");
        }

        private static void InnerWriteLogFile(string message)
        {
            MySqlLogger.Info(message);
        }

        private static void InnerWriteLogFile(IDbCommand command)
        {
            MySqlLogger.Info(UtilsCore.GetStringSql(command));

        }


        void ISession.WriteLogFile(IDbCommand command)
        {
            InnerWriteLogFile($"WriteLogFile: {command}");

        }

        IEnumerable<TableColumn> ISession.GetTableColumns(string tableName)
        {
            Check.NotEmpty(tableName, "tableName", () => Transactionale.isError = true);
            var com = ProviderFactories.GetCommand(_factoryOtherBase, ((ISession)this).IsDispose);
            com.Connection = _connect;

            try
            {
                return ColumnsTableFactory.GeTableColumns(MyProviderName, com, UtilsCore.ClearTrim(tableName.Trim()));
            }
            catch (Exception ex)
            {
                Transactionale.isError = true;
                MySqlLogger.Error(UtilsCore.GetStringSql(com), ex);
                throw;
            }
            finally
            {
                ComDisposable(com);
            }
        }
    }
}
