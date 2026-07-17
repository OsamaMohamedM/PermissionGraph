# PermissionGraph Documentation Package

This package converts the original large specification into repository-native documentation that Codex can use reliably across multiple sessions.

## Keep all four layers

```text
AGENTS.md
  Always-on implementation and safety rules

PLANS.md
  Required format for the active milestone execution plan

docs/spec/
  Focused shared specifications grouped by concern

docs/milestones/
  One execution contract and acceptance gate per milestone

docs/PermissionGraph-Master-Spec.md
  Canonical complete reference
```

## How to start

1. Copy this package into the root of the new PermissionGraph repository.
2. Open `docs/milestones/M00-foundation.md`.
3. Give Codex the kickoff prompt at the bottom of that file.
4. Require Codex to create `docs/plans/M00-exec-plan.md`.
5. Review the plan before allowing code changes.
6. After implementation, review every acceptance checkbox.
7. Move to M01 only after M00 has evidence.

## Important change from the original discussion

A dedicated performance-test environment specification was intentionally not added.

The package retains:

- Performance implementation rules.
- Performance targets.
- Required scenarios.
- Measured-results documentation.

It does not prescribe:

- Fixed local hardware.
- A separate performance Docker environment.
- A mandatory performance seed tool.
- A specific CI benchmark machine.

## Authorization architecture clarification

`docs/spec/01-architecture.md` now defines the fixed integration:

```text
ASP.NET authentication
  -> Permission policy
  -> PermissionAuthorizationHandler
  -> IAuthorizationDecisionService
  -> custom owner/membership/role/grant/scope/time evaluation
```

Endpoint permission checks and detailed grantability checks are separate and both mandatory.
