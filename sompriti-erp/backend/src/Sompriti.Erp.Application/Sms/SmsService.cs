using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Application.Sms;

public sealed record SmsLogDto(Guid Uuid, string RecipientNumber, string Message, string ReferenceType, Guid ReferenceUuid,
    SmsStatus Status, int AttemptCount, DateTimeOffset? NextAttemptDate, string? LastError, DateTimeOffset CreatedDate, DateTimeOffset? SentDate);

public sealed record SmsLogQuery : PageQuery
{
    public SmsStatus? Status { get; init; }
}

public sealed class SmsService(IAppDbContext db, ISmsSender sender, TimeProvider clock, ILogger<SmsService> logger)
{
    /// <summary>Retry delays after each failed attempt (SRS 11.3): 1, 5, 15, 60, 240 minutes.</summary>
    public static readonly int[] RetryMinutes = [1, 5, 15, 60, 240];

    public Task<PagedResult<SmsLogDto>> ListAsync(SmsLogQuery q, CancellationToken ct)
    {
        var query = db.SmsOutbox.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(x => x.Status == s);
        if (q.Term is { } t)
        {
            var mobile = BdMobile.Normalize(t);
            query = query.Where(x => x.Message.Contains(t) || (mobile != null && x.RecipientNumber == mobile));
        }
        return query.OrderByDescending(x => x.CreatedDate)
            .ToPagedAsync(q, x => new SmsLogDto(x.Uuid, x.RecipientNumber, x.Message, x.ReferenceType, x.ReferenceUuid,
                x.Status, x.AttemptCount, x.NextAttemptDate, x.LastError, x.CreatedDate, x.SentDate), ct);
    }

    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        var sms = (await db.SmsOutbox.FirstOrDefaultAsync(x => x.Uuid == id, ct)).OrNotFound("SMS");
        if (sms.Status is not (SmsStatus.Failed or SmsStatus.Skipped))
            throw DomainException.Rule(ErrorCodes.BusinessRule, "Only FAILED or SKIPPED messages can be retried.");
        if (!sender.Enabled)
            throw DomainException.Rule(ErrorCodes.BusinessRule, "SMS sending is disabled in configuration.");
        sms.Status = SmsStatus.Pending;
        sms.AttemptCount = 0;
        sms.NextAttemptDate = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sends due messages. Rows are claimed with FOR UPDATE SKIP LOCKED so several app instances never send the same SMS twice.
    /// Returns the number of messages processed.
    /// </summary>
    public async Task<int> DispatchDueAsync(int batchSize, CancellationToken ct)
    {
        if (!sender.Enabled) return 0;
        var now = clock.GetUtcNow();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var due = await db.SmsOutbox
            .FromSqlRaw("SELECT * FROM sms_outbox WHERE status = 'PENDING' AND (next_attempt_date IS NULL OR next_attempt_date <= {0}) " +
                        "ORDER BY created_date LIMIT {1} FOR UPDATE SKIP LOCKED", now, batchSize)
            .ToListAsync(ct);

        foreach (var sms in due)
        {
            sms.AttemptCount++;
            try
            {
                sms.ProviderMessageId = await sender.SendAsync(sms.RecipientNumber, sms.Message, ct);
                sms.Status = SmsStatus.Sent;
                sms.SentDate = clock.GetUtcNow();
                sms.NextAttemptDate = null;
                sms.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                sms.LastError = error;
                if (sms.AttemptCount > RetryMinutes.Length)
                {
                    sms.Status = SmsStatus.Failed;
                    sms.NextAttemptDate = null;
                    logger.LogError("SMS {SmsUuid} to {Number} failed permanently: {Error}", sms.Uuid, sms.RecipientNumber, error);
                }
                else
                {
                    sms.NextAttemptDate = clock.GetUtcNow().AddMinutes(RetryMinutes[sms.AttemptCount - 1]);
                    logger.LogWarning("SMS {SmsUuid} attempt {Attempt} failed: {Error}", sms.Uuid, sms.AttemptCount, error);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return due.Count;
    }
}
