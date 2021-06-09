using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Geex.Common.Abstractions;
using Geex.Common.Gql;
using Geex.Common.Gql.Interceptors;
using Geex.Common.Gql.Roots;
using Geex.Common.Gql.Types;
using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Voyager;
using HotChocolate.Execution.Configuration;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Execution.Options;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Pagination;
using HotChocolate.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using Volo.Abp;

namespace Geex.Common
{
    public static class Extensions
    {

        public static void AddGeexGraphQL<T>(this IServiceCollection services) where T : GeexEntryModule<T>
        {
            services.AddTransient<ClaimsPrincipal>(x => x.GetService<IHttpContextAccessor>()?.HttpContext?.User);
            services.AddStorage();
            var schemaBuilder = services.AddGraphQLServer();
            schemaBuilder.AddConvention<ITypeInspector>(typeof(ClassEnumTypeConvention))
                .AddTypeConverter((Type source, Type target, out ChangeType? converter) =>
                {
                    converter = o => o;
                    return source.GetBaseClasses().Intersect(target.GetBaseClasses()).Any();
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
            .AddGeexTypes()
            .AddErrorFilter<UserFriendlyErrorFilter>(_ => new UserFriendlyErrorFilter(services.GetServiceProviderOrNull().GetService<ILoggerProvider>()))
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
                .AddApolloTracing();

            //schemaBuilder.ConfigureSchemaServices(x=>x.AddApplication<T>());
            services.AddSingleton(schemaBuilder);
            services.AddApplication<T>();
        }

        private static IRequestExecutorBuilder AddGeexTypes(this IRequestExecutorBuilder services)
        {
            return services.AddQueryType<Query>()
                            .AddMutationType<Mutation>()
                            .AddSubscriptionType<Subscription>()
                            .BindRuntimeType<ObjectId, ObjectIdType>();
        }

        public static void UseGeexGraphQL(this IApplicationBuilder app)
        {
            app.UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapGraphQL();
                });
            app.UseVoyager("/graphql", "/voyager");
            app.UsePlayground();
        }

        public static IRequestExecutorBuilder AddModuleTypes<TModule>(this IRequestExecutorBuilder schemaBuilder)
        {
            return schemaBuilder
                .AddTypes(typeof(TModule).Assembly.GetExportedTypes().Where(x => x.Namespace != null && x.Namespace.Contains($"{typeof(TModule).Namespace}.GqlSchemas")).ToArray());
        }

        public static IRequestExecutorBuilder AddModuleTypes(this IRequestExecutorBuilder schemaBuilder, Type gqlModuleType)
        {
            if (GeexModule.KnownAssembly.AddIfNotContains(gqlModuleType.Assembly))
            {
                var exportedTypes = gqlModuleType.Assembly.GetExportedTypes();
                var rootTypes = exportedTypes.Where(x => !x.IsAbstract &&
                                                              (AbpTypeExtensions.IsAssignableTo<Query>(x) ||
                                                               AbpTypeExtensions.IsAssignableTo<Mutation>(x) ||
                                                               AbpTypeExtensions.IsAssignableTo<Subscription>(x))).ToArray();
                schemaBuilder.AddTypes(rootTypes);
                var objectTypes = exportedTypes.Where(x => !x.IsAbstract && AbpTypeExtensions.IsAssignableTo<IType>(x)).ToList();
                schemaBuilder.AddTypes(objectTypes.ToArray());
                var classEnumTypes = exportedTypes.Where(x => !x.IsAbstract && x.IsClassEnum()).ToList();
                for (var i = 0; i < classEnumTypes.Count; i++)
                {
                    var baseEnumClasses = classEnumTypes[i].GetClassEnumBases();
                    foreach (var baseEnumClass in baseEnumClasses)
                    {
                        classEnumTypes.AddLast(baseEnumClass);
                    }
                }
                foreach (var classEnumType in classEnumTypes)
                {
                    schemaBuilder.BindRuntimeType(classEnumType, typeof(EnumerationType<,>).MakeGenericType(classEnumType, classEnumType.GetClassEnumValueType()));
                }
            }
            return schemaBuilder;
        }

        public static bool IsValidEmail(this string str)
        {
            return new Regex(@"\w[-\w.+]*@([A-Za-z0-9][-A-Za-z0-9]+\.)+[A-Za-z]{2,14}").IsMatch(str);
        }

        public static bool IsValidPhoneNumber(this string str)
        {
            return new Regex(@"\d{11}").IsMatch(str);
        }

        public static List<TPropertyType> GetPropertiesOfType<TPropertyType>(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(p => type.IsAssignableFrom(p.PropertyType))
                .Select(pi => (TPropertyType)pi.GetValue(null))
                .ToList();
        }

        public static bool IsClassEnum(this Type type)
        {
            if (type.IsValueType)
            {
                return false;
            }

            return AbpTypeExtensions.IsAssignableTo<IEnumeration>(type);
        }


        public static IServiceCollection AddStorage(this IServiceCollection builder, string connectionStringName = "GeexMongo")
        {
            var configuration = builder.GetConfiguration();
            var mongoUrl = new MongoUrl(configuration.GetConnectionString("GeexMongo")) { };
            var mongoSettings = MongoClientSettings.FromUrl(mongoUrl);
            mongoSettings.ApplicationName = configuration.GetAppName();
            DB.InitAsync(mongoUrl.DatabaseName, mongoSettings).Wait();
            builder.AddScoped<DbContext>(x => new DbContext(transactional: true));
            return builder;
        }

        public static IRequestExecutorBuilder AddApolloTracing(
      this IRequestExecutorBuilder builder,
      TracingPreference tracingPreference = TracingPreference.OnDemand,
      ITimestampProvider? timestampProvider = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            return tracingPreference == TracingPreference.Never ? builder : builder.ConfigureSchemaServices((Action<IServiceCollection>)(s => s.AddSingleton<IDiagnosticEventListener>((Func<IServiceProvider, IDiagnosticEventListener>)(sp => (IDiagnosticEventListener)new GeexApolloTracingDiagnosticEventListener(sp.GetApplicationService<ILogger<GeexApolloTracingDiagnosticEventListener>>(), tracingPreference, timestampProvider ?? sp.GetService<ITimestampProvider>())))));
        }
    }

    public class UserFriendlyErrorFilter : IErrorFilter
    {

        public UserFriendlyErrorFilter(ILoggerProvider? loggerProvider)
        {
            LoggerProvider = loggerProvider;

        }

        public ILoggerProvider LoggerProvider { get; }

        public IError OnError(IError error)
        {
            if (error.Exception is UserFriendlyException)
            {
                error.RemoveException();
            }

            if (error.Exception != default)
            {
                if (error.Exception?.TargetSite?.DeclaringType != default)
                {
                    LoggerProvider.CreateLogger(error.Exception.TargetSite.DeclaringType.FullName).LogException(error.Exception);
                }
                else
                {
                    LoggerProvider.CreateLogger("Null").LogException(error.Exception);
                }
            }
            return error;
        }
    }
}
