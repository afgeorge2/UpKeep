# UpKeep backend

ASP.NET Core 8 controller-based API with Entity Framework Core 8 and SQL Server. Start with the [project README](../README.md) for prerequisites and initial setup.

## Run the API

From the repository root:

```powershell
dotnet run --project UpKeep/UpKeep.csproj --launch-profile http
```

The HTTP development profile serves the API at `http://localhost:5085` and Swagger at [http://localhost:5085/swagger](http://localhost:5085/swagger). The React development server proxies `/api` requests to this port.

## Configuration and database

[appsettings.json](appsettings.json) defines `ConnectionStrings:UpKeepDatabase`, using `(localdb)\MSSQLLocalDB` and database `UpKeepDb`. LocalDB runs under its owning Windows user. Use that account when starting the API or running migrations.

To target a different SQL Server, set `ConnectionStrings__UpKeepDatabase` in the API process environment. Keep credentials out of committed configuration. [Program.cs](Program.cs) registers the SQL Server provider.

From the repository root, apply existing migrations:

```powershell
dotnet ef database update --project UpKeep/UpKeep.csproj
```

After changing a persisted model, generate a migration with a descriptive name:

```powershell
dotnet ef migrations add DescribeYourChange --project UpKeep/UpKeep.csproj --output-dir Data/Migrations
```

Review the generated migration and model snapshot before applying it. Existing databases contain saved property and task data; preserve those records when evolving the schema. The API does not run migrations automatically.

For equipment-analysis credentials, model configuration, and provider behavior, use [AI setup](../AI-SETUP.md).

## Code map

| Location | Responsibility |
| --- | --- |
| [Program.cs](Program.cs) | Dependency injection, SQL Server, HTTP client, CORS, rate limiting, and development Swagger |
| [Controllers](Controllers) | Property, maintenance-task, and equipment endpoints |
| [Contracts](Contracts) | Request validation and maintenance-task response types |
| [Models](Models) | Persisted property and task entities |
| [Data](Data) | EF context and migration history |
| [Services/EquipmentAnalysis.cs](Services/EquipmentAnalysis.cs) | Equipment-analysis provider request and result validation |

## API routes

| Method | Route | Purpose |
| --- | --- | --- |
| GET / POST | `/api/properties` | List or create properties |
| GET / DELETE | `/api/properties/{id}` | Retrieve or delete a property |
| GET / POST | `/api/properties/{propertyId}/maintenance-tasks` | List or create tasks |
| GET / PUT / DELETE | `/api/properties/{propertyId}/maintenance-tasks/{id}` | Retrieve, edit, or delete a task |
| PATCH | `/api/properties/{propertyId}/maintenance-tasks/{id}/status` | Complete, skip, or reopen an occurrence |
| PATCH | `/api/properties/{propertyId}/maintenance-tasks/{id}/postpone` | Move an open task to a later due date |
| GET | `/api/maintenance-tasks/upcoming?today=YYYY-MM-DD` | Open tasks through seven days after the supplied local date, including overdue tasks |
| GET | `/api/equipment/status` | Report whether an AI key is configured |
| POST | `/api/equipment/analyze` | Return equipment identification and maintenance drafts |

Swagger provides request schemas. Task IDs are scoped to their property. Property deletion also removes its tasks through the database relationship.

## Scheduling behavior

- `repeatEveryMonths = 0` means a task does not repeat. A completed recurring occurrence can generate one successor.
- Scheduled recurrence retains the original anchor, including month-end behavior. Postponement preserves that anchor; the next occurrence is the next scheduled date after the postponed due date.
- Completion-based recurrence uses the caller's `localDate` to calculate the next due date. Completion timestamps are recorded in UTC.
- Skipping closes a recurring occurrence without recording completed work.
- Undo removes an untouched successor. If the successor was edited or completed, it is retained, and completing the original again does not generate a duplicate.

## Tests

From the repository root:

```powershell
dotnet test UpKeep.Tests/UpKeep.Tests.csproj
```

[MaintenanceTasksApiTests.cs](../UpKeep.Tests/MaintenanceTasksApiTests.cs) covers CRUD, validation, property scoping, recurrence, undo, completion history, postponement, and the upcoming overview. [EquipmentAnalysisTests.cs](../UpKeep.Tests/EquipmentAnalysisTests.cs) checks provider handling with simulated responses.

The test host substitutes EF's in-memory database for SQL Server. These tests do not verify relational constraints, SQL Server concurrency behavior, or migration execution. Check schema changes against a disposable SQL Server database as well.
