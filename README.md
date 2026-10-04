# CaptchaKit.Redis

افزونه‌ای برای ذخیره‌سازی CAPTCHAهای [CaptchaKit](https://www.nuget.org/packages/CaptchaKit) در Redis. این پکیج پیاده‌سازی `ICaptchaStore` را ثبت می‌کند و مقدار CAPTCHA را با زمان انقضا نگه می‌دارد. پس از وارد شدن پاسخ صحیح، مقدار به‌صورت اتمیک از Redis حذف می‌شود تا دوباره قابل استفاده نباشد.

## ویژگی‌ها

- ذخیره‌سازی مقدار CAPTCHA در Redis با زمان انقضای دلخواه
- اعتبارسنجی پاسخ و حذف آن فقط در صورت تطابق
- اعتبارسنجی و حذف اتمیک با اسکریپت Lua برای جلوگیری از مصرف چندباره
- ثبت خودکار `ICaptchaStore` و اتصال Redis در Dependency Injection
- امکان تعیین پیشوند برای کلیدهای ذخیره‌شده در Redis

## پیش‌نیازها

- .NET 10
- یک سرور Redis در دسترس برنامه
- پکیج `CaptchaKit` برای استفاده از قابلیت‌های اصلی CaptchaKit

## نصب

پکیج را به پروژه اضافه کنید:

```bash
dotnet add package CaptchaKit.Redis
```

## پیکربندی

در برنامه‌های ASP.NET Core، سرویس Redis را در زمان راه‌اندازی ثبت کنید. نمونه زیر connection string را از بخش `ConnectionStrings` تنظیمات می‌خواند:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

```csharp
using CaptchaKit.Redis.Extensions;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' is not configured.");

builder.Services.AddCaptchaRedis(options =>
{
    options.ConnectionString = redisConnectionString;
    options.KeyPrefix = "myapp:captcha:";
});

var app = builder.Build();
app.Run();
```

`ConnectionString` الزامی است. اگر این مقدار خالی یا فقط شامل فاصله باشد، هنگام ایجاد اتصال Redis، خطای `InvalidOperationException` با پیام `Redis connection string is required.` صادر می‌شود.

`KeyPrefix` اختیاری است و به ابتدای کلید هر CAPTCHA اضافه می‌شود. مقدار پیش‌فرض آن `captchakit:` است. برای نمونه، با پیشوند `myapp:captcha:` و کلید `login-123`، کلید Redis برابر `myapp:captcha:login-123` خواهد بود.

اتصال `IConnectionMultiplexer` و سرویس `ICaptchaStore` به‌صورت Singleton ثبت می‌شوند. برای مدیریت اتصال Redis، از همین ثبت سرویس استفاده کنید و در برنامه اتصال Multiplexer جداگانه‌ای برای این Store نسازید.

## استفاده از Store

پس از ثبت سرویس‌ها، `ICaptchaStore` را از Dependency Injection دریافت کنید:

```csharp
using CaptchaKit.Abstractions;

public sealed class CaptchaService(ICaptchaStore captchaStore)
{
    public Task SaveAsync(
        string challengeId,
        string answer,
        CancellationToken cancellationToken = default)
    {
        return captchaStore.SetAsync(
            challengeId,
            answer,
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    public Task<bool> VerifyAsync(
        string challengeId,
        string submittedAnswer,
        CancellationToken cancellationToken = default)
    {
        return captchaStore.ValidateAndRemoveAsync(
            challengeId,
            submittedAnswer,
            cancellationToken);
    }
}
```

جریان معمول استفاده:

1. هنگام ایجاد چالش، با `SetAsync` شناسه یکتا، پاسخ و مدت اعتبار آن را ذخیره کنید.
2. هنگام دریافت پاسخ کاربر، همان شناسه و پاسخ واردشده را به `ValidateAndRemoveAsync` بدهید.
3. اگر متد `true` برگرداند، پاسخ صحیح بوده و چالش حذف شده است؛ اگر `false` برگرداند، چالش وجود نداشته، منقضی شده یا پاسخ با مقدار ذخیره‌شده تطابق نداشته است.

پاسخ اشتباه باعث حذف چالش نمی‌شود؛ بنابراین کاربر می‌تواند تا پایان زمان انقضا دوباره تلاش کند. پس از پاسخ صحیح، مقدار حذف می‌شود و اعتبارسنجی دوباره همان چالش موفق نخواهد بود.

## نکات فنی و امنیتی

- برای هر چالش یک کلید یکتا بسازید؛ استفاده مجدد از یک کلید ممکن است مقدار قبلی را بازنویسی کند.
- مقدار ذخیره‌شده و مقایسه‌شده، پاسخ CAPTCHA است. Redis را در شبکه قابل اعتماد اجرا کنید و در محیط عملیاتی از احراز هویت و TLS متناسب با زیرساخت خود استفاده کنید.
- زمان انقضا هنگام فراخوانی `SetAsync` تعیین می‌شود و Redis پس از پایان آن، مقدار را منقضی می‌کند.
- اعتبارسنجی و حذف در یک اسکریپت Redis انجام می‌شود؛ بین مقایسه پاسخ و حذف کلید، درخواست دیگری نمی‌تواند همان چالش را مصرف کند.
- `CancellationToken` قبل از آغاز عملیات Redis بررسی می‌شود. APIهای Redis استفاده‌شده در این پیاده‌سازی توکن را دریافت نمی‌کنند؛ بنابراین لغو پس از شروع درخواست، آن درخواست را متوقف نمی‌کند.
- خطاهای اتصال یا اجرای فرمان Redis به فراخواننده منتقل می‌شوند و به نتیجه موفقیت‌آمیز تبدیل نمی‌شوند.

## اجرای تست‌ها

تست‌ها بدون نیاز به اجرای Redis، رفتار Store و ثبت Dependency Injection را با Mock بررسی می‌کنند:

```bash
dotnet test
```
