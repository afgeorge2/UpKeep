# UpKeep

UpKeep organizes home maintenance across multiple properties. It combines a React and TypeScript frontend with an ASP.NET Core API and SQL Server.

## Features

- Properties and tasks with due dates, supplies, instructions, and time estimates.
- Recurring schedules, completion history and notes, postponement, skipping, and undo.
- Upcoming maintenance across properties and suggestions that fit an available time budget.
- Guided plans, local equipment-label reading, and optional AI equipment identification.

## Local setup (Windows / PowerShell)

Install the .NET 8 SDK, Node.js 22.13 or newer in the Node 22 release line (with npm), and SQL Server Express LocalDB. The frontend has been run with Node 22.23.2. Visual Studio's SQL Server Express LocalDB component can provide the database dependency.

Run these commands from the repository root, the folder containing `UpKeep.sln`:

```powershell
dotnet restore
Push-Location UpKeepClient
npm ci
Pop-Location
```

Install the Entity Framework CLI if `dotnet ef --version` is unavailable:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.30
```

Prepare the local database. If `sqllocaldb info MSSQLLocalDB` reports that the instance does not exist, first create it with `sqllocaldb create MSSQLLocalDB`.

```powershell
sqllocaldb start MSSQLLocalDB
dotnet ef database update --project UpKeep/UpKeep.csproj
```

The default connection uses Windows authentication and the `UpKeepDb` database in `(localdb)\MSSQLLocalDB`. Migrations create or update its schema; the API does not apply them automatically at startup. For another SQL Server connection or migration development, see the [backend README](UpKeep/README.md).

Start both applications:

```powershell
.\dev.ps1
```

Open [UpKeep](http://localhost:5173) or the development [Swagger UI](http://localhost:5085/swagger). Keep the launcher terminal open; Ctrl+C stops the processes it started. If it detects an already-running, healthy app, it leaves those servers running and exits.

Core maintenance features and the local label reader work without an OpenAI key. Follow [AI setup](AI-SETUP.md) to enable AI equipment identification.

## Project guide

| Location | Purpose |
| --- | --- |
| [UpKeep](UpKeep/README.md) | API, data models, database migrations, and equipment analysis |
| [UpKeepClient](UpKeepClient/README.md) | Browser interface, planning controls, and local OCR |
| [UpKeep.Tests](UpKeep.Tests/UpKeep.Tests.csproj) | Backend API and equipment-analysis tests |
| [AGENTS.md](AGENTS.md) | Repository instructions for coding agents |

## Validation

From the repository root:

```powershell
dotnet test
Push-Location UpKeepClient
npm test
npm run build
npm run lint
Pop-Location
```

Backend tests use an in-memory database and simulated AI responses. They do not require LocalDB or an API key, and do not verify SQL Server migrations or real provider access. See each component README for test scope.

## Troubleshooting

- **Properties or tasks fail to load:** Check the API terminal, start LocalDB, and apply pending migrations. Run the API under the Windows account that owns the LocalDB instance; a restricted agent sandbox may not have access even when tests pass.
- **Port already occupied:** Stop the relevant development server in its owning terminal before restarting. The expected frontend and API ports are 5173 and 5085. Vite can choose another port when 5173 is occupied, but the launcher health check expects 5173.
- **Label reader assets missing:** Run `npm ci` in `UpKeepClient`, then restart with `npm run dev` or the root launcher. These prepare the local OCR assets.
- **AI lookup unavailable:** Follow [AI setup](AI-SETUP.md) and restart the API after changing its environment variables.

This is currently a local-development application. Authentication and a production deployment configuration have not been implemented.
