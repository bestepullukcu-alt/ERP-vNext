# MOCKUP BRIEF — Ticari müşteri: kurumun ticari profili (satan şirketimize göre), ticari roller, "Ticari müşteriler" görünümü, kredi / bakiye özeti

> **Tarih:** 2026-10-14 · **Hazırlayan:** CT · **İsteyen:** ürün sahibi.
>
> **Dayanaklar:**
> - Blueprint v8.1, Enterprise Object Catalog: **Müşteri (Account)** nesnesinin tek sahibi **MOD-0149** (CRM Kurumlar).
> - Ticari tarafın modülleri:
>   - MOD-0156 fiyat listeleri;
>   - MOD-0168 sipariş;
>   - MOD-0169 faturalama;
>   - MOD-0120 alacaklar (kredi, tahsilat);
>   - MOD-0118 genel muhasebe.
> - Yol haritası [ROADMAP-visit-workspace](../../ROADMAP-visit-workspace.md) **K-W13** ve "Ticari müşteri + finans" programı (en sonda).
> - Mevcut ekranlar:
>   - **CRM › Kurumlar** listesi (sunucu tarafı, 43 bin kurum);
>   - kurum ayrıntısı: bölüm kartları, "İlgili kurumlar" ilişkileri, dış referanslar.
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: W3E ve W4 mockup'larıyla aynı bileşenler, açık / koyu tema. Masaüstü öncelikli; tablet bozulmasın.
> - **Kullanıcı:** satış operasyonu / ticari ekip ve finans. Saha temsilcisi bu ekranları görmez; yalnız sipariş ekranında sonucu görür (W6).
> - **Dil:** Türkçe çiz. Arapçada sağdan sola düzen bozulmamalı; bir örnek yeter.
> - **Gerçekçi demo verisi:**
>   - Satan şirketlerimiz:
>     - **Diten İlaç A.Ş.** (Türkiye, TRY);
>     - **Grand Medical Gulf FZ-LLC** (BAE, AED).
>   - Müşteri şirketler:
>     - **Selçuk Ecza Deposu** (distribütör, İstanbul; şubeleri Ankara, İzmir);
>     - **Nevzat Ecza Deposu**;
>     - **Eczane Yıldız Zinciri** (merkez + 12 şube eczane);
>     - **Acıbadem Sağlık Grubu** (satın alma merkezi; hastaneleri teslim noktası);
>     - **Gulf Drug LLC** (Dubai).

---

## Kavram ve kurallar (ekranın anlatması gerekenler)
1. **Ticari müşteri ayrı bir kayıt değildir.** Mevcut kurum kaydına eklenen ticari profildir. Aynı kurum hem ziyaret edilen bir yer hem ürün alan bir müşteri olabilir.
2. **Profil satan şirketimize göredir.** Bir müşteri Diten İlaç A.Ş.'den TRY ve 60 gün vadeyle, Grand Medical Gulf'tan AED ve 30 gün vadeyle alabilir. Her satan şirket için ayrı satır ya da sekme.
3. **Ticari roller:**
   - **Sipariş veren:** siparişi veren müşteri.
   - **Teslim alan:** malın gittiği adres / kurum. Örn. zincirin şube eczaneleri, grubun hastaneleri.
   - **Fatura alan.**
   - **Ödeyen.**
   - Bir kurum hepsini birden üstlenebilir ya da roller farklı kurumlara bağlanabilir. Örn. Eczane Yıldız Zinciri merkezi siparişi verir ve öder, 12 şube teslim alır.
4. **Ticari koşullar** (bu profilde): para birimi, ödeme vadesi, fiyat listesi, iskonto grubu, varsayılan teslim deposu / adresi, e-fatura / e-arşiv mükellefiyeti (TR), vergi numarası / vergi dairesi (TR VKN; BAE TRN).
5. **Kredi limiti, kullanılan kredi, açık bakiye ve vadesi geçmiş borç** finans tarafının verisidir (alacaklar modülü).
   - Kartta **ayrı bir "Finans" bölümünde**, kaynağı belirtilerek görünür.
   - Kredi limiti değişikliği **onaya** gider: hazırlayan onaylayamaz.
6. **Müşteri bloke edilebilir:** sipariş bloğu (gerekçeli). Bloke müşteriye yeni sipariş alınamaz; temsilci ön siparişte uyarı görür.
7. **Eczane ↔ depo ilişkisi** mevcut "İlgili kurumlar" ilişkisiyle tutulur ("çalıştığı depolar"). Ön sipariş (W6) depoyu bu listeden önerir.

---

## T. Ekranlar

### T1. Kurum ayrıntısında "Ticari bilgiler" kartı
**Durumlar:**
- **(a) Ticari müşteri değil:** açıklama + "Ticari müşteri yap" (satan şirketi seçerek).
- **(b) Tek satan şirketle aktif:**
  - özet satırı: para birimi, vade, fiyat listesi, varsayılan depo;
  - roller çipleri;
  - vergi bilgisi;
  - finans özeti: kredi limiti, kullanım % çubuğu, açık bakiye, vadesi geçmiş.
- **(c) İki satan şirketle:** sekme ya da iki satır (TR / BAE).
- **(d) Bloke:** kırmızı değil, uyarı şeridi. "Sipariş bloğu · gerekçe · kişi · tarih" + "Bloğu kaldır".
- **(e) Salt okuma:** düğmeler gizli.

Geçmiş bağlantısı: alan değişiklikleri (kim, ne zaman, eski → yeni).

### T2. Ticari profil oluştur / düzenle (satan şirket başına)
**Alanlar:**
- satan şirketimiz (salt okunur başlık);
- durum;
- para birimi;
- ödeme vadesi (referans listesi: peşin, 30, 60, 90 gün…);
- fiyat listesi (seçim; liste yoksa "Fiyat listeleri · yakında");
- iskonto grubu;
- varsayılan teslim deposu / kurumu;
- vergi no + vergi dairesi (ülkeye göre doğrulama);
- e-fatura mükellefi;
- finans müşteri numarası (otomatik);
- not.

**Roller bölümü:**
- "Sipariş veren / Teslim alan / Fatura alan / Ödeyen" — her biri "bu kurum" ya da başka kurum seçimi.
- Teslim alan için **çoklu** liste (şubeler).

### T3. Kurumlar listesinde "Ticari müşteriler" görünümü
**Süzgeçler:** satan şirketimiz, ülke / şehir, kurum türü (depo, zincir, hastane grubu…), durum (aktif / bloke), kredi kullanımı (> %80), vadesi geçmiş var.

**Sütunlar:**
- kurum;
- tür;
- satan şirketler (çipler);
- para birimi;
- vade;
- kredi limiti + kullanım çubuğu;
- açık bakiye;
- vadesi geçmiş;
- durum;
- son sipariş tarihi.

**Durumlar:** boş ("Ticari müşteri yok"), Arapça.

### T4. Kredi limiti değişikliği (onaylı)
- **Pencere:** mevcut limit → yeni limit, gerekçe, "Onaya gönder".
- Durum çipi: "Onay bekliyor · kişi, tarih". Hazırlayan için görev ayrılığı bilgi kutusu. Onaylayıcı görünümü: Görev Merkezi'nde.

### T5. Sipariş bloğu koy / kaldır
Gerekçe zorunlu (≤ 500). Etkisi: "Yeni sipariş ve ön sipariş alınamaz; açık siparişler etkilenmez."

### T6. Ticari roller ağacı (isteğe bağlı görünüm)
Sipariş veren → teslim alan şubeler → ödeyen; küçük ağaç / liste görünümü. Örnek: Eczane Yıldız Zinciri.

---

## Mockup ekran listesi (seçicide)
T1 a / b / c / d / e + geçmiş · T2 yeni + düzenle · T3 liste + boş + AR · T4 + onay bekliyor · T5 · T6.

## Mockup kapsamı dışında (bilgi için)
- **Fiyat listesi yönetimi** (MOD-0156), **sipariş** (MOD-0168, W6 ön siparişin devamı), **fatura / ekstre / tahsilat** (MOD-0169 / MOD-0120), **genel muhasebe** (MOD-0118): finans programının ayrı mockup'ları.
- Temsilcinin ön sipariş ekranı: W6 mockup'ında (05-eczane-on-siparis).
