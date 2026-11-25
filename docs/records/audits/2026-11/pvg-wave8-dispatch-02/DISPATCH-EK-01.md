# PVG sekizinci dalga — dağıtım kaydı eki 01: WP-0230-DL10-R1 çalışma yeri

> Control Tower kaydı, 2026-11-02 12:50. `DISPATCH.md`'nin "prompt'tan sapma yok" satırının yerini alır; ötekiler aynen geçerlidir.

- **İlk deneme (12:42):** alt ajan işçi prompt'taki worktree'ye (`pvg-wp0230-dl10-r1`) yazamadı. Alt ajan kule worktree'sine yalıtılmış; `cd` ile git ve `EnterWorktree` reddedildi. Hiçbir dosya değişmedi, commit olmadı. İşçi engeli aşmayı denemedi ve durdu.
- **Düzeltme:** WP-0230-DL10 dağıtımındaki (`docs/records/audits/2026-10/pvg-wave8-dispatch-01/DISPATCH.md`) yol izlendi. Boş hazırlık worktree'si `git worktree remove` ile kaldırıldı; komut kirli ağaçta reddeder, ağaç temizdi. Dal korundu, ucu `6422dd703` (ölçüldü). İşçi kendi yalıtılmış worktree'sinde `feature/pvg/wp-0230-dl10-r1` dalına geçer. İzin genişletilmedi.
- **Prompt'tan sapma:** yalnız çalışma ağacı yolu. Dal, Expected HEAD ve başlangıç merge'ü aynıdır.
- **Model:** iki denemede de Sonnet 5.5 · high istendi. İlk deneme kendini `claude-sonnet-5-5` olarak bildirdi.
