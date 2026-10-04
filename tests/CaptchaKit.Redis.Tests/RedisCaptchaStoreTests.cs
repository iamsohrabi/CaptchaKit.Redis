using CaptchaKit.Abstractions;
using CaptchaKit.Redis.Extensions;
using CaptchaKit.Redis.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CaptchaKit.Redis.Tests;

public sealed class RedisCaptchaStoreTests
{
    private const string KeyPrefix = "test-captcha:";

    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexer = new();
    private readonly RedisCaptchaStore _store;

    public RedisCaptchaStoreTests()
    {
        _connectionMultiplexer
            .Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);

        _store = new RedisCaptchaStore(
            _connectionMultiplexer.Object,
            Options.Create(new RedisCaptchaOptions
            {
                ConnectionString = "localhost",
                KeyPrefix = KeyPrefix
            }));
    }

    [Fact]
    public async Task SetAsync_StoresValueWithPrefixedKeyAndExpiration()
    {
        var expiration = TimeSpan.FromMinutes(5);
        _database
            .Setup(database => database.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _store.SetAsync("session-1", "AB12", expiration);

        _database.Verify(database => database.StringSetAsync(
            (RedisKey)(KeyPrefix + "session-1"),
            (RedisValue)"AB12",
            It.Is<Expiration>(value => value.Equals((Expiration)expiration)),
            It.IsAny<ValueCondition>(),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRemoveAsync_ReturnsTrueWhenRedisScriptReturnsOne()
    {
        _database
            .Setup(database => database.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1, ResultType.Integer));

        var result = await _store.ValidateAndRemoveAsync("session-1", "AB12");

        Assert.True(result);
        _database.Verify(database => database.ScriptEvaluateAsync(
            It.Is<string>(script =>
                script.Contains("redis.call('GET'", StringComparison.Ordinal) &&
                script.Contains("redis.call('DEL'", StringComparison.Ordinal)),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 && keys[0] == (RedisKey)(KeyPrefix + "session-1")),
            It.Is<RedisValue[]>(values =>
                values.Length == 1 && values[0] == (RedisValue)"AB12"),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRemoveAsync_ReturnsFalseWhenRedisScriptReturnsZero()
    {
        _database
            .Setup(database => database.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)0, ResultType.Integer));

        var result = await _store.ValidateAndRemoveAsync("session-1", "wrong");

        Assert.False(result);
    }

    [Fact]
    public async Task SetAsync_ThrowsWhenCancellationIsRequestedBeforeCall()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.SetAsync("session-1", "AB12", TimeSpan.FromMinutes(5), cancellation.Token));

        _database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidateAndRemoveAsync_ThrowsWhenCancellationIsRequestedBeforeCall()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.ValidateAndRemoveAsync("session-1", "AB12", cancellation.Token));

        _database.VerifyNoOtherCalls();
    }

    [Fact]
    public void AddCaptchaRedis_RegistersStoreAndAppliesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddCaptchaRedis(options =>
        {
            options.ConnectionString = "localhost";
            options.KeyPrefix = KeyPrefix;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisCaptchaOptions>>().Value;

        Assert.Equal(KeyPrefix, options.KeyPrefix);
        Assert.Equal("localhost", options.ConnectionString);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICaptchaStore));
    }

    [Fact]
    public void AddCaptchaRedis_ThrowsWhenConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        services.AddCaptchaRedis(options => options.ConnectionString = "");

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IConnectionMultiplexer>());

        Assert.Equal("Redis connection string is required.", exception.Message);
    }
}
