using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Entities;

namespace Geex.Common.Gql.Interceptors
{
    public class UnitOfWorkInterceptor : DefaultHttpRequestInterceptor
    {

        public UnitOfWorkInterceptor() { }

        public override ValueTask OnCreateAsync(HttpContext context, IRequestExecutor requestExecutor, IQueryRequestBuilder requestBuilder,
            CancellationToken cancellationToken)
        {
            var task = base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
            var result = task.AsTask().ContinueWith((task1 => context.RequestServices.GetService<DbContext>().CommitAsync(cancellationToken).Wait(cancellationToken)), cancellationToken);
            return new ValueTask(result);
        }
    }
}