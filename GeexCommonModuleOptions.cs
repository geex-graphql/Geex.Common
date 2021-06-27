using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Geex.Common.Abstraction;
using Geex.Common.Abstractions;

namespace Geex.Common
{
    public class GeexCommonModuleOptions : IGeexModuleOption<GeexCommonModule>
    {
        public string ConnectionString { get; set; } = "mongodb://localhost:27017/geex";
        public string AppName { get; set; } = "geex";
    }
}
