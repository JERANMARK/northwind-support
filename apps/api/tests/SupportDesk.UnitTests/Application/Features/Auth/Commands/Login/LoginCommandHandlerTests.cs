using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Authentication;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth.Commands.Login;

public class LoginCommandHandlerTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndAgent()
    {
        var passwordHasher = new PasswordHasher();
        var password = "Password123!";
        var passwordHash = passwordHasher.HashPassword(password);

        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now);
        agent.SetPasswordHash(passwordHash);

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "Test_Signing_Key_That_Is_Long_Enough_For_HMAC_SHA256",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            LifetimeMinutes = 120
        });
        var jwtTokenService = new JwtTokenService(jwtOptions, new FixedClock(Now));

        var handler = new LoginCommandHandler(
            agents.Object,
            passwordHasher,
            jwtTokenService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.HandleAsync(new LoginRequest(agent.Email, password), CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal(agent.Id, result.Agent.Id);
        Assert.Equal(agent.FullName, result.Agent.FullName);
        Assert.Equal(agent.Email, result.Agent.Email);
        Assert.True(result.ExpiresAtUtc > Now);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ThrowsUnauthorized()
    {
        var passwordHasher = new PasswordHasher();
        var password = "Password123!";
        var passwordHash = passwordHasher.HashPassword(password);

        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now);
        agent.SetPasswordHash(passwordHash);

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "Test_Signing_Key_That_Is_Long_Enough_For_HMAC_SHA256",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            LifetimeMinutes = 120
        });
        var jwtTokenService = new JwtTokenService(jwtOptions, new FixedClock(Now));

        var handler = new LoginCommandHandler(
            agents.Object,
            passwordHasher,
            jwtTokenService,
            NullLogger<LoginCommandHandler>.Instance);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(new LoginRequest(agent.Email, "WrongPassword"), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithUnknownEmail_ThrowsUnauthorized()
    {
        var passwordHasher = new PasswordHasher();

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "Test_Signing_Key_That_Is_Long_Enough_For_HMAC_SHA256",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            LifetimeMinutes = 120
        });
        var jwtTokenService = new JwtTokenService(jwtOptions, new FixedClock(Now));

        var handler = new LoginCommandHandler(
            agents.Object,
            passwordHasher,
            jwtTokenService,
            NullLogger<LoginCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(new LoginRequest("unknown@example.com", "anypassword"), CancellationToken.None));

        Assert.Equal("Invalid credentials.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveAgent_ThrowsUnauthorized()
    {
        var passwordHasher = new PasswordHasher();
        var password = "Password123!";
        var passwordHash = passwordHasher.HashPassword(password);

        var agent = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, Now);
        agent.SetPasswordHash(passwordHash);
        agent.Deactivate();

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "Test_Signing_Key_That_Is_Long_Enough_For_HMAC_SHA256",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            LifetimeMinutes = 120
        });
        var jwtTokenService = new JwtTokenService(jwtOptions, new FixedClock(Now));

        var handler = new LoginCommandHandler(
            agents.Object,
            passwordHasher,
            jwtTokenService,
            NullLogger<LoginCommandHandler>.Instance);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(new LoginRequest(agent.Email, password), CancellationToken.None));

        Assert.Equal("Invalid credentials.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyPassword_ReturnsFalseFromVerifier()
    {
        var passwordHasher = new PasswordHasher();
        var passwordHash = passwordHasher.HashPassword("Password123!");

        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now);
        agent.SetPasswordHash(passwordHash);

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "Test_Signing_Key_That_Is_Long_Enough_For_HMAC_SHA256",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            LifetimeMinutes = 120
        });
        var jwtTokenService = new JwtTokenService(jwtOptions, new FixedClock(Now));

        var handler = new LoginCommandHandler(
            agents.Object,
            passwordHasher,
            jwtTokenService,
            NullLogger<LoginCommandHandler>.Instance);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.HandleAsync(new LoginRequest(agent.Email, ""), CancellationToken.None));
    }
}