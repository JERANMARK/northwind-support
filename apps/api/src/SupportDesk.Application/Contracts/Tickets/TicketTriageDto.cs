using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class TicketTriageDto
{
    public TicketPriority Priority { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public int? AssignedAgentId { get; set; }

    public string Reason { get; set; } = string.Empty;
}
