# HCM-EMP-03 — Codex incelemesi 05'in CT karşılaştırması (SOP §20.4/4)

> Control Tower kaydıdır; onay, paket değişikliği ya da kod izni değildir.

## Künye

- İnceleme: Codex, inceleme 05; istek `docs/records/reviews/MOD-0251/REVIEW-REQUEST-05.md` (prompt bloğu sha256
  `10c43907…`); kaynak commit a1a6526d4 (dal `chore/ct/hcm-employee-ct-01` @ 8c0efd7ca, sahip sözü `push-8c0efd7`).
  Model, effort, oturum: "verilemiyor".
- Yol: sahip → Yönetici · Ofis → bu kule (2026-10-29 17:33; kart dışı yeni akış, Yönetici bildirimi). Ham metin
  `draft-codex-05-raw.txt`, 1407 bayt, sha256 `d251a34b…` — Yönetici'nin bildirdiği özetle aynı. Ham metin commit'e girmez.
- Hedef: MOD-0251 paketi revizyon 5 @ sha256 `619ec85b1f56a5d91cb5abafe92574e38d8116570a789d7f6d6cbc699ade7526`.
- Sonuç: **UYGUN** — inceleme 04'ten açık bulgu yoktu; yeni bulgu yok.
- Bulgu dosyası: `docs/records/reviews/MOD-0251/2026-10-29-619ec85b.md`. Biçim çevirisi: ilk satırdaki "OD-0251"
  kopyalamada düşen "M" ile "MOD-0251" yapıldı (Yönetici notu); boş satırlar inceleme 04 kaydının düzenine getirildi;
  içerik aynı. Depodaki ayrıştırıcı (`pack_approval.parse_findings`) 0 bulgu satırı okur, sorun yok.

## Codex'in ölçümlerinin CT tarafından yeniden ölçülmesi

| Codex iddiası | CT ölçümü |
|---|---|
| Revizyon 4 ile hedef arasında yalnız paket değişti; services, frontend, gateway, scripts değişmedi | `git diff --stat 111b1b4c6 a1a6526d4`: 1 dosya (paket), 49 ekleme 13 silme; bu dört klasörde fark yok. |
| `pack_approval_limit` (None, None), dispatch kararı `ok`; önceki sürümde `stop` | Ön incelemeler 1 ve 2 ile paket yazarı aynı sonucu ölçtü (S-93 kayıt dışı). |
| Ön koşullar araçla denetlenmiyor; paket bunu CT'ye ve NEDEN satırındaki path@commit'e bağlıyor | Paket Revizyon 5 notu ve ikinci "Revision 5 (testable)" maddesi; Kurulum'a aday öneri olarak iletildi (sahip kararı ayrı). |
| DL-1…DL-7 | `slices()` yedi dilim. |

## CT gözlemleri (inceleyen bulgusu değil)

- **Codex tur sayısı (S-93 §5):** MOD-0251 için 5 Codex turu (01–03 UYGUN DEĞİL, 04 UYGUN, 05 UYGUN). 05, 04'ün UYGUN
  sürümünde kaçırılan bir künye alanı yüzünden gerekti (işçi kapısı CT-FIX-04); 05'ten önce iki ön inceleme turu koşuldu,
  ikisi ENGEL'siz.
- **Sıra kazası:** PR #296, revizyon 5 commit'leri dala girdikten sonra ve "henüz birleştirmeyin" yorumuna rağmen
  2026-10-29 13:15Z'de birleşti (009b91d4f). main'de paket revizyon 5, onay ve inceleme satırları revizyon 4'e bağlı;
  `pack_approval check --ref origin/main` → "paket onaydan sonra değişti (62 satır) — yeniden onay gerekli". Revizyon 4
  satırları yalnız-ekleme kuralıyla kalır; yeni satırlar yeni bir PR ile gelir.
- **Onay dağıtım izni değildir:** her dilim yalnız kendi ön koşulu kayda geçince başlatılır (DL-2: izin genişliği kararı
  F-13; DL-3: liste/detay kararı F-5; DL-4…DL-7 açık kararları).
