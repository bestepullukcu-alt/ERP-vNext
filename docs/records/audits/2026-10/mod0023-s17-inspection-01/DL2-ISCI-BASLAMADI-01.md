# WF-S17-DL2 — işçi başlamadı (2026-11-02)

Durum: işçi kod yazmadı; hiçbir dalda iş commit'i yok. Kuyruk satırı BLOCKED.

## Denemeler

| Sıra | Yol | Sonuç |
|---|---|---|
| 1 | Oturum, `pss-wf-s17-dl2-r1` klasöründe | Sahip tarafı klasör güveni reddedildi; oturum açılmadı |
| 2 | Alt ajan, `EnterWorktree path` ile `pss-wf-s17-dl2-r2` (kart S-80) | Reddedildi: "would not give this agent write access there"; yazma kule klasörüyle sınırlı |
| 3 | Alt ajan `isolation: worktree`, aynı geçiş | Aynı ret; yazma ajanın kendi klasörüyle sınırlı |
| 4 | Talimat v1.4, ajanın kendi klasörüne ve dalına bağlandı | Ajan değişiklik yapmadan bitince klasör ve dal silindi (ölçüm: bağımsız inceleme); v1.4 UYGUN DEĞİL, dağıtılmadı |

Kuyruk satırı 2. denemeden önce DISPATCHED yazıldı; işçi fiilen başlamadığı için bu erkendi. Bu kayıt onu düzeltir.

## İnceleme kaydı: v1.4 (Claude Opus 5.5 · high, ajan ace2bd0d3c540d729)

- ENGEL: başlıktaki klasör ve dal inceleme sırasında ortadan kalktı.
- DÜŞÜK: `worktree-agent-…` dal adı GIT-002 §1 ve `AGENTS.md` §9 biçiminden sapmadır; güvenlik ihlali değildir.
- Temiz: iki taban arasında yalnız SupplyChain Returns ve belge dosyaları değişti; MOD-0023 yolları ve onay dosyaları aynı.

## Geçerli olan

Talimat v1.3 (inceleme 04, UYGUN), `pss-wf-s17-dl2-r2` klasörüne bağlıdır ve o klasör tabanındadır. Normal işçi oturumu bu klasörde açılırsa yeni sürüm gerekmez; yalnız kapı yeniden koşulur.

## Kurulum için bulgu

S-80 ve S-114'ün alt ajan yolu bu ortamda çalışmıyor: alt ajan hazır bir klasöre geçemez, kendi klasörü de dağıtımdan önce bilinemez ve iş yoksa silinir. Kapı ise klasörü ve dalı dağıtımdan önce ölçer.
