---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-07
status: approved — D114 2026-10-31 itibarıyla yürürlükte
decided_at_local: 2026-10-31
decided_by: Ali (`.antigravity` kural sahibi; yazılı onay); K1 / gmg-10 aktardı
decision_source: Treasury Intake Control Tower oturumu (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); kaynak Y-36
scope: RCS-001 §3, yalnız MOD-0217 satırı; başka satır ve sahiplik değişmez
bound_to: dal `chore/ct/treasury-rcs-agr-01`, taban origin/main cc63c620c
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, push, PR ya da onay satırı izni değildir
---

# Treasury intake — RCS-001 `AGR` satırı (2026-10-31)

## Y-36 — Ali, RCS-001 yazılı onayı (2026-10-31; K1 aktardı)

> AGR satırı onaylandı.
> Kapsam: .antigravity/rules/record-coding-standard.md §3, yalnız MOD-0217 satırı (CTR → AGR), metin tam sizin gönderdiğiniz gibi. Başka satır ve sahiplik değişmez.
> Not: AGR'nin Platform belge listesindeki "Agreement" belge türüyle aynı harfleri taşıdığını biliyor olun; kayıt kodu ad alanı ayrı, çakışma yok diye yazın.
> .antigravity değişikliği olduğu için PR'ı Beste onaylar; push ve PR için ayrıca bana sorun

## D114 — MOD-0217 tür öneki `AGR` kurala yazıldı (2026-10-31)

Onay kanıtı: Y-36 (Ali, K1 aktardı). Kapsam ve metin Ali'nin onayladığı metindir.

1. **D114/1 — kural satırı:** RCS-001 §3 MOD-0217 satırında `CTR` → `AGR`; metin onaylanan metinle birebir aynıdır. D94/1'i uygular. Satırdaki D94/1 kaydı `treasury-intake-owner-decisions-05.md` içindedir; bu kayıt tarihinde main'de değildir, #314 ile gelir.
2. **D114/2 — ad alanı notu:** `AGR` harfleri `docs/reference/integrations/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv` listesindeki "Agreement" belge türüyle aynıdır (ör. `GMG-COM-AGR-0001`). Bu belge kodu, QMS doküman numaralandırmasıdır (RCS-001 §3 "Doküman (QMS)" satırı, KALSIN). MOD-0217 kayıt kodu RCS-001 biçimindedir (şirket öneki + `AGR` + altı hane, ör. `MG-AGR-000042`). Kayıt kodu ad alanı ayrıdır, çakışma yoktur.
3. **D114/3 — sonraki adım:** `.antigravity` değişikliği olduğu için PR'ı Beste onaylar. Push ve PR için Ali'ye ayrıca sorulur; bu kayıt ikisine de izin vermez.

Ölçüm: origin/main cc63c620c'de `.antigravity`, `scripts`, `src`, `tests` ve finance-treasury paketlerinde tam kelime `CTR` ya da `AGR` yalnız bu satırda geçiyordu.
