# MOCKUP BRIEF — MOD-0182 Barkod / RFID ve MOD-0365 Cihaz / Uç Entegrasyonu

> Tarih: 2026-11-10 · Hazırlayan: MVP-5 CT. **Paket yok** — bu özet paketten önce yazıldı (sahip kuralı: her yeni modülde önce mockup
> özeti). Kaynaklar: Blueprint 8.8 (MOD-0182: "Scan Event Monitor; Compliance Trace; Device Health", SCAN-BUNDLE — tarama olayı şeması,
> cihaz kimliği, **çevrimdışı tampon / yeniden oynatma** sözleşmesi; bağımlılık MOD-0178 + MOD-0365. MOD-0365: dış sağlayıcı, "External
> System Profile; Integration/Contract Profile", alan **platform-shared-services**), MOD-0178 ve MOD-0181 paketlerindeki opak `scanRef`
> kapısı (FU-0178-SERIAL-01, FU-0181-BARCODE), MOD-0290-FU07 (ürün barkodları / GTIN — MDM, onay bekliyor). Kapsam kararı (Beste):
> **el terminali (okuyucu) + telefon kamerası**.
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Mevcut ürün düzenine ve depo ekranlarına uy. Kurallar henüz pakette yazılı
> değildir; mockup bu kuralları **önerir**, paket kesinleştirir. Emin olmadığın her yeri "açık soru" olarak yaz.

## BAĞLAM
- **Kullanıcılar:** depo çalışanı (yerleştirir, toplar, sayar — okutarak), vardiya sorumlusu (okutma hatalarını izler), kalite / denetçi
  (izlenebilirlik kanıtı ister), BT / cihaz sorumlusu (cihazları kaydeder, sağlığını izler).
- **Ne:** depo işlemlerinde elle yazmak yerine **okutmak**. İlaçta karekod (GS1 DataMatrix) tek okutmada ürün (GTIN), lot, son kullanma
  tarihi ve seri numarasını verir. Okutma; ürünü, lotu, rafı doğrular ve **tarama olayı** olarak kaydedilir; ilgili görevin onayına
  `scanRef` ile bağlanır.
- **İki modül:**
  - **MOD-0182 (WMS):** okutma ekranları, okutmanın görevle eşleşmesi, tarama olayı izi, uyum izi, cihaz sağlığı görünümü.
  - **MOD-0365 (dış sağlayıcı, platform-shared-services):** el terminali / RFID ara yazılımının sisteme bağlanma profili (kimlik, sözleşme).
    Kendi iş ekranı yoktur; yalnız sağlayıcı profili ve bağlantı bilgisi.
- **Ne değil:** RFID kapı okuyucuları ilk sürümde yok (öneri — açık soru). Etiket basımı ilk sürümde yok (açık soru).

## 1) OKUTMA DENEYİMİ (mevcut görev ekranlarına eklenir — **375 px el terminali / telefon öncelikli**)
- Yerleştirme, toplama ve sayım formlarında üstte **okutma alanı**: el terminali okutunca alan otomatik dolar; telefonda **"Kamera ile
  okut"** düğmesi tam ekran kamera açar (çerçeve, flaş düğmesi, iptal).
- Okutma sonrası **anında geri bildirim** (renk + ikon + metin + titreşim / ses — yalnız renge dayanmaz):
  - ✅ Doğru raf / ürün / lot → satır işaretlenir, miktar +1 (ya da okunan miktar).
  - ⚠️ Doğru ürün, farklı lot → "Bu lot görevde değil" + seçenekler (yetkiye göre).
  - ❌ Yanlış ürün / yanlış raf / tanınmayan barkod / geri çağrılmış lot / SKT geçmiş → engel + neden.
- Karekoddan okunan alanlar (GTIN, lot, SKT, seri) satırda küçük etiketlerle gösterilir.
- **Seri numaralı ürün:** her okutma bir seri ekler; listede okunan seriler, aynı seri iki kez okutulursa uyarı (kısmi yerleştirme
  FU-0178-SERIAL-01).
- Elle giriş her zaman mümkün (okuyucu bozuksa) ama "elle girildi" olarak işaretlenir (öneri).
- **Bağlantı yoksa:** okutmalar cihazda **tamponda** tutulur, üstte "Çevrimdışı — 12 okutma bekliyor" bandı; bağlantı gelince sırayla
  gönderilir, reddedilenler listelenir (SCAN-BUNDLE yeniden oynatma). (v1'de var mı — açık soru S-1.)

## 2) TARAMA OLAYI İZLEYİCİSİ — `/Warehouse/ScanEvents` (menüde)
- **Satır:** zaman, kullanıcı, cihaz, depo, işlem türü (Yerleştirme / Toplama / Sayım / Sorgu), okunan değer (karekod çözümlenmiş:
  GTIN, lot, SKT, seri), sonuç (Kabul / Uyarı / Ret + neden), bağlı görev (bağlantı), çevrimdışı mı (rozet).
- **Süzgeçler:** depo, tarih (en çok 31 gün), kullanıcı, cihaz, sonuç, işlem türü.
- **Üst kartlar:** bugünkü okutma · ret oranı % · en sık ret nedeni · çevrimdışı gönderilmeyi bekleyen.

## 3) UYUM İZİ — `/Warehouse/ScanTrace` (menüde)
- Lot ya da seri numarası ile ara → o lot / serinin depodaki yolculuğu: kabul → yerleştirme → toplama → paketleme → sevk; her adımda
  kim, ne zaman, hangi cihaz, okutuldu mu / elle mi.
- "Kanıt paketi indir" (PDF / CSV) — denetim ve geri çağırma için (dosya isteyenin dilinde).

## 4) CİHAZ SAĞLIĞI — `/Warehouse/Devices` (menüde)
- **Liste:** cihaz adı / kimliği, tür (El terminali / Telefon-kamera / RFID okuyucu), depo, son görülme, son kullanıcı, yazılım sürümü,
  pil (sağlayıcı bildiriyorsa), durum (Etkin / Sessiz / Kayıp-engelli).
- **Eylemler:** Kaydet (yeni cihaz), Engelle (gerekçe; kayıp / çalıntı), Etkinleştir. **"Sil" yok.**
- Telefon kameralarının ayrıca kaydedilip kaydedilmeyeceği açık soru (S-3).

## 5) MOD-0365 SAĞLAYICI PROFİLİ — Platform'un dış sistem kayıt ekranında (yalnız kart)
- Sağlayıcı adı, bağlantı türü, sözleşme kimliği / sürümü, sağlık (son olay), sorumlu. Sır gösterilmez. Bu ekran platform-shared-services
  alanındadır; mockup'ta yalnız örnek kart olarak çiz (XMC-001).

## 6) DURUMLAR (her ekranda çiz)
- Boş; yükleniyor; hata ("Stok servisine ulaşılamadı"); kamera izni verilmedi ("Kamera izni gerekli — tarayıcı ayarlarından açın" +
  elle giriş yolu).
- Yetkisiz kullanıcı (UAS-001): sol menü ve üst çubuk kalır; içerikte **tek bir açıklama**. İskelet, yönlendirme, yalnız toast yok.
- Ekranlar izne göre çizilir (izin namespace'i MOD-0182'nin kendisinin, ör. `scan.*` — Q-08 kuralı).
- Engellenmiş cihazdan gelen okutma reddedilir ve nedeni gösterilir.

## 7) GENEL KURALLAR
- 7 dil; Arapça sağdan sola. Her sayfada "Yardım" paneli; tur yok.
- Okutma ekranlarında dokunma hedefleri en az 48 × 48 px; onay düğmesi altta sabit; tek elle kullanım. Diğer ekranlar masaüstü, 375 px'te
  okunabilir; yatay sayfa kaydırması yok.
- Geri bildirim yalnız renge dayanmaz (ikon + metin + ses / titreşim).

## 8) İSTENEN ÇIKTI
- Tek dosyalık tıklanabilir HTML prototip (örnek veriyle) + ekran metinleri listesi.
- Ekranlar: Okutma deneyimi (toplama ve sayım formu üzerinde; el terminali ve kamera; başarılı / uyarı / ret / çevrimdışı), Tarama olayı
  izleyicisi, Uyum izi, Cihaz sağlığı, MOD-0365 profil kartı, durum örnekleri.
- Açık sorular listesi. Bilinen açık sorular:
  - **S-1** Çevrimdışı okutma (tampon + yeniden oynatma) v1'de var mı? (Blueprint SCAN-BUNDLE içeriyor)
  - **S-2** RFID ve etiket basımı v1'de var mı? (öneri: yok)
  - **S-3** Telefon kameraları cihaz olarak kaydedilir mi, yoksa yalnız kullanıcıyla mı izlenir?
  - **S-4** Barkoddan ürün bulma **MOD-0290-FU07 (GTIN, MDM)** ana dala girmeden yapılamaz — bağımlılık; o paket onay bekliyor.
  - **S-5** MOD-0365 sahibi kim? (platform-shared-services; sahip TBD — plan W-8)
  - **S-6** Türkiye İTS (İlaç Takip Sistemi) bildirimi bu modülün işi mi, ayrı modül mü? (öneri: ayrı; burada yalnız okunan seriler)
