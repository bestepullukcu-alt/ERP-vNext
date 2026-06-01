---
description: "CAP-001 — Delivery Capability Pack Standard (çok modüllü / cross-cutting orkestrasyon sözleşmesi)"
---

# Delivery Capability Pack Standard

Bu standart, **Delivery Capability Pack** adlı governance ve orkestrasyon artefaktının formatını ve kullanım kapısını tanımlar.

> **Otorite:** Bu standart `.antigravity/` katmanındadır (AGENTS.md §1). Delivery Capability Pack, AGENTS.md yetki hiyerarşisini **değiştirmez**: tek modülün kod kararlarında hâlâ `Module Pack > Domain Config > AGENTS.md > .antigravity/` geçerlidir.

---

## 1. Purpose

Delivery Capability Pack; birden fazla modülü, follow-up'ı, platform endişesini veya business-module enforcement noktasını **kesen** işler için bir governance ve orkestrasyon artefaktıdır. Çok modüllü teslimatın sınırını, sırasını, sahipliğini ve gate kriterlerini tek bir yerde toplar.

**Delivery Capability Pack ŞU DEĞİLDİR (disambiguation):**

- bir **runtime entity** değildir (kodda tablo/collection/aggregate üretmez),
- bir **module pack** değildir (tek modülün kimlik + AC dosyası module pack'tir),
- **MOD-0014 runtime Capability Group** değildir (o, katalog taksonomisinde `Domain ▸ Suite ▸ Capability Group ▸ Module` seviyesidir ve çalışma zamanı verisidir),
- bir **business capability matrix** satırı değildir (o, enterprise-blueprint kapsamındaki iş yeteneği matrisidir),
- bir **follow-up module pack** değildir,
- bir **production module** değildir.

> ⚠️ `Capability` terimi bu repoda fazlaca yüklüdür. Bu artefakt her zaman tam adıyla **Delivery Capability Pack** olarak anılır; yalın `Capability` adı bu artefakt için kullanılmaz.

## 2. When it is required

Aşağıdakilerden **en az biri** doğruysa Delivery Capability Pack gereklidir:

- birden fazla modül bir bağımlılık sırasıyla teslim edilmek zorundaysa,
- cross-cutting bir platform yeteneği birçok modülü etkiliyorsa,
- normal bir module pack aşırı yüklenecekse,
- uygulama domain'ler arası mimari kararlar gerektiriyorsa,
- follow-up paketleri arasında governance drift riski varsa,
- birden fazla business modülü ortak bir platform foundation'a bağımlıysa.

## 3. When it is not required

Şunlar için gerekli **DEĞİLDİR**:

- hedefli bir kod düzeltmesi (targeted code fix),
- tek bir endpoint,
- tek bir sayfa,
- sınırlı/contained bir UI iyileştirmesi,
- normal bağımsız bir modül,
- cross-module bağımlılık etkisi olmayan izole bir follow-up.

Bu durumlarda DUR ve `/prepare-module-pack` yoluna yönlendir.

## 4. Mandatory sections

Her Delivery Capability Pack en az şu bölümleri içerir:

1. Identity and status
2. Business outcome
3. Problem statement
4. Capability boundary
5. Member modules and follow-ups
6. Ownership map
7. Dependency graph
8. Ordered delivery sequence
9. Prerequisites
10. Architecture decisions
11. Scope
12. Explicit exclusions
13. Governance drift risks
14. Review questions
15. Gate criteria
16. Acceptance criteria
17. Downstream business-module impacts
18. Open decisions
19. Future follow-ups
20. Audit and reconciliation notes

## 5. Status lifecycle

```text
draft
→ under-review
→ approved
→ ready-for-execution
→ in-progress
→ completed
→ reconciled
```

- `draft`: yalnızca planlama; production implementation tetiklemez.
- `under-review`: kullanıcı incelemesinde.
- `approved` / `ready-for-execution`: kullanıcı onayı alınmış; üyelerin module pack'leri kendi `approved`/`ready-for-dev` kapılarından geçtikten sonra geliştirme sıraya göre başlatılabilir.
- `in-progress` / `completed`: teslim fazları yürür/biter.
- `reconciled`: implementasyon fazları sonrası §20 reconciliation notları doldurulmuş.

Mevcut module pack status semantiği (`draft`/`approved`/`ready-for-dev`/`done`) ile **çelişen** yeni semantik üretilmez; bu lifecycle ona ek, daha üst seviye bir orkestrasyon lifecycle'ıdır.

## 6. Canonical artifact location

Önerilen, çakışmayan portfolio konumu:

```text
execution/portfolio/delivery-capability-packs/
```

> Bu klasör bu görevde **oluşturulmaz**. İlk Delivery Capability Pack authoring'i sırasında açılır.

Naming önerisi: `DCP-{NNN}-{slug}.md` (Delivery Capability Pack). Bu prefix bilinçli olarak module ID formatlarından (`MOD-xxxx`, `{DOMAIN}-NNN`) ve runtime Capability Group'tan ayrıdır. Module ID format tartışması bu standardın kapsamı dışındadır (follow-up).

## 7. Relationship to module packs (premature coding guard)

- Delivery Capability Pack, üye modülleri **ID ile referanslar**; onların yerine geçmez.
- Her üye modül kendi module pack'i ile `module-pack-standard.md` kapısından **ayrıca** geçer.
- Production kod **iki koşul** sağlanmadan başlamaz: (a) Delivery Capability Pack `approved`/`ready-for-execution`, (b) sıradaki üye modülün module pack'i `approved`/`ready-for-dev`.
- `@orchestrator` çok modüllü / cross-cutting talebi önce `/prepare-capability-pack` üzerinden geçirir; doğrudan kod yazmaz.

## 8. Access Governance handling

- Access Governance, **aday ilk Delivery Capability Pack**'tir (candidate first Delivery Capability Pack).
- Bu standart, Access Governance artefaktını **oluşturmaz**, üye listesini **kesinleştirmez** ve onu tamamlanmış bir worked example olarak kullanmaz.
- Access Governance authoring'i yalnızca açık kullanıcı onayı sonrası `/prepare-capability-pack` ile başlar.

## ✅ Kontrol Listesi

- [ ] Talep gerçekten çok modüllü / cross-cutting mi (yoksa tek module pack mi yeterli)?
- [ ] Artefakt tam adıyla "Delivery Capability Pack" olarak mı anılıyor (yalın `Capability` değil)?
- [ ] 20 zorunlu bölüm planlandı mı?
- [ ] Status lifecycle module pack semantiği ile çelişmiyor mu?
- [ ] Üye module pack'ler kendi kapılarından ayrıca geçecek mi?
- [ ] Access Governance bu aşamada yalnızca aday olarak mı işaretli (authoring yok)?

> **Mühür:** Bu standart, çok modüllü işin governance omurgasıdır. Sınır, sıra ve sahiplik kâğıt üzerinde netleşmeden kod başlamaz.
