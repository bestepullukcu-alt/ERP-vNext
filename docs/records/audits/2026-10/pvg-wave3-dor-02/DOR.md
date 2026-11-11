# PVG üçüncü dalga — DoR (kart §1) kanıtı: WP-0230-DL3C

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir.
> Dağıtım için iki koşul var:
> - `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli.
> - Sahip, işçi oturumunun açılmasını bu CT sohbetinde onaylamalı.
>
> Tarih 2026-10-27. CT dalı `chore/ct/pvg-ct-01`.
> Plan kararı: `docs/records/decisions/2026-10/pvg-mod0230-dalga3-plani-01.md` @ 97d28d2da (D3-3, D3-4). K3 kararı: `docs/records/decisions/2026-10/pvg-mod0230-dalga2-kararlari-01.md` @ 2e67261eb.

| DoR maddesi | WP-0230-DL3C |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi". |
| Paket yetkisi | `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` revizyon 10 (`c236cb87…`). Expected HEAD 356e1240a'da onaylı (`pack_approval.py check` çıkış 0). |
| Sözleşme | K3: `ASPNETCORE_ENVIRONMENT` boşsa Production sayılır. Ayrıca `Program`'ın `ASPNETCORE_URLS` okuması korunur. 16 alıntı Expected HEAD'de birebir. |
| Bağımlılıklar | WP-0230-DL3A main'de (#213). WP-0230-DL3B main'de (#234). İkisi de INTEGRATED. D3-3 koşulu sağlandı. |
| Kapsam dışı | AC-10 (OD-PVG-6). DL-4 dosyalarının hepsi (açık PR #250). |
| Kapsam | `PvgServiceApiHost.cs` içinde K3 kuralı tek yardımcıda toplanır. Yeni test dosyası `PvgServiceHostEnvironmentTests.cs` (H1–H8). `PvgServiceApiHostTests.cs` içinde yalnız M1 değişir (satır 1725, DL3B testinin beklenen değeri). `Program.cs` yalnız geçici sabotaj için açıktır; teslimde farkı boş olmalı. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl3c-r1`, dal `feature/pvg/wp-0230-dl3c-r1`, Expected HEAD `356e1240a`, temiz. |
| Yürütme / şerit | Risk HIGH (yetki, uyum). WP-0230-DL4A (PR #250) ile K5 paralel: ikinci biten iş `origin/main`'i birleştirir ve kontrolleri yeniden koşar. #250'nin `fd8c3540f`'teki dosyasıyla kuru birleştirmede çakışma yok; yazar ve inceleyici bunu ayrı ayrı ölçtü. |
| Teslim (kart S-63) | İşçi yalnız yerel commit yapar. `origin/main`'i birleştirir (dal birinci ebeveyn). Filtreli testleri koşar. Dal, sha ve test sonucunu bildirip durur. Push ve PR'ı, sahibin dal ve sha ile verdiği onayla CT yapar. |
| Hedef ajan | Prompt'un ilk satırı. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL3C-v1.0.md`, sha256 `c95885adee07d2fdada0afb15ecf8ffa427ed8ef0e04135191f86f1f1f4df8ad` @ 2c1433816. İşçiye yol ve sha ile verilir (S-52). |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL3C-v1.0-inceleme-01.md` @ 6f828a62a, UYGUN. ENGEL 0, YÜKSEK 0. |
| Kabul | H1–H8 ve S1–S8 sabotajları. **Kanıt sınırı:** DL-3 için gerekli seviye E3; bu iş E2 üretir. Resmî kabul canlı kapıya kadar açık kalır. |
| Keşif | `docs/records/audits/2026-10/pvg-m1-01-discovery-02/MOD-0230-case-intake-triage.txt` @ 4ebe4b15f |

## CT okumaları

1. **Yeni test dosyası (inceleme bulgusu 1).** Paket DL-3 "Yollar" yalnız `PvgServiceApiHostTests.cs`'i sayar. CT, DL-3'ün testlerini Api test projesinde yeni bir dosyaya yazmayı aynı "Yollar"ın kapsamında okur. Nedeni: aynı dosyada açık bir başka PR (#250) var, ayrı dosya çakışma riskini en aza indirir (D3-4'ün amacı). Dosya yalnız DL-3 testlerini taşır.
2. **Yalnız boşluktan oluşan ortam adı.** K3 "boş dize" der. CT, yalnız boşluktan oluşan adı da boş sayar ve Production kabul eder. Nedeni: DL3B'nin yapılandırma taraması bu adı zaten üretim benzeri sayıyor; host da aynı hizaya gelir.
3. **" Production " gibi boşluk taşıyan ad kırpılmaz (inceleme bulgusu 2).** Karar CT'nindir. Nedeni: ASP.NET'in `IsProduction` denetimi de adı kırpmaz; host çalışma zamanının anlamını korur. Sonucu: host böyle bir adla üretim dışı sayılır, açılışı reddetmez ve tümünü reddeden adaptörlerle açılır. Tarama ise bu adı üretim benzeri sayar. Bu fark işçinin raporunda Known gaps'e yazılır.

## Dağıtım mesajında işçiye ayrıca söylenecekler

- Birleştirme adımı "Already up to date" verirse merge commit oluşmaz (inceleme bulgusu 3). Bu durumda raporda "HEAD^1" yerine "merge yok, dal güncel" yazılır.
- CT okuması 3'ün sonucu Known gaps'e yazılır.
