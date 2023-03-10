using MySql.Data.MySqlClient;
using ORM_1_21_;
using System;
using System.Data.Common;

namespace TestLibrary
{
  public  class MyDbMySql : IOtherDataBaseFactory
    {
        public const string s1 = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=audi124;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        public const string s2 = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=test;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

        private static readonly Lazy<DbProviderFactory> DbProviderFactory = new Lazy<DbProviderFactory>(() => new MySqlClientFactory());
        public ProviderName GetProviderName()
        {
            return ProviderName.MySql;
        }
        public string GetConnectionString()
        {
            return "Server=localhost;Database=test;Uid=root;Pwd=12345;";
        }

        public DbProviderFactory GetDbProviderFactories()
        {
            return DbProviderFactory.Value;
        }
    }
  public class MyDbPostgres : IOtherDataBaseFactory
  {
      private static readonly Lazy<DbProviderFactory> DbProviderFactory = new Lazy<DbProviderFactory>(() => Npgsql.NpgsqlFactory.Instance);
      public ProviderName GetProviderName()
      {
          return ProviderName.Postgresql;
      }
      public string GetConnectionString()
      {
          return "Server=localhost;Port=5432;Database=testorm;User Id=postgres;Password=ion100312873;";
      }

      public DbProviderFactory GetDbProviderFactories()
      {
          return DbProviderFactory.Value;
      }
  }
  public class MyDbMsSql : IOtherDataBaseFactory
  {
      private static readonly Lazy<DbProviderFactory> DbProviderFactory = new Lazy<DbProviderFactory>(() => System.Data.SqlClient.SqlClientFactory.Instance);
      public ProviderName GetProviderName()
      {
          return ProviderName.MsSql;
      }
      public string GetConnectionString()
      {
          return MyDbMySql.s2;
      }

      public DbProviderFactory GetDbProviderFactories()
      {
          return DbProviderFactory.Value;
      }
  }
  public class MyDbSqlite : IOtherDataBaseFactory
  {
      private static readonly Lazy<DbProviderFactory> DbProviderFactory = new Lazy<DbProviderFactory>(() => System.Data.SQLite.SQLiteFactory.Instance);
      public ProviderName GetProviderName()
      {
          return ProviderName.Sqlite;
      }
      public string GetConnectionString()
      {
          return "Data Source=mydb.db;Version=3;BinaryGUID=False;";
      }

      public DbProviderFactory GetDbProviderFactories()
      {
          return DbProviderFactory.Value;
      }
  }
}