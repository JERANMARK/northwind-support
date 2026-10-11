using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetMe;

public sealed class GetMeQueryHandler(IAgentRepository agents)
{
    public async Task<AuthenticatedAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("Agent not found or inactive.");
        }

        return new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}