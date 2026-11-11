# Onay dosyası · yalnız ekleme istisnası (S-91)

- Tarih: 2026-10-29 · Karar: sahip (K3, GMG-ERP), sözü `onay-istisna` (CT · Setup sohbeti)
- Ölçen: Yönetici · Ofis ve CT · Setup (Mac, origin/main 4c08fc75a)

## Ne oldu

`0e818e3c155cfba992e5b59ff597d52e89488d67` (gmg-10, 2026-10-28, "Merge origin/main into docs/sce/mvp6-decisions-2026-10-26")
çakışmayı main'in dosyalarını alarak çözdü. Dalın kendi iki satırı düştü: MOD-0140 Revizyon 2 onay satırı
(`approvals.tsv`) ve onun inceleme satırı (`reviews.tsv`). Bu commit PR #241 ile main'e girdi (2026-10-28, e055e6185).
`pack_approval.py` doğru biçimde "onay dosyası güvenilmez" dedi; bu yüzden bütün paketlerin onayı durdu.

Düşen satırlar main'de hiç bulunmadı (ölçüldü: main'in birinci ebeveyn çizgisindeki hiçbir sürümde yok). MOD-0140'ın main'deki
onayı Revizyon 5 satırıdır. Satırı geri eklemek zararlı olur: eski revizyon son onay gibi okunur.

## Karar

Kontrol kaldırılmaz. Bu tek commit için kayıtlı istisna, `execution/approvals/append-only-exceptions.tsv`'de iki satır.
Araç bir istisnayı yalnız şu koşulların hepsi tutunca sayar:

1. İstisna satırı **main'den** okunur (dal kendini affedemez).
2. Düşen satırların SHA-256 özeti kayıttakiyle aynıdır.
3. Düşmenin dışında sıra ya da içerik bozulması yoktur.
4. Düşen satırlardan hiçbiri main'de hiç bulunmamıştır.
5. Düşen her satır olumludur: onay satırında `approve`, inceleme satırında UYGUN ya da KOŞULLU ve açık ENGEL yok (bir iptal
   ya da olumsuz inceleme affedilmez; düşen satırlar: MOD-0140 Rev 2 `approve` + KOŞULLU, ENGEL 0).
6. İstisna satırı main'e PR ile gelmiştir; satırın onaylayanı merge etmiştir (yüksek risk yetkili) ve yazan ile
   onaylayan onaylayıcı listesinde farklı kişilerdir.

## Tekrarlanmasın

GitHub işi `.github/workflows/approvals-append-only.yml`, onay dosyalarına dokunan her PR'da
`pack_approval.py history --base <main>` koşar. PR yalnız kendi getirdiği bozulmayla kırmızı olur. İstisna dosyası da
yalnız ekleme kabul eder.

Bağımsız inceleme (Claude alt ajanı, 2026-10-29): KOŞULLU → F-1 (olumsuz satır), F-2/F-3 (her korumaya ayrı sabotaj
testi), F-4 (yazan listede olmalı), F-5 (istisna dosyası yalnız ekleme) kapatıldı; her koruma tek başına bozulunca kendi
testi kırmızı (ölçüldü).

SAP / Oracle karşılığı: değişiklik ve onay kayıtları silinmez; bilinen bir hata, yönetici onaylı ve belgeli bir
düzeltmeyle kapatılır, denetim kapatılmaz.
