using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class SlaOptions
{
    public static SlaOptions Default { get; } = new();

    public Dictionary<string, int> BaseWindowHours { get; set; } = new()
    {
        [nameof(TicketPriority.Low)] = 72,
        [nameof(TicketPriority.Medium)] = 24,
        [nameof(TicketPriority.High)] = 8,
        [nameof(TicketPriority.Critical)] = 4,
    };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1;

    public int AtRiskThresholdPercent { get; set; } = 25;

    public int GetBaseWindowHours(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Low => BaseWindowHours.TryGetValue(nameof(TicketPriority.Low), out var low) ? low : 72,
            TicketPriority.Medium => BaseWindowHours.TryGetValue(nameof(TicketPriority.Medium), out var medium) ? medium : 24,
            TicketPriority.High => BaseWindowHours.TryGetValue(nameof(TicketPriority.High), out var high) ? high : 8,
            TicketPriority.Critical => BaseWindowHours.TryGetValue(nameof(TicketPriority.Critical), out var critical) ? critical : 4,
            _ => 24,
        };
}
