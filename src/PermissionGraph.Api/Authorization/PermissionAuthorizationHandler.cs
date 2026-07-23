namespace PermissionGraph.Api.Authorization;

internal sealed class PermissionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationDecisionService authorizationDecisionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null ||
            !TryGetRouteGuid(httpContext, "organizationId", out var organizationId))
        {
            return;
        }

        var projectId = TryGetRouteGuid(httpContext, "projectId", out var routeProjectId)
            ? routeProjectId
            : (Guid?)null;

        var decision = await authorizationDecisionService.CheckAsync(
            new CheckPermissionQuery(
                SubjectUserId: null,
                organizationId,
                projectId,
                requirement.PermissionKey),
            httpContext.RequestAborted);

        if (decision.Allowed)
        {
            context.Succeed(requirement);
        }
    }

    private static bool TryGetRouteGuid(HttpContext httpContext, string routeKey, out Guid value)
    {
        if (httpContext.Request.RouteValues.TryGetValue(routeKey, out var routeValue) &&
            Guid.TryParse(Convert.ToString(routeValue), out value))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }
}
