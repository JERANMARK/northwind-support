using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<SlaOptions> slaOptions,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;

        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? new Customer("Unknown customer", "unknown@example.com", null, CustomerTier.Standard, now);

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? new Category("General", false, false);
        var priority = category.ForcesCriticalPriority ? TicketPriority.Critical : request.RequestedPriority ?? TicketPriority.Medium;
        var activeAgents = await agents.GetEligibleAsync(request.CategoryId, ct);
        var eligibleAgents = activeAgents
            .Where(a => a.IsActive)
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.CategoryId == request.CategoryId))
            .ToList();

        var openCounts = eligibleAgents.Count == 0
            ? new Dictionary<int, int>()
            : await tickets.GetAllOpenCountsAsync(eligibleAgents.Select(a => a.Id).ToList(), ct);

        var triage = TicketTriageService.Decide(
            customer,
            category,
            priority,
            now,
            now,
            eligibleAgents,
            openCounts,
            slaOptions.Value);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            triage.Priority,
            now);

        ticket.AssignTo(triage.AssignedAgentId, now);
        ticket.SetDueAtUtc(triage.DueAtUtc, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId} with priority {Priority}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority);

        var created = await getTicket.HandleAsync(ticket.Id, ct);
        created.Triage = new TicketTriageDto
        {
            Priority = triage.Priority,
            DueAtUtc = triage.DueAtUtc,
            AssignedAgentId = triage.AssignedAgentId,
            Reason = triage.Reason
        };

        return created;
    }
}
