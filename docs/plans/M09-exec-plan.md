# Milestone 09 — ExecPlan

## Goal
Implement Explain Access end-to-end for the final backend milestone. Explanations must be safe and must match the normal authorization decision for the same supported current-time input.

## Required reading completed
- `AGENTS.md`
- `PLANS.md`
- `docs/milestones/M09-explain-access.md`
- `docs/spec/01-architecture.md`
- `docs/spec/02-domain-model.md`
- `docs/spec/04-authorization-access-control.md`
- `docs/spec/05-api-contracts-sequences.md`
- `docs/spec/07-security.md`
- `docs/spec/08-performance-scalability-operations.md`
- `docs/spec/09-testing-acceptance.md`
- User acceptance note for M07 and M09 kickoff.

## Current repository state
M00-M07 are accepted. RoleAssignments are implemented end-to-end, consumed by the authorization engine, cached through versioned Redis keys, and exposed through API endpoints. Access Requests are removed from project scope. DirectPermissionGrant and frontend remain out of scope.

## In scope
- Current-time Explain Access for:
  - Self explanation.
  - Owner explanation of organization users.
  - Delegated explain-other by actors with `pg.authorization.explain_others` in the requested organization/resource scope.
  - Owner override allow path.
  - RoleAssignment -> Role -> RolePermission -> Permission path.
  - ProjectAdministratorAssignment compatibility path.
  - Deny-by-default and stable denial explanations.
- `POST /api/v1/organizations/{organizationId}/authorization/explain`.
- Application query/handler/result models.
- Infrastructure explanation read service with targeted projections.
- Safe response contracts and validators.
- Tests for parity, security, endpoint behavior, and read-model behavior.

## Explicitly out of scope
- AccessRequest.
- DirectPermissionGrant.
- Frontend.
- Role inheritance.
- ABAC.
- SSO.
- Historical-time authorization simulation.
- Graph visualization.

## Architecture decisions used
- Final `allowed` and `reasonCode` come from `IAuthorizationDecisionService`.
- Explanation trace is loaded fresh from the database and is never returned from Redis alone.
- The explanation read service is an Application abstraction implemented by Infrastructure.
- API remains DTO mapping and HTTP behavior only.

## Domain and database changes
No Domain or migration changes are planned. Explain Access is read-only.

## Application use cases
- Add `ExplainAccessQuery`.
- Add `ExplainAccessHandler`.
- Add result models for steps, subject, scope, matched path, and safe details.
- Add `IAccessExplanationReadService` and read-model records.

## API changes
- Add Explain Access request/response contracts.
- Add request validator.
- Add endpoint mapping and route under the existing authorization endpoint group.

## Authorization checks
- Self-explain: active authenticated user can explain their own current-time access.
- Other-user explain: organization owner or actor with `pg.authorization.explain_others` in the requested organization/resource scope.
- Non-owner explain-other without `pg.authorization.explain_others` returns forbidden.
- Normal `/authorization/check` check-other behavior must use the same rule so Explain Access and Authorization Check stay consistent.
- The `pg.authorization.explain_others` permission check evaluates the actor's own access, never the target user's access, and must avoid recursive calls through Explain Access.
- Cross-tenant or hidden resources use existing safe not-found/denied conventions through the normal decision and read model.

## Security considerations
- No SQL, cache keys, tokens, IP addresses, raw claims, or unrelated roles in responses.
- Ordinary self-explain includes only relevant current-user/current-scope details.
- Owner explain-other includes richer but still safe role/assignment path details for the requested subject and permission only.
- Explain-other attempts are audited when the request reaches the handler.
- Delegated explain-other responses include only relevant details for the requested subject, permission, organization, and project/resource.

## Performance considerations
- Use `AsNoTracking`.
- Use projections and scoped queries by organization, subject, project, and normalized permission key.
- Do not use `Include` for explanation read paths.
- No batch explain endpoint unless a later approved milestone asks for it.

## Migration and seed changes
No migrations. Existing platform permissions include `pg.authorization.explain_self` and `pg.authorization.explain_others`.

## Test plan
- Application tests:
  - Owner override explanation parity.
  - RoleAssignment allowed explanation parity.
  - Project RoleAssignment allowed explanation parity.
  - Scheduled/expired/revoked/archived-role/inactive-permission/wrong-project/wrong-scope/no-grant denied explanations.
  - Self-explain allowed.
  - Non-owner with `pg.authorization.explain_others` explain-other allowed.
  - Non-owner without `pg.authorization.explain_others` explain-other denied.
  - Explain-other does not leak cross-tenant data.
  - Explain-other permission check does not recurse infinitely.
  - Owner explain-other allowed and audited.
- Infrastructure tests:
  - Read model loads assignments, roles, permissions, and project-admin paths.
  - Cross-tenant data is not exposed.
  - Expired active row is explainable as denied before worker.
- API/integration tests:
  - Requires auth.
  - Self response shape.
  - Owner explain other succeeds.
  - Delegated admin with `pg.authorization.explain_others` explain-other succeeds.
  - Non-owner without `pg.authorization.explain_others` explain-other returns 403.
  - `/authorization/check` and `/authorization/explain` behave consistently for check-other permissions.
  - Cross-tenant request does not leak.
  - Validation Problem Details.
  - Parity with `/authorization/check`.
- Architecture tests:
  - Clean Architecture boundaries remain intact.

## Implementation steps
1. Add Application models, abstraction, handler, and DI.
2. Add Infrastructure read service and DI.
3. Add Contracts, validators, mapping, and endpoint.
4. Add tests.
5. Run `dotnet build PermissionGraph.slnx -c Release`.
6. Run `dotnet test PermissionGraph.slnx -c Release --no-build`.
7. Record evidence.

## Progress
- Planning complete.
- Application, Infrastructure, API contracts/endpoints, validators, mappings, and tests implemented.
- Normal authorization check-other now allows organization owner or actor self-permission `pg.authorization.explain_others`.
- Explain Access uses the same delegated explain-other rule and preserves final decision parity through `IAuthorizationDecisionService`.
- Build and full solution tests completed successfully.

## Decisions and deviations
- Historical `evaluatedAtUtc` is accepted as nullable in the contract but non-null values are rejected as unsupported because M09 explicitly excludes historical-time simulation and normal authorization checks reject historical evaluation.
- `pg.authorization.explain_self` is documented as an existing platform permission. M09 accepts self-explain for any active authenticated user according to the approved behavior.
- `pg.authorization.explain_others` is required for delegated non-owner explain-other.
- Safe non-recursive delegated check-other is implemented in the normal authorization decision path by evaluating the actor's own `pg.authorization.explain_others` permission.

## Validation evidence
- `dotnet build PermissionGraph.slnx -c Release` passed with 0 warnings and 0 errors.
- `dotnet test tests\PermissionGraph.IntegrationTests\PermissionGraph.IntegrationTests.csproj -c Release --no-build --filter FullyQualifiedName~AuthorizationEndpointTests` passed 9/9.
- `dotnet test PermissionGraph.slnx -c Release --no-build` passed:
  - Domain: 140/140.
  - Application: 117/117.
  - Architecture: 10/10.
  - Integration: 127/127.

## Remaining gaps
- Historical-time explanation remains intentionally unsupported.
- DirectPermissionGrant, AccessRequest, frontend, role inheritance, ABAC, SSO, and graph visualization remain out of scope.
