using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Secretary.Application.Abstractions;
using Secretary.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;

namespace Secretary.Api.Auth;

/// <summary>Token issuance lives in Api per the auth convention — Application services never
/// touch JWTs directly.</summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenGenerator(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string GenerateToken(Account account)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(AppClaimTypes.Role, account.Role.ToString()),
        };

        if (account.TenantId is { } tenantId)
        {
            claims.Add(new Claim(AppClaimTypes.TenantId, tenantId.ToString()));
        }

        var now = _clock.GetCurrentInstant().ToDateTimeUtc();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
