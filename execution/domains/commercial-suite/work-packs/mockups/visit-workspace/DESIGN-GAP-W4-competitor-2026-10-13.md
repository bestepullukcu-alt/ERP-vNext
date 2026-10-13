# DESIGN GAP — W4 rakip ürün yönetimi: mockup ↔ brif ve kararlar

> **Tarih:** 2026-10-13 · **Hazırlayan:** CT
>
> **Kaynaklar:**
> - Mockup [`W4 Rakip Urunler (standalone).html`](W4%20Rakip%20Urunler%20(standalone).html); çözülmüş hâli `decoded-w4-rakip/Rakip.dc.html`. 15 ekran: list · empty · oneriler · new · edit · detail · approve · merge1–3 · reject · readonly · unauth · conflict · ar.
> - Brif + kararlar [BRIEF-W4-competitor-products](BRIEF-W4-competitor-products.md).
>
> **Kural:** kararlar mockup'ın önüne geçer.

## 1. Brifle uyumlu (✅, aynen uygulanır)
- **R1 liste:** süzgeçler (ürünümüz, firma, etken madde, ülke, geri ödeme, durum), "ayrıca:" diğer adlar, ülke çipleri + "n/m fiyatlı", raporlarda kullanım sayısı, durum, son değişiklik, eylemler, "Dışa aktar", iki sekme ("Temsilci önerileri" rozetli).
- **R1 öneriler:**
  - öneri kartı (ad, yazılan firma, öneren, tarih, hangi ürünün raporunda, kullanım sayısı);
  - **benzer kayıtlar + benzerlik yüzdesi**;
  - üç eylem.
- **R2 yeni / düzenle:**
  - kimlik, diğer adlar (birleştirmeden gelenler işaretli), etken madde etiketleri, form, güç, ATC;
  - rekabet (ürünlerimiz zorunlu, endikasyonlar Kavram Grafiği'nden);
  - **ülke tablosu:** kutu fiyatı + para birimi, geri ödeme, geçerlilik, kaynak; "Geçmiş (n)" salt okunur önceki dönemler;
  - **temsilci kartı önizlemesi** (ülke seçimli).
- **R3 ayrıntı:**
  - kimlik, rekabet, ülke tablosu + geçmiş;
  - değişiklik geçmişi (fiyat dönemi, diğer ad, geri ödeme);
  - **raporlarda kullanım** (son 3 ay, doktor / rapor, ürünümüze ve ülkeye göre, "doktor kimliği gösterilmez");
  - birleştirme izi;
  - "Pazar verisi · yakında" (W7);
  - eylemler: Düzenle, Pasife al, Başka kayıtla birleştir.
- **R4 öneriyi onayla:** form öneri bilgisiyle; bilgi şeridi "Temsilci önerisi · kişi, tarih · n raporda".
- **R5 birleştir:**
  - 1/3 hedef seç (benzer kayıtlar + yüzde);
  - 2/3 alan alan fark (sarı) + ülke satırı taşı / korunur;
  - 3/3 etkisi (rapor bağlama, diğer ad, ülke taşıma, "Birleştirildi", geri alınamaz) + onay kutusu.
- **R6 reddet:** gerekçe zorunlu (≤ 500), açıklama.
- **R7:** salt okuma; yetkisiz (UAS-001: iskelet yok, yalnız mesaj); kaydetme çakışması ("Kayıt başkası tarafından değiştirildi…" + Yenile); AR liste (RTL).

## 2. Kararla değişenler (✏️ / ➕)
### G1 · Workflow onayı (karar 2026-10-13: temsilci önerisinin kararı MDM'de Workflow ile, Satış Operasyon onaylar)
Mockup brifin ilk sürümüyle çizilmiş; Workflow öğeleri yok. **Bizde, W3 itiraz kütüphanesi deseniyle** (canlıda doğrulandı):
- **R4 / R5 / R6 son düğmeleri:** "Onayla ve kaydet" / "Birleştir" / "Reddet" → **"Onaya gönder"**.
  - Karar taslak olarak kaydedilir ("Kararı kaydet") ve onaya gönderilir.
  - R5'teki "Etkisini okudum" onay kutusu korunur; gönderim için zorunlu.
- **Öneri kartı durumu:**
  - "Karar bekliyor" (henüz hazırlanmadı);
  - "Taslak karar · Tamamla / Birleştir / Reddet";
  - "**Onay bekliyor** · kişi, tarih";
  - "Taslağa geri gönderildi · not".
- **Hazırlayan için:**
  - "Geri çek" düğmesi;
  - SoD kutusu "Bu kararı siz hazırladınız; Görev Merkezi'nde başka biri onaylar."
- **Onaylayıcı görünümü** (Görev Merkezi "Kaynak kayda git" → öneri ayrıntısı): kararın özeti (tamamla → doldurulan alanlar; birleştir → hedef + etkisi; reddet → gerekçe) + "Onayla" / "Taslağa geri gönder" (not zorunlu). Kararı hazırlayan / gönderen için düğmeler pasif + SoD notu.
- **Onaylanınca** karar uygulanır (Aktif / Birleştirildi / Reddedildi) ve öneri listeden düşer; "Uygulandı · onaylayan, tarih" izi ayrıntıda kalır.
- **R3 "Başka kayıtla birleştir"** (iki **aktif** kaydın birleşmesi, temsilci önerisi değil): karar gereği **doğrudan**, yetkiyle, onaysız. Aynı 3 adım, son düğme "Birleştir".

### G2 · Raporlarda kullanım sayıları (R1 sütunu, R1 öneri kartı, R3 bölümü)
Sayılar CRM raporlarından gelir.
- CRM toplu okuma ucu (doktor kimliği yok, hücre < 5 → "<5") → **W4-BE-a'ya eklenir**.
- Web bu ucu MDM verisiyle birleştirir (WEB-c).
- MDM CRM'i çağırmaz.

### G3 · Endikasyon seçici (R2)
Seçenekler CRM Kavram Grafiği'nden (BE-c `products/{productId}/indication-profiles`, seçili ürünlerimiz için). MDM yalnız düğüm kimliğini saklar; ad CRM'den okunur. Kavram grafiği yoksa "Bu ürün için endikasyon tanımlı değil" (seçici pasif).

### G4 · Form listesi
Mockup listesi (Tablet, Film tablet, MR tablet, Uzatılmış salımlı tablet, Kapsül, MR kapsül, Şurup, Ampul) yerine **MDM form sözlüğü** (`BrandProductVocabulary` DosageForm). Eksik form varsa sözlüğe eklenir; ayrı liste açılmaz.

### G5 · Benzerlik yüzdesi
BE-b2 öneri ve hedef aramasında **normalize ad benzerliği** (diğer adlar dahil, 0–100) döner. Yöntem: küçük harf + Türkçe katlama + boşluk / noktalama temizliği + trigram. ≥ 80 "olası aynı kayıt" vurgusu.

### G6 · Mockup eksikleri bizde (W3'teki gibi)
- Menü (MDM sol menü, sayfa kaydı).
- Koyu tema token'ları.
- 7 dil.
- Telefon gerekmez (masaüstü + tablet).
