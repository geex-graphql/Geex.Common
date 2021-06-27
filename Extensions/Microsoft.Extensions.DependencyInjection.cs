using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geex.Common;
using MongoDB.Driver;
using MongoDB.Entities;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extension
    {

        public static IServiceCollection AddStorage(this IServiceCollection builder)
        {
            var commonModuleOptions = builder.GetSingletonInstance<GeexCommonModuleOptions>();
            var mongoUrl = new MongoUrl(commonModuleOptions.ConnectionString) { };
            var mongoSettings = MongoClientSettings.FromUrl(mongoUrl);
            mongoSettings.ApplicationName = commonModuleOptions.AppName;
            DB.InitAsync(mongoUrl.DatabaseName, mongoSettings).Wait();
            builder.AddScoped<DbContext>(x => new DbContext(transactional: true));
            return builder;
        }
    }
}
