# Diten.WebUI (frontend)

## Why `ERR_CONNECTION_REFUSED` on localhost?

The browser can only open the app **after** you start the ASP.NET process.  
`http://localhost:5001/...` works only while **`dotnet run`** is running for this project.

## Run the Web UI

The repo folder must be **`DITEN_NEW`** (e.g. `~/Documents/DITEN_NEW`).  
If `cd src/Frontend/Diten.WebUI` fails, you are **not** in that folder — `cd` there first.

**Demand & Ideas** (`/api/v1/...` and `GET /health`) are **hosted inside this project**. You do **not** need to run **Diten.WebAPI** for Capture or the list, as long as **MongoDB** is running and `ConnectionStrings:MongoDb` + `DatabaseName` in `appsettings*.json` match your database.

From the repo root:

```bash
cd ~/Documents/DITEN_NEW/src/Frontend/Diten.WebUI
dotnet run
```

Or from any directory:

```bash
dotnet run --project ~/Documents/DITEN_NEW/src/Frontend/Diten.WebUI/Diten.WebUI.csproj
```

Or with an explicit profile (see `Properties/launchSettings.json`):

```bash
dotnet run --launch-profile http    # http://localhost:5001
dotnet run --launch-profile https   # https://localhost:5002 and http://localhost:5001
```

Wait until the console shows something like:

`Now listening on: http://localhost:5001`

Then open:

| Page | URL |
|------|-----|
| Demand & Ideas list | http://localhost:5001/DemandIdeas |
| Capture (new) | http://localhost:5001/DemandIdeas/Capture |
| Capture (HTTPS profile) | https://localhost:5002/DemandIdeas/Capture |

After the first save, the address bar includes `?id=…` — use **Copy page link** on the capture page to share that URL.  
If the port is busy, .NET may choose another port — **use the URL printed in the terminal**, not a guess.

## API for Capture (same host)

Demand & Ideas **Capture** and the **Demand & Ideas list** call **`/api/v1/...` on the same origin** as the Web UI (e.g. `http://localhost:5001/api/v1/demand-ideas`). Those endpoints are implemented **in this app** (`Controllers/Api`).

**Optional:** set `ApiSettings:PublicApiUrl` to a full API base URL (e.g. `https://api.example.com`) if you want the **browser** to call a **different** API host (CORS must allow the Web UI origin). Leave it **empty** for the default (same-origin API).

If the list or Capture shows **“Demand API unavailable”**:

1. Ensure **MongoDB** is running (`mongodb://localhost:27017` by default).
2. Check **`ConnectionStrings:MongoDb`** and **`DatabaseName`** in `appsettings.json` / `appsettings.Development.json` (defaults align with **Diten.WebAPI**: `DitenEnterpriseDb`).
3. Restart **`dotnet run`** for **Diten.WebUI** after config changes.
4. Verify **`GET /health`** returns **200** (see below).

**Separate WebAPI:** you can still run **`src/Backend/Diten.WebAPI`** for a standalone API process; it is **not** required when using the embedded API in WebUI.

## MongoDB health check

This app exposes **`GET /health`**: MongoDB `ping` on the configured database — **200** with `{ "status": "Healthy", "databaseName": "..." }` or **503** if MongoDB is unreachable.

| URL |
|-----|
| `http://localhost:5001/health` (port from your Web UI console) |

Requires **MongoDB** on the connection string in `appsettings.json` (default `mongodb://localhost:27017`) and **`DatabaseName`** (default `DitenEnterpriseDb`).

## Management & Governance shell boundaries

The `Management & Governance` frontend (`/management-governance/...`) is intentionally a thin orchestration layer.

- Domain shell owns: cross-subdomain navigation, roll-up KPIs, governance queues, cadence, risk signals, saved views, and drill-through links.
- Subdomain shell owns: subdomain-level module catalog, aggregation widgets, queue summary, dependency visibility, alerts, and upcoming reviews.
- Module workspaces own: transactional business workflows and system-of-record CRUD (not duplicated in domain/subdomain shells).
- Future API integration seam: `Services/ManagementGovernance/IManagementGovernanceFrontendAdapter.cs` and `MockManagementGovernanceFrontendAdapter.cs` are the contract boundary for replacing mock data with real backend endpoints.
