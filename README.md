# UpKeep

UpKeep is a .NET 8 and React application for organizing properties and their
maintenance tasks. The API stores data in SQL Server and the Vite client uses
the API through a same-origin `/api` route.

## Local setup

Prerequisites:

- .NET 8 SDK
- Node.js 20.19 or newer
- SQL Server LocalDB (the default development database)

From the repository root:

```powershell
dotnet restore
dotnet ef database update --project .\UpKeep\UpKeep.csproj
Set-Location .\UpKeepClient
npm install
Set-Location ..
.\dev.ps1
```

Open `http://localhost:5173`. Development uses one stable local identity, so no
external sign-in provider is needed. `dev.ps1` starts both the API on port 5085
and the Vite client, and stops both when you press Ctrl+C.

## Validation

```powershell
dotnet test
Set-Location .\UpKeepClient
npm test
npm run build
npm run lint
```

See [UpKeep/README.md](UpKeep/README.md) for API, database, migration, and
production authentication details. See
[UpKeepClient/README.md](UpKeepClient/README.md) for browser-client behavior.
