# Demand & Ideas Capture (API-driven)

- **URL:** `/DemandIdeas/Capture` (create) or `/DemandIdeas/Capture?id={mongoId}` (edit)
- **Views:** `Capture.cshtml` + `_CaptureApiFormPartial.cshtml`
- **Script:** `wwwroot/assets/js/pages/demand-ideas/demandIdeaCapture.js`
- **Styles:** `wwwroot/assets/css/pages/demand-idea-capture.css`

## Configuration

The **Demand Ideas API** (`/api/v1/...`) runs **inside Diten.WebUI** (`Controllers/Api`). Ensure **MongoDB** is up and **`ConnectionStrings:MongoDb`** + **`DatabaseName`** in `appsettings*.json` are correct. You do **not** need a separate **Diten.WebAPI** process for Capture unless you point **`ApiSettings:PublicApiUrl`** at an external API.

The page root includes `data-api-base` and `data-initial-id` for the client script.

After pulling changes, run **`dotnet build`** — Razor views are compiled into the DLL.
