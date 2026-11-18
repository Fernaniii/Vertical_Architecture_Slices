using Microsoft.Extensions.Caching.Hybrid;

namespace MyApp.Api.Common.Caching;

public static class DependencyInjection
{
    /// <summary>Must match the Redis resource name declared in the AppHost.</summary>
    public const string RedisConnectionName = "redis";

    /// <summary>HybridCache: in-memory L1, Redis L2 (picked up automatically through IDistributedCache).</summary>
    public static IHostApplicationBuilder AddCaching(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache(RedisConnectionName);

        builder.Services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            };
        });

        return builder;
    }
}
