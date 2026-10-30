# PSS-UI-P1-05 — DoR (SOP §8.1) kanıtı: MOD-0286 DL-3 yeniden açma, tek seviye sayfalar

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir.
> Dağıtımdan önce `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli. İşçiyi başlatma sözü sahibindir.
> Tarih 2026-10-31. CT dalı `chore/ct/pss-ui-ct-01`.
> Sahip kararları: `tek-seviye-borc-01` ve sahip tablosu için "Uygun" (`docs/records/decisions/2026-10/pss-mod0286-tek-seviye-borc-01.md`), ikisi de 2026-10-31.

| DoR maddesi | PSS-UI-P1-05 |
|---|---|
| Kimlik + sahip | MOD-0286; paket sahibi K3. |
| Paket yetkisi | Paket rev 15, sha256 `0c53533e…`, main'de onaylı: PR #312, merge `819cc642a`, onaylayan bestepullukcu-alt. DL-3 için gerekli kanıt seviyesi E2 (paket satır 974). |
| Sözleşme | DL-3 bölüm 1-2 (paket satır 325-351, 450-462), CI tablosu 485-499, "DL-3 tek seviye sayfalar" 523-609, Validation Rules 744-757, AC-21 satır 825. Prompt'ta 5 alıntı; Expected HEAD'de birebir doğrulandı. API ve izin N/A. |
| Bağımlılık | PSS-UI-P1-04 main'de (PR #275). Workflow değişmez. |
| Kapsam | İzinli: `scripts/uic_changed_pages.py`, `tests/ui-compliance-baseline.tsv`, koşu dizini `…/mod-0286-uic-ci-01/p1-05-kanit-01/`, rapor dizini. Korunan: paket, `.github/**`, `.antigravity/**`, kod ağaçları, kanıt betikleri, öteki kayıtlar. |
| Git yalıtımı | Worktree `.claude/worktrees/pss-ui-p1-05`, dal `feature/pss/pss-ui-p1-05`, Expected HEAD `5d2774ae973f65797b0d67e75fee611ee83baeef`; dal bu commit'te, ağaç temiz. |
| Yürütme | Tek işçi; işçi oturumu ya da alt ajan (sahip sözüyle); en çok üç yerel commit; push yok. Risk HIGH. |
| Hedef ajan | `devops-agent` (prompt'un ilk satırı). §17.4 tablosunda satırı yok; komut sözleşmesi ve çıktı dili CT tarafından prompt'ta verildi. |
| Prompt (§36.1) | `execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-05-v1.1.md`, sha256 `ee4beffc29a5c2cf7b5586a65a36819e7e0269aa1d53201709c872833722a36a` @ 56e8cabee |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0286/prompts/PSS-UI-P1-05-v1.1-inceleme-01.md` @ 45d150f86: kritik 0, engelleyici 0, diğer 3. Önceki tur v1.0'da 1 engelleyici vardı (`…-v1.0-inceleme-01.md`); kapandı. |
| Kanıt koruması | CT kanıt betiği `docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh` @ 5d2774ae9 ve bağımsız ölçüm `uic-tek-seviye-oracle.py` @ 51b0370b6: 10 zorunlu kimlik. BASE, betiği ekleyen commit'tir; değiştirilmediği denetlenir. Bugün kırmızı: 3/10 (CT-2, AC-21c, REG). İşçinin kayıtlı koşusunda beklenen 10/10. |
| Kabul | Tek kabul kaydı. Kanıt, iş commit'inde `uic-tek-seviye-kanit-02.sh <out> <iş>` kayıtlı koşusudur; ham çıkış 0 ve 10/10. Doğrulamayı işçiden farklı bir ajan yapar. |
| Keşif | `docs/records/audits/2026-10/pss-ui-p1-03-discovery-01/MOD-0286-ui-design-system-page-pattern-governance.txt` @ b72aff010 (keşif kararları 1-7, rev 15'e göre yazıldı). |

## Kabulde ayrıca bakılacak notlar ("diğer")

1. **İnceleme v1.1 / F2 — AC-21g yalnız adı sayar.** İçi boş self-test durumları betikten geçer. Doğrulayıcı 14 durumu kod okuyarak ölçer (gerçek koşu ve sonuç denetimi; base-only dalı; `_ViewImports.cshtml`); koşu çıktısında FU-17 satırı birebir.
2. **İnceleme v1.1 / F5 — işçi dalı.** Bu kayıtla kapandı: dal `5d2774ae9`'a ileri alındı; kapı dağıtım kipinde yeniden koşuldu.
3. **İnceleme v1.1 / F6 — geçici klon yolu.** Betik `TMPD`'yi yazmaz; prompt zorunlu içerik (7) tam karşılanamayabilir. İşi durdurmaz. Sistem geçici dizininde `tmp.*/klon` birikimi CT'nin temizlik işidir.
4. **Kalan bilgi — `ct_queue.py validate --queue pss-ui`.** PSS-UI-P1-03 ve PSS-UI-P1-04 ACCEPTED satırları rev 14'e bağlı olduğu için 2 hata veriyor, main'de de aynı (paket rev 15'e geçti). Bu iş bunları değiştirmez; `require` bunu NOT olarak yazar. Yeniden bağlama sonra ayrı ele alınır.
