---
name: local-routing
description: >-
  Repo-specific routing overrides for the RUG orchestrator. Customizations here
  take precedence over the canonical rug-routing rules synced from agent-repo.
  Add file-pattern overrides, custom triage rules, and repo-specific routing preferences.
---

# Local Routing Overrides

This file extends the canonical `rug-routing/SKILL.md` with repo-specific routing rules. The base rug-routing skill (synced from agent-repo) covers **core agents** that are always present: RUG, Foundry, Code Reviewer, Software Engineer Agent, Handoff, and Context7-Expert.

When you add **optional specialist agents** to your `.copilot-deps.json` `agents` array, you should add their routing rules here so RUG knows how to use them.

**How it works:**

- RUG reads `rug-routing` FIRST for the core agent roster and default rules
- RUG reads this file (local-routing) SECOND — rules here **override or extend** the defaults
- Core agent routing is already handled; this file is for optional agents and repo-specific customizations

**How to use this file:**

1. Add an optional agent to your `.copilot-deps.json` `agents` array (e.g., `"backend-engineer"`)
2. Run the sync workflow to pull the agent definition into your repo
3. Uncomment the corresponding rows in each section below
4. Adjust file patterns, triage rules, and handoffs to match your repo structure

---

## 1. Optional Agent Roster Extension

The core agent roster lives in `rug-routing`. The table below lists all **optional specialist agents** available from agent-repo. Uncomment agents you've added to your `.copilot-deps.json` `agents` array.

| #   | Agent                   | Domain                     | When to Route                                                                      | Skills Loaded                                       |
| --- | ----------------------- | -------------------------- | ---------------------------------------------------------------------------------- | --------------------------------------------------- |
| 1   | **Architect**           | System design              | API design, system diagrams, architecture decisions, service decomposition         | api-design-pro                                      |
| 2   | **Backend Engineer**    | Server-side                | .NET controllers, MediatR features, Dapper queries, DB migrations, auth middleware | dotnet-server, dapper-pro, mediatr-pro              |
| 3   | **Frontend Engineer**   | Frontend web               | UI components, state management, composables, service workers, web app manifests   | vue-pro, pwa-pro                                    |
| 4   | **Full-Stack Engineer** | Cross-layer implementation | End-to-end features spanning frontend + backend in a single coherent task          | All relevant framework skills (loaded dynamically)  |
| 7   | **Test Writer**         | Testing                    | Test generation for any language/framework. Writes comprehensive test suites       | Framework skills loaded dynamically per test target |

<!-- Not enabled: Mobile Engineer, Infrastructure Engineer, App Store Deployment Expert, CI Monitor Subagent -->

---

## 2. File Pattern Overrides

Add rows to route specific file patterns to the correct specialist agent. Uncomment rows for agents you've enabled, and adjust patterns to match your repo's directory structure.

| File Pattern / Path                                                                                                                                        | Route To             | Notes                                               |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | --------------------------------------------------- |
| `commerceApi/**/*.cs`, `commerceApi/Controllers/**`, `commerceApi/Features/**`, `commerceApi/Data/**`, `commerceApi/Endpoints/**`, `commerceApi/Models/**` | **Backend Engineer** | .NET application code                               |
| `db/**`, `**/*.sql`                                                                                                                                        | **Backend Engineer** | Database schema and seed files                      |
| `commerceApi/**/*.csproj`, `commerceApi/appsettings*.json`, `commerceApi/Program.cs`                                                                       | **Backend Engineer** | Project config and entry point                      |
| `docs/**`                                                                                                                                                  | **Architect**        | Architecture documentation and design               |
| `tests/**`                                                                                                                                                 | **Test Writer**      | Test files — route here for test generation/updates |

<!-- Not enabled: Mobile Engineer (no mobile files), Infrastructure Engineer (no infra agent), App Store Deployment Expert, CI Monitor Subagent -->

---

## 3. Task Phase Overrides

Route tasks to the correct agent based on the current phase of work. Uncomment rows for agents you've enabled.

| Phase                                                                                    | Route To                | Notes                                         |
| ---------------------------------------------------------------------------------------- | ----------------------- | --------------------------------------------- |
| **Implementation** — .NET controllers, MediatR handlers, Dapper queries, DB schema, auth | **Backend Engineer**    | Server-side production code                   |
| **Implementation** — API design, system architecture, service decomposition              | **Architect**           | Architecture decisions and design docs        |
| **Implementation** — UI components, state, composables, service workers, PWA             | **Frontend Engineer**   | Browser-side production code                  |
| **Implementation** — End-to-end feature spanning frontend + backend                      | **Full-Stack Engineer** | Cross-layer feature implementation            |
| **Testing** — writing or updating tests post-implementation                              | **Test Writer**         | Launch AFTER implementation for test coverage |

---

## 4. Bug Triage Overrides

Add rows to route specific bug symptoms to the correct diagnosis agent. Uncomment rows for agents you've enabled.

| Symptoms                                                                 | Primary Diagnosis Agent | Notes                       |
| ------------------------------------------------------------------------ | ----------------------- | --------------------------- |
| API errors, HTTP 4xx/5xx, database query failures, auth failures         | **Backend Engineer**    | Server-side errors          |
| MediatR pipeline issues, validation failures, command/query handler bugs | **Backend Engineer**    | CQRS / MediatR errors       |
| SQL errors, Dapper mapping failures, schema mismatches                   | **Backend Engineer**    | Data layer errors           |
| API contract mismatches, schema validation errors across services        | **Architect**           | Cross-service design issues |
| UI rendering bugs, state management issues, routing errors               | **Frontend Engineer**   | Browser-side rendering      |
| Test failures, test coverage gaps                                        | **Test Writer**         | Testing issues              |

---

## 5. Handoff Matrix Extension

Shows which optional agents can hand off to which. Uncomment the full matrix when you've enabled optional agents. A ✅ means the row agent can initiate a handoff to the column agent.

| From ↓ \ To →           | Context7 | Backend | Frontend | Architect | Full-Stack | Code Reviewer | Test Writer | SW Engineer | Foundry |
| ----------------------- | -------- | ------- | -------- | --------- | ---------- | ------------- | ----------- | ----------- | ------- |
| **Architect**           | ✅       | ✅      | ✅       | —         | —          | ✅            | —           | —           | —       |
| **Backend Engineer**    | ✅       | —       | ✅       | ✅        | —          | ✅            | ✅          | —           | —       |
| **Frontend Engineer**   | ✅       | ✅      | —        | —         | —          | ✅            | ✅          | —           | —       |
| **Full-Stack Engineer** | ✅       | —       | —        | ✅        | —          | ✅            | ✅          | —           | —       |
| **Test Writer**         | ✅       | —       | —        | —         | —          | ✅            | —           | —           | —       |

---

## 6. Custom Routing Rules

- Always route database files (`db/**`, `**/*.sql`) to **Backend Engineer**, not SW Engineer.
- Route `commerceApi/Features/**` (MediatR commands/queries) to **Backend Engineer**.
- Route `commerceApi/Behaviors/**` (pipeline behaviors) to **Backend Engineer**.
- For architecture questions touching multiple layers, prefer **Architect** over **Backend Engineer**.
- This repo has no frontend code — do not route to **Frontend Engineer** unless adding a frontend.
