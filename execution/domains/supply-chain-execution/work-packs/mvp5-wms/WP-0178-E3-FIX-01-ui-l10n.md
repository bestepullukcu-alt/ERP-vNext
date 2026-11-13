# WP-0178-E3-FIX-01 — Outbound ekranları: onay modalı, miktar biçimi, "Outbound" çevirisi

- Kaynak: MOD-0178 E3 canlı turu, `docs/records/audits/2026-11/mod-0178-e3-live-tour-2026-11-13.md` (G-1, G-2) + sahip gözlemi (Beste, 2026-11-13).
- Sahip kararı: üç düzeltme de açılır (Beste, 2026-11-13: "açalım").
- Kapsam: yalnız MOD-0178 Web ekranları (OutboundShipments, PickTasks, Packing) ve onların `.resx` / `*.l10n.js` dosyaları. Servis, sözleşme ve Gateway değişmez.
- Dil: bu iş kiracı modülü — 7 dil (en, tr, fr, es, zh, ar, ru); bir dil eksikse iş bitmemiştir.
- Git: CT dalı / hat kuralları (GIT-002); açık yol ile stage; push / PR yok.

## 1. "Sevke Hazır" onayı modal standardına uymuyor

- Bugün: Detay sayfasında "Sevke Hazır" düz Bootstrap modalı açıyor (`details.js` :188-194, `#modalOutboundRelease`).
- Beklenen: "Sevkiyatı Kontrol Et" ve "Koliyi Kapat"taki gibi premium onay standardı (`.antigravity/rules/premium-modal-standard.md`, MOD-0013; `window.showConfirm`, ikon halkası, kayıt numarası satırı, `btn btn-primary` / `btn-label-secondary`).
- FEFO gerekçesi gerektiğinde (422 `FEFO_BYPASS_REASON_REQUIRED`) açılan gerekçe formu da aynı görsel standarda uymalı (yuvarlak kenar, başlık ikonu, buton sınıfları). Akış ve idempotency anahtarı kuralları değişmez (`details.js` :196-215).
- "İptal" (`#modalOutboundCancel`) da aynı standarda çekilir; gerekçe alanı korunur.

## 2. Miktar ve ölçü biçimi kültüre göre değil

- Bugün: Türkçe arayüzde servis değerleri ("2.000", "408.000", "1.500") olduğu gibi yazılıyor; nokta Türkçede binlik ayırıcı olduğu için "iki bin" okunuyor. Aynı sayfada "Kolilenecek 2" ondalıksız.
- Beklenen: ekranda gösterilen her miktar, alınabilir miktar, ağırlık ve ölçü kullanıcının kültürüyle biçimlenir (tr: `2`, `1,5`; en: `2`, `1.5`); gereksiz sıfırlar gösterilmez, birim (EA, kg) korunur. Servise giden değer biçimden bağımsız (nokta ondalık string) kalır.
- Kapsam: Outbound liste / detay / form (önizleme dahil), Toplama Görevleri liste + onay paneli, Paketleme (toplanan kalemler, koli kartı, koli formu girişi "1,5" kabul eder).
- Ortak biçimleyici varsa onu kullan; yoksa sayfa başına `Intl.NumberFormat(window.L10n culture)` ile tek yardımcı.

## 3. "Outbound" çevrilmemiş

- Bugün: tr başlık ve metinlerde "Outbound Sevkiyatlar", "Yeni outbound", "Outbound'u düzenle", "Outbound Ayrıntısı", modal metinleri ("Bu outbound'un sevkiyatları okunur…").
- Beklenen: tr'de "Çıkış Sevkiyatları", "Yeni çıkış sevkiyatı", "Çıkış sevkiyatını düzenle", "Çıkış sevkiyatı ayrıntısı" (WMS sözlüğünde başka bir terim kararlaştırılmışsa o kullanılır; önce `WarehouseVocabulary.cs` ve sayfa `.resx`'leri tara). Menü (Nav) başlığı dahil. Diğer 5 dilde de terim kontrol edilir.

## 4. "Tahsisi Önizle" seçilen lotu yok sayıyor (sahip: "çalışmıyor gibi")

- Bugün: önizleme her satırı `lotId: ''` ile gönderiyor (`form.js` :198); kullanıcı "Lot değişimi"nden FEFO 2 lotunu (G1-20261020) seçse de önizleme yine FEFO 1 lotunu (AMX-2604) gösteriyor. Kullanıcı seçiminin işe yaramadığını düşünüyor (sahip turu, 2026-11-13; önizleme 200 döndü, ölçüldü).
- Ayrıca seçilen sevkiyat hazırlık lokasyonu Sevkiyat Hazırlık rolünde değilse ilk önizleme 422 `WMS_TARGET_NOT_ALLOWED` dönüyor; uyarının ekranda göründüğü ve hangi alanı işaret ettiği kontrol edilir.
- Beklenen (en dar yol): seçilen lot önizlemede gösterilir — servis önizlemesi lot değişimini kabul ediyorsa `lotId` gönderilir; etmiyorsa önizleme satırında "seçtiğiniz lot (G1-20261020) kayıtta uygulanır; FEFO sapma gerekçesi gerekir" notu çizilir ve FEFO önerisi ayrıca gösterilir. Hangisinin mümkün olduğu MOD-0178 §8.17 / önizleme sözleşmesinden doğrulanır; sözleşme değişikliği gerekiyorsa bu iş durur ve CT'ye sorulur.

## Kabul

- Üç ekranda tr ve en ile elle tur: modal standardı, "1,5" girişi ve gösterimi, başlıklar.
- `resx-check` (kiracı, 7 dil) temiz; vitest / l10n bekçileri yeşil.
- Biçim için bir test: tr kültüründe "2.000" servis değeri ekranda "2" gösterilir (sabotaj: biçimleyiciyi kaldır → kırmızı).
- Rapor: değişen dosyalar, koşulan testler, ekran görüntüleri (tr, 375 px dahil).
