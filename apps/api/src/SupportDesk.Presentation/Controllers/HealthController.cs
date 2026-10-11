using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly SupportDbContext _db;

    public HealthController(SupportDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Health check endpoint. Returns 200 if the database is reachable.
    /// This endpoint does not require authentication.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth(CancellationToken ct)
    {
        try
        {
            await _db.Database.CanConnectAsync(ct);

            return Ok(new HealthCheckResponse(
                Status: "Healthy",
                Database: "Connected",
                CheckedAtUtc: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthCheckResponse(
                Status: "Unhealthy",
                Database: $"Failed: {ex.Message}",
                CheckedAtUtc: DateTime.UtcNow));
        }
    }
}

public sealed record HealthCheckResponse(string Status, string Database, DateTime CheckedAtUtc);