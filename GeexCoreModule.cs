using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Geex.Common.Abstractions;
using Geex.Common.Gql;
using Geex.Common.Gql.Interceptors;
using Geex.Common.Gql.Roots;
using Geex.Common.Gql.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Pagination;
using HotChocolate.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Modularity;

namespace Geex.Common
{
    public class GeexCoreModule:GeexModule<GeexCoreModule>
    {
         public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient(typeof(Lazy<>), typeof(LazyInject<>));
            context.Services.AddTransient<ClaimsPrincipal>(x =>
                x.GetService<IHttpContextAccessor>()?.HttpContext?.User);
            base.PreConfigureServices(context);
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddStorage();
            var schemaBuilder = context.Services.AddGraphQLServer();
            context.Services.AddSingleton(schemaBuilder);
            schemaBuilder.AddConvention<ITypeInspector>(typeof(ClassEnumTypeConvention))
                .AddTypeConverter((Type source, Type target, out ChangeType? converter) =>
                {
                    converter = o => o;
                    return source.GetBaseClasses(false).Intersect(target.GetBaseClasses(false)).Any();
                })
                .AddFiltering()
                .AddSorting()
                .AddProjections()
                .AddHttpRequestInterceptor<UnitOfWorkInterceptor>()
                .AddQueryType<Query>()
                .AddMutationType<Mutation>()
                .AddSubscriptionType<Subscription>()
                .BindRuntimeType<ObjectId, ObjectIdType>()
                .OnSchemaError((ctx, err) => { throw new Exception("schema error", err); });

            context.Services.AddHttpContextAccessor();
            context.Services.AddObjectAccessor<IApplicationBuilder>();

            context.Services.AddHealthChecks();
            
            base.ConfigureServices(context);
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var _env = context.GetEnvironment();
            var _configuration = context.GetConfiguration();

            app.UseCors();
            if (_env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.Strict,
            });


            app.UseHealthChecks("/health-check");

            base.OnApplicationInitialization(context);
            app.UseGeexGraphQL();

        }
    }
}
