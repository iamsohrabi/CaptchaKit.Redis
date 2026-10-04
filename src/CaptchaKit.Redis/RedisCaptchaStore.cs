
using CaptchaKit.Abstractions;
using CaptchaKit.Redis.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CaptchaKit.Redis;

/// <summary>
/// Stores CAPTCHA values in Redis and validates them as single-use entries.
/// </summary>
internal sealed class RedisCaptchaStore : ICaptchaStore
{
    private readonly IDatabase _database;
    private readonly RedisCaptchaOptions _options;

    // Compare and delete in one Redis operation so a value can only be consumed once.
    private const string ValidateAndRemoveScript = """
        local value = redis.call('GET', KEYS[1])

        if not value then
            return 0
        end

        if value ~= ARGV[1] then
            return 0
        end

        redis.call('DEL', KEYS[1])

        return 1
        """;

    /// <summary>
    /// Creates a CAPTCHA store using the configured Redis connection and options.
    /// </summary>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
    /// <param name="options">The Redis CAPTCHA configuration.</param>
    public RedisCaptchaStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisCaptchaOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
    }

    /// <summary>
    /// Saves a CAPTCHA value with the specified expiration.
    /// </summary>
    /// <param name="key">The CAPTCHA entry key.</param>
    /// <param name="value">The CAPTCHA value to store.</param>
    /// <param name="expiration">How long the entry remains available.</param>
    /// <param name="cancellationToken">A token used to cancel the operation before it starts.</param>
    public async Task SetAsync(
        string key,
        string value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.StringSetAsync(
            BuildKey(key),
            value,
            expiration);
    }

    /// <summary>
    /// Validates a CAPTCHA value and removes its entry if it matches.
    /// </summary>
    /// <param name="key">The CAPTCHA entry key.</param>
    /// <param name="value">The value to compare with the stored CAPTCHA.</param>
    /// <param name="cancellationToken">A token used to cancel the operation before it starts.</param>
    /// <returns><see langword="true"/> if the value matched and was removed; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ValidateAndRemoveAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _database.ScriptEvaluateAsync(
            ValidateAndRemoveScript,
            new RedisKey[]
            {
                BuildKey(key)
            },
            new RedisValue[]
            {
                value
            });

        return (int)result == 1;
    }

    /// <summary>
    /// Adds the configured prefix to a CAPTCHA entry key.
    /// </summary>
    /// <param name="key">The CAPTCHA entry key.</param>
    /// <returns>The key to use in Redis.</returns>
    private RedisKey BuildKey(string key)
        => $"{_options.KeyPrefix}{key}";
}
