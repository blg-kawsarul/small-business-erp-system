using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Domain.Entities;
using Sompriti.Erp.Domain.Enums;
using Sompriti.Erp.Domain.Rules;

namespace Sompriti.Erp.Infrastructure.Persistence;

/// <summary>
/// Applies versioned SQL migrations (embedded Persistence/Migrations/NNNN_name.sql files) and seeds the first ADMIN.
/// A PostgreSQL advisory lock guarantees only one app instance migrates at a time.
/// </summary>
public sealed class DatabaseInitializer(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DatabaseInitializer> logger)
{
    private const long AdvisoryLockKey = 72_600_001;

    public async Task InitializeAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({AdvisoryLockKey})", ct);
            try
            {
                await MigrateAsync(ct);
                await SeedAdminAsync(ct);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (version varchar(100) PRIMARY KEY, applied_date timestamptz NOT NULL DEFAULT now())", ct);

        var applied = (await db.Database.SqlQueryRaw<string>("SELECT version AS \"Value\" FROM schema_migrations").ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var assembly = typeof(DatabaseInitializer).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Persistence.Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(n => (Resource: n, Version: VersionOf(n)))
            .OrderBy(s => s.Version, StringComparer.Ordinal)
            .ToList();

        foreach (var (resource, version) in scripts)
        {
            if (applied.Contains(version)) continue;
            logger.LogInformation("Applying database migration {Version}", version);
            var sql = await ReadAsync(assembly, resource);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Plain ADO.NET command so the script text is sent exactly as written.
            await using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Transaction = tx.GetDbTransaction();
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await db.Database.ExecuteSqlRawAsync("INSERT INTO schema_migrations (version) VALUES ({0})", [version], ct);
            await tx.CommitAsync(ct);
        }
    }

    /// <summary>Creates the first ADMIN from SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD when no active admin exists.</summary>
    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Role == Role.Admin && u.Status == RecordStatus.Active, ct)) return;

        var email = config["SEED_ADMIN_EMAIL"];
        var password = config["SEED_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No ADMIN user exists. Set SEED_ADMIN_EMAIL and SEED_ADMIN_PASSWORD to create one on startup.");
            return;
        }
        if (!PasswordPolicy.IsValid(password))
        {
            logger.LogError("SEED_ADMIN_PASSWORD does not meet the password policy: {Policy}", PasswordPolicy.Description);
            return;
        }

        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);
        if (existing is not null)
        {
            existing.Role = Role.Admin;
            existing.Status = RecordStatus.Active;
            logger.LogWarning("Promoted existing user {Email} to ADMIN because no active admin existed.", normalized);
        }
        else
        {
            db.Users.Add(new AppUser
            {
                Uuid = Guid.NewGuid(),
                UserName = config["SEED_ADMIN_NAME"] ?? "Administrator",
                Email = normalized,
                PhoneNumber = BdMobile.Normalize(config["SEED_ADMIN_PHONE"]) ?? "8801700000000",
                PasswordHash = hasher.Hash(password),
                Role = Role.Admin,
                MustChangePassword = true,
            });
            logger.LogInformation("Seeded ADMIN user {Email}. The password must be changed after first login.", normalized);
        }
        await db.SaveChangesAsync(ct);
    }

    private static string VersionOf(string resourceName)
    {
        // "Sompriti.Erp.Infrastructure.Persistence.Migrations.0001_initial.sql" -> "0001_initial"
        var marker = ".Persistence.Migrations.";
        var name = resourceName[(resourceName.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
        return name[..^4];
    }

    private static async Task<string> ReadAsync(Assembly assembly, string resource)
    {
        await using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
