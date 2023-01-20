#### ORM-1_21
��� ( MySql,PostgreSQL,MSSQL,Sqlite)\
����������� �������������:
```
   string path = null;
#if DEBUG
   path = "SqlLog.txt";
#endif
    _ = new Configure("ConnektionString",ProviderName.MsSql, path);
```
� ������ ������� ��������� ���� ����:SqlLog.txt\
��������: ���� �� ����������� �� �����!\
���� ������� ������� � ����, � ���������� � �������.
```
IF not exists (select 1 from information_schema.tables where table_name = 'my_class')CREATE TABLE [dbo].[my_class](
[id] uniqueIdentifier default (newId()) primary key,
 [name] [NVARCHAR] (256) NULL,
 [age] [INT] NOT NULL DEFAULT '0',
 [desc] TEXT NULL,
 [age1] [INT] NULL,
 [price] [decimal] NULL,
 [enum] [INT] NOT NULL DEFAULT '0',
 [date] [DATETIME] NULL,
 [test] [nvarchar] (max) NULL,
);
CREATE INDEX [INDEX_my_class] ON [my_class] ([age]
,[age1]);
INSERT INTO [my_class] ([my_class].[id], [my_class].[name],[my_class].[age],[my_class].[desc],[my_class].[age1],[my_class].[price],[my_class].[enum],[my_class].[date],[my_class].[test])VALUES (@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9);SELECT IDENT_CURRENT ('[my_class]'); params:  @p1 - 0d3541c6-6f02-45d6-b808-351b1927c77f  @p2 - ion100FROMfromFrom ass  @p3 - 12  @p4 - simple  @p5 -   @p6 -   @p7 - 1  @p8 - 20.01.2023 9:53:57  @p9 - [{"Name":"simple"}] 
```
����� ���� ����, �������������� ������ ���������� ������������.\
������ �������� - ������ �����������: [ConnectionString](https://www.connectionstrings.com)\
��� ������������ ���������� ������������, ������ �� ����� ��������� ���������� ������� ���������� ���\
��������� ���� ������, ����� NuGet (Npgsql,Mysql.Data,System.Data.SQLite,System.Data.SqlClient)\
��������: ��� PostgreSQL, �������� ���� �������������� � ������ ������.
###### ������ ������.
```
 [MapTableName("my_class")]
 class MyClass
    {
        [MapPrimaryKey("id", Generator.Assigned)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MapColumnName("name")] 
        public string Name { get; set; }

        [MapColumnName("age")] 
        [MapIndex] 
        public int Age { get; set; }

        [MapColumnName("desc")]
        [MapColumnType("TEXT NULL")]
        public string Description { get; set; }

        [MapColumnName("enum")] 
        public MyEnum MyEnum { get; set; } = MyEnum.First;

        [MapColumnName("date")] 
        public DateTime DateTime { get; set; } = DateTime.Now;

        [MapColumnName("test")] 
        public List<MyTest> Test23 { get; set; } = new List<MyTest>() { new MyTest() { Name = "simple" }
    };
```
�������� �� ���������� ���������, � ������� �� ������������.\
�������� ���������� �������� ������ � ������� ����� ������������� ��������,\
���� ����� �������� - ����� ������� �������� ������ ��� �������� �������.\
������� �������� ���������� ����� - �����������.\
��������� ���������� �����: Generator.Native, ������� ���������������� ���� � �������.\
����������� ����� (int)\
��� ���� ���� SQLite, �������� ������ ���� - Generator.Native.\
������ ��� ������� � PosgreSql  � ����:\
```
CREATE TABLE IF NOT EXISTS "my_class" (
 "id" UUID  PRIMARY KEY,
 "name" VARCHAR(256) NULL ,
 "age" INTEGER NOT NULL DEFAULT '0' ,
 "desc" TEXT NULL ,
 "enum" INTEGER NOT NULL DEFAULT '0' ,
 "date" TIMESTAMP NULL ,
 "test" TEXT NULL );
CREATE INDEX IF NOT EXISTS INDEX_my_class_age ON "my_class" ("age");
```

