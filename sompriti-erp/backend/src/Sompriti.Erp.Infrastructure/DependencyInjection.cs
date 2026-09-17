using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.Sms;
using Sompriti.Erp.Infrastructure.Notifications;
using Sompriti.Erp.Infrastructure.Pdf;
using Sompriti.Erp.Infrastructure.Persistence;
using Sompriti.Erp.Infrastructure.Security;

namespace Sompriti.Erp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = ConnectionStrings.Resolve(config);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString, npg => npg.CommandTimeout(60)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseInitializer>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<ITokenService>(sp => sp.GetRequiredService<JwtTokenService>());
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        services.AddScoped<IOrderPdfRenderer, OrderPdfRenderer>();

        // SMS
        services.Configure<SmsOptions>(config.GetSection(SmsOptions.Section));
        var smsProvider = config[$"{SmsOptions.Section}:Provider"] ?? "Log";
        switch (smsProvider.Trim().ToLowerInvariant())
        {
            case "bulksmsbd":
                services.AddHttpClient<ISmsSender, BulkSmsBdSender>(c => c.Timeout = TimeSpan.FromSeconds(30));
                break;
            case "generichttp":
                services.AddHttpClient<ISmsSender, GenericHttpSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(30));
                break;
            default:
                services.AddSingleton<ISmsSender, LogSmsSender>();
                break;
        }
        services.AddHostedService<SmsDispatchWorker>();

        // Email
        services.Configure<EmailOptions>(config.GetSection(EmailOptions.Section));
        var emailProvider = config[$"{EmailOptions.Section}:Provider"] ?? "Log";
        switch (emailProvider.Trim().ToLowerInvariant())
        {
            case "brevo":
                services.AddHttpClient<IEmailSender, BrevoEmailSender>(c => c.Timeout = TimeSpan.FromSeconds(30));
                break;
            case "resend":
                services.AddHttpClient<IEmailSender, ResendEmailSender>(c => c.Timeout = TimeSpan.FromSeconds(30));
                break;
            default:
                services.AddSingleton<IEmailSender, LogEmailSender>();
                break;
        }

        return services;
    }
}

public static class ConnectionStrings
{
    /// <summary>
    /// Uses ConnectionStrings:Default when set; otherwise converts Railway's DATABASE_URL
    /// (postgresql://user:pass@host:port/db) into an Npgsql connection string.
    /// </summary>
    public static string Resolve(IConfiguration config)
    {
        var explicitCs = config.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(explicitCs)) return explicitCs;

        var url = config["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Set ConnectionStrings__Default or DATABASE_URL.");
        return FromUrl(url);
    }

    public static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"] ?? "Prefer";
        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password};" +
               $"SSL Mode={sslMode};Maximum Pool Size=20;Timezone=UTC";
    }
}

/// <summary>Background worker that sends queued SMS every 30 seconds (SRS 11.3).</summary>
public sealed class SmsDispatchWorker(IServiceScopeFactory scopes, IOptions<SmsOptions> options, ILogger<SmsDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("SMS sending is disabled (Sms:Enabled=false). Messages will be stored as SKIPPED.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sms = scope.ServiceProvider.GetRequiredService<SmsService>();
                int processed;
                do { processed = await sms.DispatchDueAsync(20, stoppingToken); } while (processed == 20 && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMS dispatch cycle failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
