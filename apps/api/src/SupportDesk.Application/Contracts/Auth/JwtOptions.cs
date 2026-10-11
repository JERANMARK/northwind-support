namespace SupportDesk.Application.Contracts.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "DevLocal_Default_Jwt_Signing_Secret_Key_32_Chars!";

    public string Issuer { get; set; } = "NorthwindSupportDesk";

    public string Audience { get; set; } = "NorthwindSupportDesk";

    public int LifetimeMinutes { get; set; } = 120;
}
