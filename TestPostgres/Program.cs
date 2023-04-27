﻿using ORM_1_21_;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestLibrary;


namespace TestPostgres
{
    internal class Program
    {
        private const ProviderName ProviderName = ORM_1_21_.ProviderName.MsSql;
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            switch (ProviderName)
            {
                case ProviderName.MsSql:
                    Starter.Run(ConnectionStrings.MsSql, ProviderName);
                    break;
                case ProviderName.MySql:
                    Starter.Run(ConnectionStrings.Mysql, ProviderName);
                    break;
                case ProviderName.PostgreSql:
                    Starter.Run(ConnectionStrings.Postgesql, ProviderName);
                    break;
                case ProviderName.SqLite:
                    Starter.Run(ConnectionStrings.Sqlite, ProviderName);//
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            //Execute.RunOtherSession();
            //Execute.RunThread();
            //Console.ReadKey();
            //Console.ReadKey();
            Execute.TotalTest();
            Execute.TestNativeInsert();
            Execute.TestAssignetInsert();
            Execute2.TestTimeStamp();
            await Execute3.TotalTestAsync();
            await ExecuteLinqAll.Run();
            ExecutePrimaryKey.Run();
            await ExecuteFree.Run();
            await ExecuteSp.Run();
            TestCapacity.Run();
            await TestSelector.Run();
            InsertUpdate.Run();





            ISession session = Configure.Session;
            {
                var sas = session.IsBlobGuid;
                session.DropTableIfExists<Sqlite>();
                session.TableCreate<Sqlite>();
                var rr = new Sqlite();
                // session.InsertBulk(new List<Sqlite>
                // {
                //    rr
                // });
                session.Insert(rr);
                var res = session.Query<Sqlite>().First();



            }



        }
        [MapTable("sqlite1")]
        class Sqlite
        {
            [MapPrimaryKey(Generator.Assigned)]
            public Guid id { get; set; } = Guid.NewGuid();

            [MapColumn] public DateTime DateTime { get; set; } = Configure.Utils.DefaultSqlDateTime();
            // [MapColumn]
            // public Guid idcore { get; set; }
            // [MapColumn]
            // public Guid? idcorenull { get; set; }
            // [MapColumn]
            // public string name { get; set; }
            // [MapColumn]
            // public int? IntNull { get; set; }
            // [MapColumn]
            // public bool Bool { get; set; }
        }




    }













}
