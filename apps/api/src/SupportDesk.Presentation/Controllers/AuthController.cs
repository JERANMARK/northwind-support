using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetMe;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Authenticates an agent and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>Returns the current authenticated agent.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<AuthenticatedAgentDto> Me(
        [FromServices] GetMeQueryHandler handler,
        CancellationToken ct)
    {
        var agentIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(agentIdClaim, out var agentId))
        {
            throw new UnauthorizedAccessException("Invalid token.");
        }
        return handler.HandleAsync(agentId, ct);
    }
}