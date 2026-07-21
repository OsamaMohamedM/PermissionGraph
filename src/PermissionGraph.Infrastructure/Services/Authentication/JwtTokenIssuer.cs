namespace PermissionGraph.Infrastructure.Services.Authentication;

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
            new Claim(TokenClaimsHelper.SessionId, sessionId.ToString()),
            new Claim(TokenClaimsHelper.SecurityStamp, user.SecurityStamp ?? string.Empty)
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