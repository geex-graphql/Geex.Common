using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Geex.Common.Abstraction;
using Geex.Common.Abstractions;
using Geex.Common.Gql;
using Geex.Common.Gql.Interceptors;
using Geex.Common.Gql.Roots;
using Geex.Common.Gql.Types;
using Geex.Common.Logging;
using Geex.Common.Settings;

using HotChocolate;
using HotChocolate.Execution.Configuration;
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
    [DependsOn(
        typeof(GeexCoreModule),
        typeof(LoggingModule),
        typeof(SettingsModule))]
    public class GeexCommonModule : GeexModule<GeexCommonModule>
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var schemaBuilder = context.Services.GetSingletonInstance<IRequestExecutorBuilder>();
            schemaBuilder
                .SetPagingOptions(new PagingOptions()
                {
                    DefaultPageSize = 10,
                    IncludeTotalCount = true,
                    MaxPageSize = 1000
                })
                .AddErrorFilter<UserFriendlyErrorFilter>(_ =>
                    new UserFriendlyErrorFilter(context.Services.GetServiceProviderOrNull()
                        .GetService<ILoggerProvider>()))
                .AddAuthorization();

            var env = context.Services.GetSingletonInstance<IWebHostEnvironment>();
            context.Services.AddCors(options =>
            {
                if (env.IsDevelopment())
                {
                    options.AddDefaultPolicy(x =>
                        x.SetIsOriginAllowed(x => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
                }
            });
            base.ConfigureServices(context);
        }
    }
}
