namespace PermissionGraph.Infrastructure.Services.Authorization;

internal sealed class RedisAuthorizationDecisionCache(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<RedisAuthorizationDecisionCache> logger) : IAuthorizationDecisionCache
{
    public async Task<AuthorizationDecision?> GetAsync(
        AuthorizationDecisionCacheKey key,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await connectionMultiplexer.GetDatabase().StringGetAsync(key.ToString());
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<AuthorizationDecisionCachePayload>((string)value!);
            if (payload is null)
            {
                return null;
            }

            return payload.Allowed
                ? AuthorizationDecision.Allow(payload.ReasonCode, payload.EvaluatedAtUtc)
                : AuthorizationDecision.Deny(payload.ReasonCode, payload.EvaluatedAtUtc);
        }
        catch (Exception exception) when (exception is RedisException or InvalidOperationException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Authorization decision cache read failed; falling back to source-of-truth evaluation.");
            return null;
        }
    }

    public async Task SetAsync(
        AuthorizationDecisionCacheKey key,
        AuthorizationDecision decision,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            var payload = new AuthorizationDecisionCachePayload(
                decision.Allowed,
                decision.ReasonCode,
                decision.EvaluatedAtUtc);
            var value = System.Text.Json.JsonSerializer.Serialize(payload);
            await connectionMultiplexer.GetDatabase().StringSetAsync(key.ToString(), value, ttl);
        }
        catch (Exception exception) when (exception is RedisException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Authorization decision cache write failed; continuing without cached decision.");
        }
    }

    private sealed record AuthorizationDecisionCachePayload(
        bool Allowed,
        string ReasonCode,
        DateTimeOffset EvaluatedAtUtc);
}
