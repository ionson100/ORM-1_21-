﻿using Newtonsoft.Json;
using ORM_1_21_.geo;
using ORM_1_21_.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace ORM_1_21_.Linq
{
    internal sealed partial class QueryTranslator<T> : ExpressionVisitor, ITranslate
    {
        //private bool isAddPost=false;
        private readonly List<PostExpression> _postExpressions = new List<PostExpression>();
        private readonly ProviderName _providerName;

        private string _currentMethodSelect;
        private Type _currentMethodType;
        private string _currentMethodWhere;
        private int _paramIndex;

        public QueryTranslator(ProviderName name, int paramIndex = 0)
        {
            _providerName = name;
            Param = new Dictionary<string, object>();
            _paramIndex = paramIndex;
        }

        private string ParamName => string.Format("{0}{1}",
            string.Format("{1}{0}", ParamStringName, UtilsCore.PrefParam(_providerName)), ++_paramIndex);

        private Evolution CurrentEvolution { get; set; }

        private string ParamStringName { get; set; } = "p";

        private StringBuilder StringB { get; set; } = new StringBuilder();

        public Dictionary<string, object> Param { get; set; }

        public List<OneComposite> ListOne { get; } = new List<OneComposite>();

        public string Translate(Expression expression, out Evolution ev)
        {
            CurrentEvolution = 0;
            Visit(expression);
            ev = CurrentEvolution;
            if (_providerName == ProviderName.MsSql)
            {
                var dd = new MsSqlConstructorSql().GetStringSql<T>(ListOne, _providerName);
                return dd;
            }
            else
            {
                var dd = new MySqlConstructorSql(_providerName).GetStringSql<T>(ListOne, _providerName); //,_joinCapital
                return dd;
            }
        }

        public void Translate(Expression expression, Evolution ev, List<object> paramList)
        {
            CurrentEvolution = ev;
            if (ev == Evolution.Update)
            {
                {
                    StringB.Clear();
                    if (expression is LambdaExpression l)
                    {
                        if (l.Body is ListInitExpression list)
                        {
                            //UnaryExpression
                            //ST_GeomFromText(@p1,@srid1)
                            var listNewExpression = list.Initializers;
                            foreach (ElementInit elementInit in listNewExpression)
                            {
                                Visit(elementInit.Arguments[0]);
                                StringB.Append(" = ");
                                //object param = null;
                                //var param = Osman(elementInit);


                                var typeProperty = elementInit.Arguments[0].Type;
                                string name = null;
                                var v = elementInit.Arguments[0] as MemberExpression;
                                if (v != null)
                                {
                                    name = v.Member.Name;
                                }
                                else
                                {
                                    if (elementInit.Arguments[0] is UnaryExpression ss)
                                    {
                                        var ee = ss.Operand as MemberExpression;
                                        name = ee.Member.Name;
                                    }
                                }


                                if (elementInit.Arguments[0] is UnaryExpression member)
                                {
                                    typeProperty = member.Operand.Type;
                                }

                                if (UtilsCore.IsGeo(typeProperty))
                                {
                                    var param = Osman(elementInit);
                                    if (param == null)
                                    {
                                        StringB.Append(" null, ");
                                    }
                                    else
                                    {
                                        IGeoShape shape = (IGeoShape)param;
                                        if (_providerName == ProviderName.MsSql)
                                        {
                                            StringB.Append("geometry::STGeomFromText(");
                                            AddParameter(shape.StAsText());
                                            StringB.Append(',');
                                            AddParameter(shape.StSrid());
                                            StringB.Append("), ");
                                        }
                                        else
                                        {
                                            StringB.Append("ST_GeomFromText(");
                                            AddParameter(shape.StAsText());
                                            StringB.Append(',');
                                            AddParameter(shape.StSrid());
                                            StringB.Append("), ");
                                        }
                                    }
                                }
                                else if (AttributesOfClass<T>.IsJsonName(name))
                                {
                                    var param = Osman(elementInit);
                                    if (param == null)
                                    {
                                        StringB.Append(" null, ");
                                    }
                                    else
                                    {
                                        if (_providerName == ProviderName.MsSql || _providerName == ProviderName.SqLite)
                                        {
                                            if (param is string p)
                                            {
                                                AddParameter(p);
                                            }
                                            else
                                            {
                                                AddParameter(JsonConvert.SerializeObject(param));
                                            }
                                        }
                                        else
                                        {
                                            StringB.Append("CAST(");
                                            if (param is string p)
                                            {
                                                AddParameter(p);
                                            }
                                            else
                                            {
                                                AddParameter(JsonConvert.SerializeObject(param));
                                            }

                                            StringB.Append(" as JSON), ");
                                        }
                                    }
                                }
                                else
                                {
                                    Visit(elementInit.Arguments[1]);
                                    StringB.Append(", ");
                                }
                            }

                            //;
                        }
                    }

                    AddListOne(new OneComposite
                        { Operand = Evolution.Update, Body = StringB.ToString().Trim(',', ' ') });
                    StringB.Clear();
                }
                return;
            }

            Visit(expression);

            if (ev == Evolution.Between)
            {
                StringB.Append(" BETWEEN ");
                Visit((Expression)paramList[0]);
                StringB.Append(" AND ");
                Visit((Expression)paramList[1]);

                AddListOne(new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() });
            }

            if (ev == Evolution.Delete)
            {
                AddListOne(new OneComposite { Operand = Evolution.Delete });
                AddListOne(new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() });
            }

            if (ev == Evolution.DistinctCore)
                AddListOne(new OneComposite { Operand = Evolution.DistinctCore, Body = StringB.ToString() });

            if (ev == Evolution.Limit)
                if (paramList != null && paramList.Count == 2)
                    switch (_providerName)
                    {
                        case ProviderName.MsSql:
                        {
                            AddListOne(new OneComposite
                            {
                                Operand = Evolution.Limit,
                                Body =
                                    $"LIMIT {paramList[0]},{paramList[1]}"
                            });
                        }
                            break;
                        case ProviderName.MySql:
                        {
                            AddListOne(new OneComposite
                            {
                                Operand = Evolution.Limit,
                                Body = string.Format(" Limit {0},{1}", paramList[0], paramList[1])
                            });
                        }
                            break;
                        case ProviderName.PostgreSql:
                        case ProviderName.SqLite:
                        {
                            AddListOne(new OneComposite
                            {
                                Operand = Evolution.Limit,
                                Body = string.Format(" Limit {1} OFFSET {0}", paramList[0], paramList[1])
                            });
                        }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Database type is not defined:{_providerName}");
                    }


            StringB.Length = 0;
        }

        private static object Osman(ElementInit elementInit)
        {
            var type = elementInit.Arguments[1].NodeType;
            if (type != ExpressionType.Constant)
            {
                return Expression.Lambda(elementInit.Arguments[1]).Compile().DynamicInvoke();
            }
            else
            {
                if (elementInit.Arguments[1] is ConstantExpression d)
                {
                    return d.Value;
                }
            }

            throw new Exception("osman");
        }

        public void Translate(Expression expression)
        {
            Visit(expression);
        }


        public List<OneComposite> GetListOne()
        {
            var list = new List<OneComposite>();
            foreach (var item in ListOne) list.Add(item);
            return list;
        }

        public List<PostExpression> GetListPostExpression()
        {
            return _postExpressions;
        }

        public string Translate(Expression expression, out Evolution ev1, string par)
        {
            ParamStringName = par;
            return Translate(expression, out ev1);
        }

        public void AddPostExpression(Evolution evolution, MethodCallExpression expression)
        {
            _postExpressions.Add(new PostExpression(evolution, expression));
        }

        private bool PingComposite(Evolution eval)
        {
            return ListOne.Any(a => a.Operand == eval);
        }

        private void AddListOne(OneComposite composite)
        {
            ListOne.Add(composite);
        }

        public int GetParamIndex()
        {
            return _paramIndex;
        }

        public string FieldOne()
        {
            return StringB.ToString();
        }

        private string GetColumnName(string member)
        {
            var ss = AttributesOfClass<T>.GetNameFieldForQuery(member, _providerName);
            return ss;
        }

        private bool GetIsJson(string member)
        {
            var ss = AttributesOfClass<T>.GetIsJson(member, _providerName);
            return ss;
        }


        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote) e = ((UnaryExpression)e).Operand;
            return e;
        }

        protected override Expression VisitMethodCallUpdate(MethodCallExpression m)
        {
            if (m.Method.Name == "Format")
            {
                string sql = StringB.ToString();
                StringB.Clear();
                var p = m.Arguments[0] as ConstantExpression;
                List<string> list = new List<string>();
                for (var i = 0; i < m.Arguments.Count; i++)
                {
                    if (i == 0) continue;
                    VisitUpdateBinary(m.Arguments[i]);
                    list.Add(StringB.ToString().Trim());
                    StringB.Clear();
                }

                string sBody = string.Format((string)p.Value, list.ToArray());
                StringB.Append(sql).Append(sBody);
                return m;
            }
            else
            {
                var value = UtilsCore.Compile(m);
                StringB.Append(value);
                //var obj = VisitUpdateBinary(m);
                //IEnumerable<Expression> args = VisitExpressionListUpdate(m.Arguments);
                //if (obj != m.Object || args != m.Arguments)
                //{
                //    return Expression.Call(obj, m.Method, args);
                //}
                return m;
            }
        }


        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (GeoMethodCallExpression(m) == true) return m;


            if (m.Method.Name == "ToString")
            {
                switch (_providerName)
                {
                    case ProviderName.MsSql:
                    {
                        StringB.Append("CAST (");
                        Visit(m.Object);
                        StringB.Append(" AS varchar)");
                        return m;
                    }
                    case ProviderName.MySql:
                    {
                        StringB.Append("CONVERT(");
                        Visit(m.Object);
                        StringB.Append(", CHAR)");
                        return m;
                    }
                    case ProviderName.PostgreSql:
                    {
                        StringB.Append("CAST (");
                        Visit(m.Object);
                        StringB.Append(" AS TEXT)");
                        return m;
                    }
                    case ProviderName.SqLite:
                    {
                        StringB.Append("CAST (");
                        Visit(m.Object);
                        StringB.Append(" AS TEXT)");
                        return m;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }


            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "FreeSql")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.FreeSql });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "TableCreate")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.TableCreate });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "DropTable")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.DropTable });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "TableExists")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.TableExists });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "ExecuteScalar")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.ExecuteScalar });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "TruncateTable")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.TruncateTable });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "ExecuteNonQuery")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.ExecuteNonQuery });
                return m;
            }

            if (m.Method.DeclaringType == typeof(V)
                && m.Method.Name == "DataTable")
            {
                var constantExpression = (ConstantExpression)m.Object;
                if (constantExpression == null) return m;
                var val = m.Method.Invoke(constantExpression.Value, null);

                AddListOne(new OneComposite { Body = val.ToString(), Operand = Evolution.DataTable });
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "ElementAt")
            {
                Visit(m.Arguments[0]);
                StringB.Clear();
                Visit(m.Arguments[1]);
                if (_providerName == ProviderName.MsSql)
                {
                    AddListOne(new OneComposite
                    {
                        Operand = Evolution.Limit,
                        Body = $" LIMIT {StringB},1"
                    });
                    var o = new OneComposite
                    {
                        Operand = Evolution.ElementAt,
                        Body = StringB.ToString()
                    };
                    AddListOne(o);
                }
                else
                {
                    var o = new OneComposite
                    {
                        Operand = Evolution.ElementAt,
                        Body = StringB.ToString()
                    };
                    AddListOne(o);
                }

                StringB.Clear();
                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Contains")
            {
                var o = new OneComposite
                {
                    Operand = Evolution.Contains
                };
                AddListOne(o);
                Visit(m.Arguments[0]);
                StringB.Clear();
                Visit(m.Arguments[1]);

                var o1 = new OneComposite
                {
                    Operand = Evolution.Where,
                    Body = StringB.ToString()
                };
                AddListOne(o1);
                var o2 = new OneComposite
                {
                    Operand = Evolution.Count
                };
                AddListOne(o2);
                var o3 = new OneComposite
                {
                    Operand = Evolution.All
                };
                AddListOne(o3);

                StringB.Clear();
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "ElementAtOrDefault")
            {
                if (_providerName == ProviderName.MsSql)
                {
                    Visit(m.Arguments[0]);
                    StringB.Clear();
                    var o = new OneComposite
                    {
                        Operand = Evolution.ElementAtOrDefault
                    };
                    AddListOne(o);
                    Visit(m.Arguments[1]);
                    AddListOne(new OneComposite
                    {
                        Operand = Evolution.Limit,
                        Body = $" LIMIT {StringB},1"
                    });
                    StringB.Clear();
                    return m;
                }
                else
                {
                    Visit(m.Arguments[0]);
                    StringB.Clear();
                    Visit(m.Arguments[1]);
                    var o = new OneComposite
                    {
                        Operand = Evolution.ElementAtOrDefault,
                        Body = StringB.ToString()
                    };
                    AddListOne(o);
                    StringB.Clear();
                }


                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "SelectMany")
            {
                if (m.Arguments.Count == 2) return BindSelectMany(m, m.Arguments[0], GetLambda(m.Arguments[1]), null);
                if (m.Arguments.Count == 3)
                    return BindSelectMany(m, m.Arguments[0], GetLambda(m.Arguments[1]), GetLambda(m.Arguments[2]));
            }


            if (m.Method.DeclaringType == typeof(DateTime))
                switch (m.Method.Name)
                {
                    case "op_Subtract":
                        if (m.Arguments[1].Type == typeof(DateTime))
                        {
                            StringB.Append("DATEDIFF(");
                            Visit(m.Arguments[0]);
                            StringB.Append(", ");
                            Visit(m.Arguments[1]);
                            StringB.Append(')');
                            return m;
                        }

                        break;
                    case "AddYears":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" YEAR')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATE(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" YEAR')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" YEAR)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(YEAR,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }

                        return m;
                    case "AddMonths":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" MONTH')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATE(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" MONTH')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" MONTH)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(MONTH,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }

                        return m;
                    case "AddDays": //DATE

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" DAY')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATE(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" DAY')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" DAY)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(DAY,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");

                                break;
                            }
                        }

                        return m;
                    case "AddHours":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" HOUR')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATETIME(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" HOUR')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" HOUR)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(HOUR,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }

                        return m;
                    case "AddMinutes":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" MINUTE')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATETIME(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" minutes')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" MINUTE)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(MINUTE,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }

                        return m;
                    case "AddSeconds":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" SECOND')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATETIME(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" SECOND')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" SECOND)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(SECOND,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }


                        return m;
                    case "AddMilliseconds":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.Append(" + INTERVAL '");
                                StringB.Append(m.Arguments[0]);
                                StringB.Append(" MICROSECOND')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("DATE(");
                                Visit(m.Object);
                                StringB.Append(", '");
                                Visit(m.Arguments[0]);
                                StringB.Append(" MICROSECOND')");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DATE_ADD(");
                                Visit(m.Object);
                                StringB.Append(", INTERVAL (");
                                Visit(m.Arguments[0]);
                                StringB.Append("* 1000) MICROSECOND)");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEADD(MICROSECOND,");
                                Visit(m.Arguments[0]);
                                StringB.Append(", ");
                                Visit(m.Object);
                                StringB.Append(" )");
                                break;
                            }
                        }


                        return m;
                }

            if (m.Method.ReturnType == typeof(char[]) && m.Method.Name == "ToArray")
                Visit(m.Arguments[0]); //Trin("ddas".ToArray())
            if (m.Method.DeclaringType == typeof(string) || m.Method.DeclaringType == typeof(Enumerable))
                switch (m.Method.Name)
                {
                    case "Reverse":
                    {
                        StringB.Append(" REVERSE(");
                        Visit(m.Arguments[0]);
                        StringB.Append(") ");
                        return m;
                    }
                    case "StartsWith":
                    {
                        StringB.Append('(');
                        Visit(m.Object);
                        switch (_providerName)
                        {
                            case ProviderName.SqLite:
                            {
                                StringB.AppendFormat(" {0} ", StringConst.Like);
                                var x = StringB.Length;
                                Visit(m.Arguments[0]);
                                var s = StringB.ToString().Substring(x);
                                var p = Param[s];
                                if (p != null) Param[s] = $"{p}%";
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MySql:
                            case ProviderName.PostgreSql:
                            {
                                StringB.AppendFormat(" {0} CONCAT(", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append(",'%'))");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.AppendFormat(" {0} CONCAT(", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append(",'%'))");
                                break;
                            }
                        }

                        return m;
                    }
                    case "LikeSql":
                    {
                        return m;
                    }
                    case "EndsWith":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.SqLite:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} ", StringConst.Like);
                                var x = StringB.Length;
                                Visit(m.Arguments[0]);
                                var s = StringB.ToString().Substring(x);
                                StringB.Append(')');
                                var p = Param[s];
                                if (p != null) Param[s] = $"%{p}";
                                break;
                            }
                            case ProviderName.MySql:
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} CONCAT('%',", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append("))");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} CONCAT('%',", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append("))");
                                break;
                            }
                        }

                        return m;
                    }
                    case "Contains":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.MsSql:
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} CONCAT('%',", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append(",'%'))");
                                break;
                            case ProviderName.MySql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} CONCAT('%',", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append(",'%'))");
                                break;
                            }
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} CONCAT('%',", StringConst.Like);
                                Visit(m.Arguments[0]);
                                StringB.Append(",'%'))");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append('(');
                                Visit(m.Object);
                                StringB.AppendFormat(" {0} ", StringConst.Like);
                                var x = StringB.Length;
                                Visit(m.Arguments[0]);
                                var s = StringB.ToString().Substring(x);
                                StringB.Append(')');
                                var p = Param[s];
                                if (p != null) Param[s] = $"%{p}%";
                                break;
                            }
                        }

                        return m;
                    }
                    case "Concat":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                IList<Expression> args = m.Arguments;
                                if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit)
                                    args = ((NewArrayExpression)args[0]).Expressions;
                                StringB.Append("CONCAT(");
                                for (int i = 0, n = args.Count; i < n; i++)
                                {
                                    if (i > 0) StringB.Append(", ");
                                    Visit(args[i]);
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                IList<Expression> args = m.Arguments;
                                if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit)
                                    args = ((NewArrayExpression)args[0]).Expressions;
                                StringB.Append("CONCAT(");
                                for (int i = 0, n = args.Count; i < n; i++)
                                {
                                    if (i > 0) StringB.Append(", ");
                                    Visit(args[i]);
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                IList<Expression> args = m.Arguments;
                                if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit)
                                    args = ((NewArrayExpression)args[0]).Expressions;
                                StringB.Append('(');
                                for (int i = 0, n = args.Count; i < n; i++)
                                {
                                    if (i > 0) StringB.Append(" || ");
                                    Visit(args[i]);
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                IList<Expression> args = m.Arguments;
                                if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit)
                                    args = ((NewArrayExpression)args[0]).Expressions;
                                StringB.Append("CONCAT(");
                                for (int i = 0, n = args.Count; i < n; i++)
                                {
                                    if (i > 0) StringB.Append(", ");
                                    Visit(args[i]);
                                }

                                StringB.Append(')');
                                break;
                            }
                        }

                        return m;
                    }
                    case "IsNullOrEmpty":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append('(');
                                Visit(m.Arguments[0]);
                                StringB.Append(" = '') IS NOT FALSE ");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append('(');
                                Visit(m.Arguments[0]);
                                StringB.Append(" IS NULL OR ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" = '')");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append('(');
                                Visit(m.Arguments[0]);
                                StringB.Append(" IS NULL OR ");
                                Visit(m.Arguments[0]);
                                StringB.Append(" = '')");
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("( CONVERT(VARCHAR,");
                                Visit(m.Arguments[0]);
                                StringB.Append(") ");
                                StringB.Append(" IS NULL OR CONVERT(VARCHAR,");
                                Visit(m.Arguments[0]);
                                StringB.Append(") = '' ");
                                StringB.Append(')');
                                break;
                            }
                        }

                        return m;
                    }
                    case "ToUpper":
                    {
                        StringB.Append("UPPER(");
                        Visit(m.Object);
                        StringB.Append(')');
                        return m;
                    }
                    case "ToLower":
                    {
                        StringB.Append("LOWER(");
                        Visit(m.Object);
                        StringB.Append(')');
                        return m;
                    }
                    case "Replace":
                    {
                        StringB.Append("REPLACE(");
                        Visit(m.Object);
                        StringB.Append(", ");
                        Visit(m.Arguments[0]);
                        StringB.Append(", ");
                        Visit(m.Arguments[1]);
                        StringB.Append(')');
                        return m;
                    }
                    case "Substring":
                    {
                        if (_providerName == ProviderName.MsSql)
                        {
                            StringB.Append("SUBSTRING(");
                            Visit(m.Object);
                            StringB.Append(", ");
                            Visit(m.Arguments[0]);
                            StringB.Append(" + 1");
                            if (m.Arguments.Count == 2)
                            {
                                StringB.Append(", ");
                                Visit(m.Arguments[1]);
                            }

                            if (m.Arguments.Count == 1) StringB.Append(", 1000000 ");
                            StringB.Append(')');
                            return m;
                        }

                        StringB.Append("SUBSTRING(");
                        Visit(m.Object);
                        StringB.Append(", ");
                        Visit(m.Arguments[0]);
                        StringB.Append(" + 1");
                        if (m.Arguments.Count == 2)
                        {
                            StringB.Append(", ");
                            Visit(m.Arguments[1]);
                        }

                        StringB.Append(')');
                        return m;
                    }
                    case "Remove":
                    {
                        if (m.Arguments.Count == 1)
                        {
                            StringB.Append("LEFT(");
                            Visit(m.Object);
                            StringB.Append(", ");
                            Visit(m.Arguments[0]);
                            StringB.Append(')');
                        }
                        else
                        {
                            StringB.Append("CONCAT(");
                            StringB.Append("LEFT(");
                            Visit(m.Object);
                            StringB.Append(", ");
                            Visit(m.Arguments[0]);
                            StringB.Append("), SUBSTRING(");
                            Visit(m.Object);
                            StringB.Append(", ");
                            Visit(m.Arguments[0]);
                            StringB.Append(" + ");
                            Visit(m.Arguments[1]);
                            StringB.Append("))");
                        }

                        return m;
                    }
                    case "IndexOf":
                    {
                        if (_providerName == ProviderName.MsSql)
                        {
                            StringB.Append("(CHARINDEX(");
                            Visit(m.Arguments[0]);
                            StringB.Append(", ");
                            Visit(m.Object);
                            if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                            {
                                StringB.Append(", ");
                                Visit(m.Arguments[1]);
                                StringB.Append(" + 1");
                            }

                            StringB.Append(") - 1)");
                            return m;
                        }

                        StringB.Append("(LOCATE(");
                        Visit(m.Arguments[0]);
                        StringB.Append(", ");
                        Visit(m.Object);
                        if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            StringB.Append(", ");
                            Visit(m.Arguments[1]);
                            StringB.Append(" + 1");
                        }

                        StringB.Append(") - 1)");
                        return m;
                    }
                    case "Trim":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("TRIM(both from ");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                if (m.Arguments.Count == 0)
                                {
                                    StringB.Append("TRIM(");
                                    Visit(m.Object);
                                    StringB.Append(')');
                                }
                                else if (m.Arguments.Count == 1)
                                {
                                    StringB.Append("TRIM(LEADING ");
                                    Visit(m.Arguments[0]);
                                    StringB = StringB.Replace(",", "");
                                    StringB.Append(" FROM ");
                                    Visit(m.Object);
                                    StringB.Append(')');
                                }

                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("TRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("RTRIM(");
                                StringB.Append("LTRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append("' ");
                                    }
                                }

                                StringB.Append(')');
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append("' ");
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                        }

                        return m;
                    }

                    case "TrimEnd":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("TRIM(trailing from ");
                                Visit(m.Object);
                                var ee = (NewArrayExpression)m.Arguments[0];
                                for (var i = 0; i < ee.Expressions.Count; i++)
                                {
                                    if (i == 0) StringB.Append(", '");

                                    StringB.Append(ee.Expressions[i]);
                                    if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("TRIM(TRAILING  ");

                                var ee = (NewArrayExpression)m.Arguments[0];
                                for (var i = 0; i < ee.Expressions.Count; i++)
                                {
                                    if (i == 0) StringB.Append(" '");

                                    StringB.Append(ee.Expressions[i]);
                                    if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                }

                                StringB.Append(" FROM ");
                                Visit(m.Object);

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("RTRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("RTRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append("' ");
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                        }

                        return m;
                    }
                    case "TrimStart":
                    {
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("TRIM(leading from ");
                                Visit(m.Object);
                                var ee = (NewArrayExpression)m.Arguments[0];
                                for (var i = 0; i < ee.Expressions.Count; i++)
                                {
                                    if (i == 0) StringB.Append(", '");

                                    StringB.Append(ee.Expressions[i]);
                                    if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("TRIM(LEADING   ");

                                var ee = (NewArrayExpression)m.Arguments[0];
                                for (var i = 0; i < ee.Expressions.Count; i++)
                                {
                                    if (i == 0) StringB.Append(" '");

                                    StringB.Append(ee.Expressions[i]);
                                    if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                }

                                StringB.Append(" FROM ");
                                Visit(m.Object);

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("LTRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append('\'');
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.MsSql:
                            {
                                StringB.Append("LTRIM(");
                                Visit(m.Object);
                                if (m.Arguments.Count > 0)
                                {
                                    var ee = (NewArrayExpression)m.Arguments[0];
                                    for (var i = 0; i < ee.Expressions.Count; i++)
                                    {
                                        if (i == 0) StringB.Append(", '");

                                        StringB.Append(ee.Expressions[i]);
                                        if (i == ee.Expressions.Count - 1) StringB.Append("' ");
                                    }
                                }

                                StringB.Append(')');
                                break;
                            }
                        }

                        return m;
                    }
                }

            if (m.Method.DeclaringType == typeof(decimal))
                switch (m.Method.Name)
                {
                    case "Add":
                    case "Subtract":
                    case "Multiply":
                    case "Divide":
                    case "Remainder":
                        StringB.Append('(');
                        VisitValue(m.Arguments[0]);
                        StringB.Append(' ');
                        StringB.Append(GetOperator(m.Method.Name));
                        StringB.Append(' ');
                        VisitValue(m.Arguments[1]);
                        StringB.Append(')');
                        return m;
                    case "Negate":
                        StringB.Append('-');
                        Visit(m.Arguments[0]);
                        StringB.Append("");
                        return m;
                    case "Ceiling":
                    case "Floor":
                        StringB.Append(m.Method.Name.ToUpper());
                        StringB.Append('(');
                        Visit(m.Arguments[0]);
                        StringB.Append(')');
                        return m;
                    case "Round":
                        if (m.Arguments.Count == 1)
                        {
                            StringB.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            StringB.Append(')');
                            return m;
                        }

                        if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            StringB.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            StringB.Append(", ");
                            Visit(m.Arguments[1]);
                            StringB.Append(')');
                            return m;
                        }

                        break;
                    case "Truncate":
                        StringB.Append("TRUNCATE(");
                        Visit(m.Arguments[0]);
                        StringB.Append(",0)");
                        return m;
                }
            else if (m.Method.DeclaringType == typeof(Math))
                switch (m.Method.Name)
                {
                    case "Abs":
                    case "Acos":
                    case "Asin":
                    case "Atan":
                    case "Atan2":
                    case "Cos":
                    case "Exp":
                    case "Log10":
                    case "Sin":
                    case "Tan":
                    case "Sqrt":
                    case "Sign":
                    case "Ceiling":
                    case "Floor":
                        StringB.Append(m.Method.Name.ToUpper());
                        StringB.Append('(');
                        Visit(m.Arguments[0]);
                        StringB.Append(')');
                        return m;
                    case "Log":
                        if (m.Arguments.Count == 1) goto case "Log10";
                        break;
                    case "Pow":
                        StringB.Append("POWER(");
                        Visit(m.Arguments[0]);
                        StringB.Append(", ");
                        Visit(m.Arguments[1]);
                        StringB.Append(')');
                        return m;
                    case "Round":
                        if (m.Arguments.Count == 1)
                        {
                            StringB.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            StringB.Append(')');
                            return m;
                        }

                        if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            StringB.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            StringB.Append(", ");
                            Visit(m.Arguments[1]);
                            StringB.Append(')');
                            return m;
                        }

                        break;
                    case "Truncate":
                        StringB.Append("TRUNCATE(");
                        Visit(m.Arguments[0]);
                        StringB.Append(",0)");
                        return m;
                }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Where")
            {
                _currentMethodWhere = "Where";
                var o = new OneComposite { Operand = Evolution.Where };
                Visit(m.Arguments[0]);
                var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);

                Visit(lambda.Body);
                o.Body = StringB.ToString();
                AddListOne(o);
                StringB.Length = 0;
                _currentMethodWhere = null;


                return m;
            }

            if (m.Method.Name == "FromString")
            {
                var u = m.Arguments[0] as ConstantExpression;
                var o = new OneComposite { Operand = Evolution.FromString, Body = u.Value.ToString() };
                AddListOne(o);
                StringB.Length = 0;
                return m;
            }

            if (m.Method.Name == "FromStringP")
            {
                var u = m.Arguments[0] as ConstantExpression;
                var o = new OneComposite { Operand = Evolution.FromString, Body = u.Value.ToString() };
                var p = m.Arguments[1] as ConstantExpression;
                var pv = (SqlParam[])p.Value;
                foreach (SqlParam param in pv)
                {
                    Param.Add(param.Name, param.Value);
                }

                AddListOne(o);
                StringB.Length = 0;
                return m;
            }

            if (m.Method.Name == "SelectSql")
            {
                var sas = m.Arguments[0] as ConstantExpression;
                var o = new OneComposite { Operand = Evolution.Select, Body = sas.Value.ToString() };
                AddListOne(o);

                return m;
            }

            if (m.Method.Name == "UpdateSql")
            {
                Visit(m.Arguments[0]);
                var sas = m.Arguments[1] as ConstantExpression;
                var o = new OneComposite { Operand = Evolution.Update, Body = sas.Value.ToString() };
                AddListOne(o);

                return m;
            }

            if (m.Method.Name == "SelectSqlE")
            {
                Visit(m.Arguments[0]);
                var sl = m.Arguments[1] as LambdaExpression;
                VisitUpdateBinary(sl.Body);
                var p = m.Arguments[2] as ConstantExpression;
                var pv = (SqlParam[])p.Value;
                foreach (SqlParam param in pv)
                {
                    Param.Add(param.Name, param.Value);
                }

                var ao = new OneComposite { Operand = Evolution.Select, Body = StringB.ToString() };
                AddListOne(ao);
                StringB.Clear();
                return m;
            }

            if (m.Method.Name == "WhereSqlE")
            {
                Visit(m.Arguments[0]);
                var sl = m.Arguments[1] as LambdaExpression;
                VisitUpdateBinary(sl.Body);
                var p = m.Arguments[2] as ConstantExpression;
                var pv = (SqlParam[])p.Value;
                foreach (SqlParam param in pv)
                {
                    Param.Add(param.Name, param.Value);
                }

                var ao = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                AddListOne(ao);
                StringB.Clear();
                return m;
            }

            if (m.Method.Name == "UpdateSqlE")
            {
                Visit(m.Arguments[0]);
                var sl = m.Arguments[1] as LambdaExpression;
                VisitUpdateBinary(sl.Body);
                var p = m.Arguments[2] as ConstantExpression;
                var pv = (SqlParam[])p.Value;
                foreach (SqlParam param in pv)
                {
                    Param.Add(param.Name, param.Value);
                }

                var ao = new OneComposite { Operand = Evolution.Update, Body = StringB.ToString() };
                AddListOne(ao);
                StringB.Clear();
                return m;
            }


            if (m.Method.Name == "SelectSqlP")
            {
                var sql = m.Arguments[0] as ConstantExpression;
                var param = m.Arguments[1] as ConstantExpression;
                var o = new OneComposite { Operand = Evolution.Select, Body = sql.Value.ToString() };
                AddListOne(o);
                var pv = (SqlParam[])param.Value;
                foreach (SqlParam p in pv)
                {
                    Param.Add(p.Name, p.Value);
                }

                return m;
            }

            if (m.Method.Name == "WhereString")
            {
                var t = m.Arguments[0] as ConstantExpression;
                var v = t.Value;
                StringB.Append(v);

                return m;
            }


            if (m.Method.Name == "WhereIn")
            {
                StringB.Append('(');
                Visit(m.Arguments[0]);
                var sp = m.Arguments[1] as ConstantExpression;
                var o = sp.Value as IEnumerable;
                StringB.Append(" IN (");
                foreach (var o1 in o)
                {
                    if (o1 == null)
                    {
                        StringB.Append("null").Append(", ");
                    }
                    else
                    {
                        var typeCore = o1.GetType();
                        if (UtilsCore.IsNumericType(typeCore))
                        {
                            StringB.Append(o1).Append(", ");
                        }
                        else if (typeCore.IsEnum)
                        {
                            StringB.Append((int)o1).Append(", ");
                        }
                        else if (typeCore == typeof(Guid) || typeCore == typeof(Guid?))
                        {
                            StringB.Append($"'{o1}'").Append(", ");
                        }
                        else
                        {
                            AddParameter(o1);
                            StringB.Append(", ");
                        }
                    }
                }

                var str = StringB.ToString().TrimEnd(',', ' ');
                StringB.Clear().Append(str).Append("))");
                return m;
            }

            if (m.Method.Name == "WhereNotIn")
            {
                StringB.Append('(');
                Visit(m.Arguments[0]);
                var sp = m.Arguments[1] as ConstantExpression;
                var o = sp.Value as IEnumerable;
                StringB.Append(" NOT IN (");
                foreach (var o1 in o)
                {
                    if (o1 == null)
                    {
                        StringB.Append("null").Append(", ");
                    }
                    else
                    {
                        var typeCore = o1.GetType();
                        if (UtilsCore.IsNumericType(typeCore))
                        {
                            StringB.Append(o1).Append(", ");
                        }
                        else if (typeCore.IsEnum)
                        {
                            StringB.Append((int)o1).Append(", ");
                        }
                        else if (typeCore == typeof(Guid) || typeCore == typeof(Guid?))
                        {
                            StringB.Append($"'{o1}'").Append(", ");
                        }
                        else
                        {
                            AddParameter(o1);
                            StringB.Append(", ");
                        }
                    }
                }

                var str = StringB.ToString().TrimEnd(',', ' ');
                StringB.Clear().Append(str).Append("))");
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable))
            {
                switch (m.Method.Name)
                {
                    case "Join":
                    case "GroupJoin":
                    case "Concat":
                    case "SelectMany":
                    case "Aggregate":
                    case "GroupBy":
                    case "Except":
                    {
                        throw new Exception(
                            $"Method {m.Method.Name} for IQueryable is not implemented, use method {m.Method.Name}Core or ...toList().{m.Method.Name}()");
                    }
                }
            }

            if (m.Method.Name == "Union")
            {
                Visit(m.Arguments[0]);
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && (m.Method.Name == "OrderBy" || m.Method.Name == "ThenBy"))
            {
                Visit(m.Arguments[0]);
                var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                Visit(lambda.Body);
                var o = new OneComposite { Operand = Evolution.OrderBy, Body = StringB.ToString().Trim(' ', ',') };
                var tSelect = ListOne.Where(a => a.Operand == Evolution.OrderBy).Select(d => d.Body);
                if (tSelect.Contains(o.Body) == false) AddListOne(o);

                StringB.Length = 0;
                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Reverse")
            {
                Visit(m.Arguments[0]);
                var sb = new StringBuilder();
                if (ListOne.Any(a => a.Operand == Evolution.OrderBy))
                    ListOne.Where(a => a.Operand == Evolution.OrderBy).ToList().ForEach(a =>

                    {
                        sb.AppendFormat(" {0},", a.Body);
                        ListOne.Remove(a);
                    });
                else
                    sb.AppendFormat("{0}.{1}", AttributesOfClass<T>.TableName(_providerName),
                        AttributesOfClass<T>.PkAttribute(_providerName).GetColumnName(_providerName));


                var o = new OneComposite
                {
                    Operand = Evolution.Reverse,
                    Body = string.Format("ORDER BY {0} DESC ", sb.ToString().TrimEnd(','))
                };
                AddListOne(o);
                StringB.Length = 0;
                return m;
            }

            if ((m.Method.DeclaringType == typeof(Queryable)
                 && m.Method.Name == "OrderByDescending") || m.Method.Name == "ThenByDescending")
            {
                Visit(m.Arguments[0]);
                var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                Visit(lambda.Body);
                var o = new OneComposite
                    { Operand = Evolution.OrderBy, Body = StringB.ToString().Trim(' ', ',') + " DESC " };

                AddListOne(o);
                StringB.Length = 0;
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Select")
            {
                var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                _currentMethodSelect = "Select";

                Visit(m.Arguments[0]);

                AddPostExpression(Evolution.Select, m);


                Visit(lambda.Body);

                var o = new OneComposite
                {
                    Operand = Evolution.Select,
                    Body = StringB.ToString().Trim(' ', ',')
                };
                if (!string.IsNullOrEmpty(StringB.ToString())) AddListOne(o);
                StringB.Length = 0;
                _currentMethodSelect = null;
                return m;
            }

            if ((m.Method.DeclaringType == typeof(Queryable) && m.Method.Name == "First") ||
                (m.Method.DeclaringType == typeof(Helper) && m.Method.Name == "FirstAsync"))
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);

                    var o1 = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                    AddListOne(o1);
                    StringB.Length = 0;
                    var o2 = new OneComposite { Operand = Evolution.First };
                    AddListOne(o2);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.First, Body = StringB.ToString() };
                AddListOne(o);
                StringB.Length = 0;

                return m;
            }

            if ((m.Method.DeclaringType == typeof(Queryable) && m.Method.Name == "FirstOrDefault") ||
                (m.Method.DeclaringType == typeof(Helper) && m.Method.Name == "FirstOrDefaultAsync"))
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);

                    var o1 = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                    AddListOne(o1);
                    StringB.Length = 0;
                    var o2 = new OneComposite { Operand = Evolution.FirstOrDefault };
                    AddListOne(o2);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.FirstOrDefault, Body = StringB.ToString() };
                AddListOne(o);
                StringB.Length = 0;

                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "All")
            {
                Visit(m.Arguments[0]);
                var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                Visit(lambda.Body);
                var o1 = new OneComposite { Operand = Evolution.All, Body = StringB.ToString() };
                AddListOne(o1);
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Any")
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    var o1 = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                    AddListOne(o1);
                    StringB.Length = 0;
                    var o2 = new OneComposite { Operand = Evolution.Any };
                    AddListOne(o2);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                AddListOne(o);
                AddListOne(new OneComposite { Operand = Evolution.Any });
                StringB.Length = 0;

                return m;
            }

            if ((m.Method.DeclaringType == typeof(Queryable)
                 && m.Method.Name == "Last") || m.Method.Name == "LastOrDefault")
            {
                if (_providerName == ProviderName.MsSql)
                {
                    var pizda = Evolution.Last;
                    if (m.Method.Name == "LastOrDefault")
                        pizda = Evolution.LastOrDefault;
                    Visit(m.Arguments[0]);

                    if (m.Arguments.Count == 2)
                    {
                        var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                        Visit(lambda.Body);
                        var o1 = new OneComposite
                        {
                            Operand = Evolution.Where,
                            Body = StringB.ToString()
                        };
                        AddListOne(o1);
                        StringB.Length = 0;
                    }

                    if (ListOne.Any(a => a.Operand == Evolution.OrderBy))
                    {
                        foreach (var body in ListOne.Where(a => a.Operand == Evolution.OrderBy))
                            if (body.Body.IndexOf("DESC", StringComparison.Ordinal) != -1)
                                body.Body = body.Body.Replace("DESC", string.Empty);
                            else
                                body.Body += " DESC";
                    }
                    else
                    {
                        var o = new OneComposite
                        {
                            Operand = Evolution.OrderBy,
                            Body = $" {AttributesOfClass<T>.TableName(_providerName)}." +
                                   $"{AttributesOfClass<T>.PkAttribute(_providerName).GetColumnName(_providerName)} DESC "
                        };
                        AddListOne(o);
                    }

                    var o13 = new OneComposite
                    {
                        IsAggregate = true,
                        Operand = pizda,
                        Body = "1"
                    };
                    AddListOne(o13);

                    return m;
                }

                var ee = (Evolution)Enum.Parse(typeof(Evolution), m.Method.Name);
                Visit(m.Arguments[0]);

                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    var o1 = new OneComposite
                    {
                        Operand = Evolution.Where,
                        Body = StringB.ToString()
                    };
                    AddListOne(o1);
                    StringB.Length = 0;
                }

                if (ListOne.Any(a => a.Operand == Evolution.OrderBy))
                {
                    foreach (var body in ListOne.Where(a => a.Operand == Evolution.OrderBy))
                        if (body.Body.IndexOf("DESC", StringComparison.Ordinal) != -1)
                            body.Body = body.Body.Replace("DESC", string.Empty);
                        else
                            body.Body += " DESC";
                    if (PingComposite(Evolution.Limit) == false)
                        ListOne.Last(a => a.Operand == Evolution.OrderBy).Body += " LIMIT 1";
                }
                else
                {
                    var o = new OneComposite
                    {
                        Operand = Evolution.OrderBy,
                        Body = string.Format(" {0}.{1} DESC LIMIT 1", AttributesOfClass<T>.TableName(_providerName),
                            AttributesOfClass<T>.PkAttribute(_providerName).GetColumnName(_providerName))
                    };
                    AddListOne(o);
                }

                var os = new OneComposite
                {
                    Operand = ee
                };
                AddListOne(os);

                return m;
            }

            if (m.Method.Name == "Skip")
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (ConstantExpression)StripQuotes(m.Arguments[1]);

                    var o1 = new OneComposite { Operand = Evolution.Skip, Body = lambda.Value.ToString() };
                    AddListOne(o1);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.Count, Body = StringB.ToString() };
                AddListOne(o);
                StringB.Length = 0;
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Count")
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    var o1 = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                    AddListOne(o1);
                    var o2 = new OneComposite { Operand = Evolution.Count };
                    AddListOne(o2);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.Count, Body = StringB.ToString() };
                AddListOne(o);
                StringB.Length = 0;
                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "LongCount")
            {
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    var o1 = new OneComposite { Operand = Evolution.Where, Body = StringB.ToString() };
                    AddListOne(o1);
                    var o2 = new OneComposite { Operand = Evolution.LongCount };
                    AddListOne(o2);
                    o2 = new OneComposite { Operand = Evolution.Count };
                    AddListOne(o2);
                    StringB.Length = 0;
                    return m;
                }

                var o = new OneComposite { Operand = Evolution.LongCount, Body = StringB.ToString() };
                AddListOne(o);
                o = new OneComposite { Operand = Evolution.Count, Body = StringB.ToString() };
                AddListOne(o);
                StringB.Length = 0;
                return m;
            }


            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "Single")
            {
                AddListOne(new OneComposite { Operand = Evolution.Single, Body = "", IsAggregate = true });
                Visit(m.Arguments[0]);
                if (m.Arguments.Count == 2)
                {
                    var o = new OneComposite { Operand = Evolution.Where };
                    Visit(m.Arguments[0]);
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    o.Body = StringB.ToString();
                    AddListOne(o);
                    StringB.Length = 0;
                }

                AddListOne(new OneComposite { Operand = Evolution.First, Body = "" });
                return m;
            }

            if (m.Method.DeclaringType == typeof(Queryable)
                && m.Method.Name == "SingleOrDefault")
            {
                AddListOne(new OneComposite { Operand = Evolution.SingleOrDefault, Body = "", IsAggregate = true });
                AddListOne(new OneComposite { Operand = Evolution.First, Body = "" });
                Visit(m.Arguments[0]);

                if (m.Arguments.Count == 2)
                {
                    var o = new OneComposite { Operand = Evolution.Where };
                    var lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    o.Body = StringB.ToString();
                    AddListOne(o);
                    StringB.Length = 0;
                }

                return m;
            }

            if (m.Method.MemberType == MemberTypes.Method)
            {
                var mcs = m;
                if (mcs.Method.DeclaringType == typeof(Queryable)
                    || mcs.Method.DeclaringType == typeof(Enumerable))
                {
                    switch (mcs.Method.Name)
                    {
                        case "Sum":
                        case "Min":
                        case "Max":
                        case "Average":
                        {
                            Visit(mcs.Arguments[0]);
                            var lambda = (LambdaExpression)StripQuotes(mcs.Arguments[1]);
                            StringB.Length = 0;
                            StringB.Append(mcs.Method.Name + "(");
                            Visit(lambda.Body);
                            StringB.Append(')');
                            AddListOne(new OneComposite
                                { Operand = Evolution.Select, Body = StringB.ToString(), IsAggregate = true });
                            break;
                        }
                    }

                    return m;
                }

                if (m.Method.MemberType == MemberTypes.Field)
                {
                    var df = m.Arguments.Select(a => a.GetType().GetProperty("Value").GetValue(a, null));
                    var values = m.Method.Invoke(m, df.ToArray());
                    AddParameter(values);
                    return m;
                }

                if (m.Method.MemberType == MemberTypes.Property)
                {
                    var df = m.Arguments.Select(a => a.GetType().GetProperty("Value").GetValue(a, null));
                    var value = m.Method.Invoke(m, df.ToArray());
                    AddParameter(value);
                    return m;
                }

                if (m.Method.MemberType == MemberTypes.Method && m.Object != null)
                {
                    _currentMethodType = m.Method.ReturnType;
                    if (GeoMethodCallExpression2(m) == true)
                    {
                        _currentMethodType = null;
                        return m;
                    }

                    _currentMethodType = null;
                    if (CurrentEvolution == Evolution.Update)
                    {
                        object result = Expression.Lambda(m).Compile().DynamicInvoke();
                        AddParameter(result);
                    }

                    return m;
                }

                if (m.Method.Name == "Query") return m;

                if (m.Method.Name == "get_Item")
                {
                    if (m.Object != null)
                    {
                        var value = Expression.Lambda<Func<object>>(m.Object).Compile()();
                        var dddd = m.Arguments[0].GetType().GetProperty("Value").GetValue(m.Arguments[0], null);
                        var tt = m.Method.Invoke(value, new[] { dddd });
                        AddParameter(tt);
                    }

                    return m;
                }

                if (m.Method.Name == "WhereOn")
                {
                    return m;
                }

                var la = new List<object>();
                foreach (var i in m.Arguments)
                    if (i.GetType().GetProperty("Value") ==
                        null)
                    {
                        var value = Expression.Lambda<Func<object>>(i).Compile()();
                        la.Add(value);
                    }
                    else
                    {
                        la.Add(i.GetType().GetProperty("Value").GetValue(i, null));
                    }

                var value3 = m.Method.Invoke(m, la.ToArray());
                AddParameter(value3);
                return m;
            }

            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture,
                "The method '{0}' is not supported", m.Method.Name));
        }

        private static LambdaExpression GetLambda(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote) e = ((UnaryExpression)e).Operand;
            if (e.NodeType == ExpressionType.Constant) return ((ConstantExpression)e).Value as LambdaExpression;
            return e as LambdaExpression;
        }

        protected override Expression VisitUnaryUpdate(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    StringB.Append(" NOT ");
                    VisitUpdateBinary(u.Operand);
                    break;

                case ExpressionType.Convert:
                    VisitUpdateBinary(u.Operand);
                    break;

                case ExpressionType.Quote:
                    VisitUpdateBinary(u.Operand);
                    break;

                default:
                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture,
                        "The unary operator '{0}' is not supported", u.NodeType));
            }

            return u;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    StringB.Append(" NOT ");
                    Visit(u.Operand);
                    break;

                case ExpressionType.Convert:
                    Visit(u.Operand);
                    break;

                case ExpressionType.Quote:
                    Visit(u.Operand);
                    break;

                default:
                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture,
                        "The unary operator '{0}' is not supported", u.NodeType));
            }

            return u;
        }


        protected override Expression VisitBinary(BinaryExpression b)
        {
            StringB.Append('(');
            Visit(b.Left);
            switch (b.NodeType)
            {
                case ExpressionType.AndAlso:

                case ExpressionType.And:
                    StringB.Append(" and ");
                    break;
                case ExpressionType.Or:
                    StringB.Append(" or ");
                    break;
                case ExpressionType.Add:
                    StringB.Append('+');
                    break;

                case ExpressionType.OrElse:
                    StringB.Append(" or ");
                    break;
                case ExpressionType.Equal:
                    if (b.Right.ToString() == "null")
                    {
                        StringB.Append(" is ");
                        break;
                    }

                    if (b.Right is UnaryExpression expression && expression.Operand.ToString() == "null")
                    {
                        StringB.Append(" is ");
                        break;
                    }

                    StringB.Append(" = ");
                    break;
                case ExpressionType.NotEqual:

                    if (b.Right.ToString() == "null")
                    {
                        StringB.Append(" is not  ");
                        break;
                    }

                    if (b.Right is UnaryExpression unaryExpression && unaryExpression.Operand.ToString() == "null")
                    {
                        StringB.Append(" is not  ");
                        break;
                    }

                    StringB.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    StringB.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    StringB.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    StringB.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    StringB.Append(" >= ");
                    break;
                case ExpressionType.Divide:
                    StringB.Append(" / ");
                    break;
                case ExpressionType.Modulo:
                    StringB.Append('%');
                    break;
                case ExpressionType.ExclusiveOr:
                    StringB.Append('^');
                    break;
                case ExpressionType.LeftShift:
                    StringB.Append("<<");
                    break;
                case ExpressionType.RightShift:
                    StringB.Append(">>");
                    break;
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    StringB.Append('-');
                    break;
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    StringB.Append('*');
                    break;
                default:
                    throw new NotSupportedException(
                        string.Format(CultureInfo.CurrentCulture,
                            "The binary operator '{0}' is not supported", b.NodeType));
            }

            Visit(b.Right);
            StringB.Append(')');
            return b;
        }

        protected override Expression VisitConstantUpdate(ConstantExpression c)
        {
            StringB.Append(c.Value);
            return c;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value is IQueryable) return c;
            if (c.Value == null)
                StringB.Append("null");
            else
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        if (_providerName == ProviderName.PostgreSql)
                            StringB.Append((bool)c.Value);
                        else
                            StringB.Append((bool)c.Value ? 1 : 0);
                        break;
                    case TypeCode.Decimal:
                    {
                        StringB.Append((decimal)c.Value);
                        break;
                    }
                    case TypeCode.Int64:
                    {
                        StringB.Append((long)c.Value);
                        break;
                    }
                    case TypeCode.Int32:
                    {
                        StringB.Append((int)c.Value);
                        break;
                    }
                    case TypeCode.Int16:
                    {
                        StringB.Append((short)c.Value);
                        break;
                    }
                    case TypeCode.UInt16:
                    {
                        StringB.Append((ushort)c.Value);
                        break;
                    }
                    case TypeCode.UInt32:
                    {
                        StringB.Append((uint)c.Value);
                        break;
                    }
                    case TypeCode.UInt64:
                    {
                        StringB.Append((ulong)c.Value);
                        break;
                    }
                    case TypeCode.Single:
                    {
                        StringB.Append((float)c.Value);
                        break;
                    }
                    case TypeCode.Double:
                    {
                        StringB.Append((double)c.Value);
                        break;
                    }
                    case TypeCode.String:

                        AddParameter(c.Value);

                        break;
                    case TypeCode.Object:
                    {
                        if (c.Value is T cValue && PingComposite(Evolution.Contains))
                        {
                            var propertyname = AttributesOfClass<T>.PkAttribute(_providerName).PropertyName;
                            var value = AttributesOfClass<T>.GetValueE(_providerName, propertyname, cValue);
                            var tablenane = AttributesOfClass<T>.TableName(_providerName);
                            var key = AttributesOfClass<T>.PkAttribute(_providerName).GetColumnName(_providerName);
                            StringB.Append(string.Format("({0}.{1} = '{2}')", tablenane, key, value));
                            break;
                        }

                        throw new NotSupportedException(
                            string.Format(CultureInfo.CurrentCulture,
                                "The constant for '{0}' is not supported", c.Value));
                    }
                    default:
                        AddParameter(c.Value);
                        break;
                }

            return c;
        }

        protected override Expression VisitMemberAccessUpdate(MemberExpression m)
        {
            if (m.Expression != null
                && m.Expression.NodeType == ExpressionType.Parameter)
            {
                if (m.Expression.Type != typeof(T))
                {
                    if (UtilsCore.IsAnonymousType(m.Expression.Type))
                        return m;
                }
                else
                {
                    string nameColumn = GetColumnName(m.Member.Name);


                    StringB.Append(nameColumn);
                }

                return m;
            }

            if (m.Expression != null
                && m.Expression.NodeType == ExpressionType.New)
            {
                StringB.Append(GetColumnName(m.Member.Name));
                return m;
            }

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Constant)
            {
                VisitorMemberAccessUpdate(m);
                return m;
            }

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess)
            {
                VisitorMemberAccessUpdate(m);
                return m;
            }


            if (m.Expression != null && m.NodeType == ExpressionType.MemberAccess)
            {
                VisitorMemberAccessUpdate(m);
                return m;
            }

            if (m.Expression == null && m.NodeType == ExpressionType.MemberAccess)
            {
                var value = UtilsCore.Compile(m); // = Expression.Lambda<Func<DateTime>>(m).Compile()();
                StringB.Append(value);
                return m;
            }

            throw new NotSupportedException(
                string.Format(CultureInfo.CurrentCulture, "The member '{0}' is not supported", m.Member.Name));
        }


        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Expression != null
                && m.Expression.NodeType == ExpressionType.Parameter)
            {
                if (m.Expression.Type != typeof(T))
                {
                    if (UtilsCore.IsAnonymousType(m.Expression.Type))
                        return m;
                }
                else
                {
                    string nameColumn = GetColumnName(m.Member.Name);


                    if (UtilsCore.IsGeo(m.Type))
                    {
                        if (_currentMethodWhere == null && _currentMethodSelect != null)
                        {
                            StringB.Append(UtilsCore.SqlConcat(nameColumn, _providerName));
                        }
                        else
                        {
                            StringB.Append(nameColumn);
                        }


                        AddListOne(new OneComposite { Operand = Evolution.ListGeo, Body = nameColumn, Srid = 0 });
                    }
                    else if (AttributesOfClass<T>.GetIsJson(m.Member.Name, _providerName))
                    {
                        StringB.Append(nameColumn);
                        AddListOne(new OneComposite { Operand = Evolution.ListJson, Body = nameColumn });
                    }
                    else
                    {
                        StringB.Append(nameColumn);
                    }
                }

                return m;
            }

            if (m.Expression != null
                && m.Expression.NodeType == ExpressionType.New)
            {
                StringB.Append(GetColumnName(m.Member.Name));
                return m;
            }

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Constant)
            {
                VisitorMemberAccess(m);
                return m;
            }

            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess)
            {
                VisitorMemberAccess(m);
                return m;
            }


            if (m.Expression != null && m.NodeType == ExpressionType.MemberAccess)
            {
                VisitorMemberAccess(m);
                return m;
            }

            if (m.Expression == null && m.NodeType == ExpressionType.MemberAccess)
            {
                if (m.Type == typeof(int))
                {
                    var value = Expression.Lambda<Func<int>>(m).Compile()();
                    AddParameter(value);
                    return m;
                }

                if (m.Type == typeof(long))
                {
                    var value = Expression.Lambda<Func<long>>(m).Compile()();
                    AddParameter(value);
                    return m;
                }

                if (m.Member.ReflectedType == typeof(DateTime))
                {
                    var value = Expression.Lambda<Func<DateTime>>(m).Compile()();
                    AddParameter(value);
                    return m;
                }

                if (m.Type == typeof(Guid))
                {
                    var value = Expression.Lambda<Func<Guid>>(m).Compile()();
                    AddParameter(value);
                    return m;
                }

                var str = Expression.Lambda<Func<object>>(m).Compile().Invoke();
                AddParameter(str);
                return m;
            }

            throw new NotSupportedException(
                string.Format(CultureInfo.CurrentCulture, "The member '{0}' is not supported", m.Member.Name));
        }


        private void VisitorMemberAccessUpdate(MemberExpression m)
        {
            object value;
            if (m.Member.DeclaringType == typeof(string))
                switch (m.Member.Name)
                {
                    case "Length":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("CHAR_LENGTH(");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("LENGTH(");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("CHAR_LENGTH(");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("LEN(");
                                break;
                            }
                        }

                        Visit(m.Expression);
                        StringB.Append(')');
                        return;
                }

            if (m.Member.MemberType == MemberTypes.Field)
            {
            }


            if (m.Member.ReflectedType == typeof(DateTime))
            {
                if (m.Member.DeclaringType == typeof(DateTimeOffset))
                    throw new Exception("m.Member.DeclaringType == typeof(DateTimeOffset)");

                switch (m.Member.Name)
                {
                    case "Day":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract( day from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%d', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DAY(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(DAY,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Month":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(MONTH from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%m', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("MONTH(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(MONTH, ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Year":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(YEAR from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%Y', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("YEAR(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(YEAR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Hour":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(HOUR from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%H', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("HOUR(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(HOUR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Minute":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(MINUTE from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%M', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("MINUTE(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(MINUTE,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Second":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(SECOND from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%S', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("SECOND(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(SECOND,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Millisecond":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                throw new Exception("not implemented");
                            }
                            case ProviderName.SqLite:
                            {
                                throw new Exception("not implemented");
                            }
                            case ProviderName.MySql:
                            {
                                throw new Exception("not implemented");
                            }

                            case ProviderName.MsSql:
                            {
                                throw new Exception("not implemented");
                            }
                        }

                        break;


                    case "DayOfWeek":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract( isodow from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%w', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("(DAYOFWEEK(");
                                Visit(m.Expression);
                                StringB.Append(") - 1)");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(WEEKDAY,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "DayOfYear":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(doy from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%j', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("(DAYOFYEAR(");
                                Visit(m.Expression);
                                StringB.Append(") - 1)");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(DAYOFYEAR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                }

                var str1 = Expression.Lambda<Func<DateTime>>(m.Expression).Compile()();
                var ss = str1.GetType().GetProperty(m.Member.Name);
                value = ss.GetValue(str1, null);
                AddParameter(value);
                return;
            }

            var strS = new JoinAlias().GetAlias(m.Expression);
            if (strS != null && strS.IndexOf("TransparentIdentifier", StringComparison.Ordinal) != -1)
            {
                StringB.Append(GetColumnName(m.Member.Name));
                return;
            }

            var str = Expression.Lambda<Func<object>>(m.Expression).Compile()();
            value = null;
            if (m.Member.MemberType == MemberTypes.Field)
            {
                var fieldInfo = str.GetType().GetField(m.Member.Name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (fieldInfo != null)
                {
                    value = fieldInfo.GetValue(str);
                }
                else
                {
                    FieldInfo[] asax = str.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
                                                               BindingFlags.Public | BindingFlags.Static);
                    foreach (FieldInfo info in asax)
                    {
                        if (info.Name.EndsWith(m.Member.Name))
                        {
                            value = info.GetValue(str);
                        }
                    }
                }

                if (value == null)
                {
                    throw new Exception($"I can't determine the field:{m.Member.Name} to call");
                }
            }

            if (m.Member.MemberType == MemberTypes.Property)
            {
                var ass = str.GetType().GetProperty(m.Member.Name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (ass != null)
                {
                    value = ass.GetValue(str, null);
                }
                else
                {
                    PropertyInfo[] asax = str.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic |
                                                                      BindingFlags.Public | BindingFlags.Static);
                    foreach (PropertyInfo info in asax)
                    {
                        if (info.Name.EndsWith(m.Member.Name))
                        {
                            value = info.GetValue(str);
                        }
                    }
                }

                if (value == null)
                {
                    throw new Exception($"I can't determine the property:{m.Member.Name} to call");
                }
            }


            StringB.Append(value);
        }

        private void VisitorMemberAccess(MemberExpression m)
        {
            object value;
            if (m.Member.DeclaringType == typeof(string))
                switch (m.Member.Name)
                {
                    case "Length":

                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("CHAR_LENGTH(");
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("LENGTH(");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("CHAR_LENGTH(");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("LEN(");
                                break;
                            }
                        }

                        Visit(m.Expression);
                        StringB.Append(')');
                        return;
                }

            if (m.Member.MemberType == MemberTypes.Field)
            {
            }


            if (m.Member.ReflectedType == typeof(DateTime))
            {
                if (m.Member.DeclaringType == typeof(DateTimeOffset))
                    throw new Exception("m.Member.DeclaringType == typeof(DateTimeOffset)");

                switch (m.Member.Name)
                {
                    case "Day":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract( day from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%d', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("DAY(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(DAY,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Month":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(MONTH from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%m', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("MONTH(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(MONTH, ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Year":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(YEAR from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%Y', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("YEAR(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(YEAR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Hour":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(HOUR from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%H', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("HOUR(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(HOUR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Minute":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(MINUTE from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%M', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("MINUTE(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(MINUTE,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Second":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(SECOND from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%S', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("SECOND(");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(SECOND,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "Millisecond":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                throw new Exception("not implemented");
                            }
                            case ProviderName.SqLite:
                            {
                                throw new Exception("not implemented");
                            }
                            case ProviderName.MySql:
                            {
                                throw new Exception("not implemented");
                            }

                            case ProviderName.MsSql:
                            {
                                throw new Exception("not implemented");
                            }
                        }

                        break;


                    case "DayOfWeek":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract( isodow from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%w', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("(DAYOFWEEK(");
                                Visit(m.Expression);
                                StringB.Append(") - 1)");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(WEEKDAY,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                    case "DayOfYear":
                        switch (_providerName)
                        {
                            case ProviderName.PostgreSql:
                            {
                                StringB.Append("extract(doy from ");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                            case ProviderName.SqLite:
                            {
                                StringB.Append("CAST(strftime('%j', ");
                                Visit(m.Expression);
                                StringB.Append(") as INTEGER)");
                                break;
                            }
                            case ProviderName.MySql:
                            {
                                StringB.Append("(DAYOFYEAR(");
                                Visit(m.Expression);
                                StringB.Append(") - 1)");
                                break;
                            }

                            case ProviderName.MsSql:
                            {
                                StringB.Append("DATEPART(DAYOFYEAR,");
                                Visit(m.Expression);
                                StringB.Append(')');
                                break;
                            }
                        }

                        return;
                }

                var str1 = Expression.Lambda<Func<DateTime>>(m.Expression).Compile()();
                var ss = str1.GetType().GetProperty(m.Member.Name);
                value = ss.GetValue(str1, null);
                AddParameter(value);
                return;
            }

            var strS = new JoinAlias().GetAlias(m.Expression);
            if (strS != null && strS.IndexOf("TransparentIdentifier", StringComparison.Ordinal) != -1)
            {
                StringB.Append(GetColumnName(m.Member.Name));
                return;
            }

            var str = Expression.Lambda<Func<object>>(m.Expression).Compile()();
            value = null;
            if (m.Member.MemberType == MemberTypes.Field)
            {
                var fieldInfo = str.GetType().GetField(m.Member.Name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (fieldInfo != null)
                {
                    value = fieldInfo.GetValue(str);
                }
            }

            if (m.Member.MemberType == MemberTypes.Property)
            {
                var ass = str.GetType().GetProperty(m.Member.Name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                value = ass.GetValue(str, null);
            }


            AddParameter(value);
        }

        private void AddParameter(object value)
        {
            if (value is string)
            {
                var p1 = ParamName;
                StringB.Append(p1);
                Param.Add(p1, value);
                return;
            }

            if (_providerName == ProviderName.MsSql)
            {
                if (UtilsCore.IsGeo(value.GetType()))
                {
                    var p1 = ParamName;
                    StringB.Append(p1);
                    Param.Add(p1, ((IGeoShape)value).StAsText());
                    if (CurrentEvolution == Evolution.Update)
                    {
                        var srid = $"{p1}srid";
                        Param.Add(srid, ((IGeoShape)value).StSrid());
                    }
                }

                else if (PingComposite(Evolution.ElementAt) || PingComposite(Evolution.ElementAtOrDefault))
                {
                    StringB.Append(uint.Parse(value.ToString()));
                }
                else if (UtilsCore.IsAnonymousType(value.GetType()))
                {
                    var p1 = ParamName;
                    StringB.Append(p1);
                    Param.Add(p1, JsonConvert.SerializeObject(value));
                }
                else
                {
                    var p1 = ParamName;
                    StringB.Append(p1);
                    Param.Add(p1, value);
                }
            }


            else if (UtilsCore.IsGeo(value.GetType())) //todo geo
            {
                var p1 = ParamName;

                StringB.Append(p1);
                Param.Add(p1, ((IGeoShape)value).StAsText());
                if (CurrentEvolution == Evolution.Update)
                {
                    var srid = $"{p1}srid";
                    Param.Add(srid, ((IGeoShape)value).StSrid());
                }
            }
            else if (UtilsCore.IsAnonymousType(value.GetType()))
            {
                var p1 = ParamName;
                StringB.Append(p1);
                Param.Add(p1, JsonConvert.SerializeObject(value));
            }
            else
            {
                var p1 = ParamName;
                StringB.Append(p1);
                Param.Add(p1, value);
            }
        }

        private Expression VisitValue(Expression expr)
        {
            if (IsPredicate(expr))
            {
                StringB.Append("CASE WHEN (");
                Visit(expr);
                StringB.Append(") THEN 1 ELSE 0 END");
                return expr;
            }

            return Visit(expr);
        }

        private static bool IsPredicate(Expression expr)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return IsBoolean(expr.Type);
                case ExpressionType.Not:
                    return IsBoolean(expr.Type);
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    return true;
                case ExpressionType.Call:
                    return IsBoolean(expr.Type);
                default:
                    return false;
            }
        }

        private static bool IsBoolean(Type type)
        {
            return type == typeof(bool) || type == typeof(bool?);
        }

        private static string GetOperator(string methodName)
        {
            switch (methodName)
            {
                case "Add": return "+";
                case "Subtract": return "-";
                case "Multiply": return "*";
                case "Divide": return "/";
                case "Negate": return "-";
                case "Remainder": return "%";
                default: return null;
            }
        }

        protected override ReadOnlyCollection<Expression> VisitExpressionListUpdate(
            ReadOnlyCollection<Expression> original)
        {
            List<Expression> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                StringB.Append(" ,");

                var p = VisitUpdateBinary(original[i]);

                if (list != null)
                {
                    list.Add(p);
                }
                else if (p != original[i])
                {
                    list = new List<Expression>(n);
                    for (var j = 0; j < i; j++) list.Add(original[j]);
                    list.Add(p);
                }
            }

            if (list != null) return list.AsReadOnly();
            return original;
        }

        protected override ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
        {
            List<Expression> list = null;
            for (int i = 0, n = original.Count; i < n; i++)
            {
                StringB.Append(" ,");

                var p = Visit(original[i]);

                if (list != null)
                {
                    list.Add(p);
                }
                else if (p != original[i])
                {
                    list = new List<Expression>(n);
                    for (var j = 0; j < i; j++) list.Add(original[j]);
                    list.Add(p);
                }
            }

            if (list != null) return list.AsReadOnly();
            return original;
        }

        protected override NewExpression VisitNew(NewExpression nex)
        {
            var anonymousType = UtilsCore.IsAnonymousType(nex.Type);
            if (anonymousType)
            {
            }
            else
            {
                if (UtilsCore.IsCompileExpression(nex, out var o))
                {
                    AddParameter(o);

                    return nex;
                }
            }

            IEnumerable<Expression> args = VisitExpressionList(nex.Arguments);
            if (args != nex.Arguments)
            {
                if (nex.Members != null)
                {
                    AddListOne(new OneComposite
                    {
                        Operand = Evolution.SelectNew,
                        NewConstructor = Expression.New(nex.Constructor, args, nex.Members)
                    });
                    return Expression.New(nex.Constructor, args, nex.Members);
                }

                AddListOne(new OneComposite { Operand = Evolution.SelectNew, NewConstructor = nex });
                return Expression.New(nex.Constructor, args);
            }

            //todo ion100
            AddListOne(new OneComposite { Operand = Evolution.SelectNew, NewConstructor = nex });
            return nex;
        }

        private static Expression BindSelectMany(Expression exp, Expression source, LambdaExpression collectionSelector,
            LambdaExpression resultSelector)
        {
            throw new Exception("not implemented");
        }

        protected override Expression VisitParameter(ParameterExpression m)
        {
            if (m.Type == typeof(int) && _currentMethodSelect == "Select")
            {
                switch (_providerName)
                {
                    case ProviderName.MsSql:
                        StringB.Append(" CAST((ROW_NUMBER() OVER(ORDER BY (Select 0))) AS int) ");
                        break;
                    case ProviderName.MySql:
                        StringB.Append(" (CAST((row_number() OVER ()) AS UNSIGNED)) ");
                        break;
                    case ProviderName.PostgreSql:
                        StringB.Append(" (row_number() OVER ())  ");
                        break;
                    case ProviderName.SqLite:
                        StringB.Append(" (CAST((row_number() OVER ()) AS UNSIGNED)) ");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Database type is not defined:{_providerName}");
                }
            }

            return m;
        }
    }

    internal class MyClass
    {
        public int Age { get; set; }
        public string Name { get; set; }
    }
}