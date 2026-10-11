namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, int atRiskThresholdPercent = 25) =>
        Evaluate(default, dueAtUtc, resolvedAtUtc, nowUtc, atRiskThresholdPercent);

    public static SlaStatus Evaluate(DateTime createdAtUtc, DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, int atRiskThresholdPercent = 25)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (createdAtUtc != default && dueAtUtc > createdAtUtc)
        {
            var window = dueAtUtc.Value - createdAtUtc;
            var atRiskWindow = TimeSpan.FromTicks((long)(window.Ticks * (atRiskThresholdPercent / 100d)));

            if (dueAtUtc.Value - nowUtc <= atRiskWindow)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }
}
