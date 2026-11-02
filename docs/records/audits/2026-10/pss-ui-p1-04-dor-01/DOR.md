# PSS-UI-P1-04 — DoR (SOP §8.1) kanıtı: MOD-0286 DL-3 UI uyum CI bağlantısı

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir.
> Dağıtımdan önce `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli. Kuyruk satırı CT dalından okunur; kayıt satırı `pss-ui` zaten main'dedir (`ct_queue.py` `load_for_dispatch`). İşçiyi başlatma sözü sahibindir.
> Tarih 2026-10-27. CT dalı `chore/ct/pss-ui-ct-01`.
> Sahip kararları: `dl3-basla` ve `taban-karar` (`docs/records/decisions/2026-10/pss-mod0286-taban-01.md`), ikisi de 2026-10-27.

| DoR maddesi | PSS-UI-P1-04 |
|---|---|
| Kimlik + sahip | MOD-0286; paket sahibi K3. |
| Paket yetkisi | Paket rev 13, sha256 `ce134e13…`, main'de onaylı: PR #263, merge `826150ef4`, onaylayan bestepullukcu-alt. DL-3 için gerekli kanıt seviyesi E2 (paket satır 821). |
| Sözleşme | DL-3 bölüm 1-3 (paket satır 310-513), AC-17…AC-20 (678-681). Prompt'ta 9 alıntı; Expected HEAD'de birebir doğrulandı. API ve izin N/A. |
| Bağımlılık | PSS-UI-P1-03 CT dalında ACCEPTED (kabul kaydı 02). Nihai kabul, CT-Setup'ın sahip onayıyla yazacağı workflow'a bağlıdır (AC-17 bölüm 2). |
| Kapsam | İzinli: `scripts/uic_changed_pages.py`, `tests/ui-compliance-baseline.tsv`, iş koşusu dizini `…/mod-0286-uic-ci-01/p1-04-kanit-01/`, rapor dizini. Korunan: paket, `.github/**`, `.antigravity/**`, kod ağaçları, kanıt betikleri, öteki kayıtlar. |
| Git yalıtımı | Worktree `.claude/worktrees/pss-ui-p1-04`, dal `feature/pss/pss-ui-p1-04`, Expected HEAD `5341e0dad18819e42ea3142e37852182503247d3`; dal bu commit'te, ağaç temiz. |
| Yürütme | Tek işçi; işçi oturumu ya da alt ajan (sahip sözüyle); en çok üç yerel commit; push yok. Risk HIGH. |
| Hedef ajan | `devops-agent` (prompt'un ilk satırı). §17.4 tablosunda satırı yok; komut sözleşmesi ve çıktı dil bilgisi CT tarafından prompt'ta verildi. |
| Prompt (§36.1) | `execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-04-v1.1.md`, sha256 `3797088cdb52c1313c487baa5b87b979cdfb2d466d76dfb46f1284e32b2affb5` @ d5dc8d23e |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0286/prompts/PSS-UI-P1-04-v1.1-inceleme-01.md` @ 29d300fc9 (inceleyen 6): kritik 0, engelleyici 0, diğer 3. Önceki tur v1.0'da 2 engelleyici vardı; ikisi de kapandı. |
| Kanıt koruması | CT kanıt betiği `docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-03.sh` @ 5341e0dad: 13 zorunlu kimlik. BASE, betiği ekleyen commit'tir; betiğin değiştirilmediği denetlenir. Bugün kırmızı: 2/13. İşçinin kayıtlı koşusunda beklenen 11/13; AC-17b ve AC-20 workflow gelene dek kalır. |
| Kabul | Tek kabul kaydı. Kanıt, CI koşusuyla birlikte kabul edilen commit'te `uic-dl3-kanit-03.sh <out> <iş> <ci-koşu>` koşusudur; ham çıkış 0 ve 13/13 olmalı. Doğrulamayı işçiden farklı bir ajan yapar. |
| Keşif | `docs/records/audits/2026-10/pss-ui-p1-03-discovery-01/MOD-0286-ui-design-system-page-pattern-governance.txt` @ b72aff010 (keşif kararları 1-7). |

## Kabulde ayrıca bakılacak notlar ("diğer")

1. **İnceleme 6 / 1 — kurcalama denetimi atlatılabilir.** Betik silinip değiştirilmiş hâliyle yeniden eklenirse BASE kayar. Doğrulayıcı `git log --diff-filter=AD --oneline -- docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-03.sh` komutuyla tek bir ekleme satırı (5341e0dad) olduğunu ölçer. Ayrıca `git diff 5341e0dad <iş> -- <betik>` boş olmalıdır.
2. **İnceleme 6 / 2 — nihai koşuda AC-19.** Nihai koşudan önce main birleştirilirse ve başka bir ekip bir kırmızıyı değiştirirse, taban taslakla eşleşmez. Nihai koşudan önce CT, `--draft-baseline` farkını ölçer. Fark varsa taban, satır eklemek ya da sayı artırmak için yeni bir sahip kararıyla güncellenir (AC-19); `pss-mod0286-taban-01` yeni satırlara yazılmaz.
3. **İnceleme 6 / 3 — eskimiş atıflar ve CI çıktısındaki desen anahtarı.** `acceptance_record.py` satır numaraları 3bf4e6d61 ile 33 satır kaydı: artefakt kuralı :525-529, ham çıkış kuralı :665-667. Kural değişmedi. Doğrulayıcı CI kipi çıktısında `CI-6v`, `CI-6j`, `CI-9p` yazılmadığını ayrıca ölçer.
4. **İnceleme 5 / 8 — CI tetikleyici yolları.** Paket 432-433'te seçici, taban ve workflow dosyası yok. CT-Setup'a verilecek workflow notuna girer: seçici ya da taban değişince de iş koşmalı, yoksa kabul commit'inin PR'ı işi başlatmayabilir. Bu bir paket revizyonu girdisidir.
