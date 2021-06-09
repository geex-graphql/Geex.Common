using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    public static class Extensions
    {
        public static string GetAppName(this IConfiguration configuration)
        {
            return configuration.GetValue<string>("App:Name");
        }
        public static string GetAppHostAddress(this IConfiguration configuration)
        {
            return configuration.GetValue<string>("App:HostAddress");
        }
    }
}
