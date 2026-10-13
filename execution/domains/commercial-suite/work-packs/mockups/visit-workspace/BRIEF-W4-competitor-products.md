# MOCKUP BRIEF — W4 rakip ürün yönetimi (MDM): liste, kayıt, ülke fiyat / geri ödeme, temsilci önerileri, birleştirme

> **Tarih:** 2026-10-13 · **Hazırlayan:** CT · **İsteyen:** ürün sahibi (W4-BE-b ve rakip ürün ekranı öncesi).
>
> **Dayanaklar:**
> - [Yol haritası](../../ROADMAP-visit-workspace.md):
>   - K-W6 kararı (b): MOD-0290 içinde ayrı **"Rakip ürün"** kaydı; ekibimiz ekler, veriyi pazarlama / tıbbi girer, temsilci seçer.
>   - K-W7: pazarlama doktor kimliği olmadan toplu görür.
> - Temsilcinin raporda gördüğü taraf: [03-rapor.html](decoded/03-rapor.html), "Pazar ve rakip" bölümü (rakip seçimi, rakip kartı, "Listede yok, rakip ekle"). Demo verisi [00-demo-veri.js](decoded/00-demo-veri.js) `COMP` / `COMPD`.
> - Mevcut MDM görünümü:
>   - **MDM › Ürünler** sayfası: liste + ayrı oluştur / düzenle / ayrıntı sayfaları, ayrıntıda bölüm kartları;
>   - **Global Ürünler**: liste + sağ çekmece.
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: sol menü, üst çubuk, kart yapısı, açık / koyu tema. Liste düzeni MDM Ürünler ile aynı (süzgeç kartı + tablo + sağ üstte "Yeni").
> - Bu ekranlar **pazarlama / tıbbi ekip** içindir. **Masaüstü öncelikli** çiz; tablet genişliğinde bozulmasın, telefon gerekmez. Temsilci bu sayfayı görmez.
> - **Dil:** Türkçe çiz. Arapçada sağdan sola düzen bozulmamalı.
> - **Gerçekçi demo verisi:**
>   - Bizim ürünlerimiz: TUTUKON, ALMIBA, GLUKOFIT, ÜROFLEKS.
>   - Rakipler `COMPD`'den: Koronex 100 (Medira İlaç, trimetazidin, 35 mg MR tablet), Anjiyosed (Novaton Pharma, ivabradin, 5 mg tablet), Tensilox (Bilgen İlaç, amlodipin + valsartan, 5/160 mg tablet), Amlopres, Valsen Plus, Diaform, Prostam, Tamsulin.
>   - Ülkeler: Türkiye (TRY), BAE (AED), Azerbaycan (AZN).
>   - Temsilci önerileri: "Tamsulin" (zaten var → birleştirme örneği), "Prostavit" (yeni), "koronex" (yazım farkı).

---

## Kavram ve kurallar (ekranın anlatması gerekenler)

1. **Rakip ürün bizim ürün kataloğumuzdan ayrıdır.** Satılmaz, siparişe girmez; yalnız raporda ve pazar analizinde kullanılır.
2. **Kayıt durumları:**
   - **Önerilen:** temsilci rapordan ekledi; eksik bilgili, doğrulanmamış.
   - **Aktif:** doğrulandı, raporlarda seçilebilir.
   - **Pasif:** artık seçilmez, geçmiş raporlarda görünür.
   - **Birleştirildi:** başka bir kayda katıldı; yalnız iz olarak durur.
3. **Temsilci önerisi hemen kullanılabilir.** Öneren temsilcinin raporlarında görünür; doğrulanana kadar diğer temsilcilere önerilmez. Pazarlama / tıbbi ekip öneriyi:
   - **onaylar ve tamamlar** (etken madde, form, ülke fiyatı, geri ödeme), ya da
   - **var olan bir kayıtla birleştirir**, ya da
   - **reddeder** (gerekçeli; raporlarda "doğrulanmamış rakip" olarak düz metin kalır).
4. **Birleştirme geçmişi kaybetmez.** Eski raporlardaki değer hayatta kalan kayda bağlanır. Önerilen ad, hayatta kalan kaydın **"diğer adları"** listesine girer; temsilci ileride "Tamsulin" yazınca doğru kayıt bulunur. Geri alınamaz, onay penceresi bunu söyler.
5. **Pazar payı burada girilmez.** Zamanla değişen pazar verisidir (W7). Ayrıntıda yalnız "Pazar verisi · yakında" yer tutucusu olabilir.
6. **Görünürlük:** "Raporlarda kullanım" bölümü toplu sayı gösterir (kaç doktor / rapor, hangi ürünümüzle). Pazarlama doktor kimliği görmez (K-W7).

---

## R. Rakip ürünler (yeni sayfa · MDM menüsünde, Global Ürünler / GSKU yanında)

### R1. Liste — iki sekme: "Rakip ürünler" · "Temsilci önerileri (N)"
**Rakip ürünler sekmesi:**
- **Süzgeçler:** bizim ürünümüz (hangi ürünün rakibi), firma, etken madde, ülke, geri ödeme durumu, durum (Aktif / Pasif), metin arama (ad + diğer adlar).
- **Tablo sütunları:**
  - rakip ürün adı (+ diğer adlar küçük, "ayrıca: Tamsulosin Teva");
  - firma;
  - etken madde;
  - form / doz;
  - rakibi olduğu ürünlerimiz (çipler);
  - ülkeler (bayrak / kod çipleri; fiyatı olan ülke sayısı);
  - raporlarda kullanım (sayı);
  - durum;
  - son değişiklik (kişi, tarih);
  - eylemler.
- Sağ üstte **"Yeni rakip ürün"**.
- **Boş durum:** "Henüz rakip ürün yok" + açıklama + "Yeni" düğmesi.

**Temsilci önerileri sekmesi** (rozet = bekleyen öneri sayısı):
- **Satır:** önerilen ad, yazılan firma, öneren temsilci + tarih, hangi ürünümüzün raporunda, kaç raporda kullanıldı.
- **"Benzer kayıtlar"** ipucu: ad benzerliği, örn. "Tamsulin ↔ Tamsulin (aktif, %100)", "koronex ↔ Koronex 100".
- **Eylemler:** "Onayla ve tamamla" · "Var olan kayıtla birleştir" · "Reddet".
- **Boş durum:** "Bekleyen öneri yok".

### R2. Oluştur / düzenle (ayrı sayfa, bölüm kartları)
**Kimlik:**

| alan | tür | kural |
|---|---|---|
| Rakip ürün adı | metin | zorunlu, ≤ 200; aynı firma + ad tekrarlanamaz |
| Diğer adlar | etiket listesi | yazım farkları, eski adlar (birleştirmeden gelenler burada) |
| Firma | metin + öneri listesi | zorunlu |
| Etken madde(ler) | çoklu etiket | zorunlu; kombinasyon için birden çok ("amlodipin + valsartan") |
| Form | seçim | MDM form listesi (tablet, MR tablet, kapsül…) |
| Güç / doz | metin | "5/160 mg" |
| ATC kodu | metin | isteğe bağlı |

**Rekabet:**
- Rakibi olduğu ürünlerimiz: Global Ürün çoklu seçim, zorunlu en az 1.
- Endikasyonlar: Kavram Grafiği'nden (Endikasyon kavramları) çoklu seçim, isteğe bağlı. "Raporda bu endikasyonda gösterilir" açıklaması.

**Ülke bazında fiyat ve geri ödeme (alt tablo — mockup'ın asıl odağı):**
- **Satır başına:** ülke · kutu fiyatı · para birimi · geri ödeme durumu (Geri ödemede / Geri ödemede · rapor şartlı / Geri ödeme dışı / Bilinmiyor) · geçerlilik başlangıcı · kaynak / not.
- **"Ülke ekle"**; aynı ülke iki kez eklenemez.
- **Fiyat değişikliği yeni satır değil, yeni dönemdir.** Satırda "Geçmiş (3)" bağlantısı; açılınca eski fiyatlar ve tarihleri (salt okunur).
- **Temsilcinin raporda gördüğü kart** sağda önizlenir; 03-rapor'daki rakip kartı gibi, seçili ülkeye göre. Ülke seçici: "Türkiye olarak önizle".

**Düğmeler:** "Kaydet" · "Vazgeç" (kaydedilmemiş değişiklik uyarısı).

### R3. Ayrıntı (salt okuma + düzenle)
**Bölüm kartları:**
- Kimlik;
- Rekabet;
- Ülke fiyat / geri ödeme tablosu (geçmiş açılır);
- **Raporlarda kullanım** (toplu: son 3 ay doktor sayısı, rapor sayısı, ürünümüze göre dağılım; doktor adı yok);
- **Birleştirme izi** (bu kayda katılan öneriler: ad, öneren, tarih);
- Değişiklik geçmişi (kim, ne zaman, alan, eski → yeni).

**Eylemler:** "Düzenle" · "Pasife al" (onay penceresi: "Yeni raporlarda seçilemez; geçmiş raporlar etkilenmez") · "Başka kayıtla birleştir".

### R4. Öneriyi onayla ve tamamla
- R2 formu, önerinin adı ve firması dolu açılır. Üstte bilgi şeridi: "Temsilci önerisi · öneren, tarih · N raporda kullanıldı".
- **Kaydet = onayla.** Öneri Aktif olur, raporlardaki değer bu kayda bağlanır.

### R5. Birleştir (mockup'ın ikinci odağı)
- **Adım 1 — hedef seç:** arama + "benzer kayıtlar" önerisi.
- **Adım 2 — yan yana karşılaştırma:** sol "birleşecek" (öneri ya da kayıt), sağ "hayatta kalan".
  - Alan alan fark vurgusu.
  - Ülke satırları: hayatta kalanda olmayan ülke taşınır mı? Satır başına "taşı" seçimi.
- **Adım 3 — etkisi:**
  - "N rapordaki rakip değeri 'Tamsulin' kaydına bağlanacak."
  - "'Tamsulin (öneri)' adı diğer adlar listesine eklenecek."
  - "Geri alınamaz."
- **Düğmeler:** "Birleştir" · "Vazgeç".

### R6. Reddet
Pencere: gerekçe zorunlu (≤ 500). Açıklama: "Raporlarda doğrulanmamış rakip olarak düz metin kalır; temsilciye önerilmez."

### R7. Durumlar
- Salt okuma yetkisi: düğmeler gizli.
- Yetkisiz kullanıcı: sayfa iskeleti çizilmez, yetki mesajı (UAS-001).
- Kaydetme hatası: sürüm çakışması ("Kayıt başkası tarafından değiştirildi, yenileyin").
- Arapça R1 (sağdan sola).

---

## Mockup ekran listesi (seçicide)
R1 liste · R1 boş · R1 öneriler · R2 yeni · R2 düzenle (ülke tablosu dolu, fiyat geçmişi açık) · R3 ayrıntı · R4 öneriyi onayla · R5 birleştir adım 1 / 2 / 3 · R6 reddet · salt okuma · yetkisiz · AR R1.

## Mockup kapsamı dışında (bilgi için)
- **Potansiyel eşik ayarı** (ürün başına Az / Orta / Çok): standart CRM ayar deseniyle yapılacak, mockup gerekmez.
- **Pazar payı / satış verisi:** W7.
- **Temsilcinin rapordaki rakip seçimi ve "Listede yok, rakip ekle":** 03-rapor'da çizili.

## Kararlar (ürün sahibi, 2026-10-13)
1. **Temsilci önerisi Workflow ile onaylanır; onay MDM'de çalışır.** MDM'ye Workflow bağlantısı kurulur (W3 CRM onay çekirdeği deseni).
   - Pazarlama / tıbbi kullanıcı kararı hazırlar: yeni kayıt olarak tamamla · var olan kayıtla birleştir · reddet.
   - "Onaya gönder" → onay **Satış Operasyon** (POS-SALES-OPS) pozisyonuna, Görev Merkezi'nde.
   - Görev ayrılığı: hazırlayan onaylayamaz. Onaylanınca karar uygulanır.
   - **Mockup'a ek:** R4 / R5 / R6'nın sonunda düğme "Onaya gönder" olur. Öneri satırında durum çipi "Onay bekliyor · kişi, tarih"; onaylayıcı görünümü; "Taslağa geri gönder" notlu; hazırlayan için SoD bilgi kutusu (W3 itiraz ekranlarındaki gibi).
2. **Doğrudan kayıt / düzenleme** (temsilci önerisi olmadan; yeni rakip, fiyat ve geri ödeme güncellemesi) onaya **gitmez**. "Rakip ürün yönetimi" yetkisi yeter; değişiklik geçmişiyle denetlenir.
3. **Etken madde** serbest etiket (ileride sözlük). **Geri ödeme durumu** yeni referans veri seti (kiracı uyarlar).
4. **Birleştirilen kayıt silinmez.** "Birleştirildi → hayatta kalan" işaretçisi tutar. CRM rapordaki eski değeri okurken hayatta kalan kayda yönlenir; geçmiş veri yeniden yazılmaz.
