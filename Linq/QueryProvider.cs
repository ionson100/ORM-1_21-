using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
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
                        elementType), this, expression);
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
        public  abstract Task<List<TS>> ExecuteAsync<TS>(Expression expression, CancellationToken cancellationToken);
        
        object IQueryProvider.Execute(Expression expression)
        {
            return Execute(expression);
        }

        /// <summary>
        ///Query string
        /// </summary>
        public abstract string GetQueryText(Expression expression);

        /// <summary>
        /// Main execute
        /// </summary>
        public abstract object Execute<TS>(Expression expression);

        /// <summary>
        /// Executing a database query as a stored procedure
        /// </summary>
        public abstract object ExecuteSpp<TS>(Expression expression);

        /// <summary>
        /// Executing a database query
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public abstract object Execute(Expression expression);

        public abstract Task<TSource> ExecuteAsyncExtension<TSource>(Expression expression, CancellationToken cancellationToken);
    }


















}






