---
record_id: PSS-MOD0286-TRACKING-PAGE-02
status: applied — sahip `sayfa-uyarla` (2026-10-23); S-43 (kart CT-TRK-01, SOP §22.2/4 ve /7, PR #204 ile main'de)
prepared_at_local: 2026-10-23
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance
page: https://claude.ai/artifact/8aGEWHrcX2hAi5Gs3U85cu
supersedes_section_of: docs/records/decisions/2026-10/pss-mod0286-tracking-page-01.md ("Nasıl kullanılır" maddesi; kayıt 01 değiştirilmez)
---

# MOD-0286 — iş takip sayfası S-43 uyarlaması (CT-TRK-01)

> Kayıt 01 sayfayı işaretlenebilir olarak açmıştı (sahip bir işi işaretler, CT okur). PR #204 ile main'e giren S-43 sayfayı
> salt okunur yaptı. Sahip önce sorulmasını istedi; `sayfa-uyarla` sözüyle uygulandı. Sayfa yeni açılmadı; aynı bağlantı.

## Ne değişti

- **Düğmeler:** iş başına işaretleme kutusu ve yazan kod kaldırıldı. Bitti durumu statik bir işaretle ve "kanıt:" alanıyla
  gösterilir. Kalan tek kutu "Bitenleri gizle"dir; yalnız görünümü süzer, tarayıcıda tutulur, veri yazmaz.
- **Yazma kuralı:** `db` kuralları `{path: "items", read: "view", write: "owner"}` ve `{path: "meta", read: "view",
  write: "owner"}`. `user` yeteneği kaldırıldı (artık kullanılmıyor).
- **Nasıl kullanılır (kayıt 01'in yerine):** listeyi yalnız CT günceller. Her oturumun başında ve her main
  birleştirmesinden sonra CT açık işleri depoyla karşılaştırır; PR'ı merge olmuş, kuyruk durumu kapanmış ya da kabul /
  karar kaydı yazılmış işi kanıtıyla "bitti" yapar. Kanıtı olmayan iş açık kalır. Sahip bir değişiklik isterse sohbette söyler.

## Eski işaretler

Uyarlamadan önce `marked_by_page` alanı taşıyan iş arandı: **yok**. Sahip sayfada hiç işaret bırakmamıştı; depoya
işlenecek ya da sorulacak işaret yok.

## Aynı adımda yapılan güncellemeler (S-43/7)

| İş | Değişiklik | Kanıt |
|---|---|---|
| YEDEK (`wp-yedek`) | bitti, Geçmiş'e taşındı | `origin/chore/ct/pss-ui-ct-01` = 005a0cc58 (push 2026-10-23) |
| MAIN (`g-main`) | yeni, bitti | commit cbcfeb99d (main fd8a8fe11 birleştirmesi; S-42 koşulu yoktu) |
| S-43 (`g-s43`) | yeni, bitti | sayfa sürüm 2 (1792742931-4ddf); bu kayıt |
| YEDEK (`wp-yedek2`) | yeni, açık | birleştirme commit'i origin'de değil |
| COMMIT (`wp-commit`) | yeni, açık | DL-1, DL-2 taslakları ve bu kayıt commit edilmedi |
| DL-1, DL-2 (`wp-dl1`, `wp-dl2`) | ayrıntı güncellendi, açık | taslaklar dalda izlenmeyen dosya; kabul yok |
| FU-14 (`fu-14`) | yeni takip | UIC-15 hücresinde kaçışsız dikey çizgi; a11y atfı :628-631 yerine :630-633 |
| FU-15 (`fu-15`) | yeni, başka sahip | `frontend-standards.md:215` "812 satır", ölçülen 606 |
| durum şeridi (`meta/state`) | HEAD, onay durumu, tarih | — |

## Doğrulama (2026-10-23)

- Yayım: sürüm 2, `db` (2 kural, ikisi de `write: owner`); görünürlük yalnız sahip.
- Kaynak denetimi: sayfa kodunda `update`, `set`, `delete` çağrısı ve iş başına `input` yok.
- Kural ölçümü: düzenleyici düzeyinde (`as_level: admin`) bir `items` yazması **reddedildi**; okuma düzeyinde
  (`as_level: view`) `items` okuması 56 belge döndürdü.
- Veri: tek toplu yazım, 10 belge (atomik), hepsi okunan sürüme sabitlendi.
- Ölçülmeyen: sayfanın tarayıcıdaki görüntüsü (tarayıcı panesi oturum açma sayfası gösteriyor; sahibin hesabıyla
  oturum açılmaz).
