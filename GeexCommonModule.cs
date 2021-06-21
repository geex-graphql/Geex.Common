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
using Geex.Common.Settings;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Pagination;
using HotChocolate.Utilities;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MongoDB.Bson;

using StackExchange.Redis.Extensions.Core;

using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Modularity;

namespace Geex.Common
{
    [DependsOn(typeof(SettingsModule))]
    public class GeexCommonModule : GeexModule<GeexCommonModule>
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            base.PreConfigureServices(context);
            context.Services.PreConfigure<GeexCommonModuleOptions>(options =>
            {
                Configuration.GetSection(nameof(GeexCommonModuleOptions)).Bind(new GeexCommonModuleOptions());
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var env = context.Services.GetSingletonInstance<IWebHostEnvironment>();
            context.Services.AddTransient<ClaimsPrincipal>(x => x.GetService<IHttpContextAccessor>()?.HttpContext?.User);
            context.Services.AddStorage();
            var schemaBuilder = context.Services.AddGraphQLServer();
            schemaBuilder.AddConvention<ITypeInspector>(typeof(ClassEnumTypeConvention))
                .AddTypeConverter((Type source, Type target, out ChangeType? converter) =>
                {
                    converter = o => o;
                    return source.GetBaseClasses(false).Intersect(target.GetBaseClasses(false)).Any();
                })
                .AddFiltering()
            .AddSorting()
            .AddProjections()
            .SetPagingOptions(new PagingOptions()
            {
                DefaultPageSize = 10,
                IncludeTotalCount = true,
                MaxPageSize = 1000
            })
            //.AddHttpRequestInterceptor(((context, executor, builder, token) =>
            //{
            //    context.Response.OnCompleted(() => context.RequestServices.GetService<DbContext>().CommitAsync(token));
            //    return default;
            //}))
            .AddHttpRequestInterceptor<UnitOfWorkInterceptor>()
            //.ConfigureOnRequestExecutorCreatedAsync(((provider, executor, cancellationToken) =>
            //{
            //    return new ValueTask(provider.GetService<DbContext>().CommitAsync(cancellationToken));
            //}))
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddSubscriptionType<Subscription>()
            .BindRuntimeType<ObjectId, ObjectIdType>()
            .AddErrorFilter<UserFriendlyErrorFilter>(_ => new UserFriendlyErrorFilter(context.Services.GetServiceProviderOrNull().GetService<ILoggerProvider>()))
            //.AddErrorFilter(x =>
            //{
            //    if (x.Exception is UserFriendlyException)
            //    {
            //        x.RemoveException();
            //    }
            //    else
            //    {

            //    }
            //    return x;
            //})
            .OnSchemaError((ctx, err) =>
            {
                throw new Exception("schema error", err);
            })
            //.UseExceptions()
            .AddAuthorization()
                .AddGeexApolloTracing();

            //schemaBuilder.ConfigureSchemaServices(x=>x.AddApplication<T>());
            context.Services.AddSingleton(schemaBuilder);
            context.Services.AddMediatR(typeof(GeexCommonModule));
            context.Services.AddHttpContextAccessor();
            context.Services.AddObjectAccessor<IApplicationBuilder>();

            context.Services.AddHealthChecks();
            context.Services.AddCors(options =>
            {
                if (env.IsDevelopment())
                {
                    options.AddDefaultPolicy(x => x.SetIsOriginAllowed(x => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
                }
            });
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
