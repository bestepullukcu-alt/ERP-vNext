---
decision_id: TREASURY-MOD0126-PLACEMENT-01
status: approved — yerleşim yürürlükte; "Açık kalanlar" bölümü karar değildir
decided_at_local: 2026-10-23
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code), karar 0.2 sorusuna sahibin yanıtı "1"; yazma onayı aynı gün ("oneri ile devam")
scope: MOD-0126 Payments & Approvals yerleşimi (domain + servis); başka domain'in belgesi bu kayıtla değişmez (XMC-001)
bound_to: dal `chore/ct/treasury-intake-ct-01`, taban origin/main 96bedc89374dd8fd06550c49ae2fc65ca07e6298
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# MOD-0126 Payments & Approvals — yerleşim: yeni `finance-treasury` domain'i + tek Treasury servisi

## Soru ve seçilen cevap

Soru (karar 0.2): "MOD-0126 nereye yerleşsin?"

1. **Yeni `finance-treasury` domain'i + tek Treasury servisi** — seçildi
2. `procurement` / `Diten.ProcurementService`
3. MOD-0003 kararına kadar ertele

## Gerekçe (ölçülmüş, origin/main 96bedc893)

- `execution/domains/procurement/domain-config.md:35`, `:50` — "AP/ödeme SoR = Finance/Treasury"; procurement ödeme
  yürütmez (MOD-0146 kapsam dışı).
- `execution/domains/platform-shared-services/domain-config.md:20` — Finance, PSS kapsamı dışında.
- Blueprint 8.8: MOD-0126 ve MOD-0124 Treasury & Cash; MOD-0119 ve MOD-0120 Financial Management.
- Kaynak kanıtı: dört modül kaynakta tek serviste (`DitenErpTreasuryService`); bağlanan commit S `da4abb20`'de
  1604/1604 test geçti. Kanıt: handoff `source-build-evidence-v1.0`, SHA256SUMS
  `7ec2cd3332a4ff8d8f7c5fbfe1885b49a5f1e552ee4954806090b59726c536eb` (82/82 OK, Treasury Intake CT ölçtü 2026-10-23),
  kaynak commit `0efc2ba182efd877dbaee02d0654b78d2e1f09c3` (yerel, pushlanmamış).

## Sonuç

- Domain: `execution/domains/finance-treasury/` (yeni; AGENTS §8 Yol A).
- Servis: tek yeni servis (adı: `treasury-intake-owner-decisions-01.md` D2). MOD-0119, MOD-0120 ve MOD-0124 da bu
  domain'e gelir; kendi kapıları (MOD-0003) ayrıca karara bağlıdır.
- MOD-0289 bu kararın dışındadır (MDM önerisi; MDM domain sahibinin kararı).

## Açık kalanlar (bu kararla verilmeyen)

1. Port — EA kararı; ardından `.antigravity/rules/ports.md` (korunan yol, sahip onayı) ve AGENTS §3.
2. AGENTS §2 (domain/servis listesi) ve §9 (dal kısa kodu) değişikliği — ayrı onay.
3. Gateway rotası — yalnız `integration-agent`.
4. Karar 0.1 (MOD-0269) — MOD-0126 `ready-for-dev` öncesi zorunlu.
5. 90 ortak aday dosyanın sahipliği (45'i MOD-0217 ile) — pack yazılırken, XMC-001.

## Bu karar ne değildir

Kod alma izni, servis iskeleti, port tahsisi, pack onayı ya da commit / push / PR izni değildir.
