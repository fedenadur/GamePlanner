# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Backend
cd GamePlanner.API && dotnet run
dotnet build GamePlanner.sln

# Frontend
cd game-planner-ui && npm start

# Database — run numbered scripts manually in SSMS in sequence
# New migrations: add the next numbered file (e.g. 27_...sql) and apply manually
```

## Behavior Rules

- Be concise. No restating the problem, no trailing summaries.
- Work step-by-step. WAIT for confirmation before moving to the next step.
- If something is unclear, ask one short question.
- Generate only the required files/classes — no boilerplate unless asked.
- Stored Procedures ONLY for Insert/Update/Delete. EF LINQ for reads.
- Services are ONLY for external APIs. Logic belongs in Managers.
- No unnecessary abstractions, design patterns, or features beyond what's requested.

**Priority:** Clarity > Simplicity > Performance > Completeness
