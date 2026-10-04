
namespace CaptchaKit.Redis.Models;

/// <summary>
/// Configuration settings for Redis-backed CAPTCHA storage.
/// </summary>
public sealed class RedisCaptchaOptions
{
    /// <summary>
    /// Gets or sets the connection string used to connect to Redis.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the prefix prepended to CAPTCHA keys in Redis.
    /// </summary>
    public string KeyPrefix { get; set; } = "captchakit:";
}
