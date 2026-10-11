using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets;

public class TicketTriageServiceTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Decide_ForcesCriticalPriorityForCriticalCategory()
    {
        var category = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var specialistAgent = new Agent("Sara Lindqvist", "sara@example.com", 10, Now);
        specialistAgent.AddSpecialization(category.Id);
        var agents = new List<Agent> { specialistAgent };
        var openCounts = new Dictionary<int, int> { [specialistAgent.Id] = 0 };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Low, Now, Now, agents, openCounts);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Category requires critical priority", decision.Reason);
    }

    [Fact]
    public void Decide_UsesRequestedPriorityForNonCriticalCategory()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.High, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(TicketPriority.High, decision.Priority);
    }

    [Fact]
    public void Decide_FallsBackToMediumWhenNoPriorityRequested()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, null, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(TicketPriority.Medium, decision.Priority);
    }

    [Fact]
    public void Decide_CalculatesSlaWindowForCriticalPriority()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Critical, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(Now.AddHours(4), decision.DueAtUtc);
    }

    [Fact]
    public void Decide_CalculatesSlaWindowForHighPriority()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.High, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
    }

    [Fact]
    public void Decide_CalculatesSlaWindowForMediumPriority()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(Now.AddHours(24), decision.DueAtUtc);
    }

    [Fact]
    public void Decide_CalculatesSlaWindowForLowPriority()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);
        var agents = new List<Agent>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Low, Now, Now, agents, new Dictionary<int, int>());

        Assert.Equal(Now.AddHours(72), decision.DueAtUtc);
    }

    [Fact]
    public void Decide_AppliesPremiumMultiplierWithFloor()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, Now);
        var agents = new List<Agent>();

        // Critical: 4h * 0.5 = 2h (above 1h floor)
        var criticalDecision = TicketTriageService.Decide(
            customer, category, TicketPriority.Critical, Now, Now, agents, new Dictionary<int, int>());
        Assert.Equal(Now.AddHours(2), criticalDecision.DueAtUtc);

        // Low: 72h * 0.5 = 36h (above 1h floor)
        var lowDecision = TicketTriageService.Decide(
            customer, category, TicketPriority.Low, Now, Now, agents, new Dictionary<int, int>());
        Assert.Equal(Now.AddHours(36), lowDecision.DueAtUtc);
    }

    [Fact]
    public void Decide_EnforcesMinimumOneHourFloor()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, Now);
        var agents = new List<Agent>();

        // Custom options with 0.1 multiplier would give 0.4h for Critical, but floor is 1h
        var customOptions = new SlaOptions
        {
            PremiumCustomerMultiplier = 0.1,
            MinimumWindowHours = 1
        };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Critical, Now, Now, agents, new Dictionary<int, int>(), customOptions);

        Assert.Equal(Now.AddHours(1), decision.DueAtUtc);
    }

    [Fact]
    public void Decide_RequiresSpecialistForSpecialistCategory()
    {
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var generalAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var specialistAgent = new Agent("Sara Lindqvist", "sara@example.com", 10, Now);
        specialistAgent.AddSpecialization(category.Id);

        var agents = new List<Agent> { generalAgent, specialistAgent };
        var openCounts = new Dictionary<int, int> { [generalAgent.Id] = 0, [specialistAgent.Id] = 0 };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        // Only specialist should be eligible
        Assert.Equal(specialistAgent.Id, decision.AssignedAgentId);
    }

    [Fact]
    public void Decide_ExcludesInactiveAgents()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var activeAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var inactiveAgent = new Agent("Yuki Tanaka", "yuki@example.com", 10, Now);
        inactiveAgent.Deactivate();

        var agents = new List<Agent> { activeAgent, inactiveAgent };
        var openCounts = new Dictionary<int, int> { [activeAgent.Id] = 0, [inactiveAgent.Id] = 0 };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        Assert.Equal(activeAgent.Id, decision.AssignedAgentId);
    }

    [Fact]
    public void Decide_RespectsMaxOpenTicketsCapacity()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var agentWithCapacity = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var agentAtCapacity = new Agent("Sara Lindqvist", "sara@example.com", 5, Now);

        var agents = new List<Agent> { agentWithCapacity, agentAtCapacity };
        var openCounts = new Dictionary<int, int>
        {
            [agentWithCapacity.Id] = 5, // below max of 10
            [agentAtCapacity.Id] = 5   // at max of 5
        };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        Assert.Equal(agentWithCapacity.Id, decision.AssignedAgentId);
    }

    [Fact]
    public void Decide_SelectsAgentWithFewestOpenTickets()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var busyAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var availableAgent = new Agent("Sara Lindqvist", "sara@example.com", 10, Now);

        var agents = new List<Agent> { busyAgent, availableAgent };
        var openCounts = new Dictionary<int, int>
        {
            [busyAgent.Id] = 8,
            [availableAgent.Id] = 2
        };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        Assert.Equal(availableAgent.Id, decision.AssignedAgentId);
    }

    [Fact]
    public void Decide_BreaksTiesDeterministicallyByName()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var agentA = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var agentB = new Agent("Ben Osei", "ben@example.com", 10, Now);

        var agents = new List<Agent> { agentB, agentA }; // Ben comes first in list
        var openCounts = new Dictionary<int, int>
        {
            [agentA.Id] = 3,
            [agentB.Id] = 3
        };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        // Alex should be chosen (alphabetically first)
        Assert.Equal(agentA.Id, decision.AssignedAgentId);
    }

    [Fact]
    public void Decide_ReturnsUnassignedWhenNoEligibleAgent()
    {
        var category = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        // Pass empty list to simulate no eligible agents after filtering
        var agents = new List<Agent>();
        var openCounts = new Dictionary<int, int>();

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("No eligible specialist agent", decision.Reason);
    }

    [Fact]
    public void Decide_ReturnsUnassignedWhenAllAgentsAtCapacity()
    {
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now);

        var agent1 = new Agent("Alex Turner", "alex@example.com", 5, Now);
        var agent2 = new Agent("Sara Lindqvist", "sara@example.com", 5, Now);

        var agents = new List<Agent> { agent1, agent2 };
        var openCounts = new Dictionary<int, int>
        {
            [agent1.Id] = 5, // at capacity
            [agent2.Id] = 5  // at capacity
        };

        var decision = TicketTriageService.Decide(
            customer, category, TicketPriority.Medium, Now, Now, agents, openCounts);

        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("No eligible agent", decision.Reason);
    }
}
