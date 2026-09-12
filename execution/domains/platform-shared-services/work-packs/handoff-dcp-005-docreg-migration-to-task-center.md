# DCP-005 — Görev formu CSV lookup → Document Management (Master Register) geçişi

**Kimden:** Doküman-Yönetimi tarafı (MOD-0029)
**Kime:** Görev Merkezi tarafı (MOD-0024)
**Tarih:** 2026-09-10
**Konu:** Görev oluşturma/düzenleme, dokümanları artık CSV lookup'tan (`document_reference_*`) değil, **canlı Master Register**'dan çözsün/arasın/dondursun. Bu belge **sizin (MOD-0024)** tarafınızda değiştirilecek yerleri, bizim vereceğimiz sözleşmeyi ve dokunulmayacakları tanımlar. Bizim tarafın işi (register tohumu + atıf-çözümleme ucu) ayrı WP'de yürür.

---

## 1. Özet

Görev formu bugün dokümanları **CSV lookup**'tan (Tasks altındaki `DocumentReferenceEntry` / `DocumentReferenceListVersion`) çözüyor. Hedef: aynı üç tüketim noktasını **Document Management Master Register**'a (`DocumentMasterRegisterEntry`) yönlendirmek. Atıf **dosya byte'ı** değil, bir dokümana **referans** (uid/kod/başlık/sürüm/durum) olduğundan, doğru kaynak Master Register'dır — klasör GEREKTİRMEZ. Gerçek PDF/Word (`ControlledDocument` + klasör) ayrı ve sonraki bir fazdır.

**Freeze semantiği korunur:** atıf hâlâ o andaki değerleri dondurur; yalnız kaynak değişir. Mevcut donmuş satırlara dokunulmaz (değer bazlı donmuşlar, güvendeler).

---

## 2. Sizin değiştireceğiniz noktalar

| # | Nokta | Bugün (lookup) | Yapılacak (register) | Tuzak |
|---|---|---|---|---|
| 1 | Governing docs otomatik gelme | [GetTaskTypeGoverningDocumentsHandler](../../../../services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/TaskTypeQueryHandlers.cs) → `IDocumentReferenceListRepository.GetEntriesByUidsAsync` | bizim **atıf portu** (A.2) `ResolveAsync(uids, by:"uid")` | citable/blocked/unresolved üçlü ayrımını **koru** — blocked'ı gizleme |
| 2 | Atıf dondurma | [TaskDocumentReferenceFreezer](../../../../services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskDocumentReferenceFreezer.cs) → lookup `GetLatestVersionAsync` + `GetEntriesByUidsAsync` | aynı freezer, register satırından freeze | `ListVersionId` artık nullable + `Source` işareti (§4) |
| 3 | Picker arama | frontend [Tasks/api.js](../../../../frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js) `/document-list/search` → [SearchDocumentReferencesHandler](../../../../services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/DocumentReferenceListQueryHandlers.cs) | bizim atıf portu (A.2) `SearchAsync(term, limit)`; frontend proxy rotasını da güncelle | `api.js` proxy — rota adı orada da değişmeli (yoksa 404) |
| **4** | **Etkinleştirme kapısı (Adım 2 — YAPILMADI)** | — (tüketici yok) | `PUT task-types/{id}/active` ([TasksController.cs:712](../../../../services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs)) → `IControlledDocumentEffectivenessPort.ResolveAsync(uids, by:"uid")` tüket; **fail-closed** (hepsi Effective değilse RED) | Ölçüldü 2026-09-10: `TaskTypeHandlers.cs`'te `Effectiveness` referansı **0**; port hiç tüketilmiyor. Önceki handoff'un §5 Adım 2'si yapılmamış. |

> **Not (Adım 3):** `/active` ret kuralının tam metni (blocked → `task_type_enable_blocked_documents`, port istisnası → `task_type_enable_register_unavailable`) hâlâ **G3'e** (Kalite'nin "Kural 4" kararı) bağlı. Adım 2 (portu tüketmek) G3'ü beklemez; ret cümlesinin kesinleşmesi bekler.

Frontend'de dokunulacak dar yüzey: [Tasks/api.js](../../../../frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js) (2 çağrı), [Tasks/form-page.js](../../../../frontend/Diten.Web/wwwroot/assets/js/Tasks/form-page.js) ve [Tasks/form.js](../../../../frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js) (`documentUids` gönderimi — muhtemelen değişmez, kaynak arka planda değişir).

---

## 3. Alan eşlemesi (lookup → register)

`TaskDocumentReference` alanları bugün `DocumentReferenceEntry`'den; register karşılıkları:

| TaskDocumentReference | Lookup (bugün) | Register (`DocumentMasterRegisterEntry`) |
|---|---|---|
| `DocumentUid` | `DocumentUid` | `PermanentUid` |
| `DocumentCode` | `DocumentCode` | `DocumentCode` |
| `Title` | `Title` | `DocumentTitle` |
| `DocumentVersion` | `DocumentVersion` | `CurrentVersionLabel` |
| `Status` | `Status` | `LifecycleStatus.ToString()` |
| citable mi? | `LinkableInErp` | effectiveness `State == Effective` (`ControlledDocumentLifecyclePolicy.IsOperationallyEffective`) |

Bizim **atıf DTO'muz** bilerek `DocumentReferenceEntryDto`'ya benzeyecek → eşlemeniz minimum. DTO ayrıca `Source` (=`MasterRegister`) ve traceability için register satır kimliğini taşıyabilir (§4).

---

## 4. Açık kararlar — CEVAPLANDI

- **G1 join = (a) ONAYLANDI** (2026-09-04, [WP-0029-EFFECTIVENESS-P2.md](WP-0029-EFFECTIVENESS-P2.md) "Görev Merkezi cevabı işlendi"): register CSV UID'lerini sahiplenir, çağrılarda **`by="uid"`**. Bu adım **tamamlanmış** kabul edilir; ayrı bir onay beklemez.
- **`ListVersionId` SİLİNMESİN.** Bugün `required Guid` ([TaskItem.cs:421](../../../../services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskItem.cs)) — silmek kırıcı. **`Guid?` yapılın + nullable `Source` işareti ekleyin** (`CsvLookup | MasterRegister`). Additive: eski donmuş satırlar olduğu gibi okunur (`Source=null` ⇒ eski CSV kökeni), yeni satırlar `Source=MasterRegister`, `ListVersionId=null` taşır (register'da import-sürümü yok).
- **Picker'da blocked:** **koru** — göster, sebebiyle, seçtirme (bugünkü davranışın aynısı).
- **`by`:** `"uid"` (G1-a).

---

## 5. Sıra ve bağımlılık

1. **A.2 (atıf-çözümleme ucu) bizim tarafımızca teslim edilecek** — söz verilen DTO/port şekli sabitlenince haber vereceğiz. Repoint'i o zaman başlatın (şekle göre yazıp sonra yeniden yazmamak için).
2. Sizin tarafta aynı dosyalara dokunan **görev kapanış zarfı** işi önce bitecek (sizin planınız).
3. Register tohumu (bizim Adım 0) A.2'ye paralel ilerler; repoint'iniz tohumu **beklemez** (fixture/mock ile ilerler), yalnız canlı Effective/Blocked doğrulaması tohumdan sonra.

**A.2 hazır olunca size haber vereceğiz.**

---

## 6. DOKUNMAYIN

- **Mevcut donmuş `TaskDocumentReference` satırları** — değer bazlı donmuş; migration/re-freeze YOK.
- **Register'ın kendisi + tohumlama** — bizim işimiz (Adım 0).
- **İkinci kopya tablo yaratmayın** — register tek otorite; lookup'ı register'a kopyalamak DCP-005 §6.1'in engellediği "ikinci otorite"yi geri getirir.

---

## 7. Kabul kriterleri (sizin diliminiz)

1. Görev türü seçilince governing docs **register'dan (port)** gelir; citable/blocked/unresolved doğru ayrışır.
2. Picker araması register'dan sonuç döndürür (blocked görünür/seçilemez).
3. Yeni atıf register satırından donar; alanlar §3 eşlemesine uygun; `Source=MasterRegister`.
4. **Etkinleştirme kapısı (Adım 2):** `PUT task-types/{id}/active` effectiveness portunu tüketir; fail-closed. Register tohumlanınca gerçek Effective/Blocked görür. *(Ret cümlesinin kesinleşmesi Adım 3 / G3 bekler.)*
5. Mevcut donmuş atıflar değişmeden okunur (`ListVersionId` nullable geçişi geriye-uyumlu).
6. `dotnet test` ilgili proje yeşil; build temiz.

**Repoint'in güncelleyeceği testler (sizin taraf):** `TaskDocumentReferenceTests.cs`, `DocumentReferenceListTests.cs`, `TaskDocumentFreezerDoubles.cs`, `PlatformSchemaContractMongoTests.cs`.

---

## 8. Bizim tarafın notu — Adım 0 tohumunda `CollectionInstanceId`

`DocumentMasterRegisterEntry.CollectionInstanceId` **non-nullable `Guid`** ([entity:29](../../../../services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/DocumentMasterRegisterEntry.cs)) — DCP-005 §6.1'in 2. gerekçesi tam buydu ("her kayıt bir koleksiyon ister").

**Nasıl karşılıyoruz:** 358 satırı tohumlarken `CollectionInstanceId = Guid.Empty` yazıyoruz — **mevcut FU06 manuel-create yolunun** yaptığının aynısı (`DocumentMasterRegisterService.CreateAsync` bu alanı hiç set etmez → `Guid.Empty` default). Anlamı: "koleksiyon örneği YOK — bu bir yönetişim/atıf projeksiyonu, klasöre bağlı bir kontrollü doküman değil." Gerçek klasör bağlama yalnız DM-4'te, satır bir `ControlledDocument`'a `LinkControlledDocumentAsync` ile bağlanınca olur. Yani register'ın klasörsüz yaşaması tasarımın parçası; §6.1 gerekçesi `ControlledDocument` (gerçek dosya) içindir, register projeksiyonu için değil.

---

## 9. Referanslar

- Sözleşme: [dcp-005-effectiveness-contract-v2.md](authority/dcp-005-effectiveness-contract-v2.md)
- Önceki handoff (Adım 1): [handoff-dcp-005-adim1-to-task-center.md](handoff-dcp-005-adim1-to-task-center.md)
- G1 onayı + Adım 2 durumu: [WP-0029-EFFECTIVENESS-P2.md](WP-0029-EFFECTIVENESS-P2.md)
- Effectiveness portu (hazır): `...Features.DocumentManagementMasterRegister.Services.IControlledDocumentEffectivenessPort`
- Kaynak CSV: [docs/integration/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv](../../../../docs/integration/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv) (358 satır)
