# CT sahip kararları — 2026-11-27 (Beste)

- **Tarih:** 2026-11-27
- **Karar veren:** Beste (MVP-1/2/3/4/6 sahibi, merkezi ekip), sohbette; durum tablosunun her satırına "devam edelim" ve ayrıca açık cevaplar.
- **Not:** Bu kayıt kod, commit ya da dağıtım izni değildir. Yüksek riskli paketlerde resmi onay satırı (CT-APR-01) yazan hesaptan farklı bir
  kişi tarafından atılır (`scripts/pack_approval.py` kuralı: yazan ve onaylayan aynı kişi olamaz). CT paketleri `bestepullukcu-alt`
  hesabıyla yazıldığı için aşağıdaki iki paketin onay satırı Ali (GMG-ERP) tarafından atılır; Beste'nin kararı sahip kararıdır.

## 1. Paket kararları

| Konu | Karar |
|---|---|
| MOD-0191 r7 (`588c451e`) | Sahip kabul eder; inceleme `2026-11-25-588c451e.md` (KOŞULLU) bulguları F-1 (AC-S41 "İngilizceden farklı" şartı), F-2 (politika listesi parametreleri), F-3 (DL-5 sırası) **DL-5 iş paketinin giriş koşulu** olarak kapanır. Resmi onay satırı: Ali. |
| MOD-0142-FU01 r4 (`e6bd75a6`) | Sahip kabul eder. **O-R3-1 = evet** (API LOCATION doğrulaması + mal kabul rolüne `inventory.locations.read`); **O-R3-2 = evet** (CompensationBlocked, yeniden gönderim / elle çözüm, en çok 5 deneme); **O-R3-3 = A** (kaydedene anında uyarı, Details rozeti ve çöz düğmesi, sistem günlüğü alarmı; Görev Merkezi bildirimi FU02); **S-4 = mevcut lot kullanılır** (paket önerisi). İnceleme `2026-11-25-e6bd75a6.md` (KOŞULLU) bulguları F-1…F-6 **DL-1 iş paketinin giriş koşulu** olarak kapanır. Resmi onay satırı: Ali. |

## 2. Platform düzeltmeleri (AC-15 canlı denemesinden)

| İş paketi | Karar |
|---|---|
| WP-0027-FIX-01 (davet şablonu sözleşme kapısı) | Gönderim kapısı uygulanır; ret durumunda **başka dile düşülmez**, e-posta gitmez, operatöre "e-posta gitmedi" denir. Dev ortamındaki eski `tenant.invite.email` en / tr 1.1.0 satırlarını **CT arşivler** (denetlenen arşiv yolu; seed 1.2.0'ı ekler). |
| WP-0009-FU03-FIX-01 (kiracı düzenleme formu) | Düzenleme modunda yönetici e-postası **gizlenir**; yerine "Yöneticiler sekmesinden yönetilir" notu (en, tr). |
| WP-0018-FIX-01 (rol ataması sessiz düşüyor) | **Merkezi ekip (biz) düzeltir.** Beste: "Ali şuan düzeltemez biz düzeltelim Aliye bu şekilde düzelttik diye bilgi metni verelim". Auth (MOD-0018) Ali'nin alanıdır; XMC-001 kapsamında bu iş için değişiklik izni sahip tarafından verildi, Ali'ye bilgi metni gönderilir. Dev'deki yabancı benzersiz index'in kaldırılması bu düzeltmenin dev adımıdır (kod düzeltmesinden sonra, kayıtla). |

## 3. Devam kararları (iş paketleri)

Beste'nin "devam edelim" dediği işler: MOD-0177-FU01 DL-3, MOD-0360-FU01 DL-2.0, MOD-0040-FU02 DL-2 (MDM kısa kod kilidi Ali'de — yapılabilen kısım), P9C tam Platform testi ve hatta alma, MOD-0179 DL-2, MOD-0180 kalan dilimler, MOD-0182 sıradaki dilim, MOD-0186 DL-6, MOD-0183 r12 incelemesi ve onayı, MOD-0147 CT doğrulaması, MOD-0185 / MOD-0187 korelasyon (D7) düzeltmesi, MOD-0190 / MOD-0192 yeniden doğrulama, Demand v2 açık soruları, MOD-0140 sıradaki adım analizi, MOD-0142-FU01 DL-1, INT3-01 (Ali'ye bağlı kısım bekler), WP-0217-DL1 (Treasury).
