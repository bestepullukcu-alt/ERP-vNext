# ERP-vNext Controlled Documents / Document Management — Remediation Implementation İş Planı

**Sürüm:** v1 — 2026-08-24
**Ana kaynak:** `GMG-CSV-URS-0001 v0.3 — ERP-vNext Document Management Gap Analizi v2` (`docs/audits/gmg-csv-urs-0001-gap-analysis-2026-08-24.md`)
**Doğrulama kaynakları:** URS v0.3 DRAFT · LOG-0007 v0.36 · Annex Pack v0.2 (A1/A2/A3, B, C, D) · `_HANDOVER_INDEX.txt` · repo (MOD-0028 / MOD-0029)
**Tür:** ANALİZ VE PLANLAMA — kod değişikliği, branch, commit, push veya PR **yapılmadı**.

**FU numaralandırma tabanı:** Kodda kullanılan en yüksek follow-up `MOD-0029-FU37`'dir (registry ile tutarlı). Bu plandaki yeni işler **MOD-0029-FU38**'den başlar. *(Gap analizi v2'de geçen FU38–FU70 numaraları taslaktı; bu plandaki konsolidasyon sonrası numaralandırma bağlayıcıdır.)*

---

## 0. Konsolidasyon Analizi — 40 Madde Neden 27 Follow-up Oldu

Gap analizi v2'de ~40 remediation maddesi (R-01…R-40 + P1/P2) vardı. Bunları birebir follow-up'a çevirmek **yanlış** olurdu: bazıları aynı kod yolunu ikinci kez test etmeyi, bazıları başka modülün sorumluluğunu MOD-0029'a kopyalamayı, bazıları da hiç kod gerektirmemeyi ima ediyordu.

### 0.1 Kod işine dönüşmeyen maddeler

| Madde | Neden follow-up değil | Nereye gitti |
|---|---|---|
| R-01 (10 governance kararı) | Kod değil, karar | **Governance Workstream** (§7) |
| R-02 (Annex D 103'e tamamlama) | v2'de kapandı | — |
| R-37/R-38/R-39/R-40 (IQ/OQ/UAT/negatif test yürütme) | Protokol yürütme; yazılım teslimi değil | **Validation Workstream** (Faz 5) |
| P2-10 (dosya boyutu profili izleme) | İzleme maddesi, borç değil | Ops izleme listesi |
| P1-22 (DR planı, RPO/RTO) | Altyapı/ops prosedürü; MOD-0029 kodu değil | FU61 içinde **kanıt toplama** olarak |
| URS-135 (SLA), URS-140 (supplier assessment), URS-145 | Sözleşmesel / süreç | Kapsam dışı |

### 0.2 Birleştirilen maddeler ve gerekçeleri

Birleştirme kuralı: **yalnızca aynı golden flow'u veya aynı aggregate'i paylaşıyorlarsa.**

| Birleşim | Birleşen maddeler | Gerekçe |
|---|---|---|
| **FU39** Regulated Authorization Hardening | R-10 + R-11 | İkisi de tek dosyada (`HasPermissionAttribute`), tek akışta: *istek → authz kararı → 403 + audit*. R-11, R-10'un kanıt ayağıdır; ayrı gitmeleri aynı hot path'i iki kez regresyona sokar. |
| **FU40** Controlled Vocabulary Foundation | R-04 + P1-15 + P1-26 | Vocabulary set'i oluşturmak, onu LOG-0007/Annex C'den beslemek ve code uniqueness normalizasyonu **aynı aggregate'in** (vocabulary entry) yaratılma/doğrulama akışıdır. |
| **FU41** Vocabulary Integrity Rules | R-05 + R-06 | İkisi de "vocabulary entry allocation/validation" akışında çalışan invariant'lar (entity kodu yeniden atanmaz; TYPE yalnız onaylı domain'de geçerli). |
| **FU42** Document Classification Metadata | R-07 + P1-25 + P0-11 | Register kaydının **tek save akışı**: zorunlu classification alanları, free-text alanların BRD'ye bağlanması ve default'un kaldırılıp `Held`'e düşme davranışı aynı validasyon bloğudur. |
| **FU45** QualityRecord Aggregate | R-12 + R-13 + R-14 | Tek aggregate, tek akış: *record yarat → complete → overwrite denemesi → red → controlled correction*. **Kritik:** R-13 (engel) R-14 (meşru düzeltme yolu) olmadan sevk edilirse kullanıcı çıkışsız kalır. Birlikte gitmek zorundalar. |
| **FU46** Lifecycle Transition Hardening | R-15 + R-16 + P1-27 + W-6 | Üçü de `DocumentLifecycleService.TransitionAsync` ve `ApplyMarkEffectiveGuardsAsync` içinde. Ayrı ayrı yapılırsa aynı kritik yol üç kez regresyona girer. |
| **FU50** Metadata-Based Permission Model | R-20 + R-22 + W-2 + W-3 + P1-16 | Hepsi tek karar noktasında toplanıyor: *effective permission nasıl hesaplanıyor?* 32 profili tanımlayıp değerlendirmeyi klasörden metadata'ya taşımak **aynı resolver'ın** yeniden yazımıdır. |
| **FU54** External Sharing Governance | R-25 + P1-23 | `CopyOnAdopt` bir **share mode**'dur; paylaşım governance'ıyla aynı akışta kapatılır. |
| **FU55** Document Migration Lane | R-26 + R-27 + P1-12 + P1-13 | Tek ingest akışı: *manifest oku → hash doğrula → duplicate/OS artefact ele → disposition uygula → reconcile → gerekirse geri al*. Exclusion raporu olmadan "sayımlar reconcile eder" mümkün değil; rollback set kimliği ingest anında kurulmalı. Ayrılamazlar. |
| **FU56** Metadata Search & Navigation | R-29 + P1-4 + P1-5 | Aynı read-model; 5 görünüm ve "tek adımda current-Effective" aynı sorgu katmanı. |
| **FU57** Retention & Legal Hold Dimensions | R-30 + R-31 + P1-9 | Tek kök neden: **classification boyutları yoktu**. Hold scope'u ve retention anahtar seti aynı vocabulary'yi tüketir; hold gate'inin archive yollarına yayılması da aynı evaluator'dır. |
| **FU59** API Contract + Retrieval Audit | R-33 + P1-19 + P1-20 | Dış API'yi dokümante etmek ve her retrieval'ı audit'lemek aynı çıkış noktasının iki yüzü. |
| **FU60** SoR Enforcement + Copy Marking | R-35 + P1-29 | "Bu kayıt burada master değil" ve "bu çıktı bir kopyadır" aynı ilkenin yazma ve okuma tarafı. |

### 0.3 Sahipliği MOD-0029 dışında olan maddeler

Bunlar **MOD-0029 içine kopyalanmayacak**:

| Madde | Doğru SoR | Not |
|---|---|---|
| R-32 audit export + audit retention + redaction kilidi | **MOD-0021** (Audit Trail Service — registry: *"Canonical owner of audit trail. No other module may claim this ID."*) | MOD-0029 yalnızca tüketir. |
| R-11 denied-access audit **kaydı** | **MOD-0021** | Sink MOD-0021'e yazar; MOD-0029/MOD-0018 yalnızca olayı üretir. |
| R-10 authorization decision semantiği | **MOD-0018** (RBAC/ABAC Authorization) | Bypass kuralı authorization foundation'ın kararıdır. |
| R-20 permission profile modeli, R-23 IdP/group | **MOD-0018** | Permission ownership MOD-0018'de; MOD-0029 profil **tüketicisi**. |
| R-04/R-05/R-06 vocabulary | **MOD-0048** (Reference Data Management) | Vocabulary set'leri BRD'de yaşar; MOD-0029 okur. |
| Approval/review rotası | **MOD-0029** (mevcut) | MOD-0023'e taşınması **önerilmiyor** — bkz. §6.3. |

### 0.4 Sonuç

**~40 madde → 27 follow-up + 2 workstream.** Ortalama %33 konsolidasyon; en büyük kazanç kritik yolda (lifecycle ve authorization hot path'leri tek seferde sertleştiriliyor).

---

## 1. Ana İş Paketi Tablosu

**Karmaşıklık:** S / M / L / XL. Takvim tahmini verilmemiştir.

| Sıra | Önerilen MOD/FU | İş Paketi | Kapatacağı Gap/URS | Mevcut Reuse Edilecek Capability | Bağımlılık | Governance Blocker | Öncelik | Karmaşıklık | Golden Flow | Failure Path | Exit Gate |
|---:|---|---|---|---|---|---|---|---|---|---|---|
| 1 | MOD-0029-FU38 | Runtime Blocker Kapatma | P0-17 / URS-145 | Mevcut fleet + smoke script'leri | Yok | Yok | P0 | S | Operatör → 3 endpoint çağrısı → 200 | Endpoint 500 dönüyor, kanıt üretilemiyor | Authenticated smoke 3/3 PASS; registry'den `Runtime blocked` notu kalkar |
| 2 | MOD-0029-FU39 | Regulated Authorization Hardening | P0-1, P0-2 / URS-070, 073, 074, 082, 091, 138 | `HasPermissionAttribute`, `PermissionClaimEvaluator`, `AuditEvent`, `ICorrelationContext` | FU38 | Yok | **P0** | L | Platform admin → regulated endpoint → 403 + `AuditEvent(Denied)` | Admin bypass açık kalır; her negatif test çürütülebilir | Regulated sınıflarda bypass yok; her 403 audit'te; negatif test seti yeşil |
| 3 | MOD-0048-FU / MOD-0029-FU40 | Controlled Vocabulary Foundation | P0-8 / URS-010, 011, 012, 014, 017 | `BaselineRelease` versiyonlama deseni, BRD catalog loader, XLSX/CSV parser'lar | FU39 | **G-6, G-7, G-10** | **P0** | XL | Steward → set yayınla → doküman formunda seçilebilir | Set yayınlanmaz; enum'lar kodda kalır | 8 set BRD'de published; her entry 5 nitelikli; enum hard-code kaldırıldı |
| 4 | MOD-0048-FU / MOD-0029-FU41 | Vocabulary Integrity Rules | URS-015, 016, 016a, 016b | FU40 vocabulary aggregate | FU40 | G-6, G-10 | P0 | M | Steward → retired entity koduna allocation → 409 | Retired kod yeniden atanır; tarihsel referans bozulur | Retired/in-use kod 409; TYPE domain dışında 400 |
| 5 | MOD-0029-FU42 | Document Classification Metadata + Held | P0-9, P0-11 / URS-020, 013, 066, 112, 113 | `DocumentMasterRegisterService`, protected-fields deseni, `MasterRegisterWire` | FU40, FU41 | G-6, G-7 | **P0** | L | Author → classification'sız kayıt → `Held` → operasyonel kullanım bloklu | Default `Other`/`Minor` atanır; 330 HELD sessizce çözülür | Default atama yok; `Held` operasyonel kullanımı bloklar |
| 6 | MOD-0029-FU43 | Metadata Path Resolver | P0-10 / URS-021, 024, 025, 123 | `QmsFolderTreeValidator` segment normalizasyonu | FU42 | Yok | **P0** | M | Kullanıcı → doküman aç → display path metadata'dan türetilmiş görünür | Path hâlâ klasörden; URS-123 "generated" yolu imkânsız | Path metadata'dan; >255 uyarı; ASCII segment; topoloji ERP'den üretilebiliyor |
| 7 | MOD-0029-FU44 | Stable Classification Identity | W-1 / URS-022, 023 | `Guid` kimlik, register linkage | FU42, FU43 | Yok | **P0** | L | Author → domain değiştir → kaydet → aynı `Guid`, tüm linkler geçerli, kopya yok | Attribute değişimi objeyi taşır/kopyalar | ⛔ **Migration gate:** kapanmadan FU55 başlamaz |
| 8 | MOD-0029-FU45 | QualityRecord Aggregate + Completed-Record Protection | P0-3, P0-4 / URS-040, 041, 042, 043, 044, 045, 073 | `ControlledDocumentRegistrationService` storage yolu, `DocumentGDocPCorrection*` | FU39, FU40, FU42 | G-7 | **P0** | XL | Originator → record complete → overwrite dener → 409 → controlled correction ile append | Record'a `LifecycleStatus=Effective` yazılır; üzerine versiyon yüklenir | Record'da CD lifecycle asla set edilmez; overwrite 409 (admin dahil); correction yolu çalışır |
| 9 | MOD-0029-FU46 | Lifecycle Transition Hardening | P0-6, P0-7, P0-13 / URS-032, 033, 034 | `ControlledDocumentLifecyclePolicy`, `DocumentLifecycleTransitionRecord`, `ExpectedVersion` | FU39, FU38 | Yok | **P0** | XL | QA → MarkEffective → önceki otomatik+atomik Superseded → tek Effective | Eşzamanlı iki Effective; kısmi hatada sıfır Effective | Partial unique index aktif; race 409; kısmi hata sonrası Effective sayısı asla 0/2 |
| 10 | MOD-0029-FU47 | Signature Gate | P0-5 / URS-037 | `DocumentSignatureRecord`, `ObjectFingerprint` | FU46 | Yok (G-1 **değil**) | **P0** | S | QA → imzasız MarkEffective → 409 `SIGNATURE_REQUIRED` | İmzasız Effective release | Geçerli + meaning'i doğru + fingerprint eşleşen imza yoksa 409 |
| 11 | MOD-0029-FU48 | VOID State Resolution | URS-036 | `ControlledDocumentLifecyclePolicy` | FU46 | **G-5** | P1 | S | QA → hiç Effective olmamış içeriği geri çek → tek modellenmiş yola düşer | VOID içerik hiçbir state'e uymaz | Karar uygulanmış; VOID içerik tek yolda; matris testi yeşil |
| 12 | MOD-0029-FU49 | Document Components / Annex | P0-12 / URS-050–054 | Register linkage, lifecycle matrisi | FU42, FU46 | **G-3** | P1 | L | Author → parent'a annex ekle → parent transition → annex birlikte taşınır | 44 PV annex'i modellenemez; annex bağımsız Effective olur | Parent-child kurulu; with-parent kuralında bağımsız Effective 409 |
| 13 | MOD-0018-FU / MOD-0029-FU50 | Metadata-Based Permission Model + 32 PS-* Profil | P0 §6 / URS-071, 072 | `DocumentAccessResolver`, deny-precedence, fail-safe default | FU39, FU40, FU42 | **G-8** | **P0** | XL | Kullanıcı → doküman iste → izin entity+zone+domain+recordclass'tan hesaplanır | İzin klasör adından hesaplanır (`"Effective"` string'i) | 32 profil tanımlı; `ReadOnlyStatusFolders` kaldırıldı; 6 action bağımsız |
| 14 | MOD-0018-FU / MOD-0029-FU51 | Atomic Profile + Pass-Through Container | URS-071a, 071b | FU50 profil vocabulary'si; import validator | FU50 | **G-11** | P1 | M | Records Mgmt → compound profil ("X + Y") import → reddedilir | 54 lokasyon bağlanamaz; compound sessizce kabul edilir | A3'teki 54 satır pass-through; `" + "` içeren profil değeri 400 |
| 15 | MOD-0018-FU / MOD-0029-FU52 | IdP + Security Group + Named-User Kapatma | P0-15 / URS-070, 132 | `DocumentAccessPrincipalType.Group` placeholder | FU50 | **G-8** | **P0** | XL | Yönetici → kullanıcıyı IdP grubuna ekle → erişim gelir; named-user grant reddedilir | 115 grup beslenemez; named-user istisnası açık kalır | Group principal gerçek kaynağa bağlı; `User` principal controlled scope'ta 400 |
| 16 | MOD-0018-FU / MOD-0029-FU53 | JML + Periodic Access Review | URS-075, 076 | FU52 group binding | FU52 | G-8 | P1 | L | HR → leaver işaretle → erişim düşer → review kampanyası attestation üretir | Ayrılan kullanıcı erişimini korur | Leaver'da erişim sıfır; mover'da recertification; review evidence üretiliyor |
| 17 | MOD-0029-FU54 | External Sharing Governance | URS-077, 101 | `DocumentSharePolicy`, `DocumentShareRecord`, `FolderSharePlanner` | FU50 | Yok | P1 | M | Owner → paylaş → expiry+approval zorunlu → süre dolunca erişim düşer | Süresiz/onaysız paylaşım; `CopyOnAdopt` fiziksel kopya üretir | Expiry zorunlu; revoke çalışır; controlled document'ta `CopyOnAdopt` kapalı |
| 18 | MOD-0029-FU55 | Document Migration Lane | P0-14 / URS-064, 065, 110–115 | `CollectionTreeReconciliationEngine`, `IdempotencyKey` saga, SHA-256 hash | ⛔ **FU44**, FU42, FU45 | G-4, G-9 | **P0** | XL | Records Mgmt → Annex B manifest yükle → hash doğrulanır → 1.236 promoted / 330 held → reconcile raporu | Hash doğrulanmadan ingest; HELD sessizce çözülür; geri alınamaz | 1.631 kayıt doğrulandı; mismatch kalem kalem; set-level rollback; kaynak estate değişmedi |
| 19 | MOD-0029-FU56 | Metadata Search & Navigation Views | URS-100, 101, 103 | `ControlledDocumentExplorerService`, permission-filtered search, non-leakage 404 | FU42, FU50 | Yok | P1 | L | Kullanıcı → entity'ye göre gez → aynı obje domain görünümünde de tek kayıt | 5 görünümün 2'si; duplikasyon riski | 5 görünüm tek kayıt üzerinden; document code ile tek adımda Effective |
| 20 | MOD-0029-FU57 | Retention & Legal Hold Dimension Completion | P0 §9 / URS-080, 081, 083, 084 | `DocumentLegalHoldEvaluator`, `DocumentRetentionEvaluator`, çift onaylı release | FU40, FU45 | **G-7** | **P0** | L | Legal → record-class scope'lu hold koy → tek sorguda tüm kapsam listelenir | `Repository`/`CustomQuery` scope'ları inert; hold archive yolunu tutmuyor | Inert scope kalmadı; hold tek sorguda raporlanıyor; archive yolları gate'li |
| 21 | MOD-0021-FU / MOD-0029 tüketici | Audit Export + Retention Binding + Redaction Kilidi | URS-092, 093, 094 | `AuditEvent.ValidateAppend`, `AuditEventRetentionPolicy` | FU39 | G-1 (redaction kapsamı) | P1 | M | Denetçi → audit export iste → open format, vendor müdahalesiz | Export yok; audit süresi subject'ten kısa | Export çalışıyor; audit ≥ subject retention; GxP'de redaction kapalı |
| 22 | MOD-0029-FU59 | API Contract + Retrieval Audit | URS-137, 138, 139 | Gateway route, `[HasPermission]`, MOD-0021 sink | FU39, FU50 | Yok | P1 | M | Entegratör → API ile ara → kendi izinleriyle sonuç → sorgu+dönen objeler audit'te | Blanket-read service account; retrieval iz bırakmıyor | OpenAPI yayında; her retrieval audit'te; blanket-read yok |
| 23 | MOD-0029-FU60 | SoR Enforcement + Copy Marking | URS-120, 121, 122 | `DocumentControlledCopy`, `ObsoleteCopy`, `SourceOfTruth` kolonu | FU40, FU42 | Yok | P1 | M | Kullanıcı → SoR'u başka sistem olan record class'a master yazmaya çalışır → reddedilir | Paralel master oluşur; export authoritative sanılır | Yazma kilidi aktif; her export "COPY" işaretli |
| 24 | MOD-0029-FU61 | Observability, Alerting & NFR Kanıtı | URS-131, 131a, 131c, 131d, 133 | `observability/`, `ICorrelationContext` | FU38, FU55 | Yok | P1 | L | Ops → ingestion hatası → alert düşer | 4 kritik olay sessiz; kapasite kanıtı yok | 4 alert aktif; p95<3sn, 50k/500GB/100 eşzamanlı ölçüldü; restore testi kanıtlı |
| 25 | MOD-0029-FU62 | Open-Format Tenant Export | URS-136 | FU43 path resolver, MOD-0021 audit export | FU43, FU58 | Yok | P2 | M | Yönetici → tenant export → metadata + audit açık formatta iner | Termination'da veri çıkarılamaz | Export metadata + audit içeriyor; dokümante format |
| 26 | MOD-0029-FU63 | File Size Limit Amend | P2-8 / URS-131b (amended) | — | Yok | Yok | P2 | S | Kullanıcı → 100 MB dosya yükle → kabul | 50 MB'da takılır | `MaxFileSizeBytes = 104_857_600`; 100 MB dosya PASS; checksum korunuyor |
| 27 | MOD-0029-FU64 | URS-ID ↔ Test Traceability Tooling | P0-16 / URS-143 | 860 mevcut test | Faz 1–4 | Yok | **P0** | M | CI → build → traceability matrisi üretilir | Matris elle tutulur, çürür | 103(+12) requirement ≥1 teste bağlı; matris CI çıktısı |

---

## 2. Faz 0 — Güvenlik, Runtime ve Governance Hazırlığı

> **Fazın çıkış koşulu:** Bu fazdan sonra sistemde koşulacak **negatif testlerin ve validation'ın delil değeri olmalıdır.** Bugün yoktur — `platform_admin` permission değerlendirmesini atladığı için her negatif test "ama admin zaten yapabiliyor" ile çürütülebilir.

### MOD-0029-FU38 — Runtime Blocker Kapatma

**Amaç.** Modül registry'sinde kayıtlı üç runtime hatasını kapatarak sistemin kanıt üretebilir hale gelmesini sağlamak.

**In Scope**
- Language 403 kök neden analizi ve düzeltme (RBAC seed mi, claim mi, route mu)
- Retention 500 kök neden ve düzeltme
- Controlled Documents list 500 kök neden ve düzeltme
- Üç endpoint için authenticated smoke script'i
- `execution/registries/module-implementation-status.md` üzerindeki `Runtime blocked` notunun kaldırılması

**Out of Scope**
- Yeni özellik; performans optimizasyonu; UI değişikliği
- Permission modelinin yeniden tasarımı (FU39/FU50)

**Golden Flow**
`Operatör -> authenticated GET /language, /retention, /controlled-documents -> yanıt -> 3 endpoint de 200 ve gövde dolu`

**Failure Path**
Endpoint 500 dönmeye devam eder; hiçbir smoke kanıtı üretilemez, dolayısıyla sonraki hiçbir FU'nun exit gate'i doğrulanamaz.

**Acceptance Criteria**
- *Runtime:* 3 endpoint authenticated çağrıda 200
- *Integrity:* Düzeltme veri bozmuyor; mevcut kayıtlar okunabilir
- *Authorization/Audit:* 403 alan endpoint için doğru permission key seed'li
- *Persistence:* Şema değişikliği varsa geriye dönük uyumlu
- *Tests:* Her hata için bir regresyon testi

**Bağımlılıklar.** Yok — hemen başlar.
**Parallelism.** `CAN PARALLELIZE WITH` Governance Workstream (§7).

---

### MOD-0029-FU39 — Regulated Authorization Hardening *(R-10 + R-11)*

**Amaç.** Regulated kaynaklarda administrative bypass'ı kaldırmak ve reddedilen her erişimi audit'e yazarak sonraki tüm negatif testleri delil üretebilir kılmak.

**SoR.** Karar semantiği **MOD-0018**; audit kaydı **MOD-0021** (tüketilir, kopyalanmaz); enforcement noktası MOD-0029 endpoint'leri.

**In Scope**
- `HasPermissionAttribute` içindeki `isPlatformActor` kısa devresinin **regulated resource sınıfları için** kaldırılması
- Regulated resource sınıfı tanımı (controlled document, quality record, legal hold, retention, audit) ve fail-closed varsayılan
- Ayrı, süreli, gerekçe zorunlu **break-glass** yolu — kendi permission key'i ile
- Enforcement filtresine audit sink: actor, permission key, resource, correlationId, `Outcome=Denied`
- Break-glass kullanımının ayrıca yüksek-önem audit kategorisiyle işaretlenmesi
- Negatif test seti: her regulated sınıf için admin ile deneme → 403 + audit

**Out of Scope**
- 32 PS-* profilinin tanımlanması (FU50)
- Permission modelinin metadata'ya taşınması (FU50)
- IdP/group entegrasyonu (FU52)
- Audit export (MOD-0021)

**Golden Flow**
`platform_admin -> completed quality record'a version upload dener -> istek authz filtresinde reddedilir -> 403 döner -> AuditEvent(Outcome=Denied, permissionKey, resourceId, correlationId) yazılır -> audit ekranından okunabilir`

**Failure Path**
Regulated sınıf listesi eksik kalır ve bir endpoint bypass'ta kalmaya devam eder → o endpoint üzerinden yapılan **her** negatif test geçersizdir. Bu nedenle exit gate "sınıf listesi tamdır" iddiasını da test eder.

**Acceptance Criteria**
- *Runtime:* Admin aktörüyle regulated endpoint çağrısı 403; break-glass ile süreli erişim mümkün
- *Integrity:* Bypass kaldırma mevcut meşru akışları kırmıyor (regresyon süiti yeşil)
- *Authorization/Audit:* Her 403 için tam olarak bir `AuditEvent(Denied)`; break-glass ayrıca işaretli
- *Persistence:* Audit kaydı MOD-0021 store'unda, append-only
- *Tests:* Regulated sınıf başına en az bir negatif test; break-glass süre aşımı testi

**Bağımlılıklar.** FU38 PASS.
**Parallelism.** `SERIAL ONLY` — authorization hot path'ine dokunan başka iş eşzamanlı planlanmaz.

---

## 3. Faz 1 — Metadata-Driven Document Foundation

> **Fazın amacı:** klasörün classification'ın **kendisi** olmaktan çıkması.
> **Fazın kritik özelliği:** ERP'de bugün **sıfır doküman** dosyalı. Bu refactor bugün ucuz, migration'dan sonra değil.

### MOD-0048-FU / MOD-0029-FU40 — Controlled Vocabulary Foundation *(R-04 + P1-15 + P1-26)*

**Amaç.** GMG'nin sekiz controlled vocabulary'sini kod enum'larından çıkarıp MOD-0048 reference data setlerine taşımak.

**SoR.** **MOD-0048** (Reference Data Management). MOD-0029 yalnızca **tüketici**dir.

**In Scope**
- Sekiz set: Zone (13), Entity (7), Domain (19), Function (15), Document Type (domain-scoped), Lifecycle State (6), Record Class (7), Permission Profile (32)
- Her entry için beş zorunlu nitelik: code, display name, owner, status, effective date (URS-011)
- Set versiyonlama + change control + audit (URS-012) — mevcut `BaselineRelease` deseni yeniden kullanılır
- LOG-0007 / Annex C'den import lane'i + on-demand reconcile (URS-014)
- Code uniqueness normalizasyonu: trim, case-insensitive, leading/trailing separator (URS-017)
- MOD-0029 tarafında enum hard-code'ların kaldırılıp BRD lookup'a çevrilmesi

**Out of Scope**
- Register'a zorunlu alan eklemek (FU42)
- Entity never-reassign / TYPE-domain kuralları (FU41)
- Permission profile **davranışı** (FU50) — burada yalnızca vocabulary olarak tanımlanır

**Golden Flow**
`Data steward -> Zone set'ine yeni entry ekle -> onayla ve publish et -> Master Register formunu yeniden yükle -> yeni Zone seçilebilir listede görünür -> kod deploy'u gerekmedi`

**Failure Path**
Set publish edilmeden MOD-0029 tüketmeye başlarsa doküman formu boş dropdown gösterir ve kayıt açılamaz. Bu yüzden tüketici tarafı **fail-closed + anlaşılır hata** vermeli, sessiz boş liste değil.

**Acceptance Criteria**
- *Runtime:* Sekiz set BRD'de published; MOD-0029 formları setlerden besleniyor
- *Integrity:* Aynı set içinde case/separator farkıyla çakışan kod 409
- *Authorization/Audit:* Set değişikliği change control'den geçiyor ve audit'e yazılıyor
- *Persistence:* Versiyonlu set; eski versiyon okunabilir
- *Tests:* URS-010/011/012/014/017 için birer test

**Bağımlılıklar.** FU39 PASS. **Governance:** G-6 (domain/function kodları), G-7 (record class), G-10 (entity modeli) kapalı olmalı.
**Parallelism.** `CAN PARALLELIZE WITH` FU54, FU63.

---

### MOD-0048-FU / MOD-0029-FU41 — Vocabulary Integrity Rules *(R-05 + R-06)*

**Amaç.** Entity kodlarının asla yeniden atanmamasını ve TYPE kodlarının yalnızca onaylı domain'lerinde geçerli olmasını sistemsel olarak garanti etmek.

**SoR.** **MOD-0048**.

**In Scope**
- Entity kodu: bir kez atanınca başka legal entity'ye asla atanmaz (URS-016)
- Retired veya kullanımdaki koda allocation girişimi → 409 (URS-016a)
- Entity status (active/retired) + effective date; tarihsel referansın ambiguity'siz çözülmesi (URS-016b)
- Domain-scoped TYPE geçerlilik matrisi (URS-015): `MAN`, `MTX`, `AGR`, `CHK`, `PLN`, `WIN`, `TPL`, `LOG` gibi kodlar domain başına
- Domain'de onaylı olmayan TYPE ile kayıt → 400
- Setonda → GMG İlaçları geçişinin bu kurallarla modellenebildiğinin doğrulanması

**Out of Scope**
- Vocabulary set'lerinin yaratılması (FU40)
- Migration'da bu kuralların uygulanması (FU55)

**Golden Flow**
`Steward -> retired entity kodu 20_Setonda_SL_ES'i yeni bir legal entity'ye atamayı dener -> 409 -> aynı kod eski kayıtlarda hâlâ çözümleniyor ve o entity'ye bağlı görünüyor`

**Failure Path**
Retired kod yeniden atanır; Setonda dönemine ait PV kayıtları yanlış legal entity'ye bağlanır — geri dönüşü zor bir veri bütünlüğü hatası.

**Acceptance Criteria**
- *Runtime:* Retired/in-use koda allocation 409; geçersiz TYPE-domain kombinasyonu 400
- *Integrity:* Tarihsel referanslar retired entity'ye bağlı kalır
- *Authorization/Audit:* Entity status değişimi audit'te, gerekçeli
- *Persistence:* Status + effective date kalıcı
- *Tests:* URS-015/016/016a/016b

**Bağımlılıklar.** FU40 PASS. **Governance:** G-6, G-10.
**Parallelism.** `SERIAL ONLY` (FU40 ile aynı aggregate).

---

### MOD-0029-FU42 — Document Classification Metadata + Held/Unclassified *(R-07 + P1-25 + P0-11)*

**Amaç.** Sınıflandırmayı klasörden alıp register kaydının zorunlu metadata'sı yapmak ve sınıflandırılamayan objenin default'a düşmesini engellemek.

**SoR.** **MOD-0029**, vocabulary'yi MOD-0048'den tüketir.

**In Scope**
- `DocumentMasterRegisterEntry` üzerine zorunlu alanlar: Zone, Entity, Domain, Function, Document Type, Record Class
- `DocumentClass = Other` ve `Criticality = Minor` **default'larının kaldırılması**
- `Unclassified` / `Held` register durumu + operasyonel kullanım engeli (URS-112)
- Free-text alanların BRD'ye bağlanması: `OwnerFunction`, `ProcessOwnerRole`, `RetentionClass`, `GoverningLanguage` (URS-013)
- Mevcut protected-fields korumasının yeni alanlara genişletilmesi
- UI: Master Register form + filtre alanlarının yeni boyutlarla güncellenmesi

**Out of Scope**
- Path türetme (FU43)
- Objenin taşınmaması garantisi (FU44)
- Permission'ın bu boyutlardan hesaplanması (FU50)
- Retention'ın bu boyutları kullanması (FU57)

**Golden Flow**
`Author -> yeni controlled document kaydı aç -> Domain'i boş bırak -> kaydet -> kayıt Held durumunda açılır -> Effective'e geçirmeyi dener -> engellenir -> Domain'i seç -> kayıt Active olur`

**Failure Path**
Zorunluluk eklenir ama `Held` operasyonel kullanımı bloklamazsa, sınıflandırmasız doküman normal dokümanmış gibi kullanılır — URS-112'nin tam ihlali. Bu yüzden gate testi "Held → Effective 409" olmalı.

**Acceptance Criteria**
- *Runtime:* Sınıflandırmasız kayıt `Held`; `Held` → Effective 409
- *Integrity:* Hiçbir alan default'a düşmüyor; mevcut kayıtlar için backfill planı belgeli
- *Authorization/Audit:* Classification değişimi audit'te before/after ile
- *Persistence:* Yeni alanlar indeksli; `Held` sorgulanabilir
- *Tests:* URS-013/066/112/113

**Bağımlılıklar.** FU40, FU41 PASS. **Governance:** G-6, G-7.
**Parallelism.** `SERIAL ONLY`.

---

### MOD-0029-FU43 — Metadata Path Resolver *(R-08 + P1-3)*

**Amaç.** Display ve export path'i metadata'dan türeterek klasör ağacının ERP'den üretilebilmesini sağlamak (URS-123'ün "generated" yolu).

**In Scope**
- Metadata → display path resolver
- Metadata → export path resolver, configurable root ile
- Generated absolute path >255 karakter uyarısı (ölçülen worst case 302)
- Export path segmentlerinde ASCII kısıtı; metadata ve display name'de tam Unicode
- Onaylı topoloji ağacının ERP'den üretilebildiğinin gösterilmesi
- *(Opsiyonel, URS-026 "S")* tam filesystem export iskeleti

**Out of Scope**
- Klasör ağacının fiilen emekli edilmesi (governance/ops kararı)
- Migration export'u (FU55)
- Tenant export (FU62)

**Golden Flow**
`Kullanıcı -> dokümanı aç -> display path metadata'dan türetilmiş görünür -> export path iste -> ASCII-güvenli, root'lu path üretilir -> 260 karakteri aşan senaryoda uyarı döner`

**Failure Path**
Resolver var ama uzunluk uyarısı yok → export gerçek dosya sisteminde patlar; IQ-07'nin ölçülen 302 karakterlik durumu üretimde hataya döner.

**Acceptance Criteria**
- *Runtime:* Path metadata'dan türetiliyor, klasörden okunmuyor
- *Integrity:* Aynı metadata her zaman aynı path'i üretiyor (deterministik)
- *Authorization/Audit:* Export path üretimi permission'a tabi
- *Persistence:* Path saklanmıyor, türetiliyor (veya cache invalidasyonu tanımlı)
- *Tests:* URS-021/024/025 + 302 karakter senaryosu

**Bağımlılıklar.** FU42 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU45, FU46 (farklı aggregate).

---

### MOD-0029-FU44 — Stable Classification Identity ⛔ *(R-09 — MIGRATION GATE)*

**Amaç.** Classification attribute'u değiştiğinde objenin taşınmadığını, kopyalanmadığını ve hiçbir referansın kırılmadığını garanti etmek.

**In Scope**
- Classification değişiminin storage objesini taşımaması/kopyalamaması (URS-022)
- Doküman `Guid` kimliğinin ve tüm iç referansların ömür boyu geçerliliği (URS-023)
- `ControlledDocument.CollectionInstanceId` / `CollectionPath` bağımlılığının **türetilmiş/opsiyonel** hale getirilmesi
- Klasör ağacının birincil yapı olmaktan çıkıp görünüme inmesi
- Mevcut linklerin (register ↔ document ↔ version ↔ signature ↔ hold) regresyon testi
- Backfill/uyum planının belgelenmesi

**Out of Scope**
- Migration lane'i (FU55) — **bu FU onun ön koşuludur**
- Permission'ın metadata'dan hesaplanması (FU50)
- Search görünümleri (FU56)

**Golden Flow**
`Author -> dokümanın Domain'ini 01_QMS'ten 04_PV'ye değiştir -> kaydet -> yeniden yükle -> aynı Guid, aynı storage objesi, aynı checksum -> tüm linkler (register, imza, hold) geçerli -> hiçbir kopya oluşmadı`

**Failure Path**
Attribute değişimi objeyi taşır veya kopyalar. Migration'dan **sonra** fark edilirse 1.236 promoted dosyanın yeniden sınıflandırılması gerekir. **Bu, planın ucuz şekilde geri alınamayan tek hatasıdır.**

**Acceptance Criteria**
- *Runtime:* Attribute değişimi sonrası doküman aynı `Guid` ile açılıyor
- *Integrity:* Storage objesi ve checksum değişmiyor; kopya sayısı sabit
- *Authorization/Audit:* Değişim audit'te before/after ile
- *Persistence:* Referans bütünlüğü korunuyor
- *Tests:* Her link tipi için kırılmazlık testi

**Bağımlılıklar.** FU42, FU43 PASS.
**Parallelism.** `SERIAL ONLY`. **⛔ FU55 bu FU kapanmadan başlatılamaz.**

---

## 4. Faz 2 — Controlled Document ve Quality Record Integrity

### MOD-0029-FU45 — QualityRecord Aggregate + Completed-Record Protection *(R-12 + R-13 + R-14)*

**Amaç.** Quality record'u controlled document'tan ayrı bir object class yapmak ve tamamlanmış kaydın hiçbir rol tarafından ezilememesini sağlamak.

**In Scope**
- Ayrı `QualityRecord` aggregate + repository + servis + permission ailesi
- `RecordState` (Open / Completed / Corrected) — controlled document lifecycle'ından **tamamen ayrı**
- Record kaydında `ControlledDocumentLifecycleStatus`'un **hiç yazılmaması** (bugün `Effective` yazılıyor)
- Completed record'a version upload / edit / delete engeli — **admin dahil** (FU39 üzerine oturur)
- Controlled correction / append-only yolu: orijinal içerik korunur, reason+actor+timestamp kaydedilir
- Zorunlu Record Class (FU40 vocabulary'sinden)
- Revizyon lifecycle'ı onaylı olmayan record tipinde version taşınmaması (URS-045)

**Out of Scope**
- Record class bazlı retention (FU57)
- Migration'da record'ların taşınması (FU55)
- GDocP correction'ın register alanları için mevcut davranışı (değişmiyor, genişletiliyor)

**Golden Flow**
`Originator -> quality record yarat -> doldur -> Completed işaretle -> aynı record'a yeni versiyon yüklemeyi dener -> 409 RECORD_COMPLETED -> controlled correction başlat -> gerekçe gir -> append olarak kaydedilir -> yeniden yükle -> orijinal içerik ve düzeltme birlikte görünür`

**Failure Path**
Overwrite engeli (R-13), controlled correction (R-14) olmadan sevk edilirse kullanıcı meşru bir düzeltme yapamaz ve engeli aşmak için kayıt silip yeniden yaratmaya yönelir — koruma, kendi amacının tersine çalışır. **Bu ikisi ayrı sevk edilemez.**

**Acceptance Criteria**
- *Runtime:* Completed record'a upload 409; correction yolu çalışıyor
- *Integrity:* Record'da CD lifecycle state'i asla set edilmiyor; orijinal içerik korunuyor
- *Authorization/Audit:* `platform_admin` dahil her rol için overwrite reddi + audit; correction audit'te
- *Persistence:* Append-only; hard delete yok
- *Tests:* Her rol için negatif overwrite testi (URS-042/073/144); URS-040/041/043/044/045

**Bağımlılıklar.** FU39, FU40, FU42 PASS. **Governance:** G-7 (record class listesi).
**Parallelism.** `CAN PARALLELIZE WITH` FU46 (farklı aggregate, farklı servis).

---

### MOD-0029-FU46 — Lifecycle Transition Hardening *(R-15 + R-16 + P1-27 + W-6)*

**Amaç.** Lifecycle geçişlerini transition başına yetkilendirmek, tek-Effective kuralını yarıştan korumak ve supersession'ı otomatik + atomik hale getirmek.

**In Scope**
- Transition başına permission key: `...lifecycle.review`, `.approve`, `.effective`, `.retire`, `.suspend` + fail-closed eşleme (MOD-0018 seed girdisi)
- Tek-Effective anahtar kararının netleştirilmesi (`DocumentCode` vs `PermanentUid`) ve **partial unique index** ile desteklenmesi
- Guard'ın kaynak state'ten bağımsız, **hedef-state bazlı** hale getirilmesi (`UnderRevision → Effective` yolu bugün guard'sız)
- Supersession'ın `RelatedReplacementRegisterEntryId` beklemeden **otomatik** çözülmesi
- Supersession + yeni Effective'in tek transaction/compensation kapsamında **atomik** yürütülmesi
- Effective geçişi için `EvidenceReference`'ın zorunlu kılınması
- Mevcut `ControlledDocumentLifecyclePolicy` matrisinin **korunması** (yeniden yazılmaz)

**Out of Scope**
- İmza gate'i (FU47 — hemen ardından)
- VOID state (FU48)
- Component cascade (FU49)
- Approval route mantığı (mevcut, değişmiyor)

**Golden Flow**
`QA Documentation -> v2 kaydını ApprovedPendingEffective'ten MarkEffective yap -> sistem aynı document code'un mevcut Effective v1'ini otomatik bulur -> v1 Superseded, v2 Effective aynı işlemde yazılır -> yeniden yükle -> tam bir Effective var, ledger iki transition kaydı taşıyor`

**Failure Path**
Supersession yazıldıktan sonra yeni Effective yazımı düşerse **sıfır Effective** kalır ve dokümanın yürürlükteki sürümü kaybolur. Compensation testi bu senaryoyu doğrudan kurgulamalıdır.

**Acceptance Criteria**
- *Runtime:* Yetkisiz transition 403; eşzamanlı iki MarkEffective'den biri 409
- *Integrity:* Kısmi hata sonrası Effective sayısı **asla 0 veya 2**; `UnderRevision → Effective` guard'lı
- *Authorization/Audit:* Her transition kendi permission'ı ile; ledger reason+actor+timestamp+evidence taşıyor
- *Persistence:* Partial unique index aktif ve Mongo uyumlu
- *Tests:* URS-032/033/034; race testi; compensation testi

**Bağımlılıklar.** FU38, FU39 PASS. **Girdi:** MOD-0018 permission seed'i.
**Parallelism.** `SERIAL ONLY` — lifecycle hot path.

---

### MOD-0029-FU47 — Signature Gate *(R-17)*

**Amaç.** Bir dokümanın uygulanmış elektronik imza olmadan Effective olamamasını sağlamak.

**Not.** Bu FU **G-1'e bağlı değildir**. Qualified-signature sağlayıcı yatırımı ayrıdır (§7, G-1) ve bu gate onu beklemez.

**In Scope**
- `ApplyMarkEffectiveGuardsAsync` içine imza şartı
- Şart: `SignatureStatus.Valid` + doğru `SignatureMeaning` + `ObjectFingerprint` eşleşmesi
- Fingerprint uyuşmazlığında `RequiresResign` davranışının gate'e yansıması
- 409 `SIGNATURE_REQUIRED` reason code'u
- UI'da eksik imza nedeninin görünür olması

**Out of Scope**
- Qualified signature sağlayıcı / sertifika doğrulama (G-1 sonrası)
- İçerik byte hash'ine geçiş (bugün metadata fingerprint'i)
- `NotValidated` beyanının kaldırılması — **korunur** (bkz. §6.2)

**Golden Flow**
`QA -> imzasız dokümanı MarkEffective yap -> 409 SIGNATURE_REQUIRED -> imza at (meaning: "Approved for effective release") -> MarkEffective tekrar -> Effective olur -> imza kayıtta fingerprint ile bağlı görünür`

**Failure Path**
İmza atıldıktan sonra doküman metadata'sı değişir, fingerprint uyuşmaz ve gate bunu görmezse geçersiz imzayla Effective release olur.

**Acceptance Criteria**
- *Runtime:* İmzasız MarkEffective 409
- *Integrity:* Fingerprint uyuşmayan imza gate'i geçemez
- *Authorization/Audit:* İmza + geçiş aynı correlationId ile audit'te
- *Persistence:* İmza append-only, geçişe bağlı
- *Tests:* URS-037; fingerprint drift testi

**Bağımlılıklar.** FU46 PASS.
**Parallelism.** `SERIAL ONLY` (FU46 ile aynı guard metodu).

---

### MOD-0029-FU48 — VOID State Resolution *(R-18)*

**Amaç.** Hiç Effective olmamış, geri çekilmiş içeriğin tek ve modellenmiş bir yola düşmesini sağlamak.

**In Scope**
- G-5 kararının uygulanması: ya ayrı `Void` state ya da `Retired` + zorunlu `RetirementReason=Void`
- Transition matrisine eklenmesi (`Draft`/`InReview`/`ApprovedPendingEffective` → hedef)
- Superseded ile ayrımının raporlanabilir kalması
- Migration'da VOID kayıtların bu yola bağlanabilmesi

**Out of Scope**
- Migration'da fiilen taşınması (FU55)
- Superseded/Retired ayrımının mevcut davranışı (değişmiyor)

**Golden Flow**
`QA -> InReview'daki bir dokümanı geri çek -> VOID yolunu seç, gerekçe gir -> kayıt tek modellenmiş duruma düşer -> raporda Superseded'den ayrı listelenir`

**Failure Path**
Karar uygulanmadan migration koşarsa VOID içerik `Retired` ile karışır ve ayrımı geri kazanmak elle veri düzeltme gerektirir.

**Acceptance Criteria**
- *Runtime:* VOID yolu çalışıyor, gerekçe zorunlu
- *Integrity:* VOID ≠ Superseded ≠ Retired, ayrı raporlanabilir
- *Authorization/Audit:* Ledger'da kayıtlı
- *Persistence:* Terminal durum
- *Tests:* URS-036

**Bağımlılıklar.** FU46 PASS. **Governance:** ⛔ **G-5 kapalı olmalı.**
**Parallelism.** `CAN PARALLELIZE WITH` FU49.

---

### MOD-0029-FU49 — Document Components / Annex *(R-19)*

**Amaç.** Bir controlled document'ın annex/figure/checklist gibi bileşenlerini modellemek ve parent ile ilişkisini kurala bağlamak.

**Not.** Mevcut `DocumentVariant` / `TemplateVariant` **çeviri/lokalizasyon** modelidir; component değildir ve **yeniden kullanılmaz, korunur**.

**In Scope**
- Ayrı `DocumentComponent` aggregate: parent-child ilişkisi
- Component'in parent'tan erişilebilir olması ve parent'ını tanıması (URS-051)
- Document type başına versioning kuralı: **with-parent** veya **independent** (URS-052)
- With-parent kuralında parent transition'ının component'i taşıması (URS-053)
- With-parent kuralında component'in bağımsız Effective olamaması (URS-054)
- 44 PV annex'inin (MAN-0001/MAN-0002) modellenebildiğinin gösterilmesi

**Out of Scope**
- Variant/lokalizasyon modeline dokunmak
- Migration'da annex'lerin taşınması (FU55)

**Golden Flow**
`Author -> GMG-PV-MAN-0001'e annex ekle (with-parent kuralı) -> parent'ı Effective yap -> yeniden yükle -> annex de Effective -> annex'i tek başına Effective yapmayı dener -> 409`

**Failure Path**
Versioning kuralı type başına ayarlanabilir değilse, bağımsız versiyonlanması gereken 44 annex parent'a kilitlenir veya tersi olur — ikisi de yanlış.

**Acceptance Criteria**
- *Runtime:* Parent transition component'i taşıyor; bağımsız Effective 409
- *Integrity:* Component parent'ını her zaman tanıyor
- *Authorization/Audit:* Cascade transition ledger'da görünür
- *Persistence:* Parent-child referansı stabil
- *Tests:* URS-050–054; 44 annex senaryosu

**Bağımlılıklar.** FU42, FU46 PASS. **Governance:** ⛔ **G-3 kapalı olmalı.**
**Parallelism.** `CAN PARALLELIZE WITH` FU48.

---

## 5. Faz 3 — Permission Modeli ve Migration

### MOD-0018-FU / MOD-0029-FU50 — Metadata-Based Permission Model + 32 PS-* Profil *(R-20 + R-22 + W-2 + W-3 + P1-16)*

**Amaç.** Effective permission'ı klasör adından değil, entity + zone + domain + record class kombinasyonundan hesaplamak ve A1'in 32 profilini tanımlamak.

**SoR.** **MOD-0018** (permission ownership); MOD-0029 tüketici.

**In Scope**
- A1'deki 32 PS-* profilinin FU40 vocabulary'sinde tanımlanması
- Mevcut 8 profilin (`GQMS-Controlled`, `Site-Controlled`…) PS-* setine map edilmesi veya emekli edilmesi
- Altı action'ın **bağımsız** ifadesi: read / create / modify / approve-transition / delete-dispose / external-share (URS-071)
- Effective permission'ın metadata kombinasyonundan hesaplanması (URS-072)
- `AccessProfileTemplateCatalog.ApplyStatusFolderRules` ve `ReadOnlyStatusFolders` / `RestrictedStatusFolders` string dizilerinin **kaldırılması**; read-only kararının `LifecycleStatus` alanından türetilmesi
- Approval-family matrix action'larının (`RequestApproval/Approve/Reject/Review`) inert placeholder'dan gerçek enforcement'a çevrilmesi
- Mevcut deny-precedence ve fail-safe default davranışının **korunması**

**Out of Scope**
- Compound/pass-through (FU51)
- IdP/group kaynağı (FU52)
- JML (FU53)
- `PS-MED-RESTRICTED` — A1'de `BLOCKED_TAXONOMY`, kapsam dışı

**Golden Flow**
`Kullanıcı (PS-GQMS-DOCUMENTS, entity 10_GMG_AG_CH) -> 04_PV domain'inde Effective bir SOP iste -> izin entity+zone+domain+record class'tan hesaplanır -> okuma açılır -> aynı kullanıcı 30_GM_Poland_PL entity'sindeki aynı tip dokümanı ister -> 404 non-leakage`

**Failure Path**
Status-folder kuralları kaldırılırken read-only davranışı `LifecycleStatus`'a doğru taşınmazsa, Effective dokümanlar **yazılabilir** hale gelir — sessiz ve tehlikeli bir gerileme. Bu yüzden exit gate'te "Effective doküman write action'ları reddedilir" testi zorunludur.

**Acceptance Criteria**
- *Runtime:* İzin klasör adından hesaplanmıyor; 32 profil tanımlı ve seçilebilir
- *Integrity:* Effective/Superseded dokümanda write action'ları reddediliyor (lifecycle'dan)
- *Authorization/Audit:* Her ret FU39 sink'iyle audit'te; deny-precedence korunuyor
- *Persistence:* Profil ataması metadata'da, klasörde değil
- *Tests:* URS-071/072; her profil için en az bir pozitif + bir negatif senaryo

**Bağımlılıklar.** FU39, FU40, FU42 PASS. **Governance:** ⛔ **G-8** (A1 tamamı `PENDING`).
**Parallelism.** `SERIAL ONLY` — authorization resolver.

---

### MOD-0018-FU / MOD-0029-FU51 — Atomic Profile + Pass-Through Container *(R-21)*

**Amaç.** Her lokasyonun tam olarak bir atomik profile çözülmesini sağlamak ve 54 compound lokasyonu pass-through container olarak tanımlamak.

**Doğrulanmış veri.** A3'te tam **54 satır** `Atomic Profile = NO — CANNOT BIND` taşıyor ve compound değer literal olarak `PS-GQMS-DOCUMENTS + PS-GQMS-RECORDS`. Bu **makine ile kontrol edilebilir** bir kuraldır.

**In Scope**
- Compound profil değerinin reddi (URS-071a): `" + "` içeren veya birden fazla profile çözülen değer → 400, hem register import'unda hem runtime'da
- **Pass-through container** node tipi (URS-071b): doğrudan filing kabul etmez, bağımsız erişim vermez
- A3'teki 54 lokasyonun pass-through olarak işaretlenmesi
- Import validator'ına atomic-profile kontrolü
- Pass-through container'a doküman ekleme denemesinin reddi

**Out of Scope**
- 32 profilin tanımlanması (FU50)
- `PS-MED-RESTRICTED` (BLOCKED_TAXONOMY)

**Golden Flow**
`Records Management -> A3 scope map'i import et -> 54 compound satır pass-through olarak işaretlenir -> bu lokasyonlardan birine doküman eklemeyi dener -> 400 PASS_THROUGH_NO_FILING -> alt lokasyona ekler -> başarılı`

**Failure Path**
Pass-through container bağımsız erişim vermeye devam ederse, Zone 02/03'ün zone root'ları ve entity container'ları üzerinden geniş yetki sızar.

**Acceptance Criteria**
- *Runtime:* Compound değer 400; pass-through'a filing 400
- *Integrity:* 54 lokasyonun tamamı işaretli, sayı doğrulanabilir
- *Authorization/Audit:* Pass-through hiçbir grant üretmiyor
- *Persistence:* Node tipi kalıcı
- *Tests:* URS-071a/071b; 54 satır sayım testi

**Bağımlılıklar.** FU50 PASS. **Governance:** ⛔ **G-11** (QA Documentation onayı).
**Parallelism.** `SERIAL ONLY`.

---

### MOD-0018-FU / MOD-0029-FU52 — IdP + Security Group + Named-User Kapatma *(R-23)*

**Amaç.** Erişimi rollere/gruplara bağlamak, A2'nin 115 security group'unu beslemek ve named-user istisnasını kapatmak.

**SoR.** **MOD-0018** + kimlik sağlayıcı.

**In Scope**
- OIDC/SAML ile kurumsal IdP entegrasyonu (URS-132)
- Directory group senkronu; `DocumentAccessPrincipalType.Group` placeholder'ının gerçek kaynağa bağlanması
- A2'deki 115 grubun provizyonlanabilir hale gelmesi
- `DocumentAccessPrincipalType.User` grant'ının controlled-document scope'unda **reddi** (URS-070)
- `DocumentShareRecord` üzerinden doğrudan kullanıcıya paylaşımın istisna rejimine alınması (expiry + approval — FU54 ile hizalı)
- Grup üyeliği değişiminin audit'e yazılması

**Out of Scope**
- JML süreç otomasyonu (FU53)
- External sharing governance (FU54)
- Grup üyeliklerinin doldurulması (operasyon; A2 `[TBC — populate from JML]`)

**Golden Flow**
`Yönetici -> kullanıcıyı GMG-REPO-GQMS-DOCUMENTS-GRP'ye ekle -> kullanıcı yeniden giriş yapar -> ilgili dokümanlar açılır -> yönetici aynı kullanıcıya named-user grant vermeyi dener -> 400 NAMED_USER_GRANT_FORBIDDEN`

**Failure Path**
Named-user grant kapatılır ama grup kaynağı çalışmazsa **hiç kimse erişemez**. Bu yüzden sıra: önce grup kaynağı çalışır, sonra named-user kapatılır — aynı FU içinde, feature flag ile aşamalı.

**Acceptance Criteria**
- *Runtime:* Grup üyeliğiyle erişim geliyor; named-user grant 400
- *Integrity:* 115 grup provizyonlanabiliyor
- *Authorization/Audit:* Üyelik değişimi audit'te
- *Persistence:* Grup binding kalıcı, IdP ile senkron
- *Tests:* URS-070/132; named-user negatif testi

**Bağımlılıklar.** FU50 PASS. **Governance:** ⛔ **G-8**.
**Parallelism.** `SERIAL ONLY`.

---

### MOD-0018-FU / MOD-0029-FU53 — JML + Periodic Access Review *(R-24)*

**Amaç.** Joiner/mover/leaver işlemlerini erişime bağlamak ve periyodik erişim gözden geçirmesini kanıtlı hale getirmek.

**In Scope**
- Leaver: ayrılışta erişimin kaldırılması (URS-075)
- Mover: rol değişiminde recertification zorunluluğu
- Periyodik access review kampanyası + attestation kaydı (URS-076)
- Review kanıtının dışa aktarılabilir olması
- A1'deki review sıklıklarının (annual / quarterly / on membership change) modellenmesi

**Out of Scope**
- HR sistemi entegrasyonunun kendisi (HCM tarafı)
- Grup kaynağı (FU52)

**Golden Flow**
`HR -> kullanıcıyı leaver işaretle -> gece senkronu -> kullanıcı giriş yapar -> hiçbir controlled document görünmez -> review kampanyası açılır -> owner attest eder -> kanıt kaydı üretilir`

**Failure Path**
Leaver işaretlenir ama grup üyeliği düşmezse ayrılan kişi erişimini korur — en klasik denetim bulgusu.

**Acceptance Criteria**
- *Runtime:* Leaver'da erişim sıfır; mover'da recertification isteniyor
- *Integrity:* Review kampanyası tüm kapsamı listeliyor
- *Authorization/Audit:* Attestation audit'te, aktör ve zaman ile
- *Persistence:* Review evidence kalıcı
- *Tests:* URS-075/076

**Bağımlılıklar.** FU52 PASS. **Governance:** G-8.
**Parallelism.** `CAN PARALLELIZE WITH` FU54, FU55.

---

### MOD-0029-FU54 — External Sharing Governance *(R-25 + P1-23)*

**Amaç.** Dış paylaşımı onaylı rotaya, süre sınırına ve kayda bağlamak; controlled document'ta fiziksel kopya üretimini kapatmak.

**In Scope**
- Zorunlu expiry — süresiz paylaşım imkânsız (URS-077)
- Onay rotası ve revoke
- External (organizasyon dışı) alıcı kavramı
- Her paylaşımın kaydı ve raporlanabilirliği
- `DocumentShareMode.CopyOnAdopt`'un controlled document'lar için kapatılması; `Reference` modunun zorunlu kılınması (URS-101)
- `FolderShareOutcomeStatus.Copied` yolunun controlled document kapsamından çıkarılması

**Out of Scope**
- Template paylaşımı (mevcut davranış korunur)
- Controlled copy yönetimi (mevcut, güçlü — dokunulmaz)

**Golden Flow**
`Owner -> dokümanı dış alıcıyla paylaş -> expiry ve onay zorunlu -> onay sonrası link üretilir -> süre dolar -> alıcı erişemez -> paylaşım kaydı raporda görünür`

**Failure Path**
`CopyOnAdopt` kapatılırken mevcut kopyalanmış dokümanların lineage'ı (`CopiedFromDocumentId`) kaybolursa geçmiş izlenemez hale gelir.

**Acceptance Criteria**
- *Runtime:* Expiry'siz paylaşım 400; süre dolunca erişim yok
- *Integrity:* Controlled document'ta yeni fiziksel kopya üretilmiyor; mevcut lineage korunuyor
- *Authorization/Audit:* Her paylaşım ve revoke audit'te
- *Persistence:* Share record kalıcı
- *Tests:* URS-077/101

**Bağımlılıklar.** FU50 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU53, FU56.

---

### MOD-0029-FU55 — Document Migration Lane ⛔ *(R-26 + R-27 + P1-12 + P1-13)*

**Amaç.** Annex B manifest'ini hash doğrulamalı, disposition'lı, reconcile edilebilir ve geri alınabilir biçimde sisteme almak.

**⛔ HARD DEPENDENCY: FU44 (Stable Classification Identity) kapanmadan başlatılamaz.**
Bugün ERP'de sıfır doküman var; W-1 refactoru bu yüzden bedavaya yakın. Migration'dan sonra aynı refactor **1.236 promoted dosyanın** yeniden sınıflandırılmasıdır. Projede bu ekonomi iki kez ölçüldü: Setonda→İlaçları 271 path'i bedavaya taşıdı, v0.32 eklemeleri 75 dosyayı önemsizce serbest bıraktı — her ikisi de hiçbir şey dosyalanmamış olduğu için.

**In Scope**
- Annex B manifest ingest (1.631 kayıt: 1.236 promoted / 330 held / 65 staged)
- `ExpectedSourceHash` alanı + **post-ingest** doğrulama; mismatch'in kalem kalem raporlanması (URS-110)
- Promoted / Held / Staged disposition state modeli; 330 HELD'in gerekçesiyle held kalması
- Sınıflandırması belirsiz kaydın `Held`'e düşmesi, **default atanmaması** (URS-113 — FU42 üzerine oturur)
- Estate genelinde hash duplicate detection + **explicit disposition** talebi (25 grup byte-identical — URS-064)
- OS metadata artefact exclusion (140 dosya) + excluded-report; sayımların reconcile etmesi (URS-065)
- Doküman düzeyi reconciliation raporu — mevcut `CollectionTreeReconciliationEngine` deseni genişletilir (URS-111)
- Migration set kimliği + set-level rollback / prior-state restore (URS-114)
- Kaynak estate'in salt-okunur kalması (URS-115) — mevcut "never mutates" sözleşmesi korunur
- `IdempotencyKey` saga deseninin yeniden kullanımı

**Out of Scope**
- Klasör baseline import'u (mevcut, değişmiyor)
- Superseded/Retired kararı (G-4) ve AUDIT branch kararı (G-9) — **girdi**, bu FU'nun işi değil
- Yeni storage provider

**Golden Flow**
`Records Management -> Annex B manifest yükle -> dry-run -> 1.236 promoted / 330 held / 65 staged özeti + 25 duplicate grubu + 140 OS artefact raporu görünür -> commit -> her obje ingest sonrası hash doğrulanır -> reconcile raporu sıfır beklenmeyen sapma gösterir -> kaynak estate salt-okunur kalır`

**Failure Path**
Hash doğrulama **ingest öncesi** yapılıp sonrası yapılmazsa, transfer sırasında bozulan bir dosya doğrulanmış sayılır. URS-110 açıkça "shall verify each hash **after** ingestion" diyor — test bunu ayırt etmelidir.

**Acceptance Criteria**
- *Runtime:* 1.631 kayıt dry-run + commit; rakamlar Annex B ile birebir
- *Integrity:* Post-ingest hash mismatch kalem kalem; HELD sessizce çözülmüyor; default classification yok
- *Authorization/Audit:* Migration işlemi audit'te; disposition kararları kayıtlı
- *Persistence:* Set kimliği kalıcı; rollback prior-state'i geri getiriyor; kaynak estate değişmemiş
- *Tests:* URS-064/065/110/111/112/113/114/115

**Bağımlılıklar.** ⛔ **FU44**, ayrıca FU42, FU45 PASS. **Governance girdisi:** G-4, G-9.
**Parallelism.** `SERIAL ONLY`.

---

## 6. Faz 4 — Search, Retention, Legal Hold, API ve NFR

### MOD-0029-FU56 — Metadata Search & Navigation Views *(R-29 + P1-4 + P1-5)*

**Amaç.** Aynı objeyi beş sınıflandırma boyutundan, duplikasyon üretmeden gezilebilir kılmak.

**In Scope**
- Her classification attribute'u ve kombinasyonu üzerinde arama (URS-100)
- Beş navigasyon görünümü: Entity / Domain / Type / Lifecycle / Record Class — tek kayıt üzerinden (URS-101)
- Document code ile **tek adımda** current-Effective retrieval: `GET /document-master-register/by-code/{code}/effective` (URS-103)
- Mevcut permission-filtered search ve non-leakage 404 disiplininin **korunması**
- Saved view desteği

**Out of Scope**
- Full-text içerik araması
- AI/RAG retrieval (FU59 kuralları altında, ayrı)

**Golden Flow**
`Kullanıcı -> Domain görünümünde 04_PV'yi seç -> dokümanı gör -> Entity görünümüne geç -> aynı doküman aynı Guid ile görünür -> ikinci bir kopya yok -> document code ile Effective sürümü tek çağrıda al`

**Failure Path**
Görünümler ayrı read-model'lere yazılırsa aynı obje iki kayıt gibi görünür — URS-101'in yasakladığı duplikasyon, bu kez veritabanında.

**Acceptance Criteria**
- *Runtime:* 5 görünüm çalışıyor; tek adımda Effective retrieval
- *Integrity:* Aynı obje tüm görünümlerde tek kayıt
- *Authorization/Audit:* Sonuçlar permission-filtered; yetkisiz kayıt varlığı ifşa edilmiyor
- *Persistence:* Read-model tutarlı
- *Tests:* URS-100/101/102/103

**Bağımlılıklar.** FU42, FU50 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU57, FU59, FU60.

---

### MOD-0029-FU57 — Retention & Legal Hold Dimension Completion *(R-30 + R-31 + P1-9)*

**Amaç.** Retention ve legal hold'a eksik olan classification boyutlarını (record class, entity) kazandırmak ve inert hold scope'larını kapatmak.

**Not.** Mevcut **çift onaylı hold release** (Legal + GQD) ve **"no destruction engine"** ilkesi GMG hedefinden daha katıdır ve **korunur**.

**In Scope**
- `LegalHoldScopeType.Repository` ve `CustomQuery` scope'larının **uygulanması veya açıkça reddi** (bugün `=> false` döndürüyor, sessizce engellemiyor)
- Entity / Domain / Record Class ekseninde hold scope'u (URS-080)
- Hold kapsamındaki her objenin **tek sorguda** raporlanması (URS-083)
- Hold gate'inin archive / soft-delete yollarına genişletilmesi (URS-081)
- Retention policy anahtar setine Record Class + Entity boyutları (URS-084)
- "Longest applicable requirement" davranışının korunması

**Out of Scope**
- Disposition approval akışı (mevcut, çalışıyor)
- Destruction engine — **eklenmeyecek** (URS-086 ile birebir)
- Audit retention bağı (MOD-0021, FU58)

**Golden Flow**
`Legal -> "Record Class = Batch Record, Entity = 50_GMG_Ilaclari_LTD_TR" scope'lu hold aç -> aktive et -> tek sorguda kapsam listelenir -> kapsamdaki bir kaydı archive etmeyi dener -> engellenir -> hold release: Legal onayı + GQD concurrence ister`

**Failure Path**
`Repository`/`CustomQuery` scope'ları inert kalırsa, o scope ile açılan bir hold **sessizce hiçbir şeyi korumaz** — bir hold sweep'in en tehlikeli hatası budur.

**Acceptance Criteria**
- *Runtime:* Hiçbir scope tipi sessizce inert değil; tek sorgulu hold raporu çalışıyor
- *Integrity:* Hold aktifken archive/soft-delete/disposition engelli
- *Authorization/Audit:* Hold kararları evidence reference ile; çift onay korunuyor
- *Persistence:* Scope tanımı kalıcı; retention record class + entity'den çözülüyor
- *Tests:* URS-080/081/083/084

**Bağımlılıklar.** FU40, FU45 PASS. **Governance:** ⛔ **G-7** (record class + retention takvimleri).
**Parallelism.** `CAN PARALLELIZE WITH` FU56, FU59.

---

### MOD-0021-FU — Audit Export + Retention Binding + Redaction Kilidi *(R-32)*

**Amaç.** Audit trail'i vendor müdahalesi olmadan dışa aktarılabilir kılmak, saklama süresini konusu olan kayda bağlamak ve GxP kapsamında redaction'ı kilitlemek.

**SoR.** ⚠️ **MOD-0021** — registry: *"Canonical owner of audit trail. No other module may claim this ID."* **Bu iş MOD-0029 içine yazılmaz.**

**In Scope**
- Audit export endpoint'i, açık ve dokümante format (URS-094)
- Audit retention'ın konusu olan kaydın retention'ına bağlanması: audit süresi ≥ subject süresi (URS-093)
- `RedactionStatus` / `RedactedByActorId` yolunun GxP kapsamındaki kayıtlar için kilitlenmesi veya meta-audit ile ikinci kayıt zorunluluğu (URS-092)
- Mevcut `AuditEvent.ValidateAppend()` immutability garantisinin korunması

**Out of Scope**
- MOD-0029 tarafında ayrı bir audit store — **kesinlikle hayır**
- Retrieval audit (FU59)

**Golden Flow**
`Denetçi -> audit export iste (tarih + kapsam) -> açık formatta dosya iner -> kayıtlar değiştirilemez -> GxP kapsamındaki bir kaydı redact etmeyi dener -> reddedilir veya meta-audit ile ikinci kayıt üretir`

**Failure Path**
Redaction GxP kapsamında açık kalırsa URS-092'nin "hiçbir rol düzenleyemez/silemez" mutlak ifadesi karşılanmaz ve audit trail'in delil değeri tartışmaya açılır.

**Acceptance Criteria**
- *Runtime:* Export çalışıyor, vendor müdahalesiz
- *Integrity:* Audit ≥ subject retention; append-only korunuyor
- *Authorization/Audit:* Redaction girişimi kendisi audit'e giriyor
- *Persistence:* Export tekrarlanabilir
- *Tests:* URS-092/093/094

**Bağımlılıklar.** FU39 PASS. **Governance:** G-1 (redaction kapsam sınırı).
**Parallelism.** `CAN PARALLELIZE WITH` tüm MOD-0029 işleri (farklı modül).

---

### MOD-0029-FU59 — API Contract + Retrieval Audit *(R-33 + P1-19 + P1-20)*

**Amaç.** Dış tüketiciler için dokümante API yayınlamak ve her programatik retrieval'ı çağıranın kimliğiyle audit'e yazmak.

**In Scope**
- OpenAPI / versiyonlu dış contract yayını (URS-137)
- Her retrieval'ın çağıran kullanıcı + sorgu + **dönen objeler** ile audit'e yazılması (URS-139) — MOD-0021 sink'i tüketilir
- Service account blanket-read yasağının doğrulanması (URS-138) — FU39 üzerine oturur
- AI/RAG retrieval eklenirse uygulanacak kuralın contract'ta yazılı olması: her zaman çağıran kullanıcının efektif izinleriyle
- Rate limit / kötüye kullanım koruması

**Out of Scope**
- AI/RAG retrieval yolunun kendisi (bugün yok; eklenirse ayrı FU, P0 kurallarıyla)
- Yeni endpoint ailesi

**Golden Flow**
`Entegratör -> API token ile doküman ara -> yalnızca kendi izin kapsamındaki sonuçlar döner -> AuditEvent(kullanıcı, sorgu, dönen obje id'leri) yazılır -> audit ekranından okunabilir`

**Failure Path**
Retrieval audit'i "dönen objeler"i kaydetmezse, bir veri sızıntısı sonrası **neyin okunduğu** tespit edilemez — URS-139'un varlık sebebi tam olarak budur.

**Acceptance Criteria**
- *Runtime:* OpenAPI yayında; API çağıranın izinleriyle çalışıyor
- *Integrity:* Blanket-read service account yok
- *Authorization/Audit:* Her retrieval kullanıcı + sorgu + dönen objelerle audit'te
- *Persistence:* Audit MOD-0021'de
- *Tests:* URS-137/138/139

**Bağımlılıklar.** FU39, FU50 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU56, FU57, FU60.

---

### MOD-0029-FU60 — SoR Enforcement + Copy Marking *(R-35 + P1-29)*

**Amaç.** Authoritative sistemi başka modül olan record class'larda paralel master oluşmasını engellemek ve her export'un kopya olduğunu işaretlemek.

**In Scope**
- Record class başına authoritative sistem bilgisi (URS-121) — bugün yalnızca klasörde `SourceOfTruth`, açıklayıcı
- SoR başka sistem olduğunda yerel master yazımının reddi; yalnızca link veya read-only view (URS-120)
- Her export çıktısına "COPY — not the authoritative record" işareti (URS-122)
- Mevcut `DocumentControlledCopy` / `ObsoleteCopy` yapısının **korunması** ve yeniden kullanımı

**Out of Scope**
- ERP/CRM/HRIS/RIM sistemlerine canlı connector geliştirme (ayrı entegrasyon işi)
- External document register (mevcut, çalışıyor)

**Golden Flow**
`Kullanıcı -> SoR'u HRIS olan bir record class'ta yerel master yaratmayı dener -> reddedilir, link önerilir -> mevcut kaydı export eder -> çıktı "COPY" işaretli iner`

**Failure Path**
Copy marking yalnızca controlled copy nesnesine uygulanır, genel export'lara uygulanmazsa, dışarı çıkan bir PDF authoritative sanılır.

**Acceptance Criteria**
- *Runtime:* SoR başka sistemse yerel master yazımı 400
- *Integrity:* Paralel master oluşmuyor
- *Authorization/Audit:* Export işlemi audit'te
- *Persistence:* Record class → SoR eşlemesi kalıcı
- *Tests:* URS-120/121/122

**Bağımlılıklar.** FU40, FU42 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU56, FU59.

---

### MOD-0029-FU61 — Observability, Alerting & NFR Kanıtı *(R-34 kısmi + P1-17/21/22)*

**Amaç.** Dört kritik olay için alerting kurmak ve NFR iddialarını ölçülmüş kanıta bağlamak.

**In Scope**
- Alert kuralları: failed ingestion, failed backup, failed transition, permission change (URS-131d)
- Batch ingestion lane + throughput ölçümü: ≥1.000 obje/saat, hash doğrulamalı (URS-131c)
- Retrieval p95 < 3 sn ölçümü (URS-131a)
- Kapasite ölçümü: 50k obje / 500 GB / 100 eşzamanlı kullanıcı (URS-133)
- DR/RPO/RTO: RPO ≤ 4 saat, RTO ≤ 8 saat; yıllık restore testi **kanıtının toplanması** (URS-131)
- Mevcut `observability/` ve `ICorrelationContext` altyapısının yeniden kullanımı

**Out of Scope**
- DR prosedürünün kendisi (ops sorumluluğu — burada yalnızca kanıt)
- Yeni monitoring stack

**Golden Flow**
`Ops -> ingestion job'ı kasten başarısız kıl -> alert 5 dakika içinde düşer -> alert correlationId taşır -> ilgili audit kaydına gidilebilir`

**Failure Path**
Alert kurulur ama correlationId taşımazsa, uyarıdan kök nedene gidiş elle log taramasına düşer.

**Acceptance Criteria**
- *Runtime:* 4 alert aktif ve tetikleniyor
- *Integrity:* Ölçümler tekrarlanabilir ve belgeli
- *Authorization/Audit:* Permission change alert'i FU39 sink'ini kullanıyor
- *Persistence:* Metrik geçmişi saklanıyor
- *Tests:* URS-131/131a/131c/131d/133 için ölçüm kanıtı

**Bağımlılıklar.** FU38, FU55 PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU56, FU57, FU59, FU60.

---

### MOD-0029-FU62 — Open-Format Tenant Export *(URS-136)*

**Amaç.** Sözleşme sonlanmasında metadata ve audit trail dahil, açık formatta tam veri çıkışı sağlamak.

**In Scope**
- Tenant kapsamında tam export: doküman metadata + register + lifecycle ledger
- MOD-0021 audit export'unun (FU58) çıktıya dahil edilmesi
- Açık, dokümante format (JSON/CSV)
- FU43 path resolver ile klasör topolojisinin isteğe bağlı yeniden üretimi

**Out of Scope**
- Binary içerik arşivleme stratejisi (storage tarafı, ayrı)

**Golden Flow**
`Yönetici -> tenant export başlat -> metadata + audit + ledger açık formatta iner -> üçüncü taraf araçla okunabilir`

**Failure Path**
Export metadata'yı içerir ama audit trail'i içermezse URS-136 karşılanmaz ve çıkış "eksik kopya" olur.

**Acceptance Criteria**
- *Runtime:* Export tamamlanıyor
- *Integrity:* Metadata + audit birlikte, tutarlı
- *Authorization/Audit:* Export yüksek yetkili ve audit'li
- *Persistence:* Tekrarlanabilir
- *Tests:* URS-136

**Bağımlılıklar.** FU43, MOD-0021 audit export (FU58) PASS.
**Parallelism.** `CAN PARALLELIZE WITH` FU61.

---

### MOD-0029-FU63 — File Size Limit Amend *(P2-8)*

**Amaç.** `MaxFileSizeBytes` sabitini amended URS-131b hedefine (≥100 MB) çıkarmak.

**In Scope**
- `MaxFileSizeBytes` 52_428_800 → 104_857_600
- 100 MB dosyayla uçtan uca yükleme testi
- Base64 bellek davranışının ölçülmesi (~133 MB beklenir)

**Out of Scope**
- ⛔ **Streaming / multipart refactor — İPTAL.** Gerekçe: URS-131b ≥500 MB'dan ≥100 MB'a amend edildi; estate ölçümünde en büyük dosya 16 MB, 50 MB üstü sıfır dosya. Bkz. gap analizi v2 §0b/S-DEF-1 ve W-8.

**Golden Flow**
`Kullanıcı -> 100 MB docx yükle -> kabul edilir -> indir -> checksum aynı, içerik dönüştürülmemiş`

**Failure Path**
Limit yükseltilir ama bellek profili ölçülmezse eşzamanlı yüklemelerde servis baskı altında kalır.

**Acceptance Criteria**
- *Runtime:* 100 MB dosya PASS
- *Integrity:* Checksum korunuyor; içerik dönüştürülmüyor
- *Authorization/Audit:* Değişiklik yok
- *Persistence:* Storage gateway limiti hizalı
- *Tests:* URS-131b (amended)

**Bağımlılıklar.** Yok — herhangi bir sprint'e sığar.
**Parallelism.** `CAN PARALLELIZE WITH` her şey.

---

## 7. Faz 5 — Validation / Qualification / Release Readiness

> **Ön koşul:** FU39 (admin bypass) kapanmadan bu fazın **hiçbir çıktısı delil değeri taşımaz.** Bugün IQ/OQ/UAT baştan sona yürütülse bile sonuç boş olurdu.

### MOD-0029-FU64 — URS-ID ↔ Test Traceability Tooling *(R-36)*

**Amaç.** Her URS requirement'ını en az bir yürütülen teste bağlayan matrisi otomatik üretmek.

**In Scope**
- Mevcut 860 test metodunun URS-ID ile etiketlenmesi (attribute/trait)
- Yeni testlerde etiketin zorunlu kılınması
- CI'da traceability matrisi üretimi
- Etiketsiz requirement'ın build uyarısı üretmesi
- Annex D formatıyla uyumlu çıktı

**Out of Scope**
- IQ/OQ/UAT protokollerinin yürütülmesi (aşağıdaki workstream)

**Golden Flow**
`CI -> build -> traceability matrisi üretilir -> her URS-ID'nin bağlı testleri ve sonuçları listelenir -> Annex D ile karşılaştırılabilir`

**Failure Path**
Matris elle tutulursa ilk sprintte çürür — v1'de Annex D'nin 103 yerine 101 satır taşıması tam olarak bu yüzden oldu.

**Acceptance Criteria**
- *Runtime:* Matris CI çıktısı olarak üretiliyor
- *Integrity:* 103 (+§7b kabul edilirse 115) requirement'ın her biri ≥1 teste bağlı
- *Authorization/Audit:* —
- *Persistence:* Matris versiyonlanıyor
- *Tests:* Tooling'in kendi testi

**Bağımlılıklar.** Faz 1–4 büyük ölçüde PASS.
**Parallelism.** `CAN PARALLELIZE WITH` her şey (etiketleme aşamalı yapılabilir).

### Validation Workstream (kod teslimi değil)

| İş | Bağımlılık | Çıktı |
|---|---|---|
| IQ yürütme (IQ-01…IQ-07, absolute path uzunluğu dahil) | FU43 | 7 IQ executed, named executor + independent review |
| OQ yürütme (OQ-01…OQ-04) | FU39, Faz 2, Faz 3 | 4 OQ executed |
| Negatif permission testing — **32 profilin her biri** | FU50, FU51, FU52 | 32 profil için negatif senaryo executed (URS-144) |
| Completed-record overwrite negatif testi | FU45 | Her rol için executed |
| Migration reconciliation | FU55 | Doküman düzeyi reconcile raporu |
| Runtime smoke + release readiness | Tümü | Uçtan uca smoke PASS |

---

## 8. Governance Workstream

Bunlar **kod backlog'u değildir.** Karar üretmeden ilgili FU'lar başlatılmamalıdır.

| Karar ID | Karar | Sahip | Hangi işleri blokluyor | Kod başlamadan gerekli mi? | Önerilen karar çıktısı |
|---|---|---|---|---|---|
| **G-1** | GxP sınıflandırması; Annex 11 / Part 11 uygulanabilirliği **record class başına** | Group Quality Director + CSV | Qualified-signature **harcaması**; MOD-0021 redaction kapsamı | **Kısmen.** FU47 (signature gate) **beklemez**. Yalnızca sağlayıcı yatırımı bekler. | Her record class için "Part 11 uygulanır/uygulanmaz" yazılı kararı. Kapsam dışı sınıflarda mevcut `NotValidated` beyanı **meşru kalır ve korunur**. |
| ~~G-2~~ | ~~Go-live'da hangi sistem authoritative~~ | GQD / IT / Records Mgmt | — | — | **KAPANDI: ERP authoritative.** Türev: URS-123 canlı; FU43 P0; FU44 yetkilendirildi. |
| **G-3** | Component modeli: parent ile mi bağımsız mı versiyonlanacak | QA Documentation | FU49 | **Evet** | Document type başına versioning kuralı tablosu; 44 PV annex'inin hangi kurala düştüğü. |
| **G-4** | 141 dosya için Superseded vs Retired | QA Documentation | FU55 (girdi) | **Evet** (migration öncesi) | Dosya bazında disposition listesi. |
| **G-5** | VOID modellenmiş state mi, Retired'a mı map edilecek | QA Documentation | FU48 | **Evet** | Tek cümlelik karar + etkilenen kayıt listesi. |
| **G-6** | Domain/function kod onayı (LOG-0006 hiç yazılmamış; 22 branch onaysız) | GQD / GRA / QPPV | FU40, FU41, FU42 | **Evet — kritik yol** | Onaylı 19 domain + 15 function kod listesi, effective date ile. |
| **G-7** | Record class'lar ve class bazlı retention takvimleri | Records Management / Legal | FU40, FU42, FU45, FU57 | **Evet — kritik yol** | 7 record class + her biri için retention süresi ve tetikleyici. |
| **G-8** | IdP, security group modeli, 32 profil için membership approver | IT / ISM | FU50, FU51, FU52, FU53 | **Evet** | IdP seçimi + A1 status `PENDING` → `APPROVED` + A2 membership approver'ları. |
| **G-9** | AUDIT branch disposition'ı | QA Documentation | FU55 (girdi) | **Evet** (migration öncesi) | AUDIT branch'in hedefi veya kapsam dışı bırakılması. |
| **G-10** | Entity modeli (Setonda → GMG İlaçları; Setonda PV kayıtlarının akıbeti) | Legal / Finance / HR | FU40, FU41 | **Evet — kritik yol** | 7 entity kodu + status + Setonda PV kayıtlarının hedefi. |
| **G-11** | 54 compound permission profile / pass-through container | QA Documentation | FU51 | **Evet** | URS-071b önerisinin onayı: 54 lokasyon pass-through, filing yok, bağımsız grant yok. |

**Kritik gözlem.** Vocabulary zincirini (FU40 → FU41 → FU42) **üç karar** birden blokluyor: **G-6, G-7, G-10**. Bunlar kritik yolun başındadır ve en erken kapatılması gerekenlerdir. Buna karşılık **FU38, FU39, FU46, FU47, FU63** hiçbir governance kararına bağlı değildir — proje "governance bekliyor" diye durdurulamaz.

---

## 9. Roadmap Özeti

```
Faz 0  → FU38 Runtime Blocker
       → FU39 Regulated Authorization Hardening        [delil geçerliliği kurulur]
       → (paralel) Governance Workstream: G-6, G-7, G-10 öncelikli
Faz 1  → FU40 Controlled Vocabulary Foundation
       → FU41 Vocabulary Integrity Rules
       → FU42 Document Classification Metadata + Held
       → FU43 Metadata Path Resolver
       → FU44 Stable Classification Identity           [⛔ MIGRATION GATE]
Faz 2  → FU45 QualityRecord + Completed-Record Protection
       → FU46 Lifecycle Transition Hardening
       → FU47 Signature Gate
       → FU48 VOID Resolution (G-5)
       → FU49 Document Components / Annex (G-3)
Faz 3  → FU50 Metadata-Based Permission Model + 32 PS-* (G-8)
       → FU51 Atomic Profile + Pass-Through (G-11)
       → FU52 IdP + Security Group + Named-User Kapatma (G-8)
       → FU53 JML + Access Review
       → FU54 External Sharing Governance
       → FU55 Document Migration Lane                  [⛔ FU44 sonrası]
Faz 4  → FU56 Metadata Search & Navigation
       → FU57 Retention & Legal Hold Dimensions (G-7)
       → MOD-0021-FU Audit Export + Retention + Redaction
       → FU59 API Contract + Retrieval Audit
       → FU60 SoR Enforcement + Copy Marking
       → FU61 Observability & NFR Kanıtı
       → FU62 Open-Format Tenant Export
       → FU63 File Size Amend
Faz 5  → FU64 URS-ID ↔ Test Traceability Tooling
       → IQ → OQ → 32-profil negatif testi → UAT → Release Readiness
```

### Critical Path

Gerçekten birbirine bağlı tek zincir:

```
G-6 / G-7 / G-10 governance kararları
  → FU40 Controlled Vocabulary
  → FU41 Vocabulary Integrity Rules
  → FU42 Document Classification Metadata
  → FU43 Metadata Path Resolver
  → FU44 Stable Classification Identity
  → FU55 Document Migration Lane
  → FU61 NFR Kanıtı  →  Faz 5 Validation
```

Buna **paralel yürüyen ikinci zorunlu zincir** (delil geçerliliği):

```
FU38 Runtime Blocker
  → FU39 Regulated Authorization Hardening
  → (Faz 2 ve Faz 3'ün tüm negatif testleri buna dayanır)
  → FU50 → FU51 / FU52 → FU53
  → Faz 5 OQ + 32-profil negatif testi
```

### Parallel Work

Kritik yola dokunmadan bağımsız yürüyebilecek işler:

| İş | Neden bağımsız |
|---|---|
| **FU63** File Size Amend | Tek sabit; hiçbir aggregate'e dokunmuyor |
| **MOD-0021-FU** Audit Export | Farklı modül, farklı store |
| **FU45** QualityRecord | FU46'dan farklı aggregate ve servis |
| **FU48** VOID / **FU49** Components | Birbirinden bağımsız; G-5 ve G-3 ayrı kararlar |
| **FU53** JML / **FU54** External Sharing | FU52 sonrası birbirinden bağımsız |
| **FU56, FU59, FU60, FU61** | Faz 4 içinde farklı yüzeyler |
| **FU64** test etiketleme | Aşamalı, her faz sonunda genişletilebilir |
| Governance Workstream | Kod fazlarından tamamen ayrı |

### Do Not Start Yet

Şu anda başlanırsa **rework yaratacak** işler:

| İş | Neden şimdi değil |
|---|---|
| **FU55 Document Migration Lane** | ⛔ FU44 kapanmadan koşarsa 1.236 promoted dosya klasör-merkezli modele yazılır; refactor bugün bedava, sonra 1.236 dosyanın yeniden sınıflandırılması. **Planın ucuz şekilde geri alınamayan tek kararı.** |
| **FU50/FU51/FU52 Permission modeli** | G-8 açık ve A1'in tamamı `PENDING`; onaysız profil setine kod yazmak iki kez yazmaktır. |
| **FU42 Classification Metadata** | G-6/G-7/G-10 kapanmadan zorunlu alanların değer kümesi bilinmiyor. |
| **FU49 Components** | G-3 açık; with-parent / independent kararı modelin şeklini belirliyor. |
| **Qualified signature sağlayıcı yatırımı** | G-1 açık; kapsam record class başına daralabilir. *(FU47 gate'i bundan bağımsız, hemen yapılır.)* |
| **IQ/OQ/UAT yürütme** | FU39 kapanmadan üretilen kanıt geçersiz; yeniden koşmak gerekir. |
| **Streaming upload refactor** | İptal edildi (URS-131b amend). Başlanırsa tamamen boşa iş. |

### İlk Yapılacak 5 İş

Tam sırayla:

1. **MOD-0029-FU38 — Runtime Blocker Kapatma.** Hiçbir şeye bağlı değil; kapanmadan hiçbir exit gate doğrulanamaz.
2. **MOD-0029-FU39 — Regulated Authorization Hardening.** Admin bypass + denied-access audit. Sonraki 15 P0'ın **doğrulanabilirliğinin** ön koşulu.
3. **Governance: G-6, G-7, G-10 kararlarının kapatılması.** *(FU38/FU39 ile paralel yürür.)* Kritik yolun başındaki üç blocker; vocabulary zinciri bunlar olmadan başlayamaz.
4. **MOD-0048-FU / MOD-0029-FU40 — Controlled Vocabulary Foundation.** G-6/G-7/G-10 kapanır kapanmaz başlar; Faz 1–4'ün büyük kısmı buna bağlı.
5. **MOD-0029-FU46 — Lifecycle Transition Hardening.** Governance'a bağlı **değil**, FU39 sonrası hemen başlatılabilir; tek-Effective ve atomik supersession veri bütünlüğünün çekirdeği.

> **Not:** 3. sıradaki iş bir karar, kod değil. Kasıtlı olarak listeye alındı — çünkü 4. iş onsuz başlayamaz ve bu, planın en sık gözden kaçan darboğazıdır.

---

## 10. Son Verdict

# PLAN READY WITH GOVERNANCE BLOCKERS

**Neden `PLAN READY` değil.** Planın kritik yolu, kod ekibinin veremeyeceği kararlara dayanıyor. Vocabulary zincirinin ilk halkası (FU40) **G-6** (domain/function kodları — LOG-0006 hiç yazılmamış, 22 branch onaysız), **G-7** (record class'lar ve retention takvimleri) ve **G-10** (entity modeli) kapanmadan başlayamaz; bunlar da FU41 → FU42 → FU43 → FU44 → FU55'in tamamını taşıyor. Permission fazı **G-8**'e bağlı ve A1'in 32 profilinin tamamı hâlâ `PENDING`, biri (`PS-MED-RESTRICTED`) `BLOCKED_TAXONOMY`. Onaylanmamış bir profil setine kod yazmak, o seti iki kez yazmaktır.

**Neden `PLAN NOT READY` de değil.** Plan, governance'ın kapanmasını beklemeden başlatılabilecek gerçek ve değerli bir gövdeye sahip: **FU38, FU39, FU46, FU47, FU63** ve MOD-0021 audit export'u hiçbir karara bağlı değildir. Bunlardan ikisi (FU38, FU39) planın en yüksek getirili işleridir — çünkü FU39 kapanmadan diğer on beş P0'ın **hiçbiri kanıtlanamaz**. Yani proje bugün "karar bekliyor" diye durdurulamaz; aksine, en kritik iki iş bugün başlayabilir.

**Planın en kırılgan noktası — ve tek geri alınamaz kararı.** `FU55` (migration) `FU44`'ten (stable classification identity) önce koşulursa, 1.236 promoted dosya klasör-merkezli modele yazılır ve W-1 refactoru bugünkü ~sıfır maliyetinden çıkıp bir yeniden sınıflandırma projesine dönüşür. Bu ekonomi bu projede iki kez ölçüldü: Setonda→GMG İlaçları geçişi **271 path'i bedavaya** taşıdı, v0.32 type eklemeleri **75 dosyayı** önemsiz maliyetle serbest bıraktı — her ikisi de **henüz hiçbir şey dosyalanmamış olduğu için**. ERP'de bugün sıfır doküman var. Pencere açık, ama kalıcı değil.

**Planın koruduğu şey.** Bu backlog mevcut sistemi yeniden yazmıyor. Lifecycle state machine, identifier allocation ledger, approval segregation, çift onaylı legal hold release, "no destruction engine" ilkesi, non-leakage 404 disiplini, reconciliation engine, idempotent registration saga, optimistic concurrency, correlation ID ve controlled copy yapısı — hepsi **reuse/extend** edilecek varlıklar olarak işaretlendi; birkaçı GMG hedefinin üzerinde. Gap analizi v2 §7b uyarınca bunlar ayrıca **URS'e requirement olarak yazılarak** korunmalıdır; aksi hâlde refactor sırasında sessizce kaybolabilirler.

**Karar önerisi.** Faz 0'ı bugün başlatın (FU38 → FU39). Aynı gün governance'a **G-6, G-7, G-10** için tarih verin — bu üçü kritik yolun başındadır ve gecikirse tüm Faz 1–4 gecikir. G-8'i ikinci dalga olarak planlayın. Qualified-signature harcamasını G-1'e kadar dondurun, ama FU47'yi (gate) beklemeden yapın.

---

*Bu doküman ANALİZ VE PLANLAMA çıktısıdır. Kod, veritabanı, migration, branch, commit veya PR üretilmemiştir. Tüm bağımlılıklar ve mevcut capability tespitleri gap analizi v2'nin kanıtlarına ve repo doğrulamalarına dayanır (modül sahiplikleri `execution/registries/module-id-registry.md`; FU tabanı MOD-0029-FU37; A3'te 54 `NO — CANNOT BIND` satırı sayılarak doğrulanmıştır).*
