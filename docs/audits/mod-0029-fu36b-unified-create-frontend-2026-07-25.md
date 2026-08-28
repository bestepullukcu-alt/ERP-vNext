# MOD-0029-FU36B Unified Create Frontend — Implementation Audit

Date: 2026-07-25  
Scope: frontend + MVC proxy + localization + verifier  
Commit/push: yapılmadı

## 1. Final Verdict

**PASS_WITH_GAPS**

Unified controlled-document create akışı, same-origin MVC proxy, 16 alanlı Compact form, güvenli file upload,
Completed-only success, operation retry, iki giriş noktasının yönlendirilmesi ve 7 dil localization tamamlandı.
Opsiyonel Controlled Document Details reverse Master Register kartı FU36C'ye ertelendi.

## 2. Initial Audit Summary

- FU36 pack `ready-for-dev` ve FU36A backend foundation mevcut.
- FU36A create/get/retry route ve DTO sözleşmeleri doğrulandı.
- Existing Master Register FU24–FU29 proxy, permission snapshot, RESX ve Details tab yüzeyleri korundu.
- Existing Controlled Documents multipart upload ve template/document ortak create kalıbı incelendi.
- Working tree'deki unrelated CRM, AuthService, Gateway/Ocelot ve diğer değişikliklere dokunulmadı.

## 3. Frontend Scope Delivered

- Master Register altında yeni unified create page eklendi.
- Golden Compact uyumlu ayrı route/view/partial kullanıldı; offcanvas eklenmedi.
- Formda yalnız onaylı 16 kullanıcı alanı bulunuyor.
- Governance uyarıları görünür kılındı.
- Operation sonucu, incomplete/failure state ve retry yüzeyi eklendi.

## 4. Routes & Navigation

- `GET /DocumentManagementMasterRegister/CreateControlledDocument`
- `POST /DocumentManagement/MasterRegister/api/controlled-document-registrations`
- `GET /DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId}`
- `POST /DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId}/retry`
- Master Register “New Controlled Document” aksiyonu unified route'a gider.

## 5. Unified Create Form

Alanlar: title, class, criticality, type, description, tags, governing language, owner function, owner company,
process owner role, process owner user, review cycle, retention class, company/legal entity, folder ve initial file.

TenantId, UID, DocumentCode, EffectiveDate, lifecycle/register, approval, release-gate ve signature alanları yoktur.
Template document type unified formda sunulmaz.

## 6. Operation Submit / Status / Retry Handling

- Browser `FormData` ve antiforgery token gönderir.
- Idempotency key sayfa yüklemesinde bir kez üretilir ve aynı create denemesi için korunur.
- Yalnız `Completed` success kabul edilir.
- Ara durumlar ve bilinmeyen durumlar incomplete olarak gösterilir.
- `Failed` ve `CompensationPending` success üretmez.
- Retry, full formu yeniden submit etmez; mevcut `operationId` üzerinden devam eder.
- Completed response'ta `MasterRegisterEntryId` varsa Details sayfasına yönlendirilir.

## 7. Controlled Documents Redirect

- Explorer “Add Document” unified Master Register create route'una yönlendirildi.
- `/DocumentManagementControlledDocuments/Create` normal çağrısı controller seviyesinde unified route'a redirect eder.
- `?kind=template` mevcut template create view'ını açmaya devam eder.
- Explorer, version upload, preview, download, share, move ve favorite yüzeyleri korunmuştur.

## 8. Reverse Master Register Card

Opsiyonel reverse-link kartı bu task'ta eklenmedi. Controlled Documents Details yüzeyinin mevcut karmaşıklığı ve bu
kartın bağımsız permission/error UX gerektirmesi nedeniyle FU36C'ye ertelendi. FU36A reverse lookup endpoint'i hazırdır.

## 9. MVC Proxy / API Consumption

- MVC proxy Gateway URL convention'ını kullanır.
- Bearer ve tenant context server-side çözülür.
- Browser `X-Tenant-Id`, TenantId veya doğrudan 5057 kullanmaz.
- File `IFormFile` olarak alınır; filename normalize edilir.
- MVC boundary, file içeriğini bellekte FU36A JSON `InitialFile` sözleşmesine uyarlar; disk veya JS base64 state yoktur.

## 10. Permission Gating

Create görünürlüğü:

- `platform.document-management.master-register.registration.create`
- `platform.document-management.master-register.manage`
- `platform.document-management.master-register.link`
- `platform.document-management.controlled-documents.create`

Retry:

- `platform.document-management.master-register.registration.reconcile`

Frontend kontrolleri UX guard'dır; backend yetkilendirmesi authoritative kalır.

## 11. Localization / RESX Parity

Master Register RESX key seti `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr` için eşittir. Her dosyada 792 key vardır,
duplicate key yoktur. Yeni create, warning, operation, retry, file, folder ve redirect metinleri yedi dilde eklendi.

## 12. Tests / Verifier

- FU24: PASS — 65/65
- FU25: PASS — 113/113
- FU26: PASS — 130/130
- FU27: PASS
- FU28: PASS — 155/155
- FU28A: PASS — 120/120
- FU29: PASS — 153/153
- FU36A verifier: PASS
- FU36B verifier: PASS
- `node --check create-controlled-document.js`: PASS

FU24 verifier'ın eski “controller genelinde hiç upload olamaz” kontrolü, FU36B'nin ayrı upload route'unu yanlış
pozitif saymaması için yalnız FU24 metadata create/edit/details yüzeyine daraltıldı.

## 13. Build Results

`dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug --no-restore`

Sonuç: PASS, 0 warning, 0 error.

## 14. Guardrail Verification

- Direct 5057: yok
- Browser TenantId / X-Tenant-Id: yok
- JS base64 file storage: yok
- Non-Completed success: yok
- UID/code allocation automation: yok
- Approval/effective/signature/release automation: yok
- Delete/purge/archive: eklenmedi
- Backend business logic: değiştirilmedi
- AuthService: FU36B kapsamında değiştirilmedi
- Gateway/Ocelot: FU36B kapsamında değiştirilmedi
- MOD-0028: değiştirilmedi
- Commit/push: yapılmadı

Scoped `git diff --check`: PASS. Repo-global kontrol yalnız önceden mevcut
`watch-diten.ps1:6 trailing whitespace` nedeniyle FAIL verir; scope dışı olduğu için değiştirilmedi.

## 15. Remaining Gaps

- Controlled Document Details reverse Master Register kartı FU36C'ye kaldı.
- Runtime browser smoke bu task'ın açık “runtime smoke kapsamını genişletme” sınırı nedeniyle yapılmadı.
- Generic `verify_datatable_page.py` mevcut FU24 Master Register sayfasında FU36B öncesinden gelen 21 Golden
  contract açığı raporlar; FU24–FU29 task verifier'ları yeşildir ve bu task bu legacy refactor kapsamını genişletmedi.

## 16. Files Changed

- `frontend/Diten.Web/Controllers/DocumentManagementMasterRegisterController.cs`
- `frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/CreateControlledDocument.cshtml`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_CreateControlledDocumentForm.cshtml`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_CreateControlledDocumentL10n.cshtml`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/Index.cshtml`
- `frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_IndexL10n.cshtml`
- `frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/Index.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/create-controlled-document.js`
- `frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/index.js`
- `frontend/Diten.Web/Resources/Views/DocumentManagement/MasterRegister/MasterRegisterIndex.{culture}.resx`
- `scripts/verify-mod0029-fu24-ui.ps1`
- `scripts/verify-mod0029-fu36b-unified-create-frontend.ps1`
- Bu audit dosyası

## 17. Confirmations

Frontend unified create system-of-entry olarak Master Register altında çalışır. Controlled Documents operational
explorer olarak kalır. Template create ayrıdır. Manual link normal create yoluna eklenmemiştir. Tüm değişiklikler
working tree'dedir.

## 18. Next Recommended Step

FU36B için yetkili tenant kullanıcısıyla sınırlı canlı smoke yapılması; ardından FU36C kapsamında Controlled Document
Details reverse Master Register kartının ayrı permission ve not-linked UX sözleşmesiyle uygulanması önerilir.
