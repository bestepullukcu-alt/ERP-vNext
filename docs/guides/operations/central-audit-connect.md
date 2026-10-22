# Merkezi denetime bağlanma — BL-501 ortak ileticisi

> Kim için: bir servisin yazma komutlarını Platform'un **merkezi** denetim günlüğüne (`audit_events`) bağlayacak geliştirici.
> Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) §5 yol **b**, K4 kararı. İlk bağlanan servis: InventoryService
> (WP-MVP1-AUDIT1). Ölçümler 2026-10-14, `feature/platform/bl-501-central-audit`.

## Ne var, ne yapar

| parça | proje | ne yapar |
|---|---|---|
| `ICentralAuditDeclaration` + `CentralAuditMetadata(Category, Operation, EntityType, SourceModule)` | `Diten.BuildingBlocks.CentralAudit.Abstractions` (bağımlılıksız) | Komutun işareti. Category / Operation = Platform `AuditCategory` / `AuditOperation` **adları**. |
| `ICentralAuditOutsideTransaction` | aynı | `RequireEnlistment` servisinde, kendi Mongo işlemi olmayan komut (başka modüle HTTP ile yazan). |
| `CentralAuditBehavior` | `Diten.BuildingBlocks.CentralAudit` | İşaretli komut **başarılı** biterse 1 olay; başarısızsa 0. Gövde kopyalanmaz. |
| `central_audit_outbox` | servisin KENDİ veritabanı | Kalıcı outbox. Satır asla silinmez (Delivered / Rejected / Abandoned kalır). |
| `CentralAuditTransaction.EnlistAsync(s, ct: ct)` | aynı | Olayı komutun Mongo işlemine yazar: veriyle birlikte commit olur ya da hiç (K2). |
| `CentralAuditDispatcher` | aynı | `POST /api/internal/audit/append`, `X-Internal-Api-Key`, `eventId` ile tekil; geri çekilmeli yeniden deneme. |
| `/health/central-audit` | servis eşler | Adres/anahtar yoksa, bekleyen satır eskiyse ya da Platform reddettiyse **Degraded**. |

"Başarılı" = istisna yok **ve** yanıtın `bool IsSuccessful` özelliği varsa `true`. Bu özellik yoksa yalnız istisna bakılır.

## Adımlar

1. **Paket başvurusu.** Application → `Diten.BuildingBlocks.CentralAudit.Abstractions`; Api ve Persistence → `Diten.BuildingBlocks.CentralAudit`.
2. **Kapsam bağdaştırıcısı** (Api, birkaç satır): `ICentralAuditScope` — kiracı, şirket, kullanıcı **çözülmüş kapsamdan** (imzalı JWT),
   ad ve aktör türü token'dan, korelasyon. Örnek: `services/Diten.InventoryService/src/Diten.InventoryService.Api/CentralAudit/InventoryCentralAuditScope.cs`.
3. **`Program.cs`'e tek satır** (MediatR kaydından SONRA) + sağlık ucu:
   ```csharp
   builder.Services.AddCentralAudit<MyCentralAuditScope>(builder.Configuration, "Diten.MyService", options => options.RequireEnlistment = true);
   app.MapHealthChecks("/health/central-audit").AllowAnonymous();
   ```
   `RequireEnlistment = true` yalnız servisin **her** yazması `EnlistAsync` çağıran bir Mongo işlemindeyse. Değilse seçeneği verme:
   kayıt komuttan hemen sonra yazılır (⚠ komut ile outbox yazımı arasında çökme = kayıp; log hatası yazılır).
4. **İşaretler.** Her yazma komutu `ICentralAuditDeclaration` taşır; `CentralAuditEntityId(response)` varlık kimliğini verir
   (oluşturma komutu yanıttan, diğerleri komuttan). Örnek: `InventoryCentralAudit` + `RecallCommands.cs`.
5. **İşlem içi kayıt.** Her `WithTransactionAsync` geri çağrısına bir satır: `await CentralAuditTransaction.EnlistAsync(s, ct: ct);`
   ve bunu ölçen bir koruma testi (örnek: Inventory `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in_enlists…`).
6. **Defter.** `tests/architecture/audit-ledger/<Servis>.md`: `| <servis>-merkezi-iletim | b | işaret | ICentralAuditDeclaration |`.
   Kabul edilmiş iz `AuditTrailStandardTests.AcceptedTrails`'e, sayılar `PinnedCounts`'a yazılır — **ikisi de Control Tower kararı**.
   Borç satırı çıkarılır, sayı düşürülür. Deftere `- Ad` satırı EKLENMEZ.
7. **Yapılandırma.** Aynı iki anahtar: `PlatformRegistration:BaseUrl`, `PlatformRegistration:InternalApiKey` (Platform'da
   `AuthService:InternalApiKey` ile aynı değer). İnce ayar `CentralAudit:*` (PollInterval, BatchSize, BaseBackoff, MaxBackoff …).
8. **Kanıt.** `--filter AuditTrailStandard`; kendi portlarında bir komut çalıştır → `GET /api/platform/audit/events?entityType=…`.

**Yapma:** ileticiyi kopyalama (mimari testi `CentralAuditForwarder_LivesOnlyInBuildingBlocks…` kırmızı), ürün içi geçmişi kaldırma.

## Servis notları

### MVP-3 · ManufacturingService (4 BOM komutu)

- Hazırlık commit'i **82ebedeb4** idi; **730a7fa1f** ("defer BL-501 audit connection after MVPs") onu geri aldı. Bu dalda bildirim
  YOK; `git show 82ebedeb4 -- services/Diten.ManufacturingService` ile geri getirilir. Üyeler birebir aynı: yalnız `using`
  `Diten.BuildingBlocks.CentralAudit` olur; MVP-3'ün `CentralAuditMetadata.Bom(...)` yardımcısı servis tarafında ayrı bir statik
  sınıfa taşınır (ortak kayıtta servis sabiti yok).
- İşlem: tek işlem sahibi `BomHistoryJournal.CommitAsync` (BOM + geçmiş tek işlemde) → `EnlistAsync` satırı oraya; dört komutun
  hepsi oradan yazıyorsa `RequireEnlistment = true`.
- Defter: `Diten.ManufacturingService.md` içindeki "BL-501 bekliyor" not satırları kalkar; `bom-surum-gecmisi` (c) izi kalır.
- Yanıt zarfında `IsSuccessful` var (`Application/Common/Response.cs`).

### MVP-2 · ProcurementService (35 komut, hepsi `## Bilinen borç`)

- **Hiç Mongo işlemi yok** (0 `WithTransactionAsync` / `StartTransaction`). `RequireEnlistment` VERİLMEZ; kayıt komuttan sonra
  yazılır (en iyi çaba, kayıp riski kanıtta yazılır). K2 sınıfı (GxP) bir komut varsa önce işlem kurulmalı ya da K2 borcu → CT.
- Kiracı bağlamı `Application/Common/ITenantContext.cs`; zarf `IsSuccessful` taşıyor; `IMongoDatabase` scoped.
- Bağlandığında 35 borç satırı çıkar, `PinnedCounts["Diten.ProcurementService"].Debt` 35 → 0.

### MVP-6 · SupplyChainService (17 komut, **defteri yok**)

- Defter dosyası yok (mimari testi bugün "17 yazma komutu var, defteri yok" diye kırmızı). Bağlantı defteri de getirir.
- 15 `WithTransactionAsync` noktası (CapacityPlans, Carriers, Claims, Loads, Returns, SandopPlans, Shipments; `*Schema.cs`
  dosyalarındakiler kurulum işlemleri — komut değil). Hepsi kaydoluyorsa `RequireEnlistment = true`.
- ⚠ Zarflarda `IsSuccessful` YOK: `Response<T>(Data, StatusCode, …)` ve `SandopResult(Status, …)`. Bağlanmadan önce ikisine
  `public bool IsSuccessful => StatusCode is >= 200 and < 300;` (Sandop: `Status`) eklenmeli — yoksa 4xx yanıtlar başarılı sayılır.
- Tek bir kiracı bağlamı yok: özellik başına `*RequestContext` (+ `RequestContext.Scope : ShipmentScope(TenantId, LegalEntityId, ActorId)`).
  Kapsam bağdaştırıcısı bunları tek yerde okumalı ya da servis önce ortak bir kapsam türü edinmeli.
- `IMongoDatabase` singleton — paket için sorun değil.

## Bağlan — prompt taslakları (CT ilgili ekibe iletir)

### MVP-3

```
Work Package: WP-MVP3-AUDIT-CONNECT — ManufacturingService'i BL-501 ortak merkezi denetim ileticisine bağla.
Rapor dili Türkçe. İlk iş: AGENTS.md §6.1, AUD-001 (§5 K4), docs/guides/operations/central-audit-connect.md.
Dal: main'den güncel bir feature dalı; YALITILMIŞ worktree. main'e commit yok.
1) 82ebedeb4'teki bildirimleri geri getir (730a7fa1f geri almıştı); işaret artık Diten.BuildingBlocks.CentralAudit(.Abstractions):
   yalnız using değişir, Bom(...) yardımcısını servis içi statik sınıfa taşı. 4 komut.
2) Program.cs: AddCentralAudit<ManufacturingCentralAuditScope>(…, "Diten.ManufacturingService", o => o.RequireEnlistment = true)
   + MapHealthChecks("/health/central-audit"). Bağdaştırıcı ITenantContext'ten.
3) BomHistoryJournal.CommitAsync işlem geri çağrısına CentralAuditTransaction.EnlistAsync(s, ct: ct) + koruma testi.
4) Defter: "BL-501 bekliyor" satırlarını kaldır; manufacturing-merkezi-iletim (b) izini ekle. AcceptedTrails satırı CT kararı —
   CT onayı yoksa DUR ve yaz.
5) Testler: başarılı → 1 olay; başarısız → 0; geri alınan BOM işlemi → satır yok; kiracı doğru. Sabotajlar: işaret kaldır →
   mimari kırmızı; EnlistAsync satırını kaldır → işlem testi kırmızı. Push öncesi origin/main merge (rebase yok), suite'ler.
```

### MVP-2

```
Work Package: WP-MVP2-AUDIT-CONNECT — ProcurementService'i BL-501 ortak merkezi denetim ileticisine bağla.
Rapor dili Türkçe. İlk iş: AGENTS.md §6.1, AUD-001 (§4.4 K2, §5 K4), docs/guides/operations/central-audit-connect.md.
Dal: main'den güncel feature dalı; YALITILMIŞ worktree.
Ölçüm (2026-10-14): 35 yazma komutu, hepsi borçta; servis Mongo işlemi KULLANMIYOR.
1) Komut envanteri: her komutun GxP / K2 sınıfı mı (tedarikçi onayı, kalite) — tablo kanıtta.
2) AddCentralAudit<ProcurementCentralAuditScope>(…, "Diten.ProcurementService") — RequireEnlistment VERME (işlem yok):
   kayıt komuttan sonra yazılır; "komut ile outbox arası çökme" riskini kanıtta yaz.
3) 35 komuta ICentralAuditDeclaration (kategori/işlem Platform enum adları, kaynak modül = manifest kodu).
4) Defter: borç satırlarını çıkar, PinnedCounts 35 → 0; izi ekle (AcceptedTrails = CT kararı). K2 sınıfı komut varsa ve işlem
   yoksa: K2 borcu satırı CT kararı ister → DUR ve yaz.
5) Testler + sabotajlar (işaret kaldır → kırmızı; davranış başarısızda yazsın → kırmızı). Push öncesi origin/main merge.
```

### MVP-6

```
Work Package: WP-MVP6-AUDIT-CONNECT — SupplyChainService'i BL-501 ortak merkezi denetim ileticisine bağla ve defterini aç.
Rapor dili Türkçe. İlk iş: AGENTS.md §6.1, AUD-001, docs/guides/operations/central-audit-connect.md.
Dal: main'den güncel feature dalı; YALITILMIŞ worktree.
Ölçüm (2026-10-14): 17 yazma komutu, defter YOK (mimari kırmızı); 15 WithTransactionAsync noktası; zarflarda IsSuccessful YOK.
1) Response<T> ve SandopResult'a IsSuccessful (2xx) ekle — yoksa 4xx başarılı sayılır. Testle kanıtla.
2) Tek kapsam bağdaştırıcısı: özellik başına *RequestContext'leri (ShipmentScope: TenantId, LegalEntityId, ActorId) okuyan
   SupplyChainCentralAuditScope; kiracı yalnız çözülmüş kapsamdan.
3) AddCentralAudit<…>(…, "Diten.SupplyChainService", o => o.RequireEnlistment = true) — yalnız komutların çalıştığı her işlem
   EnlistAsync çağırıyorsa; *Schema.cs kurulum işlemleri koruma testinde adıyla dışarıda.
4) 17 komuta işaret; tests/architecture/audit-ledger/Diten.SupplyChainService.md (README biçimi, borç 0) + PinnedCounts +
   AcceptedTrails (CT kararı — onay yoksa DUR).
5) Testler + sabotajlar; push öncesi origin/main merge.
```
