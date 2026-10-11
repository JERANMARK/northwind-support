using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(ITicketQueries tickets)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct) =>
        await tickets.GetEscalationsAsync(ticketId, ct);
}
