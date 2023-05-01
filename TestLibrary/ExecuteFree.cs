﻿using ORM_1_21_;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestLibrary
{
    public static class ExecuteFree
    {
        public static async Task Run()
        {

            await InnerRun<MyClassPostgres, MyDbPostgres>();
            await InnerRun<MyClassMysql, MyDbMySql>();
            await InnerRun<MyClassMsSql, MyDbMsSql>();
            await InnerRun<MyClassSqlite, MyDbSqlite>();
        }

        private static async Task InnerRun<T, TB>() where T : MyClassBase, new() where TB : IOtherDataBaseFactory, new()
        {
            var s = Activator.CreateInstance<TB>();
            Console.WriteLine($"**************************{s.GetProviderName()}*****************************");
            ISession session = Configure.GetSession<TB>();
            await session.DropTableIfExistsAsync<T>();
            await session.TableCreateAsync<T>();
            session.InsertBulk(new List<T>
            {
                new T { Age = 100, Name = "100",DateTime = DateTime.Now},
                new T { Age = 10, Name = "10",DateTime = DateTime.Now},
            });
            var list = session.FreeSql<T>($"select * from {session.TableName<T>()}");
            Execute.Log(1, list.Count() == 2);

            list = session.FreeSql<T>($"select * from {session.TableName<T>()} where {session.ColumnName<T>(a => a.Age)} = {session.SymbolParam}1", 100);
            Execute.Log(2, list.Count() == 1);

            /*-----  2 ------*/

            list = session.FreeSql<T>($"select * from {session.TableName<T>()} " +
                                      $"where {session.ColumnName<T>(a => a.Age)} = {session.SymbolParam}1 or " +
                                      $" {session.ColumnName<T>(a => a.Age)}={session.SymbolParam}2", 100, 10);
            Execute.Log(2, list.Count() == 2);


            /*-----  datetime ------*/

            list = await session.FreeSqlAsync<T>($"select * from {session.TableName<T>()} " +
                                      $"where date >{session.SymbolParam}1 and date<{session.SymbolParam}2", DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1));
            Execute.Log(3, list.Count() == 2);



            list = session.FreeSql<T>($"select * from {session.TableName<T>()} " +
                                                 $"where date >{session.SymbolParam}1 and date<{session.SymbolParam}2", DateTime.Now.AddDays(-1), DateTime.Now.AddDays(1));
            Execute.Log(4, list.Count() == 2);


            var listInt = session.FreeSql<int>($"select age from {session.TableName<T>()}").ToList();
            foreach (int i in listInt)
            {
                Console.WriteLine(i);
            }

            listInt = (List<int>)await session.FreeSqlAsync<int>($"select age from {session.TableName<T>()}");
            foreach (int i in listInt)
            {
                Console.WriteLine(i);
            }
            var tt = await session.FreeSqlAsync<Guid>($"select {session.ColumnName<T>(a => a.Id)} from {session.TableName<T>()}");
            foreach (Guid i in tt)
            {
                Console.WriteLine(i);
            }

            var ttU = await session.FreeSqlAsync<dynamic>($"select testuser from {session.TableName<T>()}");
            foreach (dynamic i in ttU)
            {
                Console.WriteLine(i.testuser);
            }

            var ttUp = await session.FreeSqlAsync<ProxyFreeSql>($"select id,age,testuser  from {session.TableName<T>()}");
            foreach (ProxyFreeSql i in ttUp)
            {
                Console.WriteLine(i.ToString());
            }



            var res = session.FreeSqlAsTemplate(new { id = Guid.Empty, age = 1 },
                $"select id, age from {session.TableName<T>()}").ToList();
            foreach (var re in res)
            {
                Console.WriteLine(re);
            }
            var tst = await session.FreeSqlAsTemplateAsync(new { id = Guid.Empty, age = 1 ,ss=MyEnum.First},
                $"select id, age, enum from {session.TableName<T>()}");
            foreach (var x1 in tst)
            {
                Console.WriteLine(x1);
            }

            list = session.FreeSql<T>($"select * from {session.TableName<T>()}").ToList();
            foreach (T f in list)
            {
                f.ValInt = 10;
                f.MyEnum = MyEnum.First;
                f.Valfloat = 3.4f;
                f.Name = null;
                f.DateTimeNull= Configure.Utils.DefaultSqlDateTime();
                f.Bytes =  new byte[0];
                

                session.Update(f);
            }
            list = session.FreeSql<T>($"select * from {session.TableName<T>()}").ToList();
            var free = session.FreeSql<FreeClass>($"select * from {session.TableName<T>()}").ToList();
            free.ForEach(Console.WriteLine);
            free = (List<FreeClass>)await session.FreeSqlAsync<FreeClass>($"select * from {session.TableName<T>()}");
            free.ForEach(Console.WriteLine);



        }

        class FreeClass
        {
            public byte[] bytes { get; set; } = {  };
            public DateTime? datenull { get; set; }
            public string name { get; set; }
            public Guid id { get; set; }
            public int age { get; set; }
            public int? test1 { get; set; }

            public int @enum { get; set; }
             public float? test6 { get; set; }
        }
        [MapReceiverFreeSql]
        public class ProxyFreeSql
        {
            public ProxyFreeSql(Guid id, int age, string user)
            {
                Id = id;
                Age = age;
                User = user;
            }

            private Guid Id { get; }
            private int Age { get; }
            private string User { get; }
            public override string ToString()
            {
                return $"id=\"{Id}\"  Age=\"{Age}\"  ";
            }
        }
        public class MyClassproxy
        {
           

            public int Issa { get; set; } = 100;


            public Guid Id { get; set; } = Guid.NewGuid();


             public string Name { get; set; }

           
            public int Age { get; set; }

            
            public string Description { get; set; }

            public MyEnum MyEnum { get; set; } 

             public DateTime DateTime { get; set; } 

             public int? ValInt { get; set; }

             public bool? Valbool { get; set; }

             public double? Valdouble { get; set; }

             public decimal? Valdecimal { get; set; }

             public float? Valfloat { get; set; }

             public short? ValInt16 { get; set; }

             public long? ValInt4 { get; set; }

            public Guid? ValGuid { get; set; }

             public string TestUser { get; set; } 
        }
    }
}
