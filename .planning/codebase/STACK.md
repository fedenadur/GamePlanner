# Technology Stack

**Analysis Date:** 2026-08-28

## Languages

**Primary:**
- C# (.NET 8.0) - Backend API (`GamePlanner.API/`)
- TypeScript 4.6.2 - Frontend application (`game-planner-ui/`)
- SQL - Database scripts (`Database/`)

**Secondary:**
- JavaScript - Node.js tooling (npm scripts, build configuration)

## Runtime

**Environment:**
- .NET 8.0 (CLR) - Backend execution
- Node.js - Frontend development and build
- SQL Server (SSMS) - Database runtime

**Package Manager:**
- NuGet (implicit via .NET SDK) - Backend dependencies
- npm - Frontend dependencies
- Lockfile: `game-planner-ui/package-lock.json` (present)

## Frameworks

**Core:**
- ASP.NET Core 8.0 - Web API framework in `GamePlanner.API/`
- Angular 13.3.0 - Frontend SPA framework in `game-planner-ui/`
- Entity Framework Core 8.0 - ORM for database access in `GamePlanner.API/`

**API/Data:**
- Dapper 2.1.72 - Lightweight micro-ORM for SQL queries in `GamePlanner.API/`
- Swashbuckle.AspNetCore 6.6.2 - Swagger/OpenAPI documentation in `GamePlanner.API/`

**Authentication:**
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0 - JWT token validation
- BCrypt.Net-Next 4.0.3 - Password hashing in `GamePlanner.API/Services/`

**Testing:**
- Jasmine 4.0.0 - Test framework in `game-planner-ui/`
- Karma 6.3.0 - Test runner in `game-planner-ui/`
- Karma Chrome Launcher 3.1.0 - Browser for testing
- Karma Jasmine HTML Reporter 1.7.0 - Test result reporting

**Build/Dev:**
- Angular CLI 13.3.1 - Frontend build tooling
- Angular DevKit Build Angular 13.3.1 - Build orchestration
- Angular Compiler CLI 13.3.0 - TypeScript compilation for Angular

**Reactive:**
- RxJS 7.5.0 - Reactive programming library in `game-planner-ui/`
- Zone.js 0.11.4 - Zone polyfill for Angular in `game-planner-ui/`

## Key Dependencies

**Critical:**
- Microsoft.EntityFrameworkCore.SqlServer 8.0.* - EF Core SQL Server provider
- Microsoft.AspNetCore.OpenApi 8.0.26 - OpenAPI/Swagger support
- Microsoft.IdentityModel.Tokens - JWT token validation in `GamePlanner.API/Program.cs`

**Infrastructure:**
- Microsoft.EntityFrameworkCore.Tools 8.0.* - EF Core migration tools (dev-only)
- tslib 2.3.0 - TypeScript runtime library in `game-planner-ui/`
- @types/jasmine 3.10.0 - Type definitions for Jasmine
- @types/node 12.11.1 - Type definitions for Node.js

## Configuration

**Environment:**
- `GamePlanner.API/appsettings.json` - Production settings
- `GamePlanner.API/appsettings.Development.json` - Development overrides
- `game-planner-ui/src/environments/environment.ts` - Frontend dev config
- `game-planner-ui/src/environments/environment.prod.ts` - Frontend production config
- `game-planner-ui/angular.json` - Angular CLI configuration
- `game-planner-ui/tsconfig.json` - TypeScript configuration

**Key Configs Required:**
- SQL Server connection string in `ConnectionStrings:DefaultConnection`
- IGDB API credentials in `IGDB:ClientId`, `IGDB:ClientSecret`
- Steam API key in `Steam:ApiKey`
- JWT secret in `Jwt:Secret`
- HLTB CSV path in `HLTB:CsvPath` (development only)
- Backend base URL in `App:BaseUrl`
- Frontend URL in `App:FrontendUrl` for CORS

**Build Configuration:**
- `GamePlanner.sln` - Visual Studio solution file
- `GamePlanner.API/GamePlanner.API.csproj` - Backend project configuration
- `game-planner-ui/tsconfig.app.json` - Application TypeScript config
- `game-planner-ui/tsconfig.spec.json` - Test TypeScript config

## Platform Requirements

**Development:**
- Windows (primary) or cross-platform .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- SQL Server (2019 or later, or SQL Server Express)
- Node.js 14+ (for frontend development)
- npm 6+ (for frontend package management)

**Production:**
- .NET 8.0 Runtime (or SDK for self-contained deployments)
- SQL Server instance
- IIS or standalone Kestrel hosting
- Node.js + npm for frontend build (or pre-built SPA)
- Nginx/IIS for static file serving of frontend

**API:**
- HTTP/HTTPS (configured on `https://localhost:7174` locally)
- CORS enabled for frontend on `http://localhost:4200` (development)

---

*Stack analysis: 2026-08-28*
