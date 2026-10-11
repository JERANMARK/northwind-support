using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets;

public sealed record TicketTriageDecision(
    TicketPriority Priority,
    DateTime? DueAtUtc,
    int? AssignedAgentId,
    string Reason);

public static class TicketTriageService
{
    public static TicketTriageDecision Decide(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        DateTime createdAtUtc,
        DateTime nowUtc,
        IReadOnlyList<Agent> eligibleAgents,
        IReadOnlyDictionary<int, int> openTicketCounts,
        SlaOptions? slaOptions = null)
    {
        var options = slaOptions ?? SlaOptions.Default;
        var priority = category.ForcesCriticalPriority ? TicketPriority.Critical : requestedPriority ?? TicketPriority.Medium;

        var baseHours = options.GetBaseWindowHours(priority);
        var adjustedHours = customer.Tier == CustomerTier.Premium
            ? Math.Max(baseHours * options.PremiumCustomerMultiplier, options.MinimumWindowHours)
            : baseHours;

        var dueAtUtc = createdAtUtc.AddHours(adjustedHours);

        var availableAgents = eligibleAgents
            .Where(agent =>
            {
                var openCount = openTicketCounts.TryGetValue(agent.Id, out var count) ? count : 0;
                return openCount < agent.MaxOpenTickets;
            })
            .ToList();

        if (availableAgents.Count == 0)
        {
            return new TicketTriageDecision(
                priority,
                dueAtUtc,
                null,
                category.RequiresSpecialist
                    ? "No eligible specialist agent is available under the current workload and capacity limits."
                    : "No eligible agent is available under the current workload and capacity limits.");
        }

        var bestAgent = availableAgents
            .OrderBy(agent => openTicketCounts.TryGetValue(agent.Id, out var count) ? count : 0)
            .ThenBy(agent => agent.FullName, StringComparer.Ordinal)
            .First();

        return new TicketTriageDecision(
            priority,
            dueAtUtc,
            bestAgent.Id,
            category.ForcesCriticalPriority
                ? "Category requires critical priority handling."
                : $"Assigned to {bestAgent.FullName} with the fewest open tickets.");
    }
}
