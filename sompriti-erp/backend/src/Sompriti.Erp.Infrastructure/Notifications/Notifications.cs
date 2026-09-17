using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sompriti.Erp.Application.Common;

namespace Sompriti.Erp.Infrastructure.Notifications;

// =====================================================================================
// SMS
// =====================================================================================

public sealed class SmsOptions
{
    public const string Section = "Sms";

    /// <summary>When false, SMS rows are stored with status SKIPPED and nothing is sent.</summary>
    public bool Enabled { get; set; }

    /// <summary>"BulkSmsBd", "GenericHttp" or "Log".</summary>
    public string Provider { get; set; } = "Log";

    public string ApiUrl { get; set; } = "https://bulksmsbd.net/api/smsapi";
    public string ApiKey { get; set; } = "";
    public string SenderId { get; set; } = "";

    // ---- GenericHttp settings (any Bangladesh gateway with an HTTP API) ----
    // Placeholders: {number} {message} {apiKey} {senderId}. Values are URL-encoded in the URL and JSON-escaped in the body.
    public string UrlTemplate { get; set; } = "";
    public string Method { get; set; } = "GET";
    public string? BodyTemplate { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string? AuthHeaderName { get; set; }
    public string? AuthHeaderValue { get; set; }
    /// <summary>Optional text that must appear in a successful response body.</summary>
    public string? SuccessContains { get; set; }
}

/// <summary>Development sender: logs the message and pretends it was sent.</summary>
public sealed class LogSmsSender(IOptions<SmsOptions> options, ILogger<LogSmsSender> logger) : ISmsSender
{
    public bool Enabled => options.Value.Enabled;

    public Task<string?> SendAsync(string normalizedNumber, string message, CancellationToken ct = default)
    {
        logger.LogInformation("[SMS:Log] To {Number}: {Message}", normalizedNumber, message);
        return Task.FromResult<string?>("log-" + Guid.NewGuid().ToString("N"));
    }
}

/// <summary>BulkSMSBD (bulksmsbd.net) HTTP API. Success response_code is 202.</summary>
public sealed class BulkSmsBdSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    private readonly SmsOptions _o = options.Value;
    public bool Enabled => _o.Enabled;

    public async Task<string?> SendAsync(string normalizedNumber, string message, CancellationToken ct = default)
    {
        var url = $"{_o.ApiUrl}?api_key={Uri.EscapeDataString(_o.ApiKey)}&type=text&number={normalizedNumber}" +
                  $"&senderid={Uri.EscapeDataString(_o.SenderId)}&message={Uri.EscapeDataString(message)}";
        using var response = await http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"BulkSMSBD HTTP {(int)response.StatusCode}: {Trim(body)}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response_code", out var code) && code.ToString() == "202")
                return doc.RootElement.TryGetProperty("message_id", out var id) ? id.ToString() : null;
        }
        catch (JsonException) { /* fall through */ }
        throw new InvalidOperationException($"BulkSMSBD rejected the message: {Trim(body)}");
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>Configurable HTTP sender for other Bangladesh SMS gateways.</summary>
public sealed class GenericHttpSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    private readonly SmsOptions _o = options.Value;
    public bool Enabled => _o.Enabled;

    public async Task<string?> SendAsync(string normalizedNumber, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_o.UrlTemplate))
            throw new InvalidOperationException("Sms:UrlTemplate is not configured.");

        string Fill(string template, Func<string, string> encode) => template
            .Replace("{number}", encode(normalizedNumber))
            .Replace("{message}", encode(message))
            .Replace("{apiKey}", encode(_o.ApiKey))
            .Replace("{senderId}", encode(_o.SenderId));

        var url = Fill(_o.UrlTemplate, Uri.EscapeDataString);
        using var request = new HttpRequestMessage(new HttpMethod(_o.Method.ToUpperInvariant()), url);
        if (!string.IsNullOrEmpty(_o.BodyTemplate))
        {
            var body = Fill(_o.BodyTemplate, v => JsonEncodedText.Encode(v).ToString());
            request.Content = new StringContent(body, Encoding.UTF8, _o.ContentType);
        }
        if (!string.IsNullOrWhiteSpace(_o.AuthHeaderName))
            request.Headers.TryAddWithoutValidation(_o.AuthHeaderName, _o.AuthHeaderValue);

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"SMS gateway HTTP {(int)response.StatusCode}: {(text.Length > 300 ? text[..300] : text)}");
        if (!string.IsNullOrEmpty(_o.SuccessContains) && !text.Contains(_o.SuccessContains, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SMS gateway rejected the message: {(text.Length > 300 ? text[..300] : text)}");
        return null;
    }
}

// =====================================================================================
// Email
// =====================================================================================

public sealed class EmailOptions
{
    public const string Section = "Email";
    /// <summary>"Log", "Brevo" or "Resend".</summary>
    public string Provider { get; set; } = "Log";
    public string ApiKey { get; set; } = "";
    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "Sompriti ERP";
}

public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[Email:Log] To {Email} | {Subject}\n{Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Brevo transactional email API (https://api.brevo.com/v3/smtp/email).</summary>
public sealed class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", o.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new
        {
            sender = new { email = o.FromAddress, name = o.FromName },
            to = new[] { new { email = toEmail, name = toName } },
            subject,
            htmlContent = htmlBody,
        });
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Brevo HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }
}

/// <summary>Resend email API (https://api.resend.com/emails).</summary>
public sealed class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = $"{o.FromName} <{o.FromAddress}>",
            to = new[] { toEmail },
            subject,
            html = htmlBody,
        });
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Resend HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }
}
