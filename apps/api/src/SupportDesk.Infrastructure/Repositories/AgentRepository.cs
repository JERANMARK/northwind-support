using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLower();
        return db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail, ct);
    }

    public async Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .Include(a => a.Specializations)
            .OrderBy(a => a.FullName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Agent>> GetEligibleAsync(int categoryId, CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .Include(a => a.Specializations)
            .Where(a => a.IsActive)
            .OrderBy(a => a.FullName)
            .ToListAsync(ct);
}
