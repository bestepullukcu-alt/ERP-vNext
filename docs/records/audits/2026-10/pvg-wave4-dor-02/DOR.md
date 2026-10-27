# PVG dördüncü dalga — DoR (kart §1) kanıtı: WP-0230-DL3D

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir. Dağıtım için iki şart var:
> - `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli.
> - Sahip bu sohbette işçi oturumunun açılmasını onaylamalı.
>
> Tarih 2026-10-27. Plan: `docs/records/decisions/2026-10/pvg-mod0230-dalga4-plani-01.md` (D4-2). Kayıt biçimi: `docs/records/decisions/2026-10/pvg-mod0230-runtime-onay-kaydi-01.md` (RTA-01, sahip "kayit-karar").

| DoR maddesi | WP-0230-DL3D |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 11 (`918dd660…`), `main`'de onaylı (#261, HIGH). |
| Sözleşme | AC-10. Bağ biçimi ODPVG6-01'den, kayıt biçimi RTA-01'den. Prompt'taki 14 alıntı Expected HEAD'de birebir. |
| Bağımlılık | OD-PVG-6 onaylı. RTA-01 kayıtlı. WP-0230-DL3C INTEGRATED. |
| Kapsam dışı | DL3B'nin R1 kuralı gevşetilmiyor; commit edilmiş açık bayrak bugün de ihlal sayılıyor. Bu kuralı gevşetmek DL-9'un işi. Canlı çalışma onayı da kapsam dışı. |
| Kapsam | `PvgServiceApiHost.cs`'e ThrowIfUnsafe'in üçüncü kuralı eklenir. Yeni test dosyası `PvgServiceRuntimeApprovalTests.cs` (A1–A6). DL3B tarayıcısının genişletilmesi ve C1, C1b, C2–C4 testleri `PvgServiceApiHostTests.cs`'te. Bayrağı açan dört mevcut test yapılandırmasına birer satır eklenir. `Program.cs` diff'i boş kalır. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl3d-r1`, dal `feature/pvg/wp-0230-dl3d-r1`, Expected HEAD `7e4a50918` (CT dalı ucu), temiz. |
| Yürütme / şerit | Risk HIGH (yetki, uyum). WP-0230-DL6 ile dosyaları ayrık, paralel gidebilir. WP-0230-DL4B bu işten sonra gelir (D4-4). |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, dal + sha + test sonucunu bildirir ve durur. `origin/main` birleştirmesi yapmaz. CT dalına birleştirmeyi ve birleşik testi CT yapar. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL3D-v1.2.md`, sha256 `e1d1d9a315ce235cab7681ec2bbda354233aa891f8847e4c41d27ed11c6cd3f6` @ 4f2e8ac02. |
| Bağımsız inceleme (HIGH) | v1.0-inceleme-01 KOŞULLU, v1.1-inceleme-01 KOŞULLU, v1.2-inceleme-01 UYGUN. ENGEL 0, YÜKSEK 0. |
| Kabul | AC-10 testleri (A1–A6, C1–C4) ve sabotajları. **Kanıt sınırı:** DL-3 için gerekli seviye E3, bu iş E2 üretir. |

## CT okumaları (v1.0 inceleme bulgusu F-3)

1. **Biçim denetimi.** Host, `^RTA-MOD0230-[0-9]{3}$` biçimine uymayan değeri reddeder. RTA-01 madde 1'deki `RTA-MOD0230-NNN` biçiminin doğrudan karşılığıdır.
2. **Mükerrer kimlik.** Aynı `runtime_approval_id`'yi taşıyan iki kayıt, denetimde "belirsiz" sayılır ve ihlal verir. RTA-01 madde 3 "tam bir kayıt" der; iki kayıt bu şartı karşılamaz.
3. **Test değeri `RTA-MOD0230-999`.** Bu değer yalnız testlere ayrılır ve depoda gerçek bir kayda karşılık gelmez. Testler bu değeri geçici dizinde kurar.
4. **Yeni test dosyası ve `PvgServiceApiHostTests.cs` uzantısı.** DL-3 "Yollar" alanı yalnız `PvgServiceApiHostTests.cs`'i sayar. Yeni dosya DL3C'deki gibi okunur: DL-3 testlerinin aynı test projesinde ayrı bir dosyaya yazılması. Tarayıcı iç içe ve private bir sınıf olduğu için tarama testleri `PvgServiceApiHostTests.cs`'in sonuna eklenir.
