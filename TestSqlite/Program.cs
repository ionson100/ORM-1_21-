﻿using ORM_1_21_;
using ORM_1_21_.Attribute;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestSqlite
{

    internal class Program
    {
        static async Task Main(string[] args)
        {
            Starter.Run();
            new Thread(() =>
            {
                while (true)
                {
                    var ses = Configure.Session;
                    var t = ses.BeginTransaction(IsolationLevel.Serializable);
                    MyClass c = new MyClass();
                    ses.Save(c);
                    
                    var s = Configure.Session.Querion<MyClass>().Where(a => a.Age != -1).ToList();
                    Console.WriteLine("1 -- "+s.Count());
                    t.Commit();

                }

            }).Start();

            new Thread(() =>
            {
                while (true)
                {
                    var ses = Configure.Session;
                    var t = ses.BeginTransaction(IsolationLevel.Serializable);
                    MyClass c = new MyClass();
                    ses.Save(c);

                    var s = Configure.Session.Querion<MyClass>().Where(a => a.Age != -1).ToList();
                    Console.WriteLine("2 -- " + s.Count());
                    t.Commit();
                }

            }).Start();
            Console.ReadKey();
            //  List<TableColumn> list33 = Configure.Session.GetTableColumns(Configure.Session.TableName<MyClass>()).ToList();
            // 
            //  MyClass myClass = new MyClass()
            //  {
            //      Age = 12,
            //      Description = "simple",
            //      Name = "ion100FROMfromFrom ass",
            //      DateTime = DateTime.Now
            //  };
            //  Configure.Session.Save(myClass);
            //  List<MyClass> classes = new List<MyClass>()
            // {
            //     new MyClass()
            //     {
            //         Age = 12,
            //         Description = "simple",
            //         Name = "ion100FROMfromFrom ass",
            //         DateTime = DateTime.Now
            //     },
            //     new MyClass()
            //     {
            //         Age = 121,
            //         Description = "simple",
            //         Name = "ion100FROMfromFrom ass",
            //         DateTime = DateTime.Now
            //     },
            //     new MyClass()
            //     {
            //         Age = 121,
            //         Description = "simple",
            //         Name = "ion100FROMfromFrom ass",
            //         DateTime = DateTime.Now
            //     }
            // };
            //  Configure.Session.InsertBulk(classes);
            //  var i = Configure.Session.Querion<MyClass>().Where(a => a.Age == 12).
            //      Update(s => new Dictionary<object, object> { { s.Age, 100 }, { s.Name, "simple" } });
            //  var @calss = Configure.Session.GetList<MyClass>("age =100 order by age ").FirstOrDefault();
            //  var eee = Configure.Session.ExecuteScalar("SELECT name FROM sqlite_temp_master WHERE type='table';");
            //  var list = Configure.Session.Querion<MyClass>().Where(a => a.Age > 5).ToList();
            //  var list1 = Configure.Session
            //      .FreeSql<MyClass>($"select * from {Configure.Session.TableName<MyClass>()}");
            //  var list2 = Configure.Session.Querion<MyClass>().Select(a => new { ageCore = a.Age, name = a.Name }).ToList();
            //  var list3 = Configure.Session.Querion<MyClass>()
            //      .Where(a => a.Age > 5 ).OrderBy(d => d.Age)
            //      .Select(f => new { age = f.Age }).Limit(0, 2);
            //  var r = await list3.ToListAsync();
            //  foreach (var v in  r)
            //  {
            //      Console.WriteLine(r);
            //  }
            //
            //  var ss = Configure.Session.FreeSql<MyClass>("select * from my_class where desc like $name",new Parameter("$name","'si%'"));

        }
    }

    static class Starter
    {
        //PRAGMA table_info("my_class")
        public static void Run()
        {

            string path = null;
#if DEBUG
            path = "SqlLog.txt";
#endif
            _ = new Configure("Data Source=mydb.db;Version=3",
                ProviderName.Sqlite, path);
            using (var ses = Configure.Session)
            {
                if (ses.TableExists<MyClass>())
                    ses.DropTable<MyClass>();
                if (!ses.TableExists<MyClass>())
                {
                    ses.TableCreate<MyClass>();
                }
            }



        }
    }

    [MapTableName("my_class")]
    class MyClass
    {
        [MapPrimaryKey("id", Generator.Native)]
        public int Id { get; set; }

        [MapColumnName("name")] public string Name { get; set; }
        [MapColumnName("age")][MapIndex] public int Age { get; set; }
        [MapColumnName("age1")][MapIndex] public int? Age1 { get; set; }

        [MapColumnName("desc")]
        [MapColumnType("TEXT")]
        public string Description { get; set; }
        [MapColumnName("price")]
        public decimal? Price { get; set; }

        [MapColumnName("enum")] public MyEnum MyEnum { get; set; } = MyEnum.First;
        [MapColumnName("date")] public DateTime? DateTime { get; set; }
        [MapColumnName("test")]
        public List<Test23> Test23 { get; set; } = new List<Test23>() { new Test23() { Name = "simple" }

        };



    }

    class Test23
    {
        public string Name { get; set; }
    }

    enum MyEnum
    {
        Def = 0, First = 1
    }

}
