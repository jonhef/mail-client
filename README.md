# Mail Client Monorepo

Client app (Vite + React) lives in `client/`. ASP.NET Core backend lives in `server/`.

## Prerequisites
- Node.js 18+ (20.x recommended)
- .NET SDK 8.0 (global.json targets 8.0; roll-forward is enabled if you only have a newer SDK)

## Quickstart
1) Install JS deps: `npm install --prefix client`
2) Start the API: `dotnet run --project server/MailClient.Server.csproj --urls http://0.0.0.0:5000`
3) Start the web app: `npm run dev --prefix client -- --host --port 5173` (uses `/api` and proxies to port 5000 in dev)
4) Open http://localhost:5173 (Swagger is at http://localhost:5000/swagger).

## Common scripts
- Client dev server: `npm run dev --prefix client`
- Client build: `npm run build --prefix client`
- Client lint: `npm run lint --prefix client`
- Client tests: `npm test --prefix client`
- API build: `dotnet build server/MailClient.Server.csproj`
- API tests: `dotnet test tests/MailClient.Server.Tests/MailClient.Server.Tests.csproj`

## Ports and env vars
- API HTTP: `5000` (set `ASPNETCORE_URLS=http://0.0.0.0:5000` to change)
- Web dev server: `5173` (override with `npm run dev -- --port <port>`)
- CORS origins: `AllowedOrigins` in `server/appsettings.*.json` (required in Production, defaults to localhost only in Development)
- API base URL for the web app: `VITE_API_BASE` (required for production builds; dev uses `/api` + Vite proxy)
- ASP.NET environment: `ASPNETCORE_ENVIRONMENT` (defaults to `Development`)
- Optional secret hardening: `Secrets__MasterPassword` (see below)
- Gmail OAuth: set `GoogleOAuth:ClientId`, `GoogleOAuth:ClientSecret`, and `GoogleOAuth:RedirectUri` (e.g. `http://localhost:5000/api/oauth/google/callback`)

## Docker Compose (optional)
`docker-compose up` will start the API on port 5000 and the Vite dev server on port 5173 using SDK images. Containers install dependencies on first run and live-reload when files change. Compose passes `AllowedOrigins` for dev and points the web app at `http://api:5000/api`.

## Config & secrets
- **AllowedOrigins**: Set per environment (`appsettings.Development.json` ships with `http://localhost:5173`). In `Production`, the app fails fast if the list is empty.
- **API base (client)**: `VITE_API_BASE` must be provided for production builds so the client never ships with a localhost URL. In dev, the client defaults to `/api` and Vite proxies to port 5000.
- **Data protection keys**: Stored under `server/Data/keys` by default. Key lifetime defaults to 90 days (30 days in Development). Back up the key folder securely if you need to decrypt secrets after redeploys.
- **Master password (optional)**: Set `Secrets__MasterPassword` to wrap account secrets with a user-supplied password (AES-GCM, PBKDF2). Existing secrets without the `mp1:` prefix continue to decrypt via data protection; new secrets use the master password. Do **not** log or persist the password; consider supplying it only via environment variables or secret stores.

## CI
GitHub Actions build the API, run API tests, and build/lint/test the client on pushes and PRs. Keep `npm run build`, `npm run test`, and `dotnet test` green to stay compatible with main.

## Troubleshooting
- Missing .NET 8 SDK: install from https://dotnet.microsoft.com/download/dotnet/8.0
- Port conflicts: change `ASPNETCORE_URLS` / `VITE_API_BASE` / `--port` flags to free ports.
- SSL/certs: compose uses HTTP for local dev; configure dev certs separately if needed.
