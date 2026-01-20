# Mail Client Monorepo

Client app (Vite + React) lives in `client/`. ASP.NET Core backend lives in `server/`.

## Prerequisites
- Node.js 18+ (20.x recommended)
- .NET SDK 8.0 (global.json targets 8.0; roll-forward is enabled if you only have a newer SDK)

## Quickstart
1) Install JS deps: `npm install --prefix client`
2) Start the API: `dotnet run --project server/MailClient.Server.csproj --urls http://0.0.0.0:5000`
3) Start the web app: `npm run dev --prefix client -- --host --port 5173`
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
- CORS origins: `AllowedOrigins` in `server/appsettings.json`
- API base URL for the web app: `VITE_API_BASE` (defaults to `http://localhost:5000/api`)
- ASP.NET environment: `ASPNETCORE_ENVIRONMENT` (defaults to `Development`)

## Docker Compose (optional)
`docker-compose up` will start the API on port 5000 and the Vite dev server on port 5173 using SDK images. Containers install dependencies on first run and live-reload when files change.

## CI
GitHub Actions build the API, run API tests, and build/lint/test the client on pushes and PRs. Keep `npm run build`, `npm run test`, and `dotnet test` green to stay compatible with main.

## Troubleshooting
- Missing .NET 8 SDK: install from https://dotnet.microsoft.com/download/dotnet/8.0
- Port conflicts: change `ASPNETCORE_URLS` / `VITE_API_BASE` / `--port` flags to free ports.
- SSL/certs: compose uses HTTP for local dev; configure dev certs separately if needed.
