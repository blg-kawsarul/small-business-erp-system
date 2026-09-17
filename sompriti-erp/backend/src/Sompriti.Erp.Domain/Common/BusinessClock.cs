namespace Sompriti.Erp.Domain.Common;

/// <summary>Business time helpers. Bangladesh uses UTC+6 with no daylight saving.</summary>
public static class BusinessClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(6);

    public static DateOnly Today(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(Offset).DateTime);

    public static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
}
