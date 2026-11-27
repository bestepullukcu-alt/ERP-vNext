# MOCKUP BRIEF — Entegrasyon Sırları (Kiracı Sır Kasası yönetim ekranı)

> Tarih: 2026-11-19 · Hazırlayan: CT (MVP-1 / merkez). Modül: **MOD-0012-FU01 Tenant Runtime Secret Vault** (Platform, 5057).
> **Durum:** sahip (Beste) 2026-11-19'da ekranı istedi; 2026-11-13 Q-7 kararı ("Platform'da yönetim / iptal ekranı yok") bu istekle
> değişecek — **paket revizyonu (yeni dilim) + bağımsız inceleme + onay olmadan kod yazılmaz.** Bu brief yalnız mockup içindir.
> **Mockup yapana not:** Görsel dil senin kararın; mevcut ürün düzenine (sol menü, üst çubuk, kart yapısı, açık / koyu tema, `_LayoutTenantShell`)
> uy. Aşağıdakiler amaç, içerik ve **değişmez güvenlik kurallarıdır**. Mockup paket kurallarını geçersiz kılmaz.

## BAĞLAM
- **Ne:** Kiracıya özel gizli bilgiler (3PL deposunun API anahtarı, kargo / taşıyıcı şifresi, bankanın bağlantı sertifikası…) Platform'daki
  sır kasasında **şifreli** tutulur. Sırrı her zaman **sahibi olan modül** yazar ve okur (örn. 3PL entegrasyonu MOD-0179, ileride kargo /
  taşıyıcı entegrasyonları, Treasury banka gönderimi BANK-DISPATCH). Modül sırrın değerini değil, yalnız opak bir referansını saklar.
- **Kural (değişmez):** her sır kendi sahip modülüyle ayrılır; **bir modül başka modülün sırrını okuyamaz**. Değer yalnız sahip modülün servis
  kimliğiyle, çalışma anında okunur.
- **Bugün:** kasanın ekranı yok; sırlar yalnız sahip modülün kendi ekranından yazılır. Kiracı yöneticisi hangi sırların var olduğunu,
  ne zaman değiştiğini, hangisinin kullanılmadığını ya da süresinin dolduğunu **tek yerde göremiyor**; bir olayda (anahtar sızdı) hepsini
  hızla iptal edemiyor.
- **Kullanıcılar:** kiracı yöneticisi / entegrasyon sorumlusu (kendi kiracısı); güvenlik / denetim sorumlusu (salt okur). Platform
  yöneticisi bu ekranı **görmez** (kiracı verisi; kiracılar arası görünüm yok).

## 1) ENTEGRASYON SIRLARI — LİSTE
- **Sütunlar:** sahip modül (ad + kod, ör. "3PL entegrasyonu"), şirket (varsa), kapsam / bağlantı adı (ör. "Depo X API"), sır adı (ör. "api-key"),
  **sürüm** (v3), durum (**Etkin / İptal edildi / Süresi doldu**), son değiştirme (kim / ne zaman), **son kullanım** (en son okunma zamanı;
  "hiç kullanılmadı" ayrıca), son kullanma tarihi (varsa; yaklaşanlar uyarı rengi), oluşturan.
- **Asla gösterilmez:** sırrın değeri, değerin bir kısmı (ilk / son karakterler), uzunluğu, maskelenmiş "••••" bile **değerden türetilmez**
  (sabit bir simge kullanılabilir). Kopyala / göster düğmesi **yoktur**.
- **Filtreler:** sahip modül, şirket, durum, "son 90 günde kullanılmayanlar", "30 gün içinde süresi dolacaklar", arama (bağlantı / sır adı).
- **Üst kartlar (KPI):** etkin sır sayısı, 30 gün içinde süresi dolacak, 90 gündür kullanılmayan, son 7 günde iptal edilen.
- Sunucu taraflı tablo (sayfalama, sıralama, CSV dışa aktarım — **CSV'de de değer yok**, yalnız meta).

## 2) AYRINTI (yan panel)
- Meta: sahip modül, şirket, kapsam, ad, sürüm, durum, oluşturma / son değiştirme (kim / ne zaman), son kullanım, son kullanma tarihi.
- **Sürüm geçmişi** (zaman çizelgesi): v1 oluşturuldu → v2 değiştirildi → … → iptal; her satırda kim / ne zaman / gerekçe (değer yok).
- **Kullanıldığı yer:** sahip modülün kaydına bağlantı (ör. "3PL bağlantısı: Depo X" → o modülün ekranına gider). Sırrı **değiştirme**
  işlemi bu bağlantıdan, sahip modülün kendi ekranında yapılır (doğru biçim / doğrulama orada).
- **Eylemler:**
  - **İptal et** (tehlikeli): gerekçe zorunlu (10–500), onay iletişim kutusunda "Bu entegrasyon çalışmayı durduracak" uyarısı ve bağlı kaydın adı.
    İptal geri alınamaz; yeni değer ancak sahip modülün ekranından yeni sürüm olarak girilir.
  - **Sahip modülde aç** (bağlantı).
- **Toplu iptal** (olay müdahalesi): listede çoklu seçim + "Seçilenleri iptal et" (gerekçe zorunlu, adet ve etkilenen modüller özetiyle onay).

## 3) DURUMLAR
- Yükleniyor (iskelet), boş ("Bu kiracıda henüz entegrasyon sırrı yok — sırlar entegrasyon ekranlarından eklenir"), hata (503 — kasaya
  ulaşılamıyor; tekrar dene), yetkisiz (UAS-001: iskelet çizilmez, yalnız erişim açıklaması), çakışma (409 — kayıt bu arada değişti; yeniden yükle).
- İptal edilmiş sır satırı soluk; süresi dolmuş satırda açık uyarı ve "sahip modülde yenile" bağlantısı.

## 4) GENEL KURALLAR
- **İzinler (öneri, paket revizyonunda kesinleşir):** okuma (meta) ve iptal ayrı anahtarlar; değişiklik / yazma bu ekranda yok. İzinsiz kullanıcıya
  sayfa iskeleti çizilmez (UAS-001).
- **Kiracı izolasyonu:** yalnız kendi kiracısının sırları; başka kiracının sırrı ya da bilinmeyen referans aynı "bulunamadı" (404) — varlığı sızmaz.
- **Denetim:** her görüntüleme listesi değil ama her **iptal** ve toplu iptal merkezi denetim kaydına yazılır (kim, ne zaman, gerekçe, hangi sırlar).
- **Yalnız Gateway (5000) üzerinden**; tarayıcı kasaya doğrudan gitmez; değer hiçbir cevapta yok (meta uçları).
- **7 dil** (en, tr, fr, es, zh, ar, ru); **ar sağdan sola**; tarih / saat kullanıcının saat diliminde; 375 px mobilde kart görünümü.
- Eylem metinleri Title Case ("İptal Et", "Sahip Modülde Aç").

## 5) NETLEŞTİRİLECEK (paket revizyonunda; mockup varsayılanı köşeli parantezde)
1. Ekran kiracı yöneticisinde mi, ayrıca "güvenlik / denetim" salt okur rolü olacak mı? [ikisi: yönetici + salt okur]
2. Son kullanma tarihi kasada mı tutulacak (bugün yok), yoksa yalnız sahip modül mü bilir? [kasada isteğe bağlı alan]
3. "Son kullanım" zamanı kasada kaydediliyor mu (resolve sayacı var; zaman damgası?) — yoksa eklenir. [eklenir]
4. Toplu iptal v1'de mi? [evet — olay müdahalesi için]
5. Platform yöneticisine yalnız sayılar (kiracı başına adet, KEK durumu) gösteren ayrı bir sağlık kartı gerekli mi? [hayır; ayrı karar]
