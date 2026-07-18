using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PermissionGraph.Application.Abstractions.Clock;

namespace PermissionGraph.Infrastructure.Authentication;

internal sealed class JwtTokenIssuer(AuthenticationOptions options, IClock clock)
{
    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(ApplicationUser user, Guid sessionId)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(options.JwtAccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(TokenClaims.SessionId, sessionId.ToString()),
            new Claim(TokenClaims.SecurityStamp, user.SecurityStamp ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
