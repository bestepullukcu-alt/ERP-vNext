# Platform Modülleri — Bağımlılık ve Sıra Analizi

> Blueprint `Dependency Gate` alanları vs repository gerçeği.

## 1. Bağımlılık Tablosu

| Modül | Blueprint Dependency Gate | Prereq'lerin gerçek durumu | Bloke ettiği modüller | Geliştirmeye başlanabilir mi? |
|---|---|---|---|---|
| MOD-0018 RBAC/ABAC | SSO/MFA | Auth runtime çalışıyor | 0019, 0023, 0028, 0031, PV | **Evet** — devam et (ABAC/admin-UI) |
| MOD-0021 Audit | SSO/MFA | Auth çalışıyor | 0023, 0028, 0031, PV | **Evet** — hardening |
| MOD-0040 Canonical ID/Correlation | Interface Registry; Logging & Monitoring | Correlation foundation kısmen; Interface Registry Platform'da var (`InterfaceRegistryController`) | PV migration | **Evet** — canonical ID modeli |
| MOD-0028 Documentation | RBAC; Audit; Internal Document Repository | RBAC ✅, Audit ✅ | 0031, PV controlled docs | **Evet** — FU06 fix |
| MOD-0023 Workflow | RBAC/ABAC; Audit | RBAC ✅, Audit ✅ | PV approval flows | **Evet** — smoke + seed |
| MOD-0019 Data Masking | RBAC/ABAC; Data Dictionary (sonra) | RBAC ✅; Data Dictionary yok | PV patient data | **Evet** — sıfırdan (P0 for PV) |
| MOD-0031 Evidence Linking | Audit; Records Management; Decision & Rationale Log | Audit ✅; Records Mgmt / Decision Log **yok** | PV audit pack | **Kısmen** — 0028+0021 üzerine kurulabilir; Records Mgmt eksik |
| MOD-0004 Metric/Semantic | SoR & Ownership Registry; Data Contract Registry; Data Dictionary | Bu registry'ler **yok** | KPI tüketicileri, 0063 | **Zor** — prereq registry'ler yok |
| MOD-0063 Lakehouse | Secrets Vault; Logging & Monitoring; Data Contract Registry | Secrets Vault var (`BuildingBlocks.Security.Secrets`); Data Contract Registry **yok** | PV analytics | **Zor** — Data Contract Registry gerek |

## 2. Repository Kanıtına Göre Düzeltilmiş Sıra

Önerilen liste (görevdeki) çoğunlukla doğru; repo kanıtına göre küçük düzeltmeler:

| Sıra | Modül | Gerekçe (repo kanıtı) |
|---|---|---|
| 1 | **MOD-0018** | RBAC core zaten runtime-proven; ABAC/admin-UI tamamlanmalı — diğer her şeyin kapısı |
| 2 | **MOD-0021** | Audit backend hazır; 0023/0028/0031 audit contract'ına bağlı — önce hardening |
| 3 | **MOD-0040** | Correlation foundation var; canonical ID PV migration'ı ve trace stitching için erken gerekli. **Registry kimlik çelişkisi (CONF-01) bu modülden ÖNCE çözülmeli** |
| 4 | **MOD-0028** | En olgun; FU06 blocker fix + 0031 için taban |
| 5 | **MOD-0023** | Backend hazır; smoke + PV seed |
| 6 | **MOD-0019** | PV için P0 ama teknik olarak 0018 üzerine; PV-öncelikli programda 1. sıraya çekilir |
| 7 | **MOD-0031** | 0028 + 0021 hazır olmadan tam değer vermez |
| 8 | **MOD-0004** | Prereq registry'ler (SoR/Data Contract/Dictionary) önce gerekir |
| 9 | **MOD-0063** | Data Contract Registry + 0004 semantic layer önce; en son |

> **PV-öncelikli** program için sıra farklıdır (bkz. PV readiness): 0019 → 0021-hardening → 0018-ext → 0040 → 0028/0031 → 0023 → 0004/0063.

## 3. Çakışma / Paralel Geliştirme Notu
- **0028 ve 0031** aynı runtime flow'u (document/evidence) paylaşır → paralel değil, **0028 → 0031** sırayla.
- **0018-ext ve 0019** aynı security service yüzeyine dokunur → policy/decision contract çakışması riski; paralelden önce contract sınırı netleştir.
- **0021-hardening ve 0023/0028** audit contract'a dokunur → audit AuditEvent v1 dondurulmadan tüketiciler paralel geliştirilmemeli.
- **0040 canonical ID** cross-cutting; erken ama izole (Building.Blocks) yapılabilir → diğerleriyle paralel güvenli.
