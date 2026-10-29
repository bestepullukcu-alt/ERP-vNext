# HCM-EMP-03 — Codex incelemesi 04'ün CT karşılaştırması (SOP §20.4/4)

> Control Tower kaydıdır; onay, paket değişikliği ya da kod izni değildir.

## Künye

- İnceleme: Codex, inceleme 04; kaynak GitHub dalı `chore/ct/hcm-employee-ct-01` @ 6febba2d303cafeb6ba0c3822e127d4071f1c0b0
  (sahip sözü `push-6febba2`). Model, effort, oturum: "verilemiyor". Sahip sohbete yapıştırdı, 2026-10-29.
- Hedef: MOD-0251 paketi revizyon 4 @ sha256 `7d04a408051d79d4c0075522b7eda41dcc84860e1ab1f4770ee5f020c1633d9d`
  (sahip satırı `ali.tufanoglu` dahil).
- Sonuç: **UYGUN** — inceleme 03'ün tek açık bulgusu F-26 kapandı; yeni bulgu yok.
- Bulgu dosyası: `docs/records/reviews/MOD-0251/2026-10-29-7d04a408.md`. Biçim çevirisi: yalnız boş satırlar ve sohbet
  arayüzünün eklediği saat satırı çıkarıldı; içerik aynı. Depodaki ayrıştırıcı (`pack_approval.parse_findings`) 0 bulgu
  satırı okur, sorun yok; `slices()` DL-1…DL-7 okur. Ham metin commit'e girmez (`draft-codex-04-raw.txt`).

## Codex'in ölçümlerinin CT tarafından yeniden ölçülmesi

| Codex iddiası | CT ölçümü |
|---|---|
| Uzak dal ucu 6febba2d3 | `git ls-remote` aynı sha (push sonrası). |
| Yedi dilim | `slices()` DL-1…DL-7. |
| Kabul eşlemesinde 27 kriter | Eşleme tablosunda GWT-A01…A13, B01…B07, D01…D07: 27 benzersiz satır. |
| DL-7 E5 çıkışı merkezi günlükte görülme, kesinti sonrası kurtarma, soğuk başlangıç | Paket:1672, 1685 (DL-7 kabulü ve denetim testleri; "cold start" geçiyor). |
| P2 kapanışı birleşik koşuya bağlı | Paket:1675 ve 2011: DL-2 ile DL-7'den ikinci gelen, ilkinin testlerini yeniden koşar (AK-31). |
| Eksik `tenant_id` claim'i ölçümü | Paket `tenant_id` 10 yerde; AK-42 düzeltmesi. |
| AE-1 K4'ün kalktığını iddia etmiyor | Paket:221 "This pack does not claim it is lifted." |

## CT gözlemleri (inceleyen bulgusu değil)

- İnceleme E1 düzeyindedir (belge ve kod okuma); derleme, test, API ya da ekran koşusu yapılmadı. Paket incelemesi için
  yeterli; her dilimin kendi kanıt düzeyi (DL-2 E4, DL-7 E5 …) dilim kapanışında ölçülür (S-85).
- UYGUN, revizyon 4'ü dağıtıma açmaz: önce `reviews.tsv` satırı ve onay satırı başka bir kişinin merge'üyle `main`'e
  ulaşmalı; kuyruk kayıt satırı da ilk işçiden önce merge commit ile birleşmeli (S-82).
- Açık kararlar onayı değil dilimleri bekletir: DL-7 için AE-1…AE-5 ve BL-324; DL-4…DL-6 için K-1…K-9 ve F-5 / F-12 /
  F-13. DL-2 (E4) bu kararları beklemez.
- Ara inceleme bulguları (AK-27…AK-50) resmî değildir; Codex bunları ayrıca numaralamadı, beklenen budur.

## Kart güncellemesine göre (origin/main d27f0272d, S-93 / S-94)

- **Codex tur sayısı (S-93 §5):** MOD-0251 bu sürüm için 4 Codex turunda UYGUN oldu (01, 02, 03 UYGUN DEĞİL; 04 UYGUN).
  Ön inceleme (S-93) kartta yokken koşulan üç alt ajan ara incelemesi yalnız 04'ten önce vardı (AK-27…AK-50).
- **İstek kaydı (S-94):** istek Codex'e S-94 main'e girmeden gönderildi; kayıt sonradan
  `docs/records/reviews/MOD-0251/REVIEW-REQUEST-04.md` olarak yazıldı, prompt bloğunun SHA-256'sı dosyada.
- **İpucu sınırı (S-94 madde 4), CT'nin kendi eksiği:** 04 prompt'u "ne değişti" ve "kapandı iddiası" dışında soru biçiminde
  CT ölçümleri de taşıdı (ör. kiracının başlıktan geldiği, ortak ileticinin alan adları). Kural o sırada yoktu; sonraki
  isteklerde yalnız değişiklik ve kapanış iddiası yazılacak.
- **Öğrenme listesi (S-93 §4):** inceleme 01…03'ün ENGEL ve ORTA bulgularının türleri
  `docs/guides/operations/pack-review-common-findings.md`'ye henüz eklenmedi; sahip sözüyle ayrı bir adım.
