# PVG beşinci dalga — DoR (kart §1) kanıtı: WP-0230-DL7A

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir.
> Dağıtım için iki koşul var:
> - PR #267 `main`'e girmeli. Paket revizyon 12'nin onayı ve incelemesi bu PR'da. S-5 ve CT-APR-01 bunu ister.
> - Sonra `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli ve sahip işçinin başlatılmasını onaylamalı.
>
> Tarih: 2026-10-27. Plan: `docs/records/decisions/2026-10/pvg-mod0230-dalga5-plani-01.md` (D5-1).

| DoR maddesi | WP-0230-DL7A |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 12 (`a51f2349…`). HIGH onay satırı CT dalında (`57a4e6470`); #267 birleşince geçerli olur. |
| Sözleşme | AC-1, AC-2, AC-3, AC-14, AC-15, AC-20. Prompt'taki 18 alıntı Expected HEAD'de birebir. |
| Bağımlılık | WP-0230-DL6 VERIFIED (CT dalı). AC-19 ve AC-31 `main`'de (#250). OD-PVG-8 ve OD-PVG-10 kapalı. |
| Kapsam dışı | AC-32 (DL7B). Seçiciler. Servis yolları. Gateway. Ortak `Views/Shared`, SharedResource, ortak JS/CSS, `Program.cs`, csproj. |
| Kapsam | Denetleyici: 10/10 eylemde ev kalıbı, vekil eşlemesi, `X-Diten-*` / `ReadJwtToken` temizliği. Görünümler. JS: `createList`. Yedi dilde resx. `Models/CaseIntakeTriage/**`. vitest V1–V6. `Diten.Web.Tests` W1–W7; W7 vekil eşlemesini ölçer, inceleme 12 F-1. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl7a-r1`, dal `feature/pvg/wp-0230-dl7a-r1`, Expected HEAD `5f90c216a` (CT dalı ucu). |
| Yürütme / şerit | Risk HIGH (yetki, kiracı, sözleşme). DL7B bu işten sonra başlar (D5-3). |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. CT dalına birleştirmeyi CT yapar. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7A-v1.2.md`, sha256 `3c323ed07e4040afa4c6f968e04f65664b0d848098ed6bc442f2ab16a1ebda62` @ 739694862 |
| Bağımsız inceleme (HIGH) | v1.1 KOŞULLU, v1.2 UYGUN (açık ENGEL/YÜKSEK yok). v1.0, revizyon 11 tabanlı olduğu için geri çekildi. |
| Kabul | AC-1/2/3/14/15/20 testleri, sabotajlar ve doğrulayıcı. **Kanıt sınırı:** DL-7 için gerekli seviye E4; bu iş E2 üretir. |

## CT okumaları

1. **Doğrulayıcı maddeleri ve OD-PVG-10.** Sahip kararı OD-PVG-10 (paket satır 214), bilinen sapmayı kategori olarak tanımlar: toplu silme, dışa aktarma, içe aktarma, tümünü seç ve bunlara bağlı kablolama. Bu kategorilere girenler:
   - Doğrulayıcının sunucu modunda koşulsuz eklediği BL-452 sunucu dışa aktarma maddesi (`verify_datatable_page.py:758–762`); bu bir dışa aktarma maddesidir.
   - `createList`'e özgü maddeler.

   AC-14'teki "13 items", ölçümün yapıldığı günkü sayıdır. Ölçüt bu yüzden kategoriye göre okunur: doğrulayıcının bastığı her madde bu kategorilerden birine girmeli, "diğer beş" maddeden hiçbiri basılmamalı. AC-14 metni paketin bir sonraki revizyonunda kategori ifadesine çevrilir (inceleme v1.2 F-4).
2. **Ortak 403 sayfası.** `/Home/Status/403` kabuksuz ve tek dillidir. Bu UAS-001 §3 ve §5 açısından ortak bir borçtur. AC-3'ü bozmaz; CRM de aynı ev kalıbını kullanıyor. İlgili backlog: BL-336. Ayrı bir backlog satırı CT tarafından açılacak.
3. **İşçinin rapor biçimi.** Biçimde kalan "kalan 13 madde" ifadesi, 1. madde gereği kategori listesi olarak okunur (inceleme v1.2 F-4).
4. **Ekranı kim görür.** Yeni izin anahtarları henüz hiçbir role verilmedi; grant MOD-0018'in işidir. Bu yüzden iş bittiğinde ekranı açan herkes 403 görür. UAS-001'in güvenli durumu budur.
