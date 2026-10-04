using CaptchaKit.Abstractions;
using CaptchaKit.Redis.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CaptchaKit.Redis.Extensions;

/// <summary>
/// Provides dependency-injection registration methods for the Redis CAPTCHA store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis-backed CAPTCHA storage and its Redis connection.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configure">A callback that configures Redis CAPTCHA options.</param>
    /// <returns>The service collection so registrations can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// The configured Redis connection string is missing or whitespace.
    /// </exception>
    public static IServiceCollection AddCaptchaRedis(
        this IServiceCollection services,
        Action<RedisCaptchaOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options =
                sp.GetRequiredService<
                    IOptions<RedisCaptchaOptions>>().Value;

            if (string.IsNullOrWhiteSpace(
                    options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Redis connection string is required.");
            }

            return ConnectionMultiplexer.Connect(
                options.ConnectionString);
        });

        services.AddSingleton<ICaptchaStore,
            RedisCaptchaStore>();

        return services;
    }
}
