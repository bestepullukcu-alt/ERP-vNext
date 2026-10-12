# MOD0185 precision approval — 2026-09-17

The repository user explicitly approved R185-C01…05 and separate R2 release-candidate preparation.
Carrier required omissions map502. Exact query/UUID/claim rules are approved as specified below.
2.0.0 is release-candidate metadata only. No canonical publication,consumer consent,new guard
binding application,pack promotion or DEV GO is authorized. Prior D18501–05 remains approved.

Exact approved proposal SHA256: ffa3452226dbe96aed9b77870b1f1c24381e41dcca3b15e831c161017b57b020

The historical proposal is preserved verbatim below; its pre-approval labels describe that
prior moment and are superseded by the explicit approval above,only for candidate preparation.

---

**Onaya sunulan tek karar seti: R185-C01…05.** Onaylı D185-01…05 korunur; aşağıdaki precision kararları henüz onaylanmış değildir.

**Düzeltme:** Önceki “Carrier karar alanları optional olabilir” iddiası yanlıştı. Frozen `CarrierSummary.required`, **carrierId, carrierCode, displayName, status, supportedModes** alanlarının tamamını içerir. Önceki omission→503 önerimi aşağıdaki **omission→502** önerisiyle değiştiriyorum.

Pointer’lar [canonical sözleşmeye](/Users/natig/Projects/ERP-vNext-recovery/docs/analysis/contracts/shipment-bundle.openapi.yaml); onay dayanağı [D185 karar kaydına](/Users/natig/Projects/ERP-vNext-recovery/docs/records/decisions/2026-09/mvp6-mod0185-design-approval-01.md) aittir.

| Karar | Önerilen exact davranış ve dayanak | Pozitif / negatif fixture sonucu | Değişiklik etkisi / onay sahibi |
|---|---|---|---|
| **C01 — Carrier omission** | Önce `CarrierListResponse` ve her item `CarrierSummary` doğrulanır. **Herhangi bir required omission veya malformed alan →502 `DEPENDENCY_RESPONSE_INVALID`**. Geçerli listede eşleşme yok→404 `CARRIER_NOT_FOUND`. Dayanak: `#/components/schemas/CarrierSummary/required`, `CarrierListResponse`; onaylı malformed-response politikası. | Tam Active/Road eşleşmesi→reference gate geçer. Beş required alanın her birini ayrı kaldır→502. `supportedModes:null`→502; `[]`→schema-valid fakat istenen mode için422 `CARRIER_MODE_UNSUPPORTED`. Geçerli `items:[]`→404. Duplicate kimlik/filter çelişkisi→502. | R1 optional iddiası ve önceki R185-01 düzeltilir; shared schema değişmez. **Omission→503 alternatifini önermiyorum:** malformed kuralına yeni istisna gerektirir. Shipment’ın schema-valid optional-field eksikliğine ilişkin onaylı503 korunur. **Contract owner.** |
| **C02 — Query scope override** | Bir kez decode edilmiş key üzerinde ASCII case-insensitive exact küme: `tenantId`, `tenant_id`, `X-Tenant-Id`, `legalEntityId`, `legal_entity_id`, `X-Legal-Entity-Id`. Herhangi birinin presence’ı, değerden bağımsız400 `INVALID_REQUEST`; onaylı query-validation aşamasında. Dayanak: `#/paths/~1loads/get/parameters`, D185-04. | `?Tenant_ID=` ve `?%74enantId=x`→400. `?foo=x`, `?tenantId[]=x`→ordinary unknown; ignore edilir, JWT scope’unu değiştirmez. | Yalnız Loads annex/fixtures. Genel unknown-query reddi veya query’den scope binding yok. **Security + contract owner.** |
| **C03 — UUID/claim ve sıra** | UUID biçimi: ASCII hex **8-4-4-4-12**, uppercase/lowercase kabul, application trim yok; ek version/variant şartı yok. Correlation nil geçerli. Trusted `tenant_id/legal_entity_id/sub`: exact-case isim, tek string, aynı UUID biçimi, non-nil. **Authentication parser/token doğrulaması reddederse401; authentication başarılı olduktan sonra unusable/duplicate trusted claim saptanırsa403.** Dayanak: `#/components/parameters/CorrelationId`, `IdempotencyKey`, `#/security`; D185-04 sırası. | Uppercase correlation→aynı UUID; nil correlation→geçerli; braces/URN/32-hex header→400. Duplicate `sub` nedeniyle parser ret→401; authentication’dan geçen duplicate `sub`→403. “Her duplicate mutlaka403” oracle’ı yok. | Loads lexical corpus/annex netleştirmesi; Carrier parser’ından örtülü default devri yok. **Security + token issuer + contract owner.** |
| **C04 — Sürüm** | **2.0.0 yalnız release-candidate önerisi**; wire `contractVersion:v1` korunur. Dayanak: `#/info/version`, `#/components/schemas/ContractVersion`, `CreateLoadCommand`; onay kaydındaki açık compatibility kapısı. | Duplicate Shipment/sequence gap/missing Pickup baseline schema-valid, onaylı semantikte422:1.2.0 minor güvenliği varsayılamaz. Non-Loads eşitliği bu davranış farkını kapatmaz. | Candidate metadata ve annex adı `loads-semantics-v2.0.0.md`. **Metadata major route negotiation veya migration sağlamaz.** Consumer uyumluluğu, uptake ve cutover ayrıca değerlendirilir. **Release/contract owner; consumer consent ayrıca.** |
| **C05 — Guard binding** | Final canonical SHA-256 için ayrı, exact onaylı authority payload/decision binding gerekir. Eski17mühürlü girdinin path/hash/içeriği korunur. Dayanak: [guard owner kaydı](/Users/natig/Projects/ERP-vNext-recovery/docs/records/decisions/2026-09/mvp6-docs-path-owner-approval-01.json) ve [R1 yayın sınırı](/private/tmp/mvp6-loads-candidate-r1-beb_lk0t/README.md). | Yeni canonical+eski pin→ret; onaylı yeni binding→ilgili integrity kontrolü geçer; tarihsel girdi mutation’ı→ret devam eder. | Ayrı governance işlemi; Loads candidate guard’ı değiştirmez. **Docs-path owner + CT.** |

C03 için duplicate/öncelik ayrıntısı: authentication → trusted tenant/LE/actor/permission → correlation/tenant/LE/key header syntax → scope eşitliği sırası korunur. Header isimleri case-insensitive; aynı değerli tekrarlar da duplicate’tir. Authentication’dan geçen signed payload’da duplicate claim’ler “last wins” ile gizlenemez. Key parser sonrası1–128 Unicode code point; trim/case-fold yok, tek value’daki virgül geçerli. Correlation geçersizse yalnız rejection trace için fallback UUID üretilir; business root yapılmaz.

**Tek mesajlık onay metni:**

> R185-C01…05 precision kararlarını ve bunlara göre ayrı R2 release candidate hazırlanmasını onaylıyorum. Carrier required omission502 olacaktır;503 istisnası kabul edilmiyor. Bu onay canonical yayın, consumer consent, guard uygulaması, pack promotion veya DEV GO değildir.

**SOP §22 ve no-change**

- **Verdict:** E1/E2 düzeltme incelemesi tamamlandı; owner onayı bekleniyor.
- **Branch/HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- **Tests:** Tam Carrier fixture schema-valid; beş ayrı required omission ve null modes schema-invalid; empty modes schema-valid — **8/8 statik assertion geçti**. HTTP status sonuçları yukarıdaki acceptance oracle’larıdır; runtime’da çalıştırılmadı.
- **Changed files:** Yok. İncelenen **11 dosyanın önce/sonra SHA-256’ları aynı**; git status aynı, worktree önceden kirli. Git mutation yapılmadı.
- **Persistence/security/observability:** Yalnız belge/schema incelemesi; runtime veya E4 kanıtı yok. Migration/rollback uygulanmadı; kapsam dışı değişiklik yok.
- **Kalan kapılar:** Precision onayı, exact R2 verification, compatibility/consumer uptake, yayın ve ayrı guard binding.

Canonical hash değişmedi:  
`ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f`