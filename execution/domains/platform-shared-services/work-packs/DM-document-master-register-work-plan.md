# DM — Document Master Register (citation + ingest + effectiveness) — İş Planı

> **Control Tower kaydı (SoR).** DCP-005 doküman-yönetimi tarafının devamı (handoff `handoff-dcp-005-docreg-migration-to-task-center.md` geri bildirimlerine göre). Kaynak otorite: kullanıcı DM planı (2026-09-10) + DCP-005 sözleşmesi. Chat SoR değil (K11).
> **Modül:** MOD-0029 (Document Master Register). **Branch:** ⚠️ KARAR BEKLİYOR (§CT-Decisions) — SCMM branch'i DEĞİL.

## Delta (handoff geri bildirimi)
| Konu | Öncesi | Şimdi |
|---|---|---|
| G1 join | ön koşul karar | **kapalı** — `by="uid"`, ayrı adım değil |
| Sıralama | ingest → seam | **contract-first**: DM-2a şeklini önce dondur/teslim |
| CollectionInstanceId | açık | **çözüldü: `Guid.Empty`** (FU06 manuel-create ile ayni, §8) |
| Citation DTO | jenerik | **Source=MasterRegister** (+ opsiyonel registerEntryId); ListVersionId=null |
| Adım 2 (activation gate) | "tüketiyor sanılıyordu" | tüketmiyor; onların işi ama tükettikleri effectiveness portu **bizde hazır** → yeni kod yok, **stabil tut** |

## DM adımları

### DM-0 — Karar/ölçüm — ✅ ÖLÇÜLDÜ + owner-onaylı mapping (2026-09-10)
**Ölçüm (Import-Csv, quoted-aware; 358 satır):** status yalnız **6 gerçek değer** (naive-split 21 sahte değer = kolon-kayması). Dağılım + **onaylı eşleme:**
| CSV status | Adet | → LifecycleStatus | linkable |
|---|---|---|---|
| Draft | 320 | `Draft` | yes |
| Draft — final draft for approval | 1 | `InReview` | yes |
| Void | 7 | `Retired` (terminal, non-citable) | no |
| Planned | 23 | RegisterStatus=Draft + LifecycleStatus=Draft + **blocked** "does not exist yet" (düşürülmez) | no |
| NOT REGISTERED | 6 | aynı (blocked "mandatory-but-unregistered") | no |
| Executed (source on file) | 1 | blocked/özel (record, controlled-doc değil) | (QA) |
- linkable_in_erp: **yes 322 / no 36** (= 23 Planned + 7 Void + 6 Mandatory-unregistered ✓).
- 🔴 **0 doküman Effective** → DCP-005 doğrulandı; auto-seed sonrası effectiveness/citation her şey için Blocked/Unresolved (Citable=false) = **doğru mevcut gerçek** (Faz 1).
- Owner onayı: 4 QA noktası öneriler kabul (final-draft→InReview, Planned/NOT-REG→Draft+blocked, Void→Retired). "Executed(1)" → blocked/özel.

#### (orijinal DM-0 notları)
- **status → LifecycleStatus kapalı eşleme** (🔴 en kritik risk, QA onayı): CSV gerçek status değerlerini say → 9 üyeli `ControlledDocumentLifecycleStatus`'a eşle; eşlenemeyen satır **düşürülmez**, "görünür-uyumsuz" (blocked+sebep) tutulur.
- **36 bağlanamayan satır** (23 planlı / 7 iptal / 6 zorunlu-ama-kayıtsız) → uygun LifecycleStatus (blocked+sebep), **gizleme yok**.
- **Register rolü**: CSV-projeksiyonu (refresh) mı ERP-SoR (end-state) mı → ingest upsert davranışını belirler (DM-1'i bloklamaz; idempotent upsert ikisini taşır).
- G1: kapalı, işlem yok.

### DM-2a — ✅ **TESLİM EDİLDİ (CT-ACCEPTED E2, 2026-09-10, commit 2317464b)** — imza SABİT (WP-DM-2a §37)
> IControlledDocumentCitationPort (Resolve+Search) + DocumentCitationItem teslim; 20/20 izole + full-run'da DocumentCitation fail=0; MOD-0262 izole. **TaskCenter repoint'i başlayabilir** (handoff'a "A.2 delivered" düşülecek).

### DM-2a (orijinal) — Citation sözleşmesini SABİTLE ve teslim et (🔥 KRİTİK YOL — onlar buna bağlı başlıyor)
- `IControlledDocumentCitationPort`: `ResolveAsync(identifiers, by)` (governing-docs + freezer) · `SearchAsync(term, limit)` (picker).
- `DocumentCitationItem { Uid, Code, Title, Version, Lifecycle, Citable, BlockedReason, Source=MasterRegister, RegisterEntryId? }` — bilerek `DocumentReferenceEntryDto`'ya benzer.
- `Citable = IsOperationallyEffective` (effectiveness gate ile aynı yargı kaynağı).
- İlk teslim: register-backed minimal impl + testler; tohum boşken Unresolved/boş döner ama **şekil sabit** → onlar fixture/mock ile repoint'e başlar. **Hazır olunca haber ver.**

### DM-1 — Register ingest (Adım 0) · paralel · **+ AUTO-SEED (kullanıcı eklentisi 2026-09-10)**
- 358 satır CSV (`docs/integration/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv`) → `DocumentMasterRegisterEntry`; **`DocumentReferenceListParser` reuse**; §3 alan eşlemesi; `IsSystemAllocated=false`; **`CollectionInstanceId=Guid.Empty`** (klasörsüz projeksiyon, §8).
- Idempotent upsert by `(TenantId, PermanentUid)` (duplicate guard var).
- **⭐ AUTO-SEED (default):** liste **otomatik** register'a yüklensin ki **push sonrası arkadaş TaskCenter repoint'ini gerçek veriyle** yapabilsin (effectiveness:batch / citation gerçek Effective/Blocked dönsün, boş değil). Yani DM-1 tek-seferlik script değil, **idempotent auto-seed** olarak da koşulur.
  - ⚠️ **Scope (CT, KARAR-2 owner): tenant-AGNOSTIK config-driven.** Hedef tenant HARDCODE EDİLMEZ (97c5 sadece local). `appsettings` `DocumentRegisterSeed:TenantId` — her env kendi tenant'ını verir (local=97c5, arkadaş=kendi, prod=boş→skip); dev-gated; idempotent; config yoksa/prod atla (leakage yok). GLOBAL all-tenant default DEĞİL.
  - ⚠️ **DM-0'a bağlı:** doğru status eşlemesi olmadan auto-seed yanlış LifecycleStatus yazar → DM-0 mapping **auto-seed'in ön koşulu**.
- **Yol:** governed import feature (dry-run+commit+hash, QMS baseline import deseni) **veya** auto-seed (DataSeeder/startup seeder, tenant-97c5). Auto-seed hızlı unblock; governed import denetlenebilirlik. GUID subtype-4 + tenant tuzakları.

### DM-2b — Citation port gerçek implementasyon
- `SearchAsync` (register repo term filtresi; lookup'ın blocked-görünür/seçilemez davranışını aynala) + `ResolveAsync` (batch `$in` — **`GetByPermanentUidsAsync` reuse**, bu branch'te MEVCUT). Ingest indikten sonra canlı doğrulanır.

### DM-3 — Effectiveness · hazır, reuse (kod yok)
- effectiveness resolver + `GetByPermanentUidsAsync` **bu branch'te mevcut**. Tohumdan sonra `effectiveness:batch` gerçek Effective/Blocked döndüğünü doğrula. Onların Adım 2'si bu portu tüketir → **imza değiştirme, stabil tut**.

### DM-4 — Gerçek dosyalar (PDF/Word) · ayrı WP, sonra
- register → ControlledDocument link + klasör taksonomisi import + "dosyayı aç". CollectionInstanceId/klasör + 36 satır sorunu burada. ⚠️ **Bu adım paralel arkadaşın MOD-0262 (binary store) işiyle DOĞRUDAN çakışır** — koordinasyon şart.

## Sıra · kritik yol · kabul
**Sıra:** DM-0 → **DM-2a** (sözleşme teslim + haber ver) ⟂ **DM-1 (ingest + auto-seed, paralel)** → DM-2b → DM-3 doğrula → DM-4 ayrı.
**Kritik yol:** DM-2a (onlar A.2 şekli gelince repoint'e başlar). Ama **auto-seed (DM-1)** da push-öncesi zorunlu (arkadaş runtime testini gerçek veriyle yapsın).
**Kabul:** DM-2a port+DTO derleniyor+testli+imza dokümante; DM-1 register'da 358 satır + `by=uid` `GetByPermanentUidsAsync` doğru satır + re-import idempotent + `CollectionInstanceId=Guid.Empty` + **auto-seed tenant-97c5'te çalışıyor**; DM-2b SearchAsync/ResolveAsync; DM-3 effectiveness:batch gerçek Effective/Blocked; build temiz + testler yeşil.

## Riskler
- 🔴 **status eşlemesi** (DM-0, QA) — auto-seed'in ön koşulu.
- 36 satırın temsili (blocked+sebep, gizleme yok).
- Script yolu seçilirse GUID subtype-4/tenant tuzakları.
- **DM-2a imzası bir kez sabitlenince değiştirmemek** (onlar ona göre yazacak).
- 🔴 **Paralel-writer çakışması** (§CT-Decisions).

## §CT — Ölçülmüş gerçek + kararlar (2026-09-10)
**Ölçüldü (bu branch, main'den):**
- CSV repoda ✓ · effectiveness resolver + `GetByPermanentUidsAsync` **bu branch'te var** (main'e merge olmuş) → DM-2b/DM-3 reuse hazır · `DocumentReferenceListParser` var (Tasks/Services) → DM-1 reuse.
- 🔴 **Paralel arkadaşın COMMIT'SİZ dirty işi tam DM alanında:** `ControlledDocumentsController/Service`, `ControlledDocumentRegistrationService`, `DocumentVersioningService`, `TemplateService`, `IContentStorageGateway.cs` **SİLİNMİŞ**, `ControlledDocumentsFixtures.cs` (MOD-0262 binary-store workstream). DocumentMasterRegister feature'ı şu an dirty DEĞİL (register/effectiveness çekirdeğimiz görece izole), ama DM-4 (register↔ControlledDocument link) ve DataSeeder doğrudan çakışır.

**KARAR-1 (branch) — VERİLDİ (owner, 2026-09-10): bu branch (`feature/structured-content-messaging`).** §16.2 waiver **W-DM-BRANCH**.
Compensating control (ölçüldü): DM çekirdeği (MasterRegister/effectiveness/citation) + seed dosyaları arkadaşın 37 dirty dosyasıyla **çakışmıyor** (izole) → **DM-0→DM-3 bu branch'te güvenli**. Tek çakışma **DM-4** (register↔ControlledDocument, arkadaşın alanı) → DM-4 **ayrı/koordineli**. Commit'ler dosya-bazlı; arkadaşın dirty dosyalarına DOKUNMA.

**KARAR-2 (auto-seed scope) — VERİLDİ (owner): tenant-AGNOSTIK.** 97c5 sadece kullanıcının localinde; arkadaş/prod farklı tenant. → Auto-seed hedef tenant'ı **HARDCODE ETME** (mevcut PositionAssignmentSeed/DataSeeder deseni hardcode 97c5 — onu izleme). **Config-driven:** `appsettings` `DocumentRegisterSeed:TenantId` (her env kendi tenant'ını verir; local=97c5, arkadaş=kendi, prod=boş→skip); dev-gated; idempotent; **config yoksa/prod'da atla** (leakage yok). *(Alternatif: mevcut dev DefaultTenant'a seed; ama config-key en taşınabilir.)*

**KARAR-3 (DM-4 boundary) — açıklandı:** Register = dokümanların **metadata kataloğu** (kod/başlık/versiyon/statü = "hangi doküman, hangi sürüm, yürürlükte mi") — **dosya YOK**. Gerçek PDF/Word = **binary store = MOD-0262** (arkadaşın işi). **DM-4** = "gerçek dosyayı aç" (register kaydı → ControlledDocument → binary dosya) → **ikisini birden** kapsar. Sınır: **BİZ** register/citation/effectiveness (metadata), **ONLAR** binary/repository (dosya). **Bu turda (DM-0→DM-3) gerçek dosya YOK** — yalnız metadata katalog + citation + auto-seed + effectiveness doğrulama. DM-4 (gerçek dosyalar) sonra, arkadaşla koordineli.
