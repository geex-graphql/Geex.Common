using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace System.Dynamic
{
    public static class SystemTypeExtensions
    {
        public static bool IsDynamic(this Type type) => typeof (IDynamicMetaObjectProvider).IsAssignableFrom(type);
    }
}
