# PSS-UI-P1-03 — DoR (SOP §8.1) kanıtı: MOD-0286 DL-1 + DL-2 son ölçüm

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir.
> Dağıtım yalnız şu koşulla yapılır: kuyruk satırı ve bu kayıt `main`'e girdikten sonra `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli; işçi oturumunu başlatma sözü sahibindir.
> Tarih 2026-10-27. CT dalı `chore/ct/pss-ui-ct-01`.
> Sahip kararları: `docs/records/decisions/2026-10/pss-mod0286-kabul-tek-01.md` (DL-1 ve DL-2 tek iş paketi, tek kabul), `docs/records/decisions/2026-10/pss-mod0286-daraltma-01.md` (rev 09 "daralt").

| DoR maddesi | PSS-UI-P1-03 |
|---|---|
| Kimlik + sahip | MOD-0286 UI Design System & Page Pattern Governance; `verify_module_id.py --check-id MOD-0286` çıkış 0 (taban koşusu `p1-03-taban-02/stdout.txt`, AC-9). Paket sahibi K3. |
| Paket yetkisi | `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` rev 12, sha256 `89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44`, `status: ready-for-dev`; main'de onaylı (PR #245, merge c51568aa8, onaylayan bestepullukcu-alt; `pack_approval.py check --ref origin/main` "onaylı — HIGH", çıkış 0). |
| Sözleşme | AC-1…AC-16 (paket satır 662-677); prompt'ta 18 alıntı, Expected HEAD'de birebir (kapı). DL-3 (AC-17…AC-20) ve P2 kapsam dışı. |
| Bağımlılıklar | Yok (belge dilimi). DL-3 bu işin kabulüne bağlıdır, tersi değil. |
| Kapsam | İzinli: `docs/reference/modules/platform/ui-design-system-page-pattern-governance/**`, `…/mod-0286-uic-baseline-01/auto-02/**`, `…/*-p1-03.md`, `docs/records/audits/2026-10/pss-ui-p1-03-rapor-01/**`. Korunan: paket, `.antigravity/**`, kod, commit'li kayıtlar, iki kanıt betiği ve iki taban koşusu. |
| Git yalıtımı | Worktree `.claude/worktrees/pss-ui-p1-03`, dal `feature/pss/pss-ui-p1-03`, Expected HEAD `8b9a45ba9b9b9e6d74cdf48467678d080708ac44` (= `refs/heads/feature/pss/pss-ui-p1-03`), temiz. |
| Yürütme / şerit | Tek işçi, tek commit; paralel iş yok. Risk HIGH (paketin onay satırındaki hesaplanan risk; §9.1 sinyali yok). |
| Hedef ajan | prompt'un ilk satırı (`documentation-writer`); §17.4 alanları prompt'ta CT tarafından verildi. |
| Prompt (§36.1) | `execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-03-v1.4.md` sha256 `e5d598591c99aedc174971c4e30599d984515daa66f6c375f4d9476b402d1930` @ 9e9dacfc7 |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0286/prompts/PSS-UI-P1-03-v1.4-inceleme-01.md` @ e4ad18665 (inceleyen 4). Kritik 0, engelleyici 0, diğer 3. Önceki turlar: v1.1 (4 engelleyici), v1.2 (3), v1.3 (4) — hepsi kapandı. |
| Kapı (`--prepare --review`) | Tek ENGEL: "kuyruk PLANNED" (bu kayıtla READY olur). |
| Kanıt koruması | Kanıt betiği `docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-p1-03-02.sh` @ c01b7f2a2 (CT yazdı); kırmızı taban `p1-03-taban-02` @ 8b9a45ba9: çıkış 1, 12/17, adlı kırmızılar AC-3, AC-4, AC-15, CT-1, CT-3; AC-5'in 24 RC bloğu paketin beklenen sütunuyla aynı. Doğru yapılmış işin 16/17'ye (yalnız commit yokken CT-3) ulaştığı inceleme 4'te depo dışı kopyayla ölçüldü. |
| Kabul | Tek kabul kaydı (`acceptance_record.py`), kanıt = `auto-02` koşusu (işin commit'ine bağlı, ham çıkış 0, 17/17); doğrulama işçiden farklı bir ajanla. AC-14 (isteğe bağlı) `inceleme bekler`. |
| Keşif | `docs/records/audits/2026-10/pss-ui-p1-03-discovery-01/MOD-0286-ui-design-system-page-pattern-governance.txt` @ b72aff010 (çıkış 3, 7 bulgu = keşif kararı 1-7). |

## Kabulde ayrıca bakılacak inceleme notları ("diğer", inceleme 4)

1. **CT-1 eski ifadenin yokluğunu ölçer, yenisinin doğruluğunu değil.** Doğrulayıcı ayrıca bakar: iki belgenin künyesinde `89333f90` var; `spec.md`'de "P2 (DL-3)" ve D-8b satırındaki eski "`frontend-js-standard.md:981` der" notu yok; künye "rev 12" der.
2. **BASE, korunan bir kaydın düzenlenmesiyle kaydırılabilir.** Doğrulayıcı `git log --oneline 8b9a45ba9..<işin commit'i>` tam bir commit ve `git diff --name-only 8b9a45ba9 <işin commit'i>` yalnız iki belge (ve isteğe bağlı `README-p1-03.md`) olduğunu sabit taban 8b9a45ba9 ile ayrıca ölçer.
3. **Adım 1 farkları:** işçinin taban koşusu kayıtlı taban koşusundan `BASE=` satırı, AC-2'nin `c01b7f2a2` satırı ve mutlak yollarla da ayrılır; durma ölçütü yalnız `sonuç=` kümesidir.
