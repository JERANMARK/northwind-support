using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands.EscalateTicket;

public class EscalateTicketCommandHandlerTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public async Task HandleAsync_IncreasesPriorityByOneLevel()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.Low, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var result = await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Customer is angry"), "Admin", CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
    }

    [Fact]
    public async Task HandleAsync_LowEscalatesToMedium()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.Low, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
    }

    [Fact]
    public async Task HandleAsync_MediumEscalatesToHigh()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.Medium, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public async Task HandleAsync_HighEscalatesToCritical()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
    }

    [Fact]
    public async Task HandleAsync_RejectsEscalationOfCriticalTicket()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.Critical, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.Handler.HandleAsync(
                ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None));

        Assert.Contains("Critical tickets cannot be escalated", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_RejectsEscalationOfResolvedTicket()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        ticket.ChangeStatus(TicketStatus.Resolved, Now);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.Handler.HandleAsync(
                ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None));

        Assert.Contains("Resolved tickets cannot be escalated", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_RejectsEscalationOfClosedTicket()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        ticket.ChangeStatus(TicketStatus.Closed, Now);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.Handler.HandleAsync(
                ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None));

        Assert.Contains("Closed tickets cannot be escalated", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_RejectsShortReason()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Handler.HandleAsync(
                ticket.Id, new EscalateTicketRequest("xyz"), "Admin", CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey(nameof(EscalateTicketRequest.Reason)));
    }

    [Fact]
    public async Task HandleAsync_RejectsMissingEscalatedBy()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Handler.HandleAsync(
                ticket.Id, new EscalateTicketRequest("Valid reason"), "", CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey("EscalatedBy"));
    }

    [Fact]
    public async Task HandleAsync_ThrowsWhenTicketNotFound()
    {
        var context = CreateContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.Handler.HandleAsync(
                999, new EscalateTicketRequest("Valid reason"), "Admin", CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_RecalculatesDueDateFromEscalationTime()
    {
        var context = CreateContext();
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: 1);
        ticket.SetDueAtUtc(Now.AddHours(8), Now); // Original due date
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        // Should be recalculated from escalation time (Critical = 4h)
        Assert.Equal(Now.AddHours(4), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_KeepsCurrentAgentWhenStillEligible()
    {
        var context = CreateContext();
        var currentAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: currentAgent.Id);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { currentAgent });
        context.Tickets.Setup(t => t.GetAllOpenCountsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [currentAgent.Id] = 3 });

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        Assert.Equal(currentAgent.Id, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenCurrentAgentIneligible()
    {
        var context = CreateContext();
        var currentAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var otherAgent = new Agent("Sara Lindqvist", "sara@example.com", 10, Now);
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: currentAgent.Id);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { currentAgent, otherAgent });
        context.Tickets.Setup(t => t.GetAllOpenCountsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [currentAgent.Id] = 10, [otherAgent.Id] = 2 });

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Escalation reason"), "Admin", CancellationToken.None);

        // Current agent at capacity, should reassign to other agent
        Assert.Equal(otherAgent.Id, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_RecordsEscalationHistory()
    {
        var context = CreateContext();
        var currentAgent = new Agent("Alex Turner", "alex@example.com", 10, Now);
        var ticket = CreateTicket(priority: TicketPriority.High, assignedAgentId: currentAgent.Id);
        ticket.SetDueAtUtc(Now.AddHours(8), Now);
        context.Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        context.Agents.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { currentAgent });
        context.Tickets.Setup(t => t.GetAllOpenCountsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [currentAgent.Id] = 3 });

        TicketEscalation? recordedEscalation = null;
        context.Tickets.Setup(t => t.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()))
            .Callback<TicketEscalation, CancellationToken>((e, _) => recordedEscalation = e)
            .Returns(Task.CompletedTask);

        await context.Handler.HandleAsync(
            ticket.Id, new EscalateTicketRequest("Customer escalation"), "Support Lead", CancellationToken.None);

        Assert.NotNull(recordedEscalation);
        Assert.Equal(ticket.Id, recordedEscalation.TicketId);
        Assert.Equal(TicketPriority.High, recordedEscalation.FromPriority);
        Assert.Equal(TicketPriority.Critical, recordedEscalation.ToPriority);
        Assert.Equal(currentAgent.Id, recordedEscalation.FromAgentId);
        Assert.Equal(currentAgent.Id, recordedEscalation.ToAgentId);
        Assert.Equal(Now.AddHours(8), recordedEscalation.FromDueAtUtc);
        Assert.Equal(Now.AddHours(4), recordedEscalation.ToDueAtUtc);
        Assert.Equal("Customer escalation", recordedEscalation.Reason);
        Assert.Equal("Support Lead", recordedEscalation.EscalatedBy);
        Assert.Equal(Now, recordedEscalation.EscalatedAtUtc);
    }

    private static TestContext CreateContext()
    {
        var tickets = new Mock<ITicketRepository>();
        var agents = new Mock<IAgentRepository>();
        var categories = new Mock<ICategoryRepository>();
        var customers = new Mock<ICustomerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var clock = new FixedClock(Now);
        var slaOptions = Options.Create(SlaOptions.Default);
        var ticketQueries = new Mock<ITicketQueries>();
        ticketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto { Id = 1, Reference = "TCK-0001" });

        var getTicket = new GetTicketQueryHandler(ticketQueries.Object);

        categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("General", requiresSpecialist: false, forcesCriticalPriority: false));

        customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Standard, Now));

        // Default: return empty list of agents
        agents.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var handler = new EscalateTicketCommandHandler(
            tickets.Object, agents.Object, categories.Object, customers.Object, unitOfWork.Object,
            clock, slaOptions, getTicket, NullLogger<EscalateTicketCommandHandler>.Instance);

        return new TestContext
        {
            Handler = handler,
            Tickets = tickets,
            Agents = agents,
            Categories = categories,
            Customers = customers,
            UnitOfWork = unitOfWork
        };
    }

    private static Ticket CreateTicket(TicketPriority priority, int? assignedAgentId)
    {
        var ticket = Ticket.Raise("TCK-0001", "Test ticket", "Description", 1, 1, priority, Now);
        if (assignedAgentId.HasValue)
        {
            ticket.AssignTo(assignedAgentId.Value, Now);
        }
        return ticket;
    }

    private class TestContext
    {
        public EscalateTicketCommandHandler Handler { get; init; } = null!;
        public Mock<ITicketRepository> Tickets { get; init; } = null!;
        public Mock<IAgentRepository> Agents { get; init; } = null!;
        public Mock<ICategoryRepository> Categories { get; init; } = null!;
        public Mock<ICustomerRepository> Customers { get; init; } = null!;
        public Mock<IUnitOfWork> UnitOfWork { get; init; } = null!;
    }
}
