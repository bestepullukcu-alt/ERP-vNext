---
status: proposal
kind: owner-decision-request
date: 2026-10-29
author: Treasury Intake Control Tower (K1 / gmg-10)
decision_owner: Ali — MOD-0018 sahibi
related: KARAR D90/3; MOD-0057 O-5
---

> **Öneri — karar değil.** Bu dosya karar sahibinin onayını ister; merge edilmesi kararı vermez. Cevap bu PR'da yorum ya da
> "Approve" ile verilir; Treasury CT cevabı kaynak cevap (Y-n) ve karar (D-n) olarak kaydeder, sonra pakete işler.
> Seçim: kabul / değişiklik; ayrı FU mu, FU27 mü

# Treasury CT → Ali: MOD-0057 (Etiketleme) Auth kaydı önerisi — O-5 (onayına)

Kaynak: `execution/domains/commercial-suite/module-packs/MOD-0057-semantic-tagging-taxonomy-management.md` "Authorization Convention"
(:569-603) ve KARAR D90/3 (K1 payı: O-5 (a) `crm.*` ad alanı + üç rol şablonu). Auth'ta kayıt MOD-0018 sahibinin işidir (D9, D53).
Treasury FU27'de olduğu gibi metni biz yazarız, sen teyit edersin; kod MOD-0057 onaylandıktan sonra (D53/2: DL-1 kabul kapısı).

## 1. Anahtarlar (17) — PKS-001 biçimi, `git grep "crm.tag-"` = 0 (yeni)

| Anahtar | Katman |
|---|---|
| `crm.tag-taxonomies.read` | T1 |
| `crm.tag-taxonomies.create` · `.update` · `.delete` | T1 |
| `crm.tag-taxonomies.bulk-delete` · `.activate` | T2 |
| `crm.tag-taxonomies.retire` | T3 (pakette kayıtlı) |
| `crm.tag-taxonomies.terms.create` · `.update` · `.delete` | T1 |
| `crm.tag-taxonomies.terms.bulk-delete` | T2 |
| `crm.tag-taxonomies.terms.deprecate` | T3 |
| `crm.tag-assignments.read` · `.create` · `.delete` | T1 |
| `crm.tag-assignments.bulk-delete` | T2 |
| `crm.tag-assignments.read-tenant-wide` | T3 — **yalnız açık atama** (`ExplicitGrantOnlyPermissions`), yalnız okuma |

Joker (`*`) yok; kaynaktaki iki parçalı `taxonomy.*` ve `TAXONOMY_*` takma adları taşınmaz.

## 2. Rol şablonları (3) — `read-tenant-wide` hiçbirinde varsayılan değil

| `RoleName` (sabit kod) | Anahtarlar |
|---|---|
| `TagReader` | `crm.tag-taxonomies.read`, `crm.tag-assignments.read` |
| `Tagger` | `TagReader` + `crm.tag-assignments.create`, `.delete`, `.bulk-delete` |
| `TaxonomyManager` | bütün `crm.tag-taxonomies.*` (12) + `TagReader` |

Taksonomi yazma izni bağ yazma izni vermez; bağ izni taksonomi değiştirmez.

## 3. Görünen adlar (kiracı modülü → 7 dil; D79/9 deseni: `SharedResource` `RoleName_{RoleName}`)

| RoleName | en | tr | fr | es | zh | ar | ru |
|---|---|---|---|---|---|---|---|
| `TagReader` | Tag reader | Etiket okuyucu | Lecteur d'étiquettes | Lector de etiquetas | 标签查看者 | قارئ الوسوم | Читатель тегов |
| `Tagger` | Tagger | Etiketleyici | Étiqueteur | Etiquetador | 标签标注者 | مُعيِّن الوسوم | Разметчик тегов |
| `TaxonomyManager` | Taxonomy manager | Taksonomi yöneticisi | Gestionnaire de taxonomies | Gestor de taxonomías | 分类法管理员 | مدير التصنيفات | Менеджер таксономий |

## 4. Açık noktalar (senin kararın)

- Modül kodu (manifest `ModuleCode`) — FU27 K-1b deseni: paket başına bir kod; değer manifest adımında.
- Admin / Viewer varsayılanı ve SuperAdmin tam katalog — FU27 K-2 / K-3 deseni (öneri: aynı karar — K-2 hiçbiri, K-3 bugünkü davranış).
- Bu kayıt FU27'ye mi eklenir, ayrı bir MOD-0018 FU mu açılır? Öneri: **ayrı FU** (FU27 Treasury'ye özgü; MOD-0057 commercial-suite).
  Kimlik `verify_module_id.py` ön kontrolüyle açılır (DCP-002); bu öneri kimlik uydurmaz.

Onaylarsan metni pakete (ya da yeni FU'ya) işleyip CT-APR-02'den geçiririz.
