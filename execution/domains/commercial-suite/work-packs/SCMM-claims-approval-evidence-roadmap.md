# Yol Haritası — İddialar (Claims): Workflow + WorkCenterNext onayı ve Document Management kanıt bağlantısı

> **Durum:** PLAN (2026-09-28) · **Sahip:** CT · **Kapsam:** CAND-CAP-0011 (SCMM) Claims × MOD-0023 Workflow × WorkCenterNext (WC) × MOD-0029/0028 Document Management × MOD-0031 Evidence Linking.
> **Girdi:** kullanıcı kararı — çok ülkeli şirket: **global çekirdek iddia → ülke sürümleri (yerel dil + yerel onay)**; sahiplik **tenant**, uygulanabilirlik ürün + pazar + dil + kitle, tüzel kişilik ayrı eksen DEĞİL (ContentScope "DESIGN LE" ile aynı). Mockup brief'i sohbette verildi; mockup gelince bu plan WP'lere bölünür.

## 0. Bugünkü durum (koddan kanıt)

| Parça | Durum | Kanıt |
|---|---|---|
| **Claim** (CRM) | Taslak → **doğrudan** Onaylı (tek kişi, tek tık); kanıt **opak metin**; kapsam opak metin; ülke sürümü yok | `CrmService.Domain/Entities/Claim.cs` — `EvidenceRefs` "OPAQUE… MOD-0031 resolution deferred — C1 blocker" |
| **MOD-0023 Workflow** | **Çalışıyor** (Platform): şablon + değişmez sürüm, **sıralı çok adımlı** (stages→steps), aday = principal/**pozisyon** (position_assignments), SLA + eskalasyon, approve/reject/delegate/request-info/cancel, SoD, idempotency, transition gate | `Platform.Application/Features/Workflow/**`, `WorkflowDefinitionRuntimePlan.cs`, `api/v1/workflow/*` (ocelot route **var**) |
| **WorkCenterNext** | MOD-0023 onay görevlerini **otomatik** gösteriyor (`WorkflowApprovalWorkItemProvider`); butonlar sunucu tarafı dispatcher'la gerçek onay uçlarına gidiyor (WC-D2) | `WorkAggregation/Providers/*`, `api/v1/work-items/{id}/actions/{code}` (route var) |
| WC başlık/link | Onayın "neyle ilgili" olduğunu **kaynak modül** söyler (`IApprovalSourceResolver`) — bugün yalnız **Tasks** (Platform içi) resolver'ı var | `WorkAggregation/Services/IApprovalSourceResolver.cs`, `Tasks/Providers/TaskApprovalSourceResolver.cs` |
| Workflow tüketicileri | Hepsi **Platform içi** (Tasks, ModuleCatalog). **Servisler arası (CRM→Platform) tüketici YOK** | grep `StartWorkflowInstance` |
| Sonuç bildirimi | Tasks deseni **pull**: kaynak modül yalnız instance id tutar, durumu MOD-0023'e sorar. Workflow tamamlanma **olayı yayınlanmıyor** | `Tasks/Services/TaskApprovalService.cs` |
| Olay altyapısı | `Diten.BuildingBlocks.Eventing` outbox + transport var (Auth tüketiyor); Platform'da outbox var; **CRM'de tüketici yok** | `BuildingBlocks.Eventing/*`, `Auth…/EntitlementSyncConsumer.cs` |
| **Document Management** | Zengin (MOD-0029, Platform): kontrollü belge + sürüm, yaşam döngüsü, e-imza (FU23), periyodik gözden geçirme (FU12), **harici belgeler + izleme** (FU14 — literatür için), **şablon varyantı yerelleştirme + yerel onay** (FU18), klasör erişim matrisi (FU04), önizleme | `Platform.Application/Features/DocumentManagement*` (30 alt alan) |
| CRM ↔ DocMgmt | Bilgi İçeriği `FileRef` → kontrollü belge; önizleme **güncel sürümü** çözüyor (sabitleme yok) | `Web/Controllers/CRM/KnowledgeController.cs` `document-preview`, `api/document-options` |
| **MOD-0031 Evidence Linking** | **Yalnız şartname** (~%10, üretim %0). EvidenceLink (nesne ↔ belge/sürüm), sorgu, gömülebilir Kanıt Paneli, tamlık kuralları, `evidence.linked/unlinked` olayı | `platform-shared-services/module-packs/MOD-0031-evidence-linking-service.md` |

**Özet:** Onay motoru ve iş kutusu hazır — eksik olan **CRM'i bağlamak** (başlat, sonucu al, WC'de anlamlı başlık). Belge tarafında depolama/sürüm/önizleme hazır — eksik olan **yapılandırılmış kanıt bağı** (MOD-0031) ve **belge değişince iddiayı işaretleme**.

---

## A. Workflow + WorkCenterNext yol haritası

### A0 — Kararlar (kod öncesi)
| # | Karar | Öneri |
|---|---|---|
| A0-1 | Sonuç CRM'e nasıl döner? | **Push:** Platform workflow tamamlanınca outbox olayı `platform.workflow.instance.completed` (ObjectType/ObjectId/outcome/stepsummary) → CRM tüketicisi iddia durumunu işler (idempotent). **+ Uzlaştırma süpürgesi** (kaçan olay için periyodik pull). Saf pull (Tasks deseni) servisler arası N+1 ve "iddia DB'de hâlâ incelemede" riski taşır. |
| A0-2 | Onaylayıcılar kim? | **Pozisyon bazlı** (motor zaten destekliyor): `Global Medikal Direktör`, `Global Ruhsat`, `TR Medikal Müdür`, `TR Hukuk`, `TR Ruhsat`, `UZ …`. Kişi değil pozisyon → işten ayrılma/vekâlet sorunsuz. |
| A0-3 | Şablon yapısı | `CLAIM-CORE-MLR` (global: Medikal → Hukuk → Ruhsat) + ülke başına `CLAIM-LOCAL-MLR-TR/UZ/AZ`. Motor **sıralı** adım destekliyor; paralel (üçü aynı anda) gerekirse ayrı karar (motor genişletmesi). |
| A0-4 | WC'de başlık/link | **Başlatırken görüntü bağlamı** (başlık, alt başlık, modül, derin link) instance'a snapshot olarak yazılır + genel "snapshot resolver". Alternatif: Platform'dan CRM'e HTTP resolver (servisler arası okuma, daha kırılgan). |
| A0-5 | E-imza gerekir mi? | MLR onayında genelde **evet** (denetim). DocMgmt FU23 e-imza deseni var → onay adımında `evidenceRequired`/imza bayrağı. Karar: MVP'de yorum + kim/ne zaman; e-imza faz 2. |
| A0-6 | Doğrudan "Onayla" ne olacak? | Kaldırılır. İddia **yalnız workflow tamamlanınca** onaylanır (SoD: gönderen onaylayamaz — motor zaten uyguluyor). Şablon yoksa **fail-closed** (gönderilemez), sessiz geri düşüş yok. |

### A1 — Platform (MOD-0023, küçük genişletmeler)
1. **Tamamlanma olayı:** instance approved/rejected/cancelled olunca transactional outbox'a `platform.workflow.instance.completed` (+ `step.completed` isteğe bağlı).
2. **Görüntü bağlamı:** `StartWorkflowInstanceRequest`'e isteğe bağlı `DisplayContext {title, subtitle, sourceModule, deepLinkUrl}`; instance'ta saklanır; **SnapshotApprovalSourceResolver** WC'de kullanır (Tasks resolver'ı aynen kalır).
3. **RBAC seed:** `platform.workflow.*` anahtarları (pack §8 "ayrı iş") + CRM rolleri için `tasks.approve/reject/request-info`.
4. **Toplu durum okuma:** `GET instances?objectType=&objectIds=` (uzlaştırma süpürgesi için).
*Sahip: Platform. Test: Platform.Application.Tests; mevcut Tasks/WC testleri kırılmaz.*

### A2 — Workflow şablonları (konfigürasyon, kod yok)
- `CLAIM-CORE-MLR` ve `CLAIM-LOCAL-MLR-{ülke}` tanımla + yayınla; adım SLA'ları (ör. 3 iş günü) + eskalasyon (bir üst pozisyon).
- Pozisyon atamaları (position_assignments) test tenant'ında (97c5) — **kullanıcı yapar** (DB/yetki yazımı).

### A3 — CRM Claims (iddia tarafı)
1. **Durum modeli:** `draft → in-review → approved → review-required → inactive/archived`; `rejected` = taslağa döner + gerekçe.
2. **Alanlar:** `WorkflowInstanceId`, `SubmittedAt/By`, `ReviewOutcome`, `RejectionReason`; çekirdek/ülke sürümü (`CoreClaimId`, `CountryCode`, dil başına metin) — mockup'a göre.
3. **Komutlar:** `SubmitForReview` (Platform'a gateway üzerinden instance başlatır, idempotency key = claimId+version), `WithdrawReview` (cancel), olay tüketicisi `ApplyReviewOutcome` (idempotent).
4. **Kurallar:** onaya gönderim için ≥1 kanıt (B'ye bağlı); ülke sürümü, çekirdeği onaylı değilse gönderilemez; onaylı iddia kilitli → yeni sürüm.
5. **Servisler arası istemci:** CRM → Gateway `api/v1/workflow/instances` (MDM okuma deseni gibi, token iletimi); CRM'e ilk **olay tüketicisi** (BuildingBlocks.Eventing).
6. **Detay:** "Onay geçmişi" sekmesi = instance + transition log okuması (salt-okuma proxy).

### A4 — WorkCenterNext
- İddia onayları "Onay: İddia CLM-TUTUKON-01 v1 · TR" başlığıyla gelir; **Aç** → iddia inceleme sayfası (salt-okuma: metin, kapsam, **kanıt paneli + belge önizleme**, çekirdek/ülke farkı).
- Onayla / Reddet (gerekçe zorunlu) / Bilgi iste / Devret → mevcut dispatcher.
- Ekip görünümü (`scope=team`) ve SLA rozetleri mevcut altyapıdan.

### A5 — Kabul (E2 + E4)
SoD (gönderen onaylayamaz) · idempotent gönderim · ret → taslak + gerekçe · bilgi iste döngüsü · SLA eskalasyonu · olay kaybında süpürge uzlaştırması · kiracı izolasyonu · WC'de başlık/link · ülke sürümü çekirdek onaysızken engelli.

---

## B. Document Management (kanıt) yol haritası

### B0 — Kararlar
| # | Karar | Öneri |
|---|---|---|
| B0-1 | Kanıt bağının sahibi | **MOD-0031 Evidence Linking (Platform) — minimal dilim.** Pack zaten "tüm yönetişimli akışların" tüketicisi olarak tanımlı (iddialar, ileride PV, QMS). CRM-yerel kanıt tablosu hızlı ama sonra taşınır → yeniden iş. |
| B0-2 | Belge türleri nerede? | KÜB/KT, ruhsat yazısı, ürün bilgi formu, çalışma raporu → **kontrollü belge** (ülke + dil + ürün metadatası). Literatür → **harici belge** (FU14; kaynak izleme tarihleri var). |
| B0-3 | Sürüm | Kanıt **belge sürümüne sabitlenir** (bugünkü içerik önizlemesi "güncel sürüm" çözüyor — kanıt için yanlış). |
| B0-4 | Erişim | İnceleyicilerin kanıt belgelerini açabilmesi için DocMgmt **klasör erişim matrisi** (FU04): "Medikal/Ruhsat Kanıt" klasörü + MLR erişim profili. Aksi halde onaylayıcı belgeyi göremez (gerçek risk). |
| B0-5 | "Yeni belge yükle" | Kayıt orkestrasyonu (FU36) ağır → iddia ekranından **DocMgmt kayıt sayfasına yönlendir**, dönüşte seç. Satır içi yükleme faz 2. |

### B1 — MOD-0031 minimal (Platform)
- `EvidenceLink {ObjectRef(module/type/id/version), DocumentId, DocumentVersionId(pinned), DocumentKind(controlled|external), EvidenceType, Locator{page, section, table, quote}, LinkedBy/At, UnlinkedBy/At/Reason}` — bağ geçmişi değişmez (unlink = yeni kayıt).
- API: `evidence.link`, `evidence.unlink`, `evidence.query(objectRef)`, `evidence.byDocument(documentId)` (ters sorgu: belge değişince etkilenen nesneler).
- Olaylar: `evidence.linked/unlinked`.
- Gömülebilir **Kanıt Paneli** (partial + JS): belge seçici (kontrollü + harici), sürüm, tip, locator, önizleme, tamlık rozeti.

### B2 — CRM Claims ← kanıt
- Claim formu/detayı Kanıt Paneli'ni gömer (ObjectRef = `crm/claim/{id}/{version}`); çekirdek kanıtları ülke sürümünde salt-okunur görünür + yerel kanıt eklenir.
- Onaya gönderim kuralı: `evidence.query` ≥1 (A3-4).
- Mevcut opak `EvidenceRefs` → geçiş: metin olarak "eski referans" gösterilir, yeni bağ zorunlu (veri taşıma yok, test verisi).

### B3 — Önizleme
- Sabit sürüm önizlemesi (mevcut `document-preview` sürüm parametresiyle) + locator'daki sayfaya atlama (PDF `#page=`).

### B4 — Değişiklik algılama (en değerli kısım)
- DocMgmt belge yaşam döngüsü: yeni sürüm **yürürlüğe girince** / askıya alma / geri çekme → olay `document-management.document.version-effective|suspended|withdrawn`.
- MOD-0031 ters sorgu → etkilenen iddialar → CRM iddiayı **"gözden geçirilmeli"** yapar (onaylı iddia kilitli kalır, yeni sürüm gerekir).
- Periyodik gözden geçirme (FU12) ve harici belge izleme (FU14) tarihleri → "kanıt süresi doluyor" uyarısı.

### B5 — Yerelleştirme deseni
- DocMgmt **FU18 şablon varyantı yerelleştirme + yerel onay** = iddia çekirdek→ülke sürümü ile **aynı problem**; kod paylaşılmaz ama **kavram ve kurallar** (hazırlık kararı "LocalUseAllowed", iki dilli gözden geçirme, sapma/drift FU03) iddia tasarımına örnek alınır.

### B6 — Kabul
Belge seç + sürüm sabitle + locator · önizleme doğru sürüm/sayfa · yetkisiz belge görünmez (erişim matrisi) · belge yeni sürümü → iddia "gözden geçirilmeli" · harici literatür izleme uyarısı · kanıtsız gönderim engelli · bağ geçmişi değişmez · kiracı izolasyonu.

---

## C. Sıralama (bağımlılık)

```
Mockup (kullanıcı) ──► Claim veri modeli kararı (çekirdek/ülke sürümü, durumlar)
A0 + B0 kararları ──┬─► A1 Platform (olay + DisplayContext + RBAC seed)  ─┐
                    ├─► B1 MOD-0031 minimal (+ Kanıt Paneli)              ─┤
                    └─► A2 şablonlar + pozisyonlar (konfig, kullanıcı)     ─┤
                                                                            ▼
                         A3 + B2 CRM Claims yeniden (durum, gönder, olay tüketici, kanıt paneli, ülke sürümü)
                                                                            ▼
                         A4 WorkCenterNext E4  ·  B3 önizleme  ·  B4 değişiklik algılama
                                                                            ▼
                         A5 + B6 kabul → içerik/yolculuk tarafına "yalnız ülkede onaylı iddia" kuralı
```

**Paralel yürüyebilir:** A1 ↔ B1 (ikisi de Platform, farklı alanlar) · A2 (konfig). **Kritik yol:** Claim veri modeli kararı (mockup'a bağlı).

## E. Organizasyon modeli — MLR onaylayıcıları nerede? (2026-09-28 analiz + kullanıcı kararı)

**Kullanıcı kararları:**
- Medikal / Hukuk / Ruhsat = **departman** (inceleme fonksiyonu).
- **E-imza MVP'de zorunlu değil.** Kim, ne zaman ve yorum kaydedilir.
- Tek kişi birden çok fonksiyonu üstlenebilir.
- **Küçük ülkede (UZ) merkez ofisten biri de onaylayabilir.**

**Projede yeri: Platform Organizasyon (SoR).** CRM'de ya da RBAC rolünde tutulmaz. HCM çalışan kaydı bu pozisyonlara referans verir (`HcmService…/IReferenceValidationClient`).

| Kavram | Entity (Platform) | Önemli alanlar | Sayfa |
|---|---|---|---|
| Tüzel kişilik | `LegalEntity` (MDM) | ülke şirketi | `/LegalEntities` |
| Departman | `OrganizationUnit` | `LegalEntityId` (**zorunlu**), `OrgUnitType` = Department/Division/Branch/Team/**HQ**/**GroupFunction**, `ManagerPositionId`, üst birim | `/OrganizationUnits` |
| Pozisyon | `Position` | `OrganizationUnitId`, `ReportsToPositionId`, `JobTitle`, durum | `/Positions` |
| Kişi ↔ pozisyon | `PositionAssignment` | `UserId`, **`AssignmentType` = Primary / Secondary / Acting / Delegated**, `AllocationPercent`, tarih aralığı | `/PositionAssignments` |

**Workflow bağlantısı (kod):**
- `WorkflowCandidateResolver` adım adaylarını `user:{id}` ya da `position:{id}` olarak alır. Pozisyonu, tarih aralığı geçerli atamalar üzerinden kişilere çözer.
- **Bir adımın birden çok aday pozisyonu olabilir**; herhangi biri onaylar. Merkez yedeği böyle tanımlanır.
- **SoD motorda yalnız "gönderen onaylayamaz"** (`WorkflowTaskTransitionSupport.cs:112`). Aynı kişinin ardışık iki adımı onaylaması **serbest**, bu kullanıcı kararıyla uyumlu.

**Hedef model (örnek):**
```
LE: Diten İlaç TR A.Ş. ── OU HQ "Genel Merkez"
                          ├─ OU GroupFunction "Global Medikal"  → POS "Global Medikal Direktör"
                          ├─ OU GroupFunction "Global Hukuk"    → POS "Global Hukuk Müşaviri"
                          └─ OU GroupFunction "Global Ruhsat"   → POS "Global Ruhsat Lideri"
LE: Diten TR ──────────── OU Division "Türkiye" → Department TR Medikal / TR Hukuk / TR Ruhsat → POS "TR Medikal Müdür" …
LE: Diten UZ LLC ──────── OU Division "Özbekistan" → Department "UZ Medikal & Ruhsat" → POS "UZ Medikal-Ruhsat Sorumlusu"
                          (UZ'de Hukuk departmanı yok)
```

**Şablonda adım → aday:**
| Şablon | Medikal | Hukuk | Ruhsat |
|---|---|---|---|
| CLAIM-CORE-MLR | Global Medikal Direktör | Global Hukuk Müşaviri | Global Ruhsat Lideri |
| CLAIM-LOCAL-MLR-TR | TR Medikal Müdür | TR Hukuk | TR Ruhsat |
| CLAIM-LOCAL-MLR-UZ | UZ Medikal-Ruhsat Sorumlusu | **Global Hukuk Müşaviri** (merkez üstlenir) | UZ Medikal-Ruhsat Sorumlusu (**aynı kişi, izinli**) + yedek Global Ruhsat Lideri |

**Alternatif:** Merkez kişisine UZ pozisyonunda `Secondary` atama verilir. Bu durumda şablon yalnız UZ pozisyonlarını içerir; kişi değişikliği atamayla yönetilir, şablona dokunulmaz.

**RBAC ayrı katman:** "İddia İnceleyici" rolü şunları içerir:
- `platform.workflow.tasks.approve/reject/request-info`,
- iddia ve kanıt okuma,
- Document Management kanıt klasörü erişimi.

Pozisyon **kimin** onaylayacağını, rol ise sistemde **yapabilir mi** sorusunu belirler.

**Canlı durum 97c5 (2026-09-28):**
- Yalnız "Genel Merkez" HQ birimi ve 2 anlamsız test birimi var.
- 22 pozisyon var, çoğu ticari (temsilci, bölge müdürü…); **Medikal, Hukuk ve Ruhsat departmanı ile pozisyonu yok**.
- 1 pozisyon ataması var.
- MDM'de 1 tüzel kişilik var; alanları boş.
- → **Org yapısı kurulmalı** (A2 öncesi). Kurulumu kullanıcı yapar, CT veriyi hazırlar.

**Koddan bulgular (düzeltme adayları):**
1. `WorkflowCandidateResolver` atamada `IsCancelled` ve pozisyon durumunu (Frozen/Closed) **kontrol etmiyor**. İptal edilmiş atama da onaylayıcı sayılabilir.
2. Aynı sınıf **tüm atamaları** okuyor (`GetAllAsync`); ölçekte performans riski.
3. Ülke başına "ayrı onaylayıcı zorunlu" kuralı gerekirse şablon bayrağı eklenebilir (ör. TR mevzuatı). Bugün yok, ihtiyaç doğarsa açılır.

## D. Riskler
1. **Servisler arası ilk workflow tüketicisi** CRM olacak — token iletimi, idempotency, olay kaybı; süpürge şart.
2. **Paralel onay** (Medikal+Hukuk+Ruhsat aynı anda) motor tarafından desteklenmiyor olabilir (bugün sıralı) → gerekiyorsa MOD-0023 genişletmesi.
3. **DocMgmt RBAC anahtarları** birçok FU'da "önerildi, seed edilmedi" → kanıt belgelerine erişim testte takılabilir.
4. İçerik tarafı (Bilgi İçeriği, İçerik Seti, yolculuk) henüz **ülke sürümü** bilmiyor → iddia ülke kuralı sonra içerik seçicilere yayılmalı.
5. Onay sonucu olayını tüketmek için CRM'e eventing altyapısı eklenmesi yeni bir işletim bileşeni (fleet'e consumer).
