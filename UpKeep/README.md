# UpKeep API

The ASP.NET Core API owns authentication, authorization, persistence, and task
scheduling. Property and task queries are scoped through the authenticated
user's workspace.

## Database

The default connection string uses SQL Server LocalDB and database `UpKeepDb`.
Apply migrations from the repository root:

```powershell
dotnet ef database update --project .\UpKeep\UpKeep.csproj
```

The `AddAuthenticationWorkspaces` migration preserves existing properties by
leaving their workspace unassigned. The first authenticated user to access a
protected API route adopts those legacy properties. Later users cannot read or
modify them.

## Authentication

Development without provider settings uses a stable local identity. Production
refuses to start until an OpenID Connect provider is configured. Set secrets in
the deployment environment rather than committing them:

```text
Authentication__Authority=https://identity.example.com
Authentication__ClientId=upkeep
Authentication__ClientSecret=<secret>
Authentication__PublicOrigin=https://upkeep.example.com
```

Register this exact callback with the provider:

```text
https://upkeep.example.com/api/auth/callback
```

`PublicOrigin` and `Authority` must use HTTPS. Serve the browser app and `/api`
under the same public origin. Session cookies are HTTP-only and secure in
production. Browser mutations also require the CSRF token returned by
`GET /api/auth/session` in the `X-CSRF-TOKEN` header.

The relevant endpoints are:

- `GET /api/auth/session` — current session status and CSRF token
- `GET /api/auth/login` — OpenID Connect challenge
- `POST /api/auth/logout` — expire the local UpKeep session

## Tests

Run `dotnet test` from the repository root. API tests use an isolated in-memory
database and simulated identities; they do not contact an external identity
provider. Schema changes must additionally be applied to a disposable SQL
Server database because the test host does not exercise SQL Server migrations.
