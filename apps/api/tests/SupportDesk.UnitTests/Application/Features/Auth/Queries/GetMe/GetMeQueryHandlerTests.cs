using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Features.Auth.Queries.GetMe;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth.Queries.GetMe;

public class GetMeQueryHandlerTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public async Task HandleAsync_WithValidAgentId_ReturnsAgent()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now);

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var handler = new GetMeQueryHandler(agents.Object);

        var result = await handler.HandleAsync(agent.Id, CancellationToken.None);

        Assert.Equal(agent.Id, result.Id);
        Assert.Equal(agent.FullName, result.FullName);
        Assert.Equal(agent.Email, result.Email);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownAgentId_ThrowsUnauthorized()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var handler = new GetMeQueryHandler(agents.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithInactiveAgent_ThrowsUnauthorized()
    {
        var agent = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, Now);
        agent.Deactivate();

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var handler = new GetMeQueryHandler(agents.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(agent.Id, CancellationToken.None));
    }
}