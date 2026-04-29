---
description: "PREPARE-MODULE-PACK — Kod yazmadan yeni modül sözleşmesi hazırlama workflow'u"
---

# /prepare-module-pack

Bu workflow yeni bir modülün geliştirmeye başlamadan önce module pack sözleşmesini hazırlar. Bu aşamada backend, frontend, gateway veya runtime kodu yazılmaz.

> **Tetikleme yolları:**
> - **Sıfırdan modül:** Kullanıcı yeni bir modül için doğrudan bu workflow'u çağırır.
> - **`bootstrap-domain` çıktısını rafine etme:** Excel'den toplu üretilen `draft` module pack'leri AC/iş kuralı/L10n anahtarı ile zenginleştirmek için bu workflow tekrar çağrılabilir; var olan pack içeriği `module-pack-author` tarafından **üzerine yazılmadan** detaylandırılır.

## 1. Giriş

Gerekli bilgiler:

- Modül adı
- Hedef domain veya domain adayları
- Kullanıcı create/edit form alanları
- DataTable olup olmadığı
- Bilinen iş kuralları ve bağımlılıklar

## 2. Bağlam Okuma

`module-pack-author` sırasıyla şunları okur:

1. `AGENTS.md`
2. `execution/domains/{domain}/domain-config.md`
3. `.antigravity/rules/module-pack-standard.md`
4. DataTable modülü ise `.antigravity/rules/frontend-datatable-template.md`

## 3. Golden Reference Kararı

DataTable modüllerinde create/edit form alanları sayılır:

- `8 ve altı`: `golden_reference: slim`
- `8'den fazla`: `golden_reference: compact`

Sayılmayan alanlar: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, audit alanları ve DataTable checkbox/action kolonları.

## 4. Module Pack Üretimi

Yeni dosya `execution/domains/{domain}/module-packs/{ID}-{slug}.md` altında oluşturulur veya mevcut dosya güncellenir.

Varsayılan status:

```yaml
status: draft
```

Kod üretimi için kullanıcı incelemesinden sonra status `approved` veya `ready-for-dev` yapılmalıdır.

## 5. Orchestrator Handoff

`@orchestrator` yalnızca mevcut ve onaylı module pack ile geliştirmeye başlar.

Module pack yoksa veya `draft` ise orchestrator kod yazmaz; kullanıcıyı bu workflow'a yönlendirir.
