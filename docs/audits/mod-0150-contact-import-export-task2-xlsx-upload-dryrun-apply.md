# MOD-0150 — Contact Import/Export Task 2: XLSX Upload + Dry-run Preview + Safe Apply

**Date:** 2026-07-23 · **Module:** Contact & Relationship Management (`Diten.CrmService` + `Diten.Web`) ·
**Type:** Post-closeout enhancement (no new module / permission / reference set / seed / Gateway route) ·
**Verdict:** **PASS** (AccountRelationship import intentionally out of scope)

Task 1 tamamladığı **şablon + export** yarısının üzerine bu gate **import döngüsünü kapatır**: Task 1 XLSX dosyası
yüklenir, şemasına göre okunur, **dry-run** ile tüm dosya doğrulanıp satır satır önizlenir, kullanıcı onaylarsa
**yalnız geçerli satırlar** uygulanır. Contact create/update ve AccountContactLink add/update/**end** desteklenir;
historical lifecycle korunur (silme yok), cross-country politikası **import yolunda da** uygulanır (Task 1 analizinde
tespit edilen governance açığı kapatıldı). CSV (FU06) ve Task 1 XLSX export/template yolları değişmedi.

---

## 1. Preflight

| Konu | Durum |
|---|---|
| MOD-0150 status | Closeout PASS (Review-ready) %100 + Location/PII-KVKK Hardening + Historical Lifecycle + Import/Export Task 1 |
| Task 1 status | PASS — XLSX şablon (Instructions/Contacts/AccountLinks/ReferenceData), MOD-0048 ReferenceData helper, dropdown, mevcut veri export'u (includeLinks/Historical/Notes), CSV korunmuş |
| Şema durumu | `ContactWorkbookSchema` tek kaynak; Contacts 23 kolon / AccountLinks 16 kolon — **Task 2 şemayı değiştirmedi**, geriye uyumlu okur |
| Bu task kapsamı | Upload + parse + dry-run + preview + safe apply + Contact create/update + AccountLink add/update/end + cross-country parity + import UI |
| Kapsam dışı (uygulanmadı) | AccountRelationship XLSX import/export · Territory/SalesRep · Visit/Route · Patient clinical data · Consent capture · yeni permission/reference set/seed · hardcoded fallback · direct 5061 · manuel Mongo · hard delete · relationship graph · MOD-0151/MOD-0155 |
| Protected paths | `.antigravity/**`, `Archive/**`, `_Layout.cshtml`, `ocelot.json` — dokunulmadı; yeni Gateway route gerekmedi (mevcut `/api/crm/contacts/{everything}` wildcard'ı) |

---

## 2. Import Format / Endpoint Decision

| Decision | Chosen Option | Reason |
|---|---|---|
| Upload formatı | **XLSX (multipart/form-data)** | Task 1 şablonu XLSX; CSV import bu turda desteklenmiyor ve UI'da açıkça yazıyor |
| Dry-run endpoint | `POST /api/crm/contacts/import-file?dryRun=true&strictMode=` | Varsayılan `dryRun=true` — yanlışlıkla yazma yapan bir çağrı mümkün değil |
| Apply endpoint | `POST /api/crm/contacts/import-file/apply?strictMode=` | Ayrı rota: yıkıcı çağrı bir "önizleme" isteğiyle kazara tetiklenemez |
| Staging / idempotency token | **Yok — dosya apply'da yeniden gönderilir** | Sunucu tarafı staging store (dosya/TTL/temizlik/çok-örnekli tutarlılık) bu taskın kapsamını belirgin büyütürdü. Aynı motor iki kez çalıştığı için apply, kullanıcının onayladığı planla **aynı doğrulamayı** tekrar üretir. **Follow-up:** dosya hash + tenant + kullanıcı bazlı idempotency anahtarı (aynı dosyanın iki kez apply edilmesi bugün ikinci kez conflict/no-change üretir, sessiz duplicate değil) |
| Parse yeri | **Backend** | İş kuralı, satır numarası ve PII tek yerde; tarayıcıya ham dosya işleme taşınmadı |
| Mevcut yollar | **FU06 JSON import + CSV export/template + Task 1 XLSX export/template değişmedi** | Canlı smoke ile doğrulandı (§14) |
| Dosya boyutu | **10 MB** (`RequestSizeLimit`, hem Gateway servisinde hem proxy'de) | Senkron istek-kapsamlı import için güvenli tavan |
| Satır tavanı | 5000 contact / 20000 link (Task 1 sabitleri) | Aşılırsa dosya seviyesinde controlled hata |

---

## 3. Parser Design

| Sheet | Required? | Behavior | Result |
|---|---|---|---|
| **Contacts** | Zorunlu değil ama biri olmalı | Başlıklar **normalize** eşleşir (büyük/küçük, boşluk, `-`/`_` toleranslı) → kolon sırası değişse de okur | ✅ `Operation` + `ContactId` yoksa **dosya-seviyesi hata** |
| **AccountLinks** | Hayır | Aynı normalize eşleşme; yoksa sadece Contacts işlenir | ✅ `Operation` yoksa dosya-seviyesi hata |
| **Instructions** | Hayır | Okunmaz | ✅ yok sayılır |
| **ReferenceData** | Hayır | **Doğrulama kaynağı olarak KULLANILMAZ** — yalnız insan için yardımcıdır; tüm referans doğrulaması MOD-0048 published-values üzerinden yapılır | ✅ güvenilmiyor |
| **Accounts** | Hayır | Okunmaz (read-only lookup) | ✅ yok sayılır |

**Hücre kuralları:** her hücre metin olarak okunur; Excel'in tipe çevirdiği değerler geri döndürülür — sayıya dönmüş
telefon/posta kodu/harici id **bilimsel gösterim ve `.0` olmadan** rakamlarını korur, tarih hücresi **ISO `yyyy-MM-dd`**
olur, boolean `TRUE/FALSE` olur. Tamamen boş satırlar atlanır. Tanınmayan ekstra kolonlar **uyarı** üretir, importu
bloklamaz. Aynı kolonun iki kez yazılması dosya hatasıdır. Okunamayan/bozuk/parolalı dosya tek bir dosya hatasıyla
döner (exception metni asla kullanıcıya verilmez — dosya yolu sızdırabilir). Satır numarası **gerçek Excel satırıdır**.

---

## 4. Operation Semantics

| Sheet | Operation | Match Priority | Behavior | Historical Rule |
|---|---|---|---|---|
| Contacts | `add` / `create` | — (yeni kayıt) | `ContactId` doluysa **hata** (`contact_id_on_add`); ad zorunlu; ContactType/ContactStatus MOD-0048; opsiyonel setler + shape doğrulama; external ref varsa duplicate kontrolü; `DisplayName` boşsa türetilir | — |
| Contacts | `update` | 1) `ContactId` → 2) `ExternalSystem`+`ExternalId` | Boş hücre = **değiştirme**; `<CLEAR>` = temizle; zorunlu alanlar temizlenemez; `ContactId` immutable; external-ref değişikliği **yok sayılır + uyarı**; hiçbir alan farklı değilse `no_change` skip | Contact silinmez |
| Contacts | `skip` / boş | — | İşlenmez (boş `Operation` = skip) | — |
| Contacts | `delete` | — | **Desteklenmez** → controlled `unsupported_operation` | Hard delete yok |
| Contacts | `end` | — | Contacts sayfasında geçersiz → controlled hata | — |
| AccountLinks | `add` | Contact: `ContactId` → external ref (aynı dosyada oluşturulan contact dahil) · Account: `AccountId` → `AccountCode` | RoleCode MOD-0048; validity; `IsPrimary`; `Status` iç enum; **CrossCountryPolicy**; ReportsTo (self/varlık/döngü); duplicate active + second-primary → conflict | Ended kayıt yeni aktif linki **bloklamaz** |
| AccountLinks | `update` | 1) `LinkId` → 2) Contact+Account+RoleCode (aktif) | `AccountId`/`ContactId` değiştirme denemesi → **immutable hata**; role/primary/status/validity/reportsTo/notes/reason güncellenir; cross-country **yeniden** değerlendirilir | Kayıt aynı id ile kalır |
| AccountLinks | `end` | 1) `LinkId` → 2) doğal anahtar (aktif) | `ValidTo` **zorunlu**; `Status=ended` + `ValidTo`; `IsDeleted=false`; zaten kapalıysa `already_ended` skip | **Silme yok, overwrite yok** |
| AccountLinks | `skip` / boş | — | İşlenmez | — |
| AccountLinks | `delete` | — | **Desteklenmez** → controlled hata | Hard delete yok |

**Boş `Operation` kararı:** export dosyası `Operation` sütunu **boş** iner. Boşu "add" saymak tüm adres defterini
çoğaltırdı; boşu "update" saymak sessiz toplu güncelleme olurdu. Bu yüzden **boş = skip** (kod: `operation_missing`),
önizlemede açıkça görünür. **E-posta/telefon ile eşleştirme yapılmaz** — bu alanlar bu modelde benzersiz değildir ve
yanlış kişinin kaydının üzerine yazma riski taşır (test ile kanıtlı).

---

## 5. Dry-run / Preview Design

| Category | Meaning | UI Behavior | Persistence |
|---|---|---|---|
| **Creates** | Yeni Contact / AccountLink açılacak | Yeşil özet kartı + satır rozeti | Dry-run'da **yok** |
| **Updates** | Mevcut kayıt güncellenecek | Mavi rozet + **ChangedFields** listesi | Dry-run'da yok |
| **Ends** | Link tarihsel olarak kapanacak | Turuncu rozet + `Status`/`ValidTo` | Dry-run'da yok |
| **Skips** | `skip`, boş `Operation`, `no_change`, `already_ended`, `skipped_dependency` | Gri rozet | — |
| **Errors** | Satır işlenemez (validation/permission/immutable/not_found) | Kırmızı rozet + kural mesajı | Hiçbir zaman uygulanmaz |
| **Conflicts** | `duplicate_external_reference`, `duplicate_link`, `second_primary`, `duplicate_row` | Kırmızı rozet | Uygulanmaz |
| **Warnings** | Dosya uyarıları (bilinmeyen kolon, eksik sayfa) + satır uyarıları | Uyarı bloğu + sayaç | — |

**Satır alanları:** `Sheet, RowNumber, Operation, EntityType, ResolvedKey, Status, Code, Message, ChangedFields,
DisplayLabel, Severity`. Dry-run **hiçbir şey yazmaz** (testlerle kanıtlı: create ve end için ayrı ayrı).

---

## 6. Apply Strategy

| Scenario | Decision | Reason |
|---|---|---|
| Genel strateji | **Validate-all, then apply-valid** | Kullanıcının onayladığı plan ile çalışan plan aynı; ilk hatada durup yarım iş bırakmaz |
| Hatalı satır varsa | Varsayılan: **geçerli satırlar uygulanır**, hatalılar uygulanmaz | Büyük dosyada tek yazım hatası yüzünden her şeyi geri çevirmek pratik değil |
| Strict mode | Kullanıcı işaretlerse **tek hata varsa hiçbir satır uygulanmaz** | "Ya hep ya hiç" isteyen operatör için |
| Hata oranı > %20 | **Apply bloklanır** ("dosya yanlış görünüyor") | Yanlış dosya/yanlış şablon senaryosunu erken yakalar |
| Zorunlu referans seti yayınlanmamış | **Apply bloklanır** | Import, MOD-0048 bağımlılığını atlayamaz |
| Uygulanacak satır yok | **Apply bloklanır** ("uygulanacak bir şey yok") | Boş/tamamen skip dosyada yanıltıcı "başarılı" göstermez |
| Sıra | **Önce Contacts, sonra AccountLinks** | Aynı dosyada oluşturulan contact'a link bağlanabilsin |
| Bağımlılık | Contact satırı hata aldıysa ona bağlı link → `skipped_dependency` | Kullanıcı doğru satırı düzeltir, yanıltıcı "contact bulunamadı" görmez |
| Aynı dosyada tekrar | Aynı contact iki kez update → `duplicate_row` conflict; aynı (account, contact, role) iki kez add → `duplicate_link` | Dosya içi çakışmalar da yakalanır |
| Atomiklik | **Multi-document transaction YOK** | Mongo replica-set transaction'ı zorunlu kılmak gereksiz risk; partial strateji UI'da ve audit'te açıkça yazılı |

---

## 7. Permission Model

| Action | Permission | Behavior |
|---|---|---|
| Import sayfası + dry-run + apply endpoint'i | `crm.contact.import` | Yoksa: toolbar öğesi görünmez, sayfa 403, API 401/403 |
| Contacts `add` satırı | + `crm.contact.create` | Yoksa **satır** `permission_denied` (fail-closed, sessiz atlama değil) |
| Contacts `update` satırı | + `crm.contact.update` | Yoksa satır `permission_denied` |
| AccountLinks `add`/`update`/`end` satırı | `crm.account-contact.manage` | Yoksa satır `permission_denied`; UI sayfada baştan uyarır |
| Export / template | Task 1 (`crm.contact.export` / `crm.contact.import`) | Değişmedi |

**Karar:** import permission'ı tek başına **yetmez** — satır seviyesinde `create`/`update`/`manage` de aranır. Böylece
import, UI'da sahip olunmayan bir yetkinin arka kapısı olamaz; blanket 403 yerine kullanıcı **hangi satırı**
çalıştıramadığını görür. **Yeni permission icat edilmedi** (grep ile kanıtlı, §14).

---

## 8. PII/KVKK Controls

| Risk | Control | Result |
|---|---|---|
| Önizleme satırında ham isim | `DisplayLabel` maskeli (`A** L****`) | ✅ test |
| Hata mesajında ham değer | Mesajlar **kural** anlatır, değeri tekrarlamaz ("Email is not a valid address.") | ✅ test |
| Notes / telefon / e-posta sızıntısı | Hiçbiri mesaja veya audit'e yazılmaz | ✅ test |
| Audit payload | `corr`, `dryRun`, `applied`, `strict`, satır sayaçları — **sadece sayı/bayrak** | ✅ test |
| Yüklenen dosya adı | Proxy'de sabit `import.xlsx` adına çevrilir; loglanmaz, saklanmaz | ✅ kod |
| Dosya diske yazılır mı | Hayır — bellek üzerinden Gateway'e stream edilir, işlem sonunda bırakılır | ✅ kod |
| Hasta/klinik veri | Import şeması klinik alan içermez; şablonda açık yasak uyarısı | ✅ (Patient scope açık follow-up) |
| `PhotoDataUri` | Şemada yok → import edilemez | ✅ |
| Bozuk dosya hatası | Exception metni kullanıcıya verilmez (dosya yolu sızabilir) | ✅ kod |

---

## 9. Implementation Summary

- **Parser** (`ContactWorkbookReader`): normalize başlık eşleşmesi, gerçek Excel satır numarası, metin/sayı/tarih/
  boolean geri dönüşümü, boş satır atlama, bilinmeyen kolon uyarısı, dosya-seviyesi hata sözleşmesi.
- **Değer kuralları** (`ImportValues`): boş = değiştirme, `<CLEAR>` = temizle, boolean/tarih/GUID toleranslı okuma,
  link `Status` iç enum'u (`active` + `RelationshipLifecycle.ClosedStatuses`) — **yeni reference set yok**.
- **Referans doğrulama** (`ImportReferenceChecker`): distinct `(set, value)` başına tek Gateway çağrısı; required set
  eksikse apply bloklanır; optional sette SetMissing tolere, InvalidValue hata → **single-write ile birebir parite**.
- **Dry-run + apply motoru** (`ContactWorkbookImportHandler`): tek geçişte plan üretir, ikinci fazda uygular; apply
  kapıları (§6); Contacts→AccountLinks sırası; dosya içi uniqueness rezervasyonları.
- **Contact create/update**: kimlik korumalı eşleştirme (ContactId → external ref), partial update, DisplayName
  auto-derive, shape/maxlen/e-posta doğrulaması, immutable alan koruması.
- **AccountLink add/update/end**: mevcut `AccountContactValidation` (role, validity, reports-to) ve
  `RelationshipLifecycle` yeniden kullanıldı; end = `Status=ended` + `ValidTo`, `IsDeleted=false`, `ReplaceOne`.
- **CrossCountryPolicy import paritesi**: add ve update yollarında `CrossCountryPolicy.Evaluate` çağrılır; reason
  yoksa satır hatası; audit'e yalnız ülke kodu, **reason metni asla**. (Task 1 analizindeki governance açığı kapandı.)
- **API**: `import-file` (dryRun varsayılan true) + `import-file/apply`, `[HasPermission("crm.contact.import")]`,
  10 MB limit, `.xlsx` uzantı kontrolü, satır-seviyesi capability'ler claim'lerden.
- **Frontend**: `/CRM/Contacts/Import` compact tam sayfa (offcanvas yok) — upload → **Validate** → özet kartları +
  filtrelenebilir satır tablosu → **Apply** (MOD-0013 premium onay). Dosya değişince önceki önizleme geçersiz olur.
- **Result report**: UI tablosu + özet kartları (indirilebilir XLSX rapor **follow-up**, §17).
- **Localization**: 36 yeni anahtar × 7 dil (ContactIndex 85 → **121**).

---

## 10. Changed Files

| File | Change | Why |
|---|---|---|
| `Application/.../Xlsx/ContactWorkbookReader.cs` | **yeni** | XLSX → satır okuma, dosya-seviyesi doğrulama |
| `Application/.../Xlsx/ContactImportModels.cs` | **yeni** | Önizleme/sonuç DTO'ları, operasyon sabitleri, PII maskeleme etiketi, capability seti |
| `Application/.../Xlsx/ContactImportEngineSupport.cs` | **yeni** | Değer okuma kuralları (`<CLEAR>`, tarih, boolean), referans checker |
| `Application/.../Xlsx/ContactWorkbookImportHandler.cs` | **yeni** | Dry-run + apply motoru (Contact create/update, Link add/update/end, cross-country, apply kapıları) |
| `Api/Controllers/CRM/ImportExportController.cs` | +2 upload endpoint + capability çözümleme + 10 MB limit | Upload yüzeyi; mevcut endpoint'ler değişmedi |
| `tests/.../ContactWorkbookImportTests.cs` | **yeni (41 test)** | Parser, dry-run, apply, historical, cross-country, permission, PII, apply kapıları |
| `frontend/.../Controllers/CRM/ContactsController.cs` | +`Import` sayfası, +`import/preview`, +`import/apply` proxy | Gateway-only upload akışı |
| `frontend/.../Views/CRM/Contacts/Import.cshtml` | **yeni** | Compact import workspace (uyarılar + upload + önizleme tablosu) |
| `frontend/.../wwwroot/assets/js/CRM/Contacts/import.js` | **yeni** | Upload → dry-run → apply akışı, özet kartları, filtre, premium onay |
| `frontend/.../wwwroot/assets/js/CRM/Contacts/index.js` | Import öğesi artık sayfaya gider + permission yoksa **hiç render edilmez** | ComingSoon kaldırıldı |
| `Resources/Views/CRM/Contacts/ContactIndex.*.resx` (7) | +36 anahtar (85 → **121**) | 7 dil parity |
| `docs/audits/mod-0150-contact-import-export-task2-...md` | **yeni** | Bu rapor |
| `execution/registries/module-implementation-status.md` | MOD-0150 satırına Task 2 notu | Registry |

**Dokunulmayanlar:** `ContactWorkbookSchema` / `ContactWorkbookBuilder` (Task 1 şeması ve yazıcısı), `Csv.cs`,
`ImportTemplates.cs`, FU06 JSON import handler'ları, `ocelot.json`, permission seed, MOD-0048 set tanımları,
entity'ler, `module-id-registry.md`.

---

## 11. Browser Golden Flow

> **Dürüstlük notu:** fleet ayakta (5000/5001/5061) ve CrmService hot-reload ile yeni yüzeyi yayımladı — runtime
> kanıtları aşağıda. **Kimlik doğrulamalı** uçtan uca dosya yükleme akışı (adım 4–13) 97c5 oturumu gerektirir; token
> yalnız kullanıcı girdisiyle alınır, tahmin edilmez. Bu adımlar **operatöre açık** bırakıldı — sahte PASS yok.

| Step | Evidence | Result |
|---|---|---|
| 1. Yeni upload yüzeyi canlı serviste | :5061 OpenAPI'de `import-file`, `import-file/apply`, `dryRun`, `strictMode` **mevcut** | ✅ |
| 2. Gateway dry-run route + guard | `POST :5000/api/crm/contacts/import-file?dryRun=true` → **401** | ✅ |
| 3. Gateway apply route + guard | `POST :5000/api/crm/contacts/import-file/apply` → **401** | ✅ |
| 4. Frontend import sayfası fail-closed | `GET :5001/CRM/Contacts/Import` → **302 → login** | ✅ |
| 5. Proxy yalnız POST kabul eder | `GET :5001/CRM/Contacts/import/preview` → **404** | ✅ |
| 6. FU06 JSON import bozulmadı | `GET :5000/api/crm/contacts/import` → **405** (rota var, POST-only) | ✅ |
| 7. CSV export/template bozulmadı | `:5000/.../export` ve `/import-template` → **401** | ✅ |
| 8. Dosya yükle → dry-run önizleme | operatör (41 unit testle kanıtlı) | ⏳ açık (Low) |
| 9. create/update/end/warning/error satırları görünür | operatör (unit) | ⏳ açık (Low) |
| 10. Cross-country reason eksik satırı hata verir | operatör (unit ✅) | ⏳ açık (Low) |
| 11. Apply → sonuç özeti | operatör (unit ✅) | ⏳ açık (Low) |
| 12. Account Details'ta eski link **ended**, yeni link **active** | operatör (unit ✅) | ⏳ açık (Low) |
| 13. `includeHistorical` export'unda eski link hâlâ görünür | operatör (Task 1 unit ✅) | ⏳ açık (Low) |
| 14. Import artık ComingSoon değil | kod: toolbar öğesi `/CRM/Contacts/Import`'a gider | ✅ |
| 15. Direct 5061 yok | grep CLEAN | ✅ |

> RESX değişiklikleri için `Diten.Web` **tam restart** gerekir (bilinen davranış).

---

## 12. Failure Path Proof

| Failure | Expected | Observed | Status |
|---|---|---|---|
| Dosya .xlsx değil / bozuk | Controlled dosya hatası, exception sızmaz | `Reader_Rejects_A_File_That_Is_Not_A_Workbook` + controller uzantı kontrolü | ✅ |
| Zorunlu kolon eksik | Dosya-seviyesi hata | `Reader_Reports_A_Missing_Required_Column_As_A_File_Error` | ✅ |
| Bilinmeyen kolon | Uyarı, bloklamaz | `Reader_Warns_About_Unknown_Columns_But_Still_Imports` | ✅ |
| Geçersiz contact-type | Satır hatası, kayıt açılmaz | `An_Invalid_Contact_Type_Fails_The_Row` | ✅ |
| Zorunlu set yayınlanmamış | **Tüm apply bloklanır** | `An_Unpublished_Required_Set_Blocks_The_Whole_Apply` | ✅ |
| Geçersiz e-posta | Satır hatası | `An_Invalid_Email_Fails_The_Row` | ✅ |
| Duplicate external reference | Conflict | `A_Duplicate_External_Reference_Is_A_Conflict` | ✅ |
| Duplicate aktif link | Conflict | `A_Duplicate_Active_Link_Is_A_Conflict` | ✅ |
| İkinci primary | Conflict | `A_Second_Primary_For_The_Same_Account_And_Role_Is_A_Conflict` | ✅ |
| ValidFrom > ValidTo | Satır hatası | `ValidFrom_After_ValidTo_Fails_The_Row` | ✅ |
| Cross-country reason yok | Satır hatası | `A_Cross_Country_Link_Without_A_Reason_Fails_On_Import_Too` | ✅ |
| Cross-country reason var | İzin verilir, reason saklanır | `A_Cross_Country_Link_With_A_Reason_Is_Allowed_And_Stores_The_Reason` | ✅ |
| Ülke bilinmiyor | Bloklamaz | `An_Unknown_Country_Never_Blocks_A_Link` | ✅ |
| `Operation=delete` | Controlled hata, silme yok | `Delete_Operation_Is_Not_Supported_On_Either_Sheet` | ✅ |
| `add` + dolu ContactId | Controlled hata | `Add_With_A_ContactId_Is_Rejected` | ✅ |
| E-posta/telefon ile update | Eşleşme **yok** → `match_key_missing` | `Update_Never_Matches_A_Contact_By_Email_Or_Phone` | ✅ |
| Link update'te account değiştirme | Immutable hata | `A_Link_Update_Cannot_Repoint_The_Account` | ✅ |
| `end` + ValidTo yok | `end_requires_validto` | `Ending_A_Link_Requires_An_End_Date` | ✅ |
| Link izni yok | Satır `permission_denied` | `Link_Rows_Fail_Closed_Without_The_Account_Contact_Manage_Permission` | ✅ |
| Contact update izni yok | Satır `permission_denied` | `Contact_Update_Rows_Fail_Closed_Without_The_Update_Permission` | ✅ |
| Strict mode + hata | Hiçbir satır uygulanmaz | `Strict_Mode_Applies_Nothing_When_Any_Row_Failed` | ✅ |
| Hata oranı > %20 | Apply bloklanır | `Too_Many_Broken_Rows_Block_The_Apply` | ✅ |
| Contact satırı hatalı → bağlı link | `skipped_dependency` | `A_Link_Whose_Contact_Row_Failed_Is_Reported_As_A_Dependency_Skip` | ✅ |

---

## 13. Historical Lifecycle Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| `end` uygulanır | `Status=ended` + `ValidTo`, `IsDeleted=false`, kayıt durur | `Apply_Ends_A_Link_By_LinkId_Without_Deleting_It` | ✅ |
| Dry-run `end` | Hiçbir şey değişmez | `DryRun_Previews_An_End_Without_Closing_The_Link` | ✅ |
| Kişi A→B taşınır (aynı dosyada end + add) | Eski link ended, yeni link active, **2 kayıt** | `Moving_A_Contact_Ends_The_Old_Link_And_Adds_A_New_One_In_The_Same_File` | ✅ |
| Ended link yeni aktif linki bloklar mı | Hayır | `An_Ended_Link_Does_Not_Block_A_New_Active_Link_With_The_Same_Key` | ✅ |
| Link update kimliği bozar mı | Hayır — `AccountId`/`ContactId` immutable | `A_Link_Update_Cannot_Repoint_The_Account` | ✅ |
| Import hiç silme yapar mı | Hayır — `Operation=delete` yok, `DeleteOne`/`IsDeleted=true` yok | grep CLEAN + `Delete_Operation_Is_Not_Supported_On_Either_Sheet` | ✅ |
| Zaten kapalı link tekrar end | `already_ended` skip (geçmiş bozulmaz) | kod + kategori | ✅ |
| Downstream satış/ziyaret/sipariş/rota | Yeniden atanmaz (link id sabit kalır) | `ReplaceOne` aynı id + kod yorumu | ✅ |
| Boş `Operation` ile indirilen export | Hiçbir kayda dokunulmaz | `A_Blank_Operation_Skips_The_Row_So_A_Plain_Export_Changes_Nothing` | ✅ |

---

## 14. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet build .../Diten.CrmService.Api.csproj` | ✅ **0 Uyarı / 0 Hata** | — |
| `dotnet test .../Diten.CrmService.Application.Tests.csproj` | ✅ **170/170** | 129 önceki + **41 yeni** (`ContactWorkbookImportTests`) |
| `dotnet build frontend/Diten.Web/Diten.Web.csproj` | ✅ **0 Uyarı / 0 Hata** | (çalışan fleet exe'yi kilitlediği için ayrı output dizinine) |
| `dotnet build gateway/Diten.ApiGateway.csproj` | ✅ **0 Hata** | route değişmedi |
| `verify_datatable_page.py --area CRM --module Contacts --reference compact` | ✅ **PASS / 0 FAIL** | Compact kontratı korundu |
| RESX parity (7 dil) | ✅ **121 × 7** | 85 → 121 |
| Live route smoke | ✅ upload 401×2, import sayfası 302, preview GET 404, FU06 import 405, CSV export/template 401 | fail-closed + regresyon yok |
| Live OpenAPI (5061) | ✅ `import-file`, `import-file/apply`, `dryRun`, `strictMode` | hot-reload doğrulaması |

**Search guards**

| Guard | Sonuç |
|---|---|
| Yeni kodda hardcoded reference değeri | ✅ CLEAN |
| Frontend'de direct 5061 | ✅ CLEAN |
| Raw PII (mesaj/audit/log) | ✅ CLEAN (test + grep) |
| Task 1 export/template bozuldu mu | ✅ Hayır (schema/builder dosyaları değişmedi) |
| CSV FU06 bozuldu mu | ✅ Hayır (`Csv.cs`, `ImportTemplates.cs` değişmedi; canlı 401/405) |
| AccountRelationship XLSX eklenmiş mi | ✅ Hayır |
| Yeni permission icat | ✅ Hayır (yalnız mevcut `crm.*` anahtarları) |
| CRM local seed | ✅ Yok |
| Fake success / mock data | ✅ Yok |
| Hard delete / `IsDeleted=true` / `DeleteOne` | ✅ Yok |
| `Operation=delete` desteklenmiş mi | ✅ Hayır (controlled error) |
| Patient clinical data import | ✅ Yok (yalnız yasaklayan uyarı) |
| Territory / SalesRep / Visit import | ✅ Yok |
| Consent capture | ✅ Yok |

---

## 15. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| Contact master | MOD-0150 | ✅ create/update (mevcut kurallarla) | none |
| AccountContactLink | MOD-0150 | ✅ add/update/end (historical korunur) | none |
| AccountRelationship | MOD-0150 | **Hayır** | none |
| Account master | MOD-0149 | Salt-okunur (id/kod çözümleme) | none |
| Reference values | MOD-0048 | consume-only (yeni set yok) | none |
| Permission engine | MOD-0018 | consume-only (yeni key yok) | none |
| Audit | MOD-0021 | mevcut seam + import olayları (counts-only) | none |
| Consent / Territory / Visit / Patient | MOD-0164/0151/0155/— | **Hayır** | none |
| Gateway routing | integration-agent | **Hayır** (mevcut wildcard) | none |

---

## 16. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| AccountRelationship XLSX import/export | Hayır | ✅ |
| Territory / SalesRep import | Hayır | ✅ |
| Visit / Route import | Hayır | ✅ |
| Patient clinical data import | Hayır | ✅ |
| Consent capture / import | Hayır | ✅ |
| Yeni permission | Hayır | ✅ |
| Yeni reference set | Hayır | ✅ |
| CRM local seed | Hayır | ✅ |
| Hardcoded reference fallback | Hayır | ✅ |
| Direct 5061 frontend call | Hayır | ✅ |
| Manuel Mongo müdahalesi | Hayır | ✅ |
| Hard delete | Hayır | ✅ |
| Relationship graph | Hayır | ✅ |
| MOD-0151 / MOD-0155 implementation | Hayır | ✅ |
| Yeni MOD ID / `module-id-registry.md` | Hayır | ✅ |

---

## 17. Open Items

| Item | Severity | Owner | Blocks MOD-0151? | Notes |
|---|---|---|---|---|
| Kimlik doğrulamalı browser golden flow (adım 8–13) | Low | operatör | Hayır | 97c5 oturumu + fleet restart (RESX); kod/unit/route kanıtı mevcut |
| İndirilebilir XLSX **result report** | Medium | UI/backend | Hayır | Bugün UI tablosu + özet var; dosya olarak indirme follow-up |
| Idempotency token / staging | Medium | backend | Hayır | Apply'da dosya yeniden gönderilir; aynı dosya iki kez apply edilirse ikinci sefer conflict/no-change üretir (sessiz duplicate değil), ama açık bir idempotency anahtarı daha güvenli olur |
| Instructions sayfası yalnız İngilizce | Medium | UI/L10n | Hayır | Task 1'den devam eden açık kalem (import UI 7 dilde) |
| Aynı dosyada oluşturulan contact'a `ReportsToContactId` | Low | backend | Hayır | Reports-to doğrulaması repo üzerinden çalışır; aynı dosyada yeni açılan yöneticiye bağlanma controlled hata verir |
| Bellek içi filtreleme + sabit satır/dosya limitleri | Medium/Low | backend | Hayır | Task 1'den devam |
| `RowVersion` / eşzamanlılık koruması | Medium | backend | Hayır | Şema 1.0'da yok; iki kullanıcı aynı contact'ı aynı anda güncellerse son yazan kazanır |
| CSV import desteği | Low | ürün | Hayır | Bilinçli olarak yok; UI'da açıkça yazıyor |
| AccountRelationship XLSX | Low | follow-up | Hayır | Ayrı iş; mevcut CSV/JSON endpoint'leri duruyor |
| **Patient/Healthcare Privacy Scope** | **High (governance)** | EA/Security | Hayır | Açık kalmaya devam ediyor — CRM genel closeout bu değerlendirme olmadan tamamlanmaz |
| `Crm:Audit:Mode=http` runtime cutover | Medium | operatör | Hayır | Import olayları bayrak açılınca HTTP audit'e gider |
| MOD-0149 Accounts compact verifier FAIL (`_Form` ↔ `Details` parity) | Medium | MOD-0149 | Hayır | **Bu taskla ilgisiz, mevcut durum** |

---

## 18. Registry / Status Update

- **Previous:** MOD-0150 — Closeout PASS (Review-ready), %100 (+ Location/PII-KVKK Hardening + Historical Lifecycle +
  Import/Export Task 1 PASS).
- **New:** aynı + **Contact Import/Export Task 2 (XLSX Upload + Dry-run Preview + Safe Apply) PASS** — round-trip
  döngüsü (indir → düzelt → yükle → önizle → uygula) tamamlandı.
- **Reason:** Closeout ve Task 1 kapsamı bozulmadan import döngüsü kapatıldı; CSV/JSON/XLSX export yolları
  değişmedi, builds/tests/verifier/RESX yeşil, boundary temiz, yeni permission/reference set/seed/route yok,
  historical lifecycle ve cross-country politikası import yolunda da garanti altında.
- **MOD-0151 hazırlığı:** Contact location ve Account link verisi artık toplu olarak **düzeltilebilir** (indir →
  düzelt → yükle) durumda; territory tasarımı için veri kalitesi yükseltme yolu açıldı (MOD-0151 implement edilmedi).

---

## 19. Final Verdict

**PASS** — XLSX upload, dry-run preview ve safe apply, Contact/AccountLink kimliği ve historical lifecycle korunarak
uygulandı. Build 0/0 (4 proje), CrmService **170/170**, compact verifier **PASS/0 FAIL**, RESX **121 × 7**, canlı route
smoke fail-closed (401/302/404/405), search guard'ları CLEAN. Açık kalemler blocker değil; en önemlisi kimlik
doğrulamalı indirme/yükleme smoke'u (Low, kod+unit+route kanıtı mevcut) ve governance düzeyindeki
Patient/Healthcare Privacy Scope (High, ayrı değerlendirme).

### Zorunlu karar cümleleri

1. **Task 2 completes the round-trip import loop by parsing Task 1 XLSX files, previewing changes, and applying only
   safe Contact and AccountLink operations.**
2. **Import must preserve stable Contact and AccountLink identity; updates must not destroy historical relationship facts.**
3. **Moving a contact from one account to another must be represented as ending the old link and adding a new link,
   not overwriting history.**
4. **Cross-country Contact↔Account policy applies equally to UI writes and XLSX import.**
5. **Import dry-run and result messages must be PII-safe and must not reveal raw names, phone numbers, emails, notes,
   or patient-related data.**
6. **Patient-related clinical data remains out of scope and requires a dedicated healthcare privacy scope.**

## 20. Next Recommended Prompt

**MOD-0151 Territory Management Pack Prep** — Contact/Account location ve ilişki verisi artık hem dışa aktarılabilir
hem toplu düzeltilebilir durumda; territory tasarımı için veri hazırlığı güçlendi. Alternatif olarak önce kapatılabilir:
**Patient/Healthcare Privacy Scope değerlendirmesi** (governance, High) — CRM genel closeout için önerilir.
