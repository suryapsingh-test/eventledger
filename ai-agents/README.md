# Event Ledger — Multi-Agent System

This folder defines **AI agents** (personas with goals and boundaries). Each agent **loads and applies** a skill from [`ai-agent-skills/`](../ai-agent-skills/) — skills are capabilities; agents are workers who use them.

## Model

```
                    ┌─────────────────────┐
                    │  Orchestrator Agent  │
                    │  (dispatches work)   │
                    └──────────┬──────────┘
                               │
         ┌─────────────────────┼─────────────────────┐
         ▼                     ▼                     ▼
   ┌───────────┐        ┌───────────┐        ┌───────────┐
   │ PM Agent  │        │ BA Agent  │        │ Arch Agent│
   │ + PM skill│        │ + BA skill│        │ + SA skill│
   └─────┬─────┘        └───────────┘        └───────────┘
         │ hub
         ├──────────────────┬──────────────────┐
         ▼                  ▼                  ▼
  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
  │ Dev-Gateway │   │ Dev-Account │   │ Dev-Platform│  ← parallel
  │ + dev skill │   │ + dev skill │   │ + dev skill │
  └─────────────┘   └─────────────┘   └─────────────┘
         │                  │                  │
         └──────────────────┼──────────────────┘
                            ▼
              ┌─────────────────────────────┐
              │ REV-Code ∥ REV-OWASP        │  ← review parallel
              └──────────────┬──────────────┘
                             ▼
              ┌─────────────────────────────┐
              │ QA Unit │ QA Integration │ QA Resiliency │  ← parallel
              └─────────────────────────────┘
```

## Agents vs skills

| Concept | Location | Purpose |
|---------|----------|---------|
| **Skill** | `ai-agent-skills/*/SKILL.md` | How to do the work (checklists, outputs, gates) |
| **Agent** | `ai-agents/agents/*.md` | Who does it (scope, parallel lane, reports to PM) |
| **Orchestrator** | `ai-agents/orchestrator/` | Assigns tasks, runs parallel agents, merges status |
| **Comms log** | `artifacts/agent-comms/` | Record of agent ↔ PM messages |
| **Dashboard** | `dashboard/project-dashboard.html` | Live progress (ORCH-01 maintains) |

## Agent roster

See [roster.md](roster.md) for full list.

| Agent ID | Name | Skill | Parallel group |
|----------|------|-------|----------------|
| ORCH-01 | Orchestrator | orchestrator | — |
| PM-01 | Project Manager | pm-planning | — |
| BA-01 | Business Analyst | business-analyst | — |
| ARCH-01 | Solution Architect | solution-architect | — |
| DEV-01 | Gateway Developer | developer | Dev wave 1 |
| DEV-02 | Account Developer | developer | Dev wave 1 |
| DEV-03 | Platform Developer | developer | Dev wave 2 |
| REV-01 | Code Reviewer | code-reviewer | Review wave 1 |
| REV-02 | Security Reviewer (OWASP) | security-reviewer | Review wave 1 |
| QA-01 | Unit QA (Gateway) | qa-engineer | QA wave 1 |
| QA-02 | Unit QA (Account) | qa-engineer | QA wave 1 |
| QA-03 | Integration QA | qa-engineer | QA wave 2 |
| QA-04 | Resiliency & Trace QA | qa-engineer | QA wave 2 |

**13 agents total** (including ORCH-01 and REV-01/02).

## How to run in Cursor

### Full orchestrated run

```
Use event-ledger orchestrator skill. Run the Event Ledger multi-agent workflow from current tracker state.
```

### Single agent

```
You are DEV-01 Gateway Developer. Read ai-agents/agents/dev-gateway-agent.md and apply ai-agent-skills/developer/SKILL.md.
```

### Parallel developers (orchestrator uses Task tool)

```
Orchestrator: launch DEV-01 and DEV-02 in parallel per ai-agents/parallel-workplan.md
```

## Submission evidence

Reviewers see:

1. **Agents** defined here with skill bindings
2. **Skills** with detailed procedures
3. **Message log** showing PM hub coordination
4. **Parallel workplan** showing concurrent dev/review/QA lanes
5. **Context + approval** — `artifacts/context/`, developer approval gates
6. **ai-sdlc.md** with per-agent session log
