using DeliveryPilot.Identity.Application.Abstractions.Security;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public static class InvitationRateLimitCalculator
{
    public static int RetryAfterSeconds(DateTimeOffset now, IEnumerable<DateTimeOffset> pairIssuances, IEnumerable<DateTimeOffset> workspaceIssuances, InvitationIssuanceLimits limits)
    {
        DateTimeOffset[] pair = pairIssuances.Where(time => time <= now).ToArray();
        DateTimeOffset retryAt = now;
        foreach (DateTimeOffset issued in pair.Where(time => time > now - limits.PairCooldown))
        {
            retryAt = Max(retryAt, issued + limits.PairCooldown);
        }

        retryAt = Max(retryAt, WindowRetryAt(pair.Where(time => time > now.AddDays(-1)), limits.PairDailyLimit, TimeSpan.FromDays(1), now));
        retryAt = Max(retryAt, WindowRetryAt(workspaceIssuances.Where(time => time <= now && time > now.AddHours(-1)), limits.WorkspaceHourlyLimit, TimeSpan.FromHours(1), now));
        return retryAt <= now ? 0 : (int)Math.Min(int.MaxValue, Math.Ceiling((retryAt - now).TotalSeconds));
    }

    private static DateTimeOffset WindowRetryAt(IEnumerable<DateTimeOffset> times, int limit, TimeSpan window, DateTimeOffset now)
    {
        DateTimeOffset[] ordered = times.Order().ToArray();
        return ordered.Length < limit ? now : ordered[ordered.Length - limit] + window;
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
