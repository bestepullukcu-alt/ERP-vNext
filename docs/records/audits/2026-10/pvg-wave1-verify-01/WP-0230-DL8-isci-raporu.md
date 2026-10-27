# WP-0230-DL8 işçi raporu (Faz 1 + Faz 2)

> Kaynak: bağlı işçi oturumu `pvg-dl8` (local_c90378e1-c2f4-49b0-8ef4-55af4feb0e3e), 2026-10-23. Bu oturum raporunu dosyaya
> yazmadı. Aşağıdaki iki bölüm, oturum dökümündeki son yanıtlardan PVG Control Tower tarafından aynen aktarılmıştır.
> Bağımsız CT ölçümü ayrı kayıttadır: `VERIFY.md`.

## WP-0230-DL8 Faz 1 raporu (PVG-WP-0230-DL8-P v1.0)

**Agent Verdict:** Faz 1 PASS, E2. Bu kapanış değildir (K13); kabul CT'nindir. Faz 2 başlamadı, "DL8 canlı deneme onaylı" mesajı bekleniyor.

**Branch / HEAD:** `feature/pvg/wp-0230-dl8` @ `95563dae14c441e4f9ed5e86874c46fc0f649e06`, beklenen HEAD ile aynı. Worktree: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-wp0230-dl8`.

**Worktree status:** Başlangıçta temizdi. Sonda `git status --porcelain` boş ve `git diff --check` çıkış 0.

**Changed files:** Yok. Sabotaj için `ocelot.json` geçici değişti, geri alındı (`git diff` çıktısı 0 bayt).

**Golden/Contract flow:** Madde 1 sayımı 4 satır verdi, paket satır 1210–1214 ve 1347 (BG-16) ile birebir:
```
4
/api/pv-case-intake-triage ['GET', 'POST'] /api/v1/pv-case-intake-triage [{'Host': 'localhost', 'Port': 5011}]
/api/pv-case-intake-triage/{intakeDraftId} ['GET', 'PUT'] /api/v1/pv-case-intake-triage/{intakeDraftId} [{'Host': 'localhost', 'Port': 5011}]
/api/pv-case-intake-triage/{intakeDraftId}/triage ['POST'] /api/v1/pv-case-intake-triage/{intakeDraftId}/triage [{'Host': 'localhost', 'Port': 5011}]
/api/pv-case-intake-triage/{intakeDraftId}/route ['POST'] /api/v1/pv-case-intake-triage/{intakeDraftId}/route [{'Host': 'localhost', 'Port': 5011}]
```
- Girdiler `ocelot.json:2856–2917` aralığında (başlangıç `2865`, `2881`, `2897`, `2912`).
- `pv-case-intake` geçen başka route yok; `pvg` ya da `5011` geçen route da yalnız bu dört.
- Dört girdide `AuthenticationOptions` yok. Anahtar kümesi: `DownstreamHostAndPorts`, `DownstreamPathTemplate`, `DownstreamScheme`, `UpstreamHttpMethod`, `UpstreamPathTemplate`, `UpstreamScheme`.
- PATCH, DELETE, OPTIONS, `{everything}`, archive, void, export, bulk-delete yok (testler de bunu ölçüyor).

**Sub-flows / Failure paths:** Faz 2'ye bırakıldı; Faz 1'de canlı süreç yok.

**Tests:**
- `dotnet test gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj -c Debug --logger "trx;LogFileName=gateway.trx"`: Başarısız 0, Başarılı 313, Atlanan 0, Toplam 313.
- `PvgCaseIntakeTriage_RouteFamilyMapsOnlyApprovedTemplates` ve `PvgCaseIntakeTriage_ForbiddenRoutesAreAbsent` `Passed` (`gateway.trx`).
- `python3 scripts/known_failures.py check gateway/Diten.ApiGateway.Tests/TestResults/gateway.trx --suite gateway`: çıkış 0. Özet BİLİNEN=0, YENİ=0, KOŞMADI=0, BİLİNMİYOR=0.
- **Sabotaj:** `/api/pv-case-intake-triage/{intakeDraftId}` girdisinin `UpstreamHttpMethod` listesine `"DELETE"` eklendi. `--filter "FullyQualifiedName~PvgCaseIntakeTriage"` ile 2 test koşuldu, ikisi de kırmızı:
  - `Diten.ApiGateway.Tests.OcelotConfigurationTests.PvgCaseIntakeTriage_ForbiddenRoutesAreAbsent`
  - `Diten.ApiGateway.Tests.OcelotConfigurationTests.PvgCaseIntakeTriage_RouteFamilyMapsOnlyApprovedTemplates` (`Expected: ["GET", "PUT"]`, `Actual: ["DELETE", "GET", "PUT"]`)
- **Geri alma:** `git diff gateway/Diten.ApiGateway/ocelot.json` boş; aynı filtreyle iki test yeşil (Başarılı 2, Başarısız 0).

**Persistence evidence:** N/A. Bu iş yalnız Gateway route yapılandırmasıdır.

**Security/RBAC/Tenant evidence:** Route düzeyinde `AuthenticationOptions` eklenmedi (paket satır 1215–1216, depo normu). İzin denetimi serviste (DL-4, DL-6). Gateway'in `X-Tenant-Id` ve `X-Diten-*` davranışına dokunulmadı.

**Audit/Evidence, Observability:** Faz 1 için N/A. E2 kanıtı: `gateway/Diten.ApiGateway.Tests/TestResults/gateway.trx` ve `sabotage.trx`, ikisi de gitignored.

**Migration/Rollback:** Yok. Değişiklik olmadığı için geri alınacak bir şey de yok.

**Decisions:** Beklenen sonuç gerçekleşti: ölçüm sapma göstermedi, bu yüzden düzeltme yapılmadı. Port 5011 değişmedi (OD-PVG-7).

**Blockers:** Yok.

**Known gaps (Faz 2'ye kalan):** BG-16'nın "Gateway üzerinden smoke" yarısı (E3) henüz ölçülmedi. Bilgi amaçlı ölçümde 5000 ve 5011'de dinleyen süreç yoktu (`lsof` boş). Faz 2 başlarken bu ölçüm yeniden yapılacak.

**Out-of-scope changes:** Yok. `gateway/Diten.ApiGateway.Tests/**` değişmedi. Commit, push, PR, dal değiştirme, stash ve reset yapılmadı.

## WP-0230-DL8 Faz 2 raporu (canlı smoke, E3)

**Agent Verdict:** Faz 2 PASS, E3. Gateway servisin kendi sonucunu döndürdü. Bu kapanış değildir (K13); kabul CT'nindir.

**Branch / HEAD:** `feature/pvg/wp-0230-dl8` @ `95563dae14c441e4f9ed5e86874c46fc0f649e06`, değişmedi.

**Worktree status:** `git status --porcelain` boş, `git diff --check` temiz. Faz 2'de dosya değiştirilmedi. Commit, push, PR, dal değiştirme ve stash yok.

**Changed files:** Yok.

**Golden/Contract flow:**
- **Ön ölçüm:** CT onayından sonra 5000 ve 5011'de dinleyen süreç yoktu (`lsof -nP -iTCP:5000 -sTCP:LISTEN` ve 5011 için aynısı, ikisi de boş). MongoDB 27017'de zaten çalışıyordu (PID 898, bana ait değil, dokunmadım).
- **PvgService:** `dotnet build ... Diten.PvgService.Api.csproj -c Debug` 0 uyarı, 0 hata. Sonra bu worktree'nin `bin/Debug/net8.0/Diten.PvgService.Api.dll` dosyası `ASPNETCORE_ENVIRONMENT=Development` ile başlatıldı. Log: `Now listening on: http://127.0.0.1:5011`, `Hosting environment: Development`.
- **Gateway:** `bin/Debug/net8.0/Diten.ApiGateway.dll`, `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://localhost:5000`, çalışma dizini `gateway/Diten.ApiGateway`. Açılış sır ya da kimlik bilgisi istemedi. Tek uyarı `Observability:Correlation:SigningKey is not set` (SupplyChain adımlarıyla ilgili, bu işi etkilemez).
- **Başlatılan PID'ler:**
  - PvgService: PID 93102, başlangıç Fri Oct 23 12:35:38 2026
  - Gateway: PID 93211, başlangıç Fri Oct 23 12:35:49 2026
- **İstek 1:** `curl -s -i http://localhost:5000/api/pv-case-intake-triage`
  ```
  HTTP/1.1 409 Conflict
  Content-Type: application/json; charset=utf-8
  Server: Kestrel
  X-Correlation-Id: 46c2d598-019c-4456-b8cf-e0898c573036

  {"outcome":"Blocked","reasonCode":"PVG_TENANT_CONTEXT_REQUIRED","validationReasonCodes":[],"intakeDraftId":null,"items":[]}
  ```
- **İstek 2:** `curl -s -i http://127.0.0.1:5011/api/v1/pv-case-intake-triage`
  ```
  HTTP/1.1 409 Conflict
  Content-Type: application/json; charset=utf-8
  Server: Kestrel

  {"outcome":"Blocked","reasonCode":"PVG_TENANT_CONTEXT_REQUIRED","validationReasonCodes":[],"intakeDraftId":null,"items":[]}
  ```
- **Karşılaştırma:** Durum satırı (409 Conflict) ve gövde birebir aynı. Tek fark Gateway'in eklediği `X-Correlation-Id` başlığı. Gövde servisin PVG neden kodunu (`PVG_TENANT_CONTEXT_REQUIRED`) taşıyor; Gateway 404, 502 ya da kendi hata sayfasını dönmedi.
- **Gateway logu:** `409 Conflict status code of request URI: http://localhost:5011/api/v1/pv-case-intake-triage` ve `HTTP "GET" "/api/pv-case-intake-triage" responded 409 in 388.7758 ms`. İstek `/api/pv-case-intake-triage` → `localhost:5011/api/v1/pv-case-intake-triage` olarak eşlendi.

**Sub-flows / Failure paths:** Yalnız GET gönderildi (gövdesiz, kişi verisiz, token yok). Sonuç, kiracı bağlamı olmayan isteğin serviste fail-closed bloke edildiğini ve bunun Gateway'den aynen geçtiğini gösteriyor. Başarılı (200) liste yolu ve diğer üç route Faz 2'de çağrılmadı (kapsam: tek GET smoke).

**Tests:** Faz 2'de test koşulmadı. Faz 1 sonuçları geçerli (313/313, `known_failures` çıkış 0, sabotajda iki test kırmızı).

**Persistence evidence:** N/A. Mongo'ya yazma yapılmadı, yalnız GET.

**Security/RBAC/Tenant evidence:** Kiracı başlığı ve token gönderilmedi. Servis `PVG_TENANT_CONTEXT_REQUIRED` ile 409 döndü; Gateway bunu değiştirmedi. Token ya da sır rapora yazılmadı.

**Audit/Evidence, Observability:** Gateway logu `scratchpad/gw.log`, servis logu `scratchpad/pvg.log` (`/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext--claude-worktrees-pvg-wp0230-dl8/3eee68a4-964d-4889-b5bb-393029024e55/scratchpad/`). Gateway yanıtı `X-Correlation-Id` taşıyor.

**Migration/Rollback:** Yok.

**Decisions:** Smoke için Auth, MDM ve Platform başlatılmadı. Gateway bunlar olmadan açıldı ve bu route'u servise iletti. İsteğin Auth gerektirmeden servise ulaşması, bu route ailesinde Gateway düzeyinde ayrı bir kimlik doğrulama kapısı olmadığı anlamına gelir. Bu, paketin "route düzeyinde `AuthenticationOptions` yok" kararıyla uyumlu.

**Blockers:** Yok.

**Known gaps:**
- Smoke yalnız kiracısız GET'i kanıtlıyor. Kiracı ve token ile uçtan uca akış (Auth, Mongo ve tenant girişi gerekir) bu işin kapsamı değil; paketteki tarayıcı smoke maddesi hâlen çalıştırılmadı.
- BG-16 kutusu işaretsiz. Kutuyu işaretlemek benim işim değil; kabul REP-001 kaydıyla CT'dedir.
- Bash aracında üç kez `cd <dizin>` oturumun çalışma dizinini değiştirdi (memory'deki bilinen davranış). Sonraki komutlarda worktree köküne dönülerek çalışıldı; depo durumuna etkisi yok.

**Out-of-scope changes:** Yok.

**Temizlik:** `kill -TERM 93211 93102` yalnız kendi PID'lerime uygulandı (önce `ps` ile PID, başlangıç zamanı ve komut satırı doğrulandı). `ps -p` ikisinin de bittiğini gösterdi. `lsof` 5000 ve 5011'in boş olduğunu gösterdi.
