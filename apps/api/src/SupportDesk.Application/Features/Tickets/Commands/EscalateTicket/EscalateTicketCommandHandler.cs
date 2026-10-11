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

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    IAgentRepository agents,
    ICategoryRepository categories,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<SlaOptions> slaOptions,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 5 or > 500)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] = ["Reason must be between 5 and 500 characters."]
            });
        }

        if (string.IsNullOrWhiteSpace(escalatedBy))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["EscalatedBy"] = ["EscalatedBy is required."]
            });
        }

        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException("Critical tickets cannot be escalated.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException($"{ticket.Status} tickets cannot be escalated.");
        }

        var now = clock.UtcNow;
        var reason = request.Reason.Trim();
        var escalatedByTrimmed = escalatedBy.Trim();

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct) ??
            new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct) ??
            new Customer("Unknown customer", "unknown@example.com", null, CustomerTier.Standard, ticket.CreatedAtUtc);

        var nextPriority = ticket.Priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => throw new ConflictException("Critical tickets cannot be escalated.")
        };

        var allAgents = await agents.GetAllAsync(ct);
        var eligibleAgents = allAgents
            .Where(a => a.IsActive)
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.CategoryId == ticket.CategoryId))
            .ToList();

        var openCounts = eligibleAgents.Count == 0
            ? new Dictionary<int, int>()
            : await tickets.GetAllOpenCountsAsync(eligibleAgents.Select(a => a.Id).ToList(), ct);

        var triage = TicketTriageService.Decide(
            customer,
            category,
            nextPriority,
            now,
            now,
            eligibleAgents,
            openCounts,
            slaOptions.Value);

        var keepCurrentAgent = ticket.AssignedAgentId is int currentId &&
            eligibleAgents.Any(a => a.Id == currentId);
        var assignedAgentId = keepCurrentAgent ? ticket.AssignedAgentId : triage.AssignedAgentId;

        var baseWindowHours = slaOptions.Value.GetBaseWindowHours(nextPriority);
        var adjustedWindowHours = customer.Tier == CustomerTier.Premium
            ? Math.Max(baseWindowHours * slaOptions.Value.PremiumCustomerMultiplier, slaOptions.Value.MinimumWindowHours)
            : baseWindowHours;

        var fromPriority = ticket.Priority;
        var previousAgentId = ticket.AssignedAgentId;
        var previousDueAtUtc = ticket.DueAtUtc;
        var dueAtUtc = now.AddHours(adjustedWindowHours);

        ticket.Escalate(nextPriority, dueAtUtc, assignedAgentId, now);

        await tickets.AddEscalationAsync(new TicketEscalation(
            ticket.Id,
            fromPriority,
            nextPriority,
            previousAgentId,
            assignedAgentId,
            previousDueAtUtc,
            dueAtUtc,
            reason,
            escalatedByTrimmed,
            now), ct);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} at {EscalatedAtUtc}.",
            ticket.Reference,
            ticket.Priority,
            nextPriority,
            now);

        return await getTicket.HandleAsync(id, ct);
    }
}
