using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;
    private readonly FixedClock _clock;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _clock = new FixedClock(new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc));
        _queries = new TicketQueries(_db, _clock);

        SeedData();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByPriorityExactly()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_CombinesFiltersWithAndSemantics()
    {
        var customer = await _db.Set<Customer>().SingleAsync(c => c.Name == "Contoso Ltd");
        var category = await _db.Set<Category>().SingleAsync(c => c.Name == "Technical");
        var agent = await _db.Set<Agent>().SingleAsync(a => a.FullName == "Alex Turner");

        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CategoryId = category.Id,
            CustomerId = customer.Id,
            AssignedAgentId = agent.Id,
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Collection(result.Items, item =>
        {
            Assert.Equal(TicketStatus.InProgress, item.Status);
            Assert.Equal(TicketPriority.High, item.Priority);
            Assert.Equal(category.Id, item.Category.Id);
            Assert.Equal(customer.Id, item.Customer.Id);
            Assert.Equal(agent.Id, item.AssignedAgent!.Id);
        });
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerNameWithWhitespaceTrimmed()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Search = "  northwind  ",
            Page = 1,
            PageSize = 20
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Northwind Traders", result.Items[0].Customer.Name);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void SeedData()
    {
        var customers = new[]
        {
            new Customer("Contoso Ltd", "support@contoso.example", "+44 20 7946 0101", CustomerTier.Premium, _clock.UtcNow.AddDays(-30)),
            new Customer("Northwind Traders", "ops@northwind.example", "+1 206 555 0104", CustomerTier.Standard, _clock.UtcNow.AddDays(-20)),
            new Customer("Tailspin Toys", "service@tailspin.example", "+353 1 555 0105", CustomerTier.Standard, _clock.UtcNow.AddDays(-10))
        };

        var categories = new[]
        {
            new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false),
            new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false),
            new Category("General", requiresSpecialist: false, forcesCriticalPriority: false)
        };

        _db.AddRange(customers);
        _db.AddRange(categories);
        _db.SaveChanges();

        var categoryTechnical = _db.Set<Category>().Single(c => c.Name == "Technical");
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, _clock.UtcNow.AddDays(-100));
        agent.AddSpecialization(categoryTechnical.Id);
        _db.Add(agent);
        _db.SaveChanges();

        var customerContoso = _db.Set<Customer>().Single(c => c.Name == "Contoso Ltd");
        var customerNorthwind = _db.Set<Customer>().Single(c => c.Name == "Northwind Traders");
        var savedAgent = _db.Set<Agent>().Single(a => a.FullName == "Alex Turner");

        var matchingTicket = Ticket.Raise("TCK-2001", "VPN not connecting for finance team", "Users cannot establish a VPN connection to the office network.", customerContoso.Id, categoryTechnical.Id, TicketPriority.High, _clock.UtcNow.AddMinutes(-30));
        matchingTicket.AssignTo(savedAgent.Id, _clock.UtcNow.AddMinutes(-29));
        matchingTicket.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow.AddMinutes(-20));

        var otherHighTicket = Ticket.Raise("TCK-2002", "Billing audit shows duplicate tax code", "The billing sync recalculates tax incorrectly for some invoices.", customerContoso.Id, categoryTechnical.Id, TicketPriority.High, _clock.UtcNow.AddMinutes(-45));
        otherHighTicket.AssignTo(savedAgent.Id, _clock.UtcNow.AddMinutes(-44));
        otherHighTicket.ChangeStatus(TicketStatus.Open, _clock.UtcNow.AddMinutes(-40));

        var lowPriorityTicket = Ticket.Raise("TCK-2003", "Request for a copy of our invoice history", "Please send the invoice file for the last quarter to the finance team.", customerNorthwind.Id, categoryTechnical.Id, TicketPriority.Low, _clock.UtcNow.AddMinutes(-60));
        lowPriorityTicket.AssignTo(savedAgent.Id, _clock.UtcNow.AddMinutes(-58));
        lowPriorityTicket.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow.AddMinutes(-55));

        var unassignedTicket = Ticket.Raise("TCK-2004", "Need help updating the user directory", "A migration is staging but the web form is still pointing to the old tenant.", customerContoso.Id, categoryTechnical.Id, TicketPriority.High, _clock.UtcNow.AddMinutes(-90));
        unassignedTicket.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow.AddMinutes(-85));

        _db.AddRange(matchingTicket, otherHighTicket, lowPriorityTicket, unassignedTicket);
        _db.SaveChanges();
    }
}
