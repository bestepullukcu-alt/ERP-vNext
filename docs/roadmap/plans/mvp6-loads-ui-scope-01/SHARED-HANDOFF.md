# Tek integration-owner handoff — HELD

CT bir integration owner atar; UI writer ve başka lane aynı shared dosyaya yazmaz. Atama/approval henüz yok. Final integration target seçilince owner aşağıdaki işler için exact paths+preimage hashes+diff sunar; geniş gateway/registration wildcard yetkisi yoktur.

| Sınır | Tek owner teslimi | Kabul / release etkisi |
|---|---|---|
| Gateway | Mevcut /api/shipment-bundle/loads GET/POST yönlendirmesini ölç; gerekirse yalnız bu iki operation'ın exact route diff'i, token/scope/correlation/key passthrough | Shipment/Carrier rotası byte/davranış regression; API'ye direct bypass yok |
| Auth/scope | Gerçek login/refresh ile signed tenant_id/legal_entity_id/sub ve read/create permission; missing LE fail-closed | Tanısal token gerçek Auth PASS değildir; header/body'den yeni scope seçilmez |
| Registration/RBAC | Tenant page route /SupplyChain/Loads, requiredPermission supplychain.loads.read, create action supplychain.loads.create; mevcut provider/DI seam'ine exact ek | Transition UI action kaydedilmez; backend transition key silinmez; permission otomatik grant edilmez |
| Navigation/search | Aynı gerçek tenant route ve page identity, ilgili manifest pattern'i; tenant dynamic navigation/search reuse | Yeni ModuleCode/PageCode registry ile uzlaştırılmadan literal identity onaylanmış sayılmaz; /Platform escalation yok |
| Shared L10n | Nav.Module/ Nav.Page ve gerçekten eksik ortak label/error anahtarları en,tr,fr,es,zh,ar,ru | Module resource içine ortak metin kopyalama yok; tek owner hash-bound resource diff |
| Personalization | Mevcut shared personalizationClient ve tenant Gateway yolunun kullanımını doğrula | Save View/Reset/ColReorder çalışır; shared servis veya localStorage alternatifi yazılmaz |
| Live references/root | Mevcut published Carrier/Shipment seams için yetkili bağlantı ve ayrı multi-Shipment root disposition; ROOT-UI-01 için gelecekteki owner çözümü | Mock acceptance live acceptance olmaz; UI writer root/API icat etmez |

Integration path'leri hedef checkout seçilmeden final değildir; bu hazırlığın açık dispatch engelidir. UI-owned path'ler OWNED-PATHS.md'de exact'tır. Shared ihtiyaçlar UI allowlist'e eklenmez. Bu paket gateway/permission/seed/nav değişikliği veya hizmet restart'ı uygulamaz.
