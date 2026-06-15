---
description: "PREPARE-CAPABILITY-PACK — Çok modüllü / cross-cutting iş için Delivery Capability Pack hazırlama workflow'u (kod yazmaz)"
---

# /prepare-capability-pack

Bu workflow, çok modüllü veya cross-cutting bir işi production'a dökmeden önce bir **Delivery Capability Pack** (CAP-001) sözleşmesi olarak hazırlar. Bu aşamada production kod yazılmaz.

> **Tetikleme yolları:**
> - `@orchestrator` bir talebi multi-module / cross-cutting / shared-platform-foundation olarak sınıflandırdığında.
> - Kullanıcı doğrudan çok modüllü bir yeteneğin planlanmasını istediğinde.
>
> **İlgili standartlar:** [../rules/capability-pack-standard.md](../rules/capability-pack-standard.md) (CAP-001), [read-only-audit.md](read-only-audit.md), [../rules/git-safety.md](../rules/git-safety.md) (GIT-002).
>
> **Kapsam notu:** Bu workflow, Access Governance'ın **tek** gelecekteki Delivery Capability Pack olduğunu varsaymaz. Aynı workflow her çok modüllü/cross-cutting yetenek için kullanılır.

---

## 1. Multi-module / cross-cutting tespiti

Talebin birden fazla modülü, follow-up'ı veya platform endişesini kesip kesmediği belirlenir. Kesmiyorsa bu workflow uygulanmaz; normal `/prepare-module-pack` yoluna yönlendirilir.

## 2. Module pack yeterlilik sınıflandırması

`capability-pack-standard.md` §2/§3 kapısı uygulanır: normal bir module pack yeterli mi, yoksa Delivery Capability Pack mi gerekiyor? Yeterliyse DUR ve `/prepare-module-pack`'e yönlendir.

## 3. TO-BE capability discovery audit

Hedeflenen yeteneğin olması gereken (TO-BE) sınırı, üyeleri, sonuçları ve gate'leri keşif olarak çıkarılır. Bu adım salt-okunur yürütülür ([read-only-audit.md](read-only-audit.md)).

## 4. AS-IS repository & runtime evidence audit

Mevcut (AS-IS) durum repo ve runtime kanıtlarıyla salt-okunur olarak denetlenir: ilgili module pack'ler, registry, delivery board, mevcut servis/kod kanıtları.

## 5. Gap-analysis & delivery-roadmap sentezi

TO-BE ile AS-IS arasındaki fark çıkarılır; bağımlılık grafiği (dependency graph) ve sıralı teslim yol haritası (ordered delivery sequence) sentezlenir.

## 6. Naming & ownership belirsizliklerini çöz

Terim çakışmaları (özellikle `Capability` yüklemesi) ve sahiplik belirsizlikleri çözülür; artefakt "Delivery Capability Pack" adıyla, üyeler ID ile netleştirilir.

## 7. Authoring branch (yalnızca yazım başlarken)

Yalnızca authoring başladığında, `git-safety.md` (GIT-002) kurallarına uygun ayrı bir feature branch açılır. Keşif/audit adımları (1–6) branch gerektirmez.

## 8. Delivery Capability Pack'i `draft` olarak yaz

Artefakt `execution/portfolio/delivery-capability-packs/` altında `capability-pack-standard.md`'deki 20 zorunlu bölüm ile `status: draft` olarak oluşturulur.

## 9. Minimal canonical portfolio link güncellemesi

Yalnızca gerekli minimal kanonik bağlantılar güncellenir (ör. portfolio index). Geniş normalizasyon, biçim düzeltme veya ilgisiz değişiklik yapılmaz.

## 10. İnsan incelemesi için DUR

Pack `draft` olarak kullanıcı incelemesine bırakılır. Onay alınmadan bir sonraki faza geçilmez.

## 11. Onaya kadar production implementation engeli

Kullanıcı açıkça onaylamadan (pack `approved`/`ready-for-execution` + ilgili üye module pack `approved`/`ready-for-dev`) production kod başlamaz.

## 12. Implementasyon fazları sonrası reconciliation

Teslim fazları tamamlandıkça pack'in §20 "Audit and reconciliation notes" bölümü güncellenir ve status `reconciled`'e taşınır.

## ✅ Kontrol Listesi

- [ ] Talep gerçekten multi-module / cross-cutting mi?
- [ ] Normal module pack yetersiz mi (CAP-001 §2/§3)?
- [ ] TO-BE ve AS-IS denetimleri salt-okunur mu yapıldı?
- [ ] Bağımlılık grafiği + sıralı teslim çıkarıldı mı?
- [ ] Naming/ownership netleşti mi ("Delivery Capability Pack", yalın `Capability` değil)?
- [ ] Authoring branch yalnızca yazım başlarken mi açıldı?
- [ ] Pack `draft` ve insan incelemesinde mi?
- [ ] Onaysız production kod engellendi mi?
