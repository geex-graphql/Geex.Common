using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geex.Common;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Entities;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class Extensions
    {
        
        public static IServiceCollection AddStorage(this IServiceCollection builder)
        {
            var configuration = builder.GetConfiguration();
            var commonModuleOptions = builder.ExecutePreConfiguredActions(new GeexCommonModuleOptions());
            var mongoUrl = new MongoUrl(commonModuleOptions.ConnectionString) { };
            var mongoSettings = MongoClientSettings.FromUrl(mongoUrl);
            mongoSettings.ApplicationName = commonModuleOptions.AppName;
            DB.InitAsync(mongoUrl.DatabaseName, mongoSettings).Wait();
            builder.AddScoped<DbContext>(x => new DbContext(transactional: true));
            return builder;
        }
    }
}
