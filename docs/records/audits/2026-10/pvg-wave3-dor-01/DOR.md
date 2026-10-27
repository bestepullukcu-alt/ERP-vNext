# PVG üçüncü dalga — DoR (kart §1) kanıtı: WP-0230-DL4A

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir.
> Dağıtım için iki şey gerekir: `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli ve sahip işçi oturumunun açılmasını bu CT sohbetinde onaylamalı.
> Tarih 2026-10-26. CT dalı `chore/ct/pvg-ct-01`.
> Plan kararı: `docs/records/decisions/2026-10/pvg-mod0230-dalga3-plani-01.md` @ 97d28d2da (D3-1…D3-6, sahip "dalga3-plan").

| DoR maddesi | WP-0230-DL4A |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` revizyon 10 (`c236cb87…`). Expected HEAD 1451b2a50'de onaylı (#223, `pack_approval.py check` çıkış 0). Prompt yazıldıktan sonra paket onayı değişmedi. |
| Sözleşme | AC-7, AC-19 (a ve b, tamamı), AC-31. Prompt'ta satırıyla alıntılı: 36 alıntı, Expected HEAD'de birebir. |
| Bağımlılıklar | WP-0230-DL2B main'de (#226), INTEGRATED. WP-0230-DL3B main'de (#234), INTEGRATED. Paket DL-4 "Yollar": aynı test dosyasında "DL-3 önce" koşulu sağlandı. |
| Kapsam dışı | AC-5 (OD-PVG-2) ve AC-6 (OD-PVG-3) WP-0230-DL4B'ye kaldı. `Post_with_unresolved_or_cross_tenant_reporter_selection_returns_404` MOD-0150 / MOD-0288 okuma sözleşmesini bekliyor. `Program.cs` ve `PvgServiceApiHost.cs` WP-0230-DL3C'ye ait. Gateway ve ekran (DL-7) de kapsam dışı. |
| Kapsam | DL-4 yolları ve dört CT okuması (aşağıda). `PvgServiceApiHostTests.cs` içinde yalnız E1–E4 değişir. Yeni testler ayrı dosyaya, `PvgCaseIntakeApiContractTests.cs`'e yazılır. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl4a-r1`, dal `feature/pvg/wp-0230-dl4a-r1`, Expected HEAD `1451b2a50`, temiz. |
| Yürütme / şerit | Risk HIGH (sözleşme, kiracı, yetki, uyum, denetim). WP-0230-DL3C ile paralel gidebilir (D3-4, K5): ikinci biten iş `origin/main`'i birleştirir ve kontrolleri yeniden koşar; satır numarasıyla değil üye adıyla ölçülür. |
| Teslim (kart S-63) | İşçi yalnız yerel commit yapar. `origin/main`'i dalına birleştirir (dal birinci ebeveyn), filtreli testleri koşar, CT'ye dal + sha + test sonucu bildirir ve durur. Push ve PR'ı, sahibin bu sohbette dal ve sha ile verdiği onayla CT yapar. |
| Hedef ajan | Prompt'un ilk satırı (`backend-architect`). |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL4A-v1.2.md` sha256 `f05812ba7202dc26eaa96cefb1aeaa99ba115056f2591ad0029f6e23656dfdd4` @ 810ea17fc. Yaklaşık 20 bin token; işçiye yol ve sha ile verilir (kart S-52). |
| Bağımsız inceleme (HIGH) | v1.0: `docs/records/reviews/MOD-0230/prompts/WP-0230-DL4A-v1.0-inceleme-01.md` @ 6c5437bbb, KOŞULLU. v1.2: `…/WP-0230-DL4A-v1.2-inceleme-01.md` @ ea61a047a, UYGUN; ENGEL 0, YÜKSEK 0. |
| Kabul | AC-7, AC-19 ve AC-31 adlı testleri ve sabotajları; ayrıca 10 CT eki test (C1–C8, R3, R4). **Kanıt sınırı:** DL-4 için gerekli seviye E4, bu iş E2 üretir. Resmî kabul canlı kapıya kadar açık kalır (KANIT-SEVIYESI-01). |
| Keşif | `docs/records/audits/2026-10/pvg-m1-01-discovery-02/MOD-0230-case-intake-triage.txt` @ 4ebe4b15f |

## CT okumaları (inceleme v1.0 bulgu 3) — DL-4 "Yollar"ın harfinde olmayan dört değişiklik

DL-4 "Yollar" şunları sayar: endpoint dosyası, servisteki kontrol sırası ve `ListDraftsAsync`, sayım üyesi ve `PvgIntakeDraftQueryResult.cs`, API testleri. Aşağıdaki dört değişiklik bu listede adıyla geçmez. CT, her birini aynı maddelerin zorunlu sonucu olarak okur:

1. **`GetDraftByIdAsync` ve `PvgIntakeDraftQueryResult.Intake`.** AC-19 (a), GET öğe yanıtının sürümü döndürmesini ister. Sürüm, kaydın kendisinden okunur.
2. **Servisteki iki yeni aşırı yükleme.** AC-31'de yalnız yanıtta olan alanlar istekte gelirse reddedilir. AC-7'ye göre bu ret izin kapısından sonra değerlendirilmelidir. Komut dosyaları değişmez.
3. **R4 (`CountAsync` kiracı denetimi testi).** AC-19 (b) kiracının bütün kayıtlarının sayısını ister. Sayım üyesi, kiracı sınırını (`multi-tenancy`) taşımak zorundadır.
4. **`PvgServiceApiHostTests.cs` E1–E4.** Bunlar DL-4 sözleşmesinin derleme ve şekil sonucudur: istek şekli, yanıt şekli, kaldırılan alan dizesi ve test deposundaki sayım üyesi. Paket DL-4 "Yollar"daki "API testleri" kapsamındadır. DL-3 önce koşulu sağlandı.

## Kabulde ayrıca bakılacak inceleme notları ("diğer")

1. **v1.2 bulgu 8 (DÜŞÜK).** Birleştirme adımında `origin/main` zaten dalın içindeyse (`Already up to date`) merge commit oluşmaz. Bu durumda işçi "HEAD^1" yerine "merge yok, dal güncel" yazar; CT bunu dağıtım mesajında söyler. C2'nin önceki sürümü GET yanıtından okuduğunu CT kabulde ölçer.
2. **v1.2 bulgu 9 (DÜŞÜK).** Biçim kusurları: "Commit ve teslim" maddesi önceki maddenin içine kaymış, NEDEN'de "Bu sürüm (v1.1)" etiketi kalmış. Yalnız metin; sözleşmeyi değiştirmez.
3. **Bilinen boşluklar (prompt'ta yazılı).**
   - Create yanıtı sürüm taşımaz; istemci sürümü GET ile okur.
   - Alan güvenliği reddedilen ama izinli çağıran, doğrulama kodlarını görür. Alan güvenliği kayıt okumasından sonra koşar.
