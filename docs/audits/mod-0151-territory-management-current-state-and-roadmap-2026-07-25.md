# MOD-0151 Territory Management — Current State and Roadmap

> **Rapor tarihi:** 2026-07-25  
> **Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> **Mevcut genel durum:** **PARTIAL — core, UI ve manual lifecycle hazır; FU02B authenticated Gateway smoke closeout bekliyor**

## 1. Executive Summary

MOD-0151, Commercial Suite (CRM + O2C) içinde CRM Core'a ait bir **Domain App / Territory Management**
capability'sidir. Satış bölgelerinin versiyonlu bir model içinde ülke, region, area, zone ve microzone seviyelerinde
tanımlanmasını sağlar.

Bugün business kullanıcı:

- Territory Model oluşturabilir ve düzenleyebilir.
- Country Scope ve birden fazla Business Unit Scope seçebilir.
- Modelin coğrafi node ağacını oluşturabilir ve görüntüleyebilir.
- Draft modeli manual olarak active yapabilir; active modeli inactive yapabilir.
- Inactive veya süresi hesaplanan modeli archive edebilir.
- Yalnız draft model/node kayıtlarını soft-delete edebilir.
- `EffectiveTo` geçtiğinde computed expired durumunu görebilir.

Bugün henüz kullanılamayan ana kabiliyetler MR/resource assignment, assignment rule ve preview, account assignment
apply/history, workflow approval, evidence pack, import/export ve visit/route planning entegrasyonudur.

FU02B backend/UI implementasyonu ve testleri tamamlanmıştır: Territory testleri **63/63**, tüm CrmService testleri
**232/232** PASS. Ancak `mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-2026-07-25.md` bulunmadığı için
FU02B operasyonel closeout'u **PARTIAL** kabul edilir.

## 2. What Has Been Implemented

| Area | Status | Explanation |
|---|---|---|
| FU00 — Pack approval / reconciliation | PASS | MOD-0151 canonical olarak Territory Management, CRM Core, Domain App (CRM) konumunda; runtime scope kontrollü açıldı |
| MOD-0048 reference readiness | PASS | 12/12 set, **73/73 published value**, tenant contract `isReady=true` |
| RBAC / permissions | PASS | Beş mevcut permission doğrulandı ve tenant admin claim smoke tamamlandı |
| FU01 — Backend core | PASS | Contract, TerritoryModel, TerritoryNode, repository, CQRS, validation ve Gateway smoke |
| FU02 — UI / Model Viewer | PASS | Tenant menüsü, DataTable, model formu, hierarchy viewer, node create/edit ve 7 dil |
| FU02A — Country/BU selectors | Implemented; original report has addendum | Country single-select, Business Unit multi-select ve `BusinessScopes` persistence/test addendum'u |
| FU02B — Manual lifecycle | PARTIAL closeout | Kod, UI, audit seam ve testler hazır; authenticated Gateway lifecycle smoke raporu yok |

### Reference Data

Hazırlanan/publish edilen setler yalnız aşağıdakilerle sınırlı olmamak üzere `territory-level`,
`territory-model-status`, `territory-node-status`, assignment status/type, rule type ve conflict policy
değerlerini içerir. Tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` için son FU01 live smoke:

- 12/12 set hazır
- 73/73 value hazır
- Contract `isReady=true`
- Gateway smoke 23/23 PASS

### RBAC

Kullanılan permission'lar:

- `crm.territory.read`
- `crm.territory.model.read`
- `crm.territory.model.manage`
- `crm.territory.node.read`
- `crm.territory.node.manage`

Bilerek eklenmeyen permission'lar:

- `crm.territory.delete`
- `crm.micro-zone.manage`

MicroZone ayrı permission veya aggregate değildir; `TerritoryNode` hiyerarşisinin bir seviyesidir. Delete işlemi
hard delete değil, mevcut manage permission ile yalnız draft soft-delete'tir.

### FU01 — Backend Core

Uygulananlar:

- Territory contract endpoint
- Model create/get/list/update
- Node create/get hierarchy/update
- Tenant izolasyonu ve cross-tenant 404
- Model code ve territory code uniqueness
- Parent/model tarih containment
- Level rank ve cycle validasyonu
- Reference-data fail-closed validasyonu
- MicroZoneProfile koşullu doğrulaması
- Gateway-only canlı smoke

### FU02 — Territory Model Viewer

Uygulananlar:

- Tenant shell Territory Management menüsü
- Golden-reference DataTable ve filtre/save-view davranışı
- Territory Model liste, detay, create/edit
- Territory hierarchy viewer
- Territory Node create/edit canvas/offcanvas akışları
- MicroZoneProfile alanlarının yalnız microzone seviyesinde gösterilmesi
- Gateway üzerinden API kullanımı; doğrudan 5061 çağrısı yok
- 7 dil RESX parity

### FU02A — Country and Business Unit Scope

- Country Scope, MOD-0048 kaynaklı single select'tir.
- Division Scope UI adı/serbest alanı kaldırılmış; Business Unit Scope multi-select kullanılmıştır.
- Business scope payload/persistence `scopeType="business-unit"` ile çalışır.
- Duplicate scope code'lar case-insensitive normalize edilir.
- Alpha/Beta/Gamma business unit olarak kullanılabilir.
- Almiba/Tutukon gibi brand değerleri business unit değildir ve bu forma bilerek eklenmemiştir.

FU02A raporunun üst verdict'i ilk uygulama anını **PARTIAL** gösterir. Aynı rapordaki backend addendum'u
`BusinessScopes` persistence'ı ve **221/221 CrmService test PASS** sonucunu belgeler. Buna rağmen live authenticated
create smoke ayrı closeout olarak izlenmelidir.

### FU02B — Lifecycle

Uygulanan manual lifecycle:

- `draft → active`
- `active → inactive`
- `inactive → active`
- `inactive/computed-expired → archived`
- `draft → soft-deleted`
- Model activation/deactivation/archive ile node status senkronizasyonu
- Single-active-model overlap guard
- Computed expiry
- Lifecycle action görünürlüğü ve Golden Slim confirmation modalları
- Structured lifecycle audit event seam'i

Bu manual activation **workflow approval değildir**. MOD-0023, submit/approve/reject, transition gate ve approval
trace FU06'ya aittir.

## 3. How Territory Management Works

### Territory Model

Territory Model, bir bölge planının **üst kabı ve versiyonudur**. Ülke, business unit kapsamı, geçerlilik dönemi ve
coğrafi node ağacı aynı model altında tutulur.

Örnek:

`TM-2027-TR` = 2027 Türkiye Territory Modeli.

Yeni dönem veya önemli organizasyon değişikliği için mevcut aktif kayıt üzerinde geçmişi bozmak yerine yeni model
versiyonu oluşturulması hedeflenir.

### Territory Node

Territory Node, model içindeki gerçek coğrafi organizasyon öğesidir. Tek node tipi farklı seviyeleri taşır:

```text
Türkiye
  Marmara Region
    İstanbul Avrupa Area
      Beylikdüzü Zone
      Esenyurt Zone
      Avcılar Zone
```

Parent seçimi ağacın yerini belirler. Örneğin İstanbul Avrupa Area'nın parent'ı Marmara Region olmalıdır.

### Country Scope

Modelin hangi ülke için geçerli olduğunu söyler:

`Country Scope = Türkiye`

Bu alan node ağacındaki her ülke kodunu tekrar etmekten farklıdır; modelin business kapsam anahtarının parçasıdır.

### Business Unit Scope

Modeldeki coğrafi ağacın hangi ticari business unit'ler için kullanılacağını söyler:

`Business Unit Scope = Alpha, Beta`

Multi-select'tir. Aynı coğrafi ağacı paylaşan business unit'ler tek modelde birlikte seçilebilir.

### Brand Scope — Future

Almiba, Tutukon veya Bekant birer brand ise Business Unit Scope'a yazılmamalıdır. Brand master ve
Brand/Marketing sahipliği netleşmeden Territory Management içine hardcoded brand listesi eklemek veri sahipliğini
bozar. Brand Scope, ilgili Brand/Marketing capability hazırlandıktan sonra ayrı entegrasyon olarak ele alınmalıdır.

### Lifecycle / Status

Model ve node ilk oluşturulduğunda draft'tır. Manual lifecycle operasyonları bugün vardır; fakat approval workflow
yoktur. Effective date'in geçmesi persisted status'u otomatik değiştirmez, API/UI computed expiry üretir.

## 4. Example Scenario

Senaryo:

- Country: Türkiye
- Business Unit: Alpha, Beta
- Brands: Almiba, Bekant
- Coğrafya: Türkiye > Marmara > İstanbul Avrupa > Beylikdüzü Zone

Önerilen kurgu:

```text
Tek Territory Model
  Country Scope: Türkiye
  Business Unit Scope: Alpha, Beta

Tek Node Ağacı
  Türkiye
    Marmara
      İstanbul Avrupa
        Beylikdüzü Zone
```

Alpha ve Beta aynı saha/coğrafya ağacını kullanıyorsa zone ağacı iki kere girilmemelidir. Tek modelde Alpha ve Beta
birlikte seçilmeli, node ağacı bir kez oluşturulmalıdır.

Gelecekte assignment tarafı şu ayrımı yapacaktır:

```text
Beylikdüzü Zone + Alpha + Almiba → MR A
Beylikdüzü Zone + Beta + Bekant → MR B
```

Bugün bu son iki assignment satırı oluşturulamaz. MR'ın hangi zone, business unit, brand veya product portfolio
için çalışacağı FU03 rule/preview, FU04 resource assignment ve ürün/brand master entegrasyonuyla çözülecektir.

## 5. Current Limitations

| Capability | Current Status | Future FU |
|---|---|---|
| Brand Scope | Yok | Brand/Marketing capability sonrası follow-up |
| Brand/Marketing integration | Yok | Future integration |
| Assignment rules | Yok | FU03 |
| Assignment preview/conflict preview | Yok | FU03 |
| MR / manager resource assignment | Yok | FU04 |
| MR-brand/product portfolio ilişkilendirmesi | Yok | FU04 + Brand/Product master follow-up |
| Account assignment apply | Yok | FU05 |
| Assignment history/effective dating | Yok | FU05 |
| Workflow approval | Yok | FU06 |
| Submit/approve/reject | Yok | FU06 |
| Approval trace / immutable approved snapshot | Yok | FU06 |
| Evidence pack / audit export | Yok | FU07 |
| XLSX import/export | Yok | FU08 |
| Visit/route readiness | Yok | FU09 / MOD-0155 |
| Background expiry scheduler | Yok | Future lifecycle hardening |
| FU02B authenticated lifecycle smoke closeout | Eksik | İlk sıradaki closeout |

## 6. Lifecycle / Status Explanation

| Status | Meaning | Current Behavior |
|---|---|---|
| Draft | Hazırlanan, henüz kullanımda olmayan model/node | Editlenebilir; yalnız bu status soft-delete edilebilir |
| Active | Operasyonel olarak kullanıma alınmış model | Manual activation ile oluşur; uygun node'lar active olur |
| Inactive | Geçici/operasyonel olarak kullanım dışı | Tekrar active yapılabilir veya archive edilebilir |
| Archived | Geçmiş kayıt | Read-only; operasyonel değişiklik yapılamaz |
| Soft-deleted | Draft kaydın mantıksal olarak silinmesi | `IsDeleted/DeletedAt`; default listelerde görünmez; hard delete yok |
| Expired | `EffectiveTo` geçmiş kaydın hesaplanan durumu | `isExpired=true` / `computedStatus=expired`; stored DB status korunur |

`EffectiveTo` geçtiğinde DB status'unu sessizce değiştiren scheduler yoktur. Bu bilinçli bir karardır:

- Read işlemi geçmişi mutate etmez.
- Kullanıcı active/inactive gibi stored durumu ayrıca görebilir.
- Expiry hesaplaması tarih ve saat bağlamında deterministiktir.
- Archive işlemi açık bir business aksiyonu ve audit izi olarak kalır.

## 7. Recommended Next Steps

1. **MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout**

   Neden: Kod/test PASS olsa da gerçek tenant tokenı ile lifecycle zinciri kapanmadı.  
   Açacağı değer: activate, overlap rejection, deactivate, archive, draft/node soft-delete ve computed expiry'nin
   gerçek Gateway kanıtı.  
   Çözdüğü risk: test ile deployment/runtime arasındaki fark.

2. **MOD-0151 FU03 — Assignment Rules + Preview**

   Neden: Coğrafi ağacın account/business scope ile nasıl eşleşeceği tanımlanmalı.  
   Açacağı değer: geography/account-list/account-type/business-scope rule ve yan etkisiz preview.  
   Çözdüğü risk: yanlış hesaplara veya çakışan territory'lere otomatik atama.

3. **MOD-0151 FU04 — Resource Assignments**

   Neden: Zone'a MR, Area Manager, Regional Manager ve diğer roller bağlanmalı.  
   Açacağı değer: kişi/pozisyon bazlı coverage ve exclusivity kontrolleri.  
   Çözdüğü risk: aynı kapsamda mükerrer primary sorumluluk.

4. **MOD-0151 FU05 — Account Assignment Apply + History**

   Neden: FU03 preview sonucu kontrollü olarak gerçek account assignment'a uygulanmalı.  
   Açacağı değer: effective-dated apply, history ve MOD-0149 CoverageSummary projection.  
   Çözdüğü risk: geçmiş atamaların silinmesi ve manual override'ların ezilmesi.

5. **MOD-0151 FU06 — Workflow Approval + Activation**

   Neden: Manual lifecycle yerine yönetişimli değişiklik/onay süreci gerekir.  
   Açacağı değer: MOD-0023 submit/approve/reject, transition gate, approval trace ve immutable approved snapshot.  
   Çözdüğü risk: onaysız kritik territory değişiklikleri ve sahte approval.

6. **MOD-0151 FU07 — Evidence Pack + Audit Export**

   Neden: Approval ve activation kararlarının denetlenebilir paketi gerekir.  
   Açacağı değer: evidence pack, correlation ve audit export.  
   Çözdüğü risk: karar kaynağının ve değişiklik izinin kanıtlanamaması.

7. **MOD-0151 FU08 — Import/Export Hardening**

   Neden: Büyük modelleri elle girmek ölçeklenmez.  
   Açacağı değer: XLSX template/export/upload/dry-run/safe apply.  
   Çözdüğü risk: toplu yüklemede sessiz veri bozulması.

8. **MOD-0151 FU09 — MOD-0155 Visit/Route Readiness APIs**

   Neden: Visit ve route planning'in territory coverage verisini güvenli okuması gerekir.  
   Açacağı değer: account/microzone/resource coverage ve roll-up API'leri.  
   Çözdüğü risk: MOD-0155'in CRM SoR verisini doğrudan veya tutarsız tüketmesi.

9. **Brand Scope Follow-up**

   Neden: Brand/Product master ve Marketing ownership hazır olmadan brand değerleri hardcode edilmemeli.  
   Açacağı değer: zone + BU + brand/product portfolio düzeyinde assignment.  
   Çözdüğü risk: Almiba/Tutukon/Bekant gibi brand'lerin business unit sanılması.

## 8. Risk / Decision Notes

- **Brand Scope neden sonra?** Brand değerlerinin canonical SoR'u MOD-0151 değildir. Önce Brand/Marketing veya MDM
  master capability hazır olmalıdır.
- **Business Unit Scope neden şimdi?** Territory modelinin hangi ticari organizasyonlarca paylaşıldığını belirler ve
  single-active-model uniqueness anahtarının parçasıdır.
- **Zone ağacı neden kopyalanmamalı?** Aynı fiziksel coğrafyanın Alpha ve Beta için iki kez tutulması drift,
  çelişkili parent ve çift maintenance üretir. Ayrım assignment katmanında yapılmalıdır.
- **EffectiveTo neden tek başına stored status değiştirmemeli?** Read sırasında geçmişi mutate etmek audit ve
  determinism sorunları doğurur. V1 computed expiry gösterir; archive açık aksiyondur.
- **Workflow approval neden FU06?** Approval; MOD-0023 instance, transition gate, before/after diff, immutable snapshot
  ve trace gerektirir. FU02B yalnız güvenli manual lifecycle'dır; sahte workflow üretmez.
- **FU02B neden henüz tam PASS değil?** Implementation/test raporu PARTIAL'dır ve ayrı authenticated Gateway
  closeout raporu bulunmamaktadır.
- **Kaynak dosya notu:** Talepteki
  `mod-0151-fu00-pack-approval-source-reconciliation-closeout-2026-07-23.md` bulunmadı; canonical mevcut rapor
  `mod-0151-fu00-pack-approval-closeout-2026-07-23.md` kullanıldı.

## 9. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-territory-management-current-state-and-roadmap-2026-07-25.md` | Created | Mevcut durum, business açıklama, sınırlar ve roadmap |

Kod, runtime, module pack, reference data, RBAC, Gateway ve Mongo değiştirilmedi. Smoke çalıştırılmadı.

## 10. Final Recommendation

İlk prompt:

`MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout`

Bu closeout; contract flags, draft model/node create, activate, overlapping model rejection, deactivate, archive,
archived edit rejection, draft soft-delete ve computed expiry akışlarını target tenant üzerinden PASS ile
kanıtlamalıdır.

FU02B live smoke PASS olduktan sonra **FU03 Assignment Rules + Preview** açılmalıdır. FU04 ve FU05 bundan sonra
sırasıyla resource ve account assignment davranışlarını eklemelidir.

Brand Scope'a; Brand/Marketing veya Product/Brand master capability canonical değerleri, sahipliği ve published
reference/API sözleşmesi hazır olduğunda dönülmelidir. O zamana kadar Alpha/Beta Business Unit Scope olarak
kullanılmalı; Almiba/Tutukon/Bekant gibi brand'ler bu alana yazılmamalıdır.
