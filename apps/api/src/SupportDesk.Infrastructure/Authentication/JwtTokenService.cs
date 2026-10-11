using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Authentication;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    public (string Token, DateTime ExpiresAtUtc) GenerateToken(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.Value.LifetimeMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, agent.Email),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            NotBefore = now,
            IssuedAt = now,
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return (handler.WriteToken(token), expiresAtUtc);
    }
}
