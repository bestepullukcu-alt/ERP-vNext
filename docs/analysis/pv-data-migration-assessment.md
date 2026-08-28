# PV Data Migration Assessment (F)

> Kaynak: 5 MongoDB veritabanı (`PvOrganization`, `PvTenant`, `PvUser`, `PvLookup`, `PvSurvey`) `mongodb://localhost:27017`.
> Not: Gerçek veri hacmi/kalitesi **runtime doğrulanmadı** — aşağıdakiler şema-tabanlı değerlendirmedir.

## Kaynak model envanteri (ana koleksiyonlar)

| Koleksiyon (DB) | PK | Referanslar | Tenant sahipliği | Status/Tarih | Ekler |
|---|---|---|---|---|---|
| `SafetyReport` (PvOrganization) | `Id` ObjectId | `CountryId`, `GlobalSkuId`, `PatientId`, `Assigned*Id`, `AssignedOrganizationId`, `SubmissionAuthorityId` | `TenantId` (string) | `Status`(bool), `Created/ModifiedDate`, çok sayıda Date | `Documents: List<Document>` (disk) |
| `Patient` (PvOrganization) | `Id` | — | `TenantId` | `Status` | — |
| `MarketingAuthorization` (PvOrganization) | `Id` | `GlobalBrandId/SkuId`, `CountryId`, `OrganizationId`, `LocalQPPVuserId` | `TenantId` | `MaStatus 0-5` | `MaDetail.Documents` |
| `RegulatoryReport` / `RegulatoryReportTask` | `Id` | `CountryId`, `AuthorityId`, `RegulatoryReportId`, `AssignedToId` | `TenantId` | `StatusId`, tarihler | `Documents` |
| `Lcppv` (reconciliation) | `Id` | anket cevapları | `TenantId` | status | `Documents` |
| `Organization` (PvOrganization) | `Id` | `CountryId`, `OperatingCountries[]` | `TenantId` | `Status` | Logo |
| `GlobalSku` | `Id` | brand/ingredient | `TenantId`? | `Status` | — |
| `Tenant` (PvTenant) | `Id` | `Domain`, `SubDomain` | kendisi | — | — |
| `User` (PvUser) | `Id` | `RoleIds[]`, `CompanyId`, `TenantId` | `TenantId` | `isActive`, `ResetToken` | `PasswordHash` |
| `Country` (PvLookup) | `Id` | — | global | — | — |

## Kritik migration soruları — cevaplar

- **ID'ler taşınabilir mi?** ObjectId → ERP GUID/kimliğine **ExternalReference olarak korunmalı** (birebir taşınmamalı). Confidence: High.
- **Tenant/company eşlemesi?** Legacy tenant `Domain/SubDomain`'e bağlı → ERP-vNext tenant kimliğine **açık eşleme tablosu** ile. Host-based mantık taşınmaz.
- **User ID eşlemesi?** ERP kimlik modeline eşle; **PasswordHash taşınmaz** (reset akışı). RoleIds → MOD-0018 rollerine map.
- **Product/Registration canonical kaynağı?** Hedefte **MDM/MOD-0290** (product/SKU) ve yeni Regulatory domain (MA). Legacy `GlobalSku`/`MarketingAuthorization` yalnızca kaynak.
- **Safety case number immutable mı?** Legacy `TrackingNumber` **kod-zorunlu immutable DEĞİL** (serbest string). Hedefte immutability zorunlu kılınmalı; migration'da mevcut değer korunur.
- **Case history korunuyor mu?** **Hayır** — versiyon geçmişi yapısal tutulmuyor (`Version` string). Yalnızca son durum taşınabilir. 🔴 Tarihsel izlenebilirlik kaybı.
- **Follow-up/version geçmişi tutulmuş mu?** **Hayır** (`FollowUpTracker` string alan; sınıf persist edilmiyor). Taşınamaz.
- **Attachment path'leri erişilebilir mi?** `wwwroot/SafetyReport/{guid}__{name}` servis diskinde → migration'da **dosya içeriği + metadata birlikte** MOD-0028'e alınmalı; orphan/broken-path riski. Medium-High risk.
- **Lookup değerleri standardize edilebilir mi?** Country/Authority/PharmaForm → MOD-0048 kanonik reference'a **değer eşleme** ile.
- **Tarih/saat/locale?** Mongo UTC saklıyor, read tarafında `TimeZoneInfo.Local` + `dd.MM.yyyy` formatlama var → migration'da **UTC normalize** et; DateTimeOffset/parallel-array tuzaklarına dikkat (bkz. proje hafızası).
- **Silinmiş/inactive kayıtlar?** `Status=false` soft-delete → migration'da işaretlenerek taşınır (hard-delete yok).
- **Idempotent mi olabilir?** Evet — kaynak ObjectId'yi ExternalReference natural-key yaparak upsert; tekrar çalıştırma güvenli tasarlanmalı.

## Risk özeti

| Risk | Seviye |
|---|---|
| Attachment içerik/path bütünlüğü | Yüksek |
| Case versiyon/follow-up geçmişi kaybı | Yüksek |
| Tenant host→kimlik yeniden eşleme | Orta-Yüksek |
| Reporter/reaction serbest-metin normalizasyonu | Orta |
| Lookup değer standardizasyonu | Orta |
| Duplicate case (dedupe yok) | Orta |
| Orphan FK (string ref'ler) | Orta |

**Sonuç:** Veri migrate edilebilir (**%50–70, Medium-Low**) ancak "as-is" değil; normalizasyon + ExternalReference + attachment yeniden yerleştirme + geçmiş kaybı kabulü gerektirir. **Dry-run + referential integrity + case count + attachment reconciliation kapıları zorunlu.**
