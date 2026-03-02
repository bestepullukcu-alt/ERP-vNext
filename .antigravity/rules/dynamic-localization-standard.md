---
description: "MOD-0013 Dynamic Localization Standard — Ensures all UI text is resource-driven with full multi-language sync"
---

# Dynamic-Localization-Standard (MOD-0013)

## Core Principles

### 1. No Static Strings
- **NEVER** write hardcoded text in `.cshtml`, `.html`, or `.js` files.
- All user-facing strings MUST come from `.resx` files via `@SharedLocalizer["Key"]` or `@Localizer["Key"]`.
- JS-side strings MUST be read from the `window.L10n` bridge object.

### 2. Discovery Rule — Scan Before Adding
When adding a new localization key:
1. Run this command to discover all existing language files:
   ```bash
   find frontend/Diten.Web/Resources -name "SharedResource.*.resx" -type f
   ```
2. Note every language code found (e.g., `en`, `tr`, `es`, `ru`, `uk`, `ka`, `kk`, `uz`).
3. The new key MUST be added to **every single file** discovered — no exceptions.

### 3. Full Sync — Real Translations Only
- Every `.resx` file MUST contain the translation in its **own language**.
- **NEVER** copy-paste the English value into non-English files as a placeholder.
- If you are unsure of a translation, use the closest accurate translation available.
- Translation quality table example:

| Key | en | tr | es | ru |
|---|---|---|---|---|
| Save | Save | Kaydet | Guardar | Сохранить |
| Cancel | Cancel | İptal | Cancelar | Отмена |
| Delete | Delete | Sil | Eliminar | Удалить |

### 4. Bridge System — Razor → JavaScript
For any text needed in `.js` files, use the L10n Bridge pattern:

**In the Razor View (`.cshtml`):**
```html
@section Scripts {
    <script>
        window.L10n = window.L10n || {};
        window.L10n.MyNewKey = @Json.Serialize(SharedLocalizer["MyNewKey"].Value);
    </script>
    <script src="~/assets/js/my-page.js"></script>
}
```

**In the JavaScript file (`.js`):**
```javascript
var label = (window.L10n && window.L10n.MyNewKey) || 'Fallback English';
```

> **Security & Stability Rule:** ALWAYS use `@Json.Serialize(...)` for JavaScript strings. 
> NEVER use `'@Html.Raw(...)'` because if the translation contains a single quote (e.g., Uzbek `o'zbekcha` or French `l'exemple`), it will terminate the JS string early and cause a Syntax Error, breaking the entire page logic.

> **Rule:** The `window.L10n` script block MUST appear BEFORE the page-specific `.js` file in the `@section Scripts` block.

### 5. XML Safety in `.resx` Files
- Always escape special XML characters in `<value>` tags:
  - `&` → `&amp;`
  - `<` → `&lt;`
  - `>` → `&gt;`
  - `"` → `&quot;`
- After adding keys, always run `dotnet build` to verify no XML parse errors.

### 6. Rebuild Protocol
After modifying ANY `.resx` file:
1. Kill running processes: `lsof -ti :5000,5001,5050 | xargs kill -9`
2. Delete cached DLLs: `rm -rf frontend/Diten.Web/bin frontend/Diten.Web/obj`
3. Rebuild and restart: `./run_all.sh`
4. Hard refresh browser: `Cmd+Shift+R`

### 7. Namespace Alignment
- The `.csproj` file MUST have `<RootNamespace>Diten.Web</RootNamespace>` and `<AssemblyName>Diten.Web</AssemblyName>`.
- The marker class `SharedResource.cs` MUST be in the `Diten.Web` namespace.
- Resource files MUST be in the `Resources/` folder.
- This alignment ensures `IHtmlLocalizer<Diten.Web.SharedResource>` correctly resolves keys from the compiled satellite DLLs.

## File Locations

| Component | Path |
|---|---|
| Shared Resources | `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` |
| Page Resources | `frontend/Diten.Web/Resources/Views/MDM/LegalEntities.{lang}.resx` |
| Marker Class | `frontend/Diten.Web/SharedResource.cs` |
| Program Config | `frontend/Diten.Web/Program.cs` (RequestLocalizationOptions) |
| Global Notification | `frontend/Diten.Web/Views/Shared/_GlobalNotification.cshtml` |
| Global Confirmation | `frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml` |

## Supported Languages

| Code | Language |
|---|---|
| `en` | English (Default) |
| `tr` | Türkçe |
| `es` | Español |
| `ru` | Русский |
| `uk` | Українська |
| `ka` | ქართული |
| `kk` | Қазақша |
| `uz` | O'zbek |

## L10n Bridge Coverage

| Katman | Layout | Durum |
|---|---|---|
| `_LayoutBackbone.cshtml` | Modern | ✅ Tüm metinler `@SharedLocalizer` ile |
| `_Layout.cshtml` | Legacy | ❌ Frozen — Hardcoded metinler var ama dokunulmaz |
| MDM JS dosyaları | Modern | ✅ `window.L10n` bridge aktif |
| Archive JS dosyaları | Legacy | ❌ Frozen |

## Registered SharedResource Keys (31+)

Aşağıdaki anahtarlar tüm 8 dilde senkronize ve çevrilmiştir:

**Global:** MDM, Title, TaxNumber, SearchFilter, Status, Actions, Active, Passive, Unknown, Export, Print, Search, ViewDetails, Filter, Reset

**CRUD:** Save, Cancel, Delete, BackToList, Saving

**Notifications:** Success, Error, AreYouSure, ErrorOccurred, RecordCreated, RecordDeleted, RecordSaved

**Confirmation Modal:** DeleteConfirmationTitle, DeleteConfirmationSubText, DeleteConfirmationYesBtn

**Controller:** FailedToLoadData, GatewayError

**Layout (Backbone):** LegalEntities, Light, Dark, Admin, MyProfile, Settings, LogOut

