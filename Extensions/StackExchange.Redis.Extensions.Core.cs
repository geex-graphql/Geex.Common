using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Geex.Common.Abstractions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Entities;

using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;

// ReSharper disable once CheckNamespace
namespace StackExchange.Redis.Extensions.Core
{
    public static class Extensions
    {
        /// <summary>
        /// Add StackExchange.Redis with its serialization provider.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="redisConfiguration">The redis configration.</param>
        /// <typeparam name="T">The typof of serializer. <see cref="ISerializer" />.</typeparam>
        public static IServiceCollection AddStackExchangeRedisExtensions(this IServiceCollection services)
        {
            var redisConfiguration = services.GetConfiguration().GetSection("Redis").Get<RedisConfiguration>();
            services.AddSingleton<IRedisCacheClient, RedisCacheClient>();
            services.AddSingleton<IRedisDatabase>(x => x.GetService<IRedisCacheClient>()?.Db0!);
            services.AddSingleton<IRedisCacheConnectionPoolManager, RedisCacheConnectionPoolManager>();
            services.AddSingleton<ISerializer, SystemTextJsonSerializer>(x => new SystemTextJsonSerializer(Json.DefaultSerializeSettings));

            services.AddSingleton((provider) =>
            {
                return provider.GetRequiredService<IRedisCacheClient>().GetDbFromConfiguration();
            });

            services.AddSingleton(redisConfiguration);

            return services;
        }
        public static string GetId(this object obj)
        {
            if (obj is IEntity entity)
            {
                return entity.Id;
            }

            if (obj is IHasId hasId)
            {
                return hasId.Id;
            }

            if (obj is IValueObject valueObject)
            {
                return string.Join("", valueObject.EqualityComponents.Select(x => x.GetId()));
            }
            var idProp = obj.GetType().GetProperty("Id");
            if (idProp != default)
            {
                return idProp.GetValue(obj)?.ToString() ?? "";
            }

            return obj.GetHashCode().ToString();
        }

        public static async Task<T> GetNamedAsync<T>(this IRedisDatabase service, string key)
        {
            return (await service.GetAsync<T>($"{typeof(T).Name}:{key}"));
        }

        public static async Task<IDictionary<string, T>> GetAllNamedAsync<T>(this IRedisDatabase service, IEnumerable<string> keys = default)
        {
            if (keys != default)
            {
                return (await service.GetAllAsync<T>(keys.Select(key => $"{typeof(T).Name}:{key}")));
            }
            keys = await service.SearchKeysAsync(typeof(T).Name+":*");
            var result =  (await service.GetAllAsync<T>(keys));
            return result;
        }

        public static async Task<bool> RemoveNamedAsync<T>(this IRedisDatabase service, string key)
        {
            return await service.RemoveAsync($"{typeof(T).Name}:{key}");
        }

        public static async Task<bool> RemoveAllNamedAsync<T>(this IRedisDatabase service)
        {
            return await service.RemoveAsync($"{typeof(T).Name}");
        }

        public static async Task<T> GetAndRemoveAsync<T>(this IRedisDatabase service, T obj)
        {
            var result = await service.GetNamedAsync<T>(obj.GetId());
            await service.RemoveNamedAsync<T>(obj.GetId());
            return result;
        }

        public static async Task<bool> SetNamedAsync<T>(
            this IRedisDatabase service,
          T obj,
          TimeSpan? expireIn = default,
          CancellationToken token = default(CancellationToken)) where T : class
        {
            if (expireIn.HasValue)
            {
                return await service.AddAsync<T>($"{typeof(T).Name}:{obj.GetId()}", obj, expireIn.Value);
            }
            return await service.AddAsync<T>($"{typeof(T).Name}:{obj.GetId()}", obj);
        }
    }
}
