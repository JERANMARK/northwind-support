using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<LoginCommandHandler> logger)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email, ct);
        if (agent is null || !agent.IsActive)
        {
            logger.LogWarning("Login failed: agent not found or inactive for email {Email}.", request.Email);
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (!passwordHasher.VerifyPassword(request.Password, agent.PasswordHash))
        {
            logger.LogWarning("Login failed: invalid password for email {Email}.", request.Email);
            throw new UnauthorizedException("Invalid credentials.");
        }

        var (token, expiresAtUtc) = jwtTokenService.GenerateToken(agent);

        logger.LogInformation("Agent {AgentId} ({Email}) logged in.", agent.Id, agent.Email);

        return new LoginResponse(
            token,
            expiresAtUtc,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}