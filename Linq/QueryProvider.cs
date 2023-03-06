﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace ORM_1_21_.Linq
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class QueryProvider : IQueryProvider
    {
        IQueryable<TS> IQueryProvider.CreateQuery<TS>(Expression expression)
        {
            return new Query<TS>(this, expression);
        }
        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            var elementType = TypeSystem.GetElementType(expression.Type);
            try
            {
                return (
                    IQueryable)Activator.CreateInstance(typeof(Query<>).MakeGenericType(
                        elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException tie)
            {
                if (tie.InnerException != null) throw tie.InnerException;
                throw;
            }
        }

        TS IQueryProvider.Execute<TS>(Expression expression)
        {
            return (TS)Execute<TS>(expression);
        }

        /// <summary>
        /// Query async
        /// </summary>
        public  abstract Task<List<TS>> ExecuteAsync<TS>(Expression expression);
       

        object IQueryProvider.Execute(Expression expression)
        {
            return Execute(expression);
        }

        


        

        /// <summary>
        ///Query string
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public abstract string GetQueryText(Expression expression);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <typeparam name="TS"></typeparam>
        /// <returns></returns>
        public abstract object Execute<TS>(Expression expression);

        /// <summary>
        /// Executing a database query as a stored procedure
        /// </summary>
        /// <param name="expression"></param>
        /// <typeparam name="TS"></typeparam>
        /// <returns></returns>
        public abstract object ExecuteSPP<TS>(Expression expression);

        /// <summary>
        /// Executing a database query
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public abstract object Execute(Expression expression);

        internal abstract string GetQueryTextForJoin(Expression expression, List<OneComprosite> comprosite, Dictionary<string, object> dictionary, string parStr);

    }


















}






