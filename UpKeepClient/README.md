# UpKeep browser client

The React and TypeScript client is built with Vite. During development, Vite
serves the UI on `http://localhost:5173` and proxies `/api` to the ASP.NET Core
API on `http://localhost:5085`.

Run both applications from the repository root with:

```powershell
.\dev.ps1
```

Or run this client separately from `UpKeepClient` with `npm run dev` after the
API is listening on port 5085.

## Authentication behavior

The client checks `GET /api/auth/session` before rendering private data. In
local development the API supplies a stable local identity. With a configured
production provider, anonymous visitors see the sign-in screen.

The API session response supplies an anti-forgery token. `src/api.ts` attaches
it to all POST, PUT, PATCH, and DELETE requests. Add new API calls through
`apiFetch`; do not call `fetch` directly for protected routes.

## Validation

```powershell
npm test
npm run build
npm run lint
```
