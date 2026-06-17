# Agent Roster — Event Ledger

| ID | Agent | Skill path | Reports to | Phase |
|----|-------|------------|------------|-------|
| ORCH-01 | [Orchestrator](orchestrator/AGENT.md) | [orchestrator](../ai-agent-skills/orchestrator/SKILL.md) | User | All |
| PM-01 | [Project Manager](agents/pm-agent.md) | [pm-planning](../ai-agent-skills/pm-planning/SKILL.md) | User | Planning |
| BA-01 | [Business Analyst](agents/ba-agent.md) | [business-analyst](../ai-agent-skills/business-analyst/SKILL.md) | PM-01 | Requirements |
| ARCH-01 | [Solution Architect](agents/architect-agent.md) | [solution-architect](../ai-agent-skills/solution-architect/SKILL.md) | PM-01 | Architecture |
| DEV-01 | [Gateway Developer](agents/dev-gateway-agent.md) | [developer](../ai-agent-skills/developer/SKILL.md) | PM-01 | Implementation |
| DEV-02 | [Account Developer](agents/dev-account-agent.md) | [developer](../ai-agent-skills/developer/SKILL.md) | PM-01 | Implementation |
| DEV-03 | [Platform Developer](agents/dev-platform-agent.md) | [developer](../ai-agent-skills/developer/SKILL.md) | PM-01 | Implementation |
| QA-01 | [Unit QA — Gateway](agents/qa-gateway-unit-agent.md) | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | PM-01 | QA |
| QA-02 | [Unit QA — Account](agents/qa-account-unit-agent.md) | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | PM-01 | QA |
| QA-03 | [Integration QA](agents/qa-integration-agent.md) | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | PM-01 | QA |
| QA-04 | [Resiliency & Trace QA](agents/qa-resiliency-agent.md) | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | PM-01 | QA |
| REV-01 | [Code Reviewer](agents/rev-code-agent.md) | [code-reviewer](../ai-agent-skills/code-reviewer/SKILL.md) | PM-01 | Review |
| REV-02 | [Security Reviewer (OWASP)](agents/rev-security-agent.md) | [security-reviewer](../ai-agent-skills/security-reviewer/SKILL.md) | PM-01 | Review |

## Parallel execution matrix

| Wave | Agents | Condition |
|------|--------|-----------|
| Dev wave 1 | DEV-01 ∥ DEV-02 | Architecture approved; Contracts project exists or scoped in design |
| Dev wave 2 | DEV-03 | After DEV-01 + DEV-02 APIs compile |
| **Review wave 1** | **REV-01 ∥ REV-02** | **After T-18 (implementation builds)** |
| Review fix | DEV-01..03 | If Critical/Major defects in `artifacts/05-code-review/defects.md` |
| QA wave 1 | QA-01 ∥ QA-02 | After review pass or fixes complete |
| QA wave 2 | QA-03 ∥ QA-04 | Gateway + Account integrated |
