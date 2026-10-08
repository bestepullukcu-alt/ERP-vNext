# WORK PACKAGE — WP-VP-4J · Hedefler mockup v3 + liste / yeni plan / ürün paneli / boş hafta düzeltmeleri (Web)

> **CT (SoR), 2026-10-08.** Kullanıcının main sonrası manuel testi.
> - **Kullanıcı (2026-10-08):**
>   - "Filtredeki ilk alanın ne olduğu belli değil."
>   - "Bulk action özelliği açık değil."
>   - "Yeni plan oluşturmada Temsilci disable gelmiyor."
>   - "Hedefler tabının tasarımını değiştirdim, mockup'a göre düzenleyelim; doktorlar kısmını mockup'taki liste gibi yapalım, datatable olmasa da olur; doktorlar sekmesindeki filtreyi de mockup'a uygun yapalım."
>   - "Ürün seç'te açılan panelde 'Dönem görünümü —' görünüyor."
>   - "Boş haftada 'Bu haftayı üret' deyince hiçbir şey olmuyor."
> - **Kullanıcı kararı (2026-10-08):** "İşlem" menüsü = **seçili boş taslakları arşivle**.
> - **Mockup v3:**
>   - kaynak: `mockups/visit-planning/Ziyaret Planlama v3 (standalone).html`;
>   - okunabilir hal: `visit-planning-v3.decoded.html` (CT çözdü; v2 çözümüyle birebir yöntem);
>   - bölümler (satır): 01 Liste ~151, 02 Yeni plan ~245, 04 Hedefler **394–575**, mantık ~921+;
>   - v2 → v3 farkı yalnız Hedefler görünümü + ince kaydırma çubukları.
> - **Kapsam:** yalnız Web. Backend'e dokunma.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4j`, dal `wp/vp-4j` (test dalı başından). Commit dala, push YOK.

## NE

### 1. Hedefler — mockup v3'e birebir (satır 394–575)
- **Başlık kartı:**
  - solda kare ikon (`bx-target-lock`), "HEDEFLER" ve alt satır "41. Hafta · N doktor, M eczane seçili";
  - sağda salt okunurken yeşil **"Salt okunur"** rozeti + açıklama ("41. Hafta onaylı: … 'Haftayı yeniden aç'").
  - Bugünkü ayrı sarı / yeşil bant kalkar.
- **Üç sütun**, her biri ayrı kart:
  - sol: hesaplar (4I hesap kartları aynen, kart içinde);
  - orta: iki kart (aşağıda);
  - sağ: özet + seçilenler.
- **Orta sütun, 1. kart — sekmeler:** "Doktorlar (16)" / "Bağlı eczaneler (2)"; seçili sekme dolu lacivert düğme, diğeri çerçeveli.
- **Orta sütun, 2. kart:**
  - **Kurum başlığı:** kare bina ikonu (`bx-buildings`); büyük kurum adı; altında gri hap biçimli üç bilgi: `bx-map` il · `bx-user` "16 doktor" · `bx-plus-medical` "2 bağlı eczane".
  - **Hızlı filtreler (mockup biçimi):** hap biçimli üç düğme, sayılı:
    - "Bu hafta görülmesi gerekenler (8)": varsayılan, seçiliyken dolu lacivert;
    - "Hiç görülmeyenler (5)";
    - "Tümü (16)".
  - **Arama ve uzmanlık satırı:** "Doktor ara.." + "Uzmanlık: Tümü" açılır seçici. Seçici, sayılı onay kutusu listesi + "Seçimi temizle" (mockup ~445–452); seçimde etiket "Uzmanlık: 2 seçili".
  - **Eylem satırı:** "Tümünü seç (8)" (filtreye uyan seçilebilirler) ve "Ürün uygula (13)" (seçili doktorlar).
  - **Doktor listesi: DataTable YOK.** Mockup'taki düz tablo (`<table>`):
    - sütunlar seçim · Doktor · Uzmanlık · Ürünler · Sıklık · Yapılan / kalan · Son ziyaret · Durum (4H / 4I sütun içeriği aynen);
    - satır ince çizgili, kurumun bütün aktif doktorları;
    - kart içinde dikey kaydırma, sayfalama yok;
    - dar ekranda tablo yatay kayar.
    - Seçim, onay kutusu, ürün çipleri / kalem, "Ürün seç", izin engelli, pasif davranışları **aynen** kalır (4C / 4H).
  - Lejant tablonun altında (mockup).
  - Bağlı eczaneler sekmesi aynı kartta, aynı düz tablo biçimiyle (mockup ~510–525).
- **Sağ sütun:**
  - "Seçim özeti" kartı mockup gibi açık lavanta zeminli: sayılar, süre çubuğu, ürün dağılımı, ürünsüz uyarısı;
  - "Seçilenler" kartı: katlanır kurum grupları, 4I "Kaldır" düğmeleri aynen.
- **İnce kaydırma çubukları:** mockup `scrollbar-width:thin; #c5c9e8`, yalnız Visit Planning sayfalarında (sayfa CSS'i; global değil).
- Bugünkü DataTable'a bağlı kod (`dt-vp-contacts` init, responsive / colvis vb.) kaldırılır. Seçimi okuyan / yazan mantık (`selectedContacts`, kaydet) değişmez.

### 2. Ürün seçici paneli — "Dönem görünümü —"
- **Kök neden:** ürün seçici, doktor paneliyle aynı offcanvas'ı kullanıyor. Hedefler'den açılınca başlık yazılıyor ama "Dönem görünümü" sekmesi hiç çizilmiyor; boş ya da önceki doktordan kalma görünüyor.
- **Tek doktor (single):** panel açılırken o doktor için doktor paneli verisi de çizilir (`doctor-panel:open` ile aynı çizim, sekme Ürünler'de kalır). Doktor henüz planda değilse (önizlemede yok) "Dönem görünümü"nde açıklayıcı boş durum: "Bu doktor henüz plana kaydedilmedi. Hedefleri kaydedince dönem görünümü dolar." "—" görünmez.
- **Toplu (bulk):** "Dönem görünümü" sekmesi **gizlenir** (toplu seçimde anlamsız); yalnız Ürünler. Panel kapanınca sekmeler eski haline döner.

### 3. Boş hafta — "Bu haftayı üret"
- **Kök neden:**
  - Düğme yalnız planı yeniden hesaplıyor (`reload-plan`).
  - Hafta, sıklık kuralı o haftaya ziyaret düşürmediği için boş: sıklığı tanımsız doktor dönemde 1 sayılıyor ve ilk haftalara yerleşiyor.
  - Yeniden hesaplama aynı sonucu veriyor ve hiçbir geri bildirim yok.
- **Düzeltme:**
  - Düğmeye basınca yükleniyor durumu (spinner, düğme kapalı) → hesap biter.
  - Hafta artık doluysa normal görünüm.
  - Hâlâ boşsa bilgi mesajı (toast) + boş durum metni: "Bu haftaya sıklık kurallarına göre ziyaret düşmüyor: seçili hedeflerin dönem ziyaretleri önceki haftalara yerleşti. Hedef ekleyebilir ya da doktorlara ziyaret sıklığı tanımlayabilirsiniz."
  - Altında ikincil bağlantı **"Hedefleri düzenle"** (Hedefler sekmesine geçer).
  - Mockup metni ("Önceki hafta onaylandığında otomatik oluşur…") bu motorla yanıltıcı olduğu için **bilinçli sapma** (rapora yaz).

### 4. Liste — süzgeç
- **"Durum" seçicisi:** seçenekler **sabit** (bütün durumlar: taslak · onaylı / kaydedilmiş · arşivli; mevcut durum sözlüğü ve yerel etiketler), satırlardan türetilmez.
- **Başlık:** her süzgecin üstünde ya da içinde görünür etiket / yer tutucu: "Durum", "Dönem", "Temsilci". Çoklu select2'de placeholder görünür.
- **"Temsilci" süzgeci** yalnız `crm.visit-plan.read-all` yetkisi olan kullanıcıda görünür (temsilci yalnız kendi planlarını görür, K-1). Yetki bilgisi mevcut sayfa modelinden / izin yardımcısından.

### 5. Liste — "İşlem": seçili boş taslakları arşivle (kullanıcı kararı)
- **Satır seçimi:** satırlara seçim kutusu; yalnız **boş taslak** (`isEmpty`, 0 hedef) seçilebilir, diğerlerinde kapalı + ipucu "Yalnız hedefi olmayan taslaklar arşivlenebilir".
- **"İşlem" menüsü:** "Seçili boş taslakları arşivle (N)". Seçim yokken kapalı.
- **Onay penceresi:** "N boş taslak arşivlenecek. Devam edilsin mi?"
- **Arşivleme:** mevcut tekil arşivleme yolu (oturum güncellemesi `requestedStatus = archived`; sunucu boş olmayanı `409 planning_session_not_empty` ile reddeder). Sırayla, her biri ayrı istek. **Yeni yazma ucu YOK.**
- **Sonuç:** "N arşivlendi" (ve varsa "M arşivlenemedi: …"). Liste yenilenir.
- **Mockup bandı:** "N boş taslak (0 hedef) var. Aynı dönem ve hafta için tek taslak tutulur." + **"Boş taslakları arşivle"** düğmesi; aynı akış, bütün boş taslaklar. Mockup "sil" diyor; biz **arşivle** (silme yok).

### 6. Yeni plan çekmecesi
- **Temsilci:** mockup ~285–287 gibi salt okunur ve **devre dışı görünümlü**: gri zemin, sağda kilit simgesi, odaklanmaz. Alt not "Oturum açan kullanıcı." (mevcut metin korunabilir).
- **Ülke:** dil etiketiyle ("Türkiye", "Turkey" değil). Kaynak ülke sözlüğü (`/api/lookups/countries` ya da sayfanın mevcut ülke okuyucusu, UI dili), yoksa kod.
- Mockup alt ipucu satırı: `bx-bulb` "Kaydettikten sonra Hedefler sekmesinde bu hafta görülmesi gereken doktorlar önerilir."

## KORU / YAPMA
- Backend'e dokunma; yeni yazma ucu yok (arşivleme mevcut yol).
- 4C / 4D / 4E / 4F / 4G / 4H / 4I davranışları aynen:
  - seçim, ürün seçici, toplu uygula;
  - kaydet;
  - bölge dışı ekle;
  - hesap kartları;
  - RTL `bidi` / `ratio` / `isolate`;
  - biçimleyici;
  - güne taşıma;
  - boş hafta boş durumu (yalnız metin + geri bildirim değişir).
- Haftalar / Rota tasarımı değişmez. UAS-001.
- 7 dil + TR diakritik. Argümansız Localizer değerinde `{0}` yok (CT koruma testi).
- Kullanıcının oturum açık sekmesine harness / mock enjekte etme. Canlı veriye yazma yok (arşivleme testi yalnız birim / sahte istemciyle).

## Acceptance
- Web testleri (üretim kodu üzerinde):
  1. Hedefler: başlık kartı + "Salt okunur" rozeti; sekmeler kartı; kurum kartı (ikon + 3 hap); hızlı filtre hapları sayılı; uzmanlık çoklu seçici; düz tablo (DataTable init yok, `dt-vp-contacts` DataTable çağrısı yok), 8 sütun, lejant.
  2. Seçim / kaydet yolu aynı (mevcut 4C testleri yeşil).
  3. Ürün seçici: tek doktorda dönem görünümü çizilir ya da açıklayıcı boş durum (asla "—"); toplu modda sekme gizli.
  4. Boş hafta: üret → yükleniyor durumu → hâlâ boşsa bilgi mesajı + "Hedefleri düzenle".
  5. Liste süzgeci: Durum seçenekleri sabit, yer tutucu görünür; Temsilci yalnız read-all.
  6. Toplu arşiv: yalnız boş taslak seçilebilir; menü sayılı; onay; tekil arşivleme isteği her seçili için; 409 sonucu mesajda.
  7. Yeni plan: Temsilci salt okunur + devre dışı görünüm + kilit; Ülke dil etiketiyle.
  8. Yeni metinler 7 dilde.
- **Sabotajlar (kırmızı kanıtla, geri al):**
  - doktor listesine DataTable init geri koy → test 1 kırmızı;
  - toplu modda dönem sekmesini göster → test 3 kırmızı;
  - boş olmayan taslağı seçilebilir yap → test 6 kırmızı.
- **E4 (CT):** mockup v3 ile yan yana Hedefler; ürün seçici; boş hafta; liste süzgeci + toplu arşiv (kullanıcı onayıyla); yeni plan.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4J · Hedefler mockup v3 + liste / yeni plan / ürün paneli / boş hafta düzeltmeleri (Web)
Repository: C:\tmp\vp-4j (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4j · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4J-targets-v3-list-fixes.md — önce oku (madde 1–6). Mockup v3: execution/domains/commercial-suite/work-packs/mockups/visit-planning/visit-planning-v3.decoded.html (Hedefler 394–575, Liste ~151–240, Yeni plan ~245–300; stiller satır içinde) + standalone HTML. Bağlam: WP-VP-4C/4H/4I §37'leri. Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/** · Resources/Views/CRM/VisitPlanning/**.
NE: (1) Hedefler mockup v3'e birebir: başlık kartı + "Salt okunur" rozeti, sekmeler kartı, kurum kartı (ikon + il/doktor/eczane hapları), sayılı hap hızlı filtreler, "Doktor ara" + "Uzmanlık" çoklu seçici, "Tümünü seç (N)" / "Ürün uygula (N)", doktor listesi DÜZ TABLO (DataTable yok, sayfalama yok, kart içi kaydırma), lejant, lavanta özet kartı, Seçilenler; ince kaydırma çubukları yalnız VP sayfalarında; seçim/kaydet/ürün mantığı aynen. (2) Ürün seçici: tek doktorda dönem görünümünü çiz ya da açıklayıcı boş durum (asla "—"); toplu modda "Dönem görünümü" sekmesi gizli. (3) Boş hafta "Bu haftayı üret": yükleniyor durumu, sonra hâlâ boşsa bilgi mesajı + açıklayıcı metin (sıklık kuralı) + "Hedefleri düzenle"; mockup metninden bilinçli sapma. (4) Liste süzgeci: Durum seçenekleri sabit + görünür yer tutucular; Temsilci süzgeci yalnız read-all. (5) "İşlem" = seçili BOŞ taslakları arşivle: satır seçimi yalnız isEmpty, onay penceresi, mevcut tekil arşivleme yolu (requestedStatus=archived) her seçili için, 409 sonucu mesajda; mockup bandı "N boş taslak … Boş taslakları arşivle" (sil değil). (6) Yeni plan: Temsilci salt okunur + gri + kilit; Ülke dil etiketiyle; mockup ipucu satırı.
KORU/YAPMA: backend'e dokunma; yeni yazma ucu yok; 4C–4I davranışları aynen (RTL bidi/ratio/isolate dahil); Haftalar/Rota tasarımı aynı; UAS-001; 7 dil + TR diakritik; argümansız Localizer değerinde {0} yok; kullanıcının oturum açık sekmesine harness/mock enjekte etme; canlı veriye yazma yok.
DOĞRULA (E2): Web (802/0 tabanı) · CRM 2457/0/5 (dokunulmaz) · mimari 27; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad> (REPO İÇİNDE). Testler belge Acceptance 1–8; sabotaj 3 (kırmızı kanıtla, geri al — dosyada commit'lenmemiş başka iş varsa `git checkout --` ile geri alma).
Commit: "feat(web): WP-VP-4J — targets tab per mockup v3, product panel period view, empty-week feedback, list filters + archive empty drafts, new-plan rep field" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, mockup'tan bilinçli sapmalar, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `8f4c48147` (ajan `bc9875d8e`, test dalına cherry-pick). Push: test dalı.

**CT K13:**
- Web 802 → **810/0** (+8). Worktree'de ve birleşik koşuda aynı (fleet derleme kilidi sonrası yeniden denendi).
- CRM 2457/0/5 (dokunulmadı).
- Mimari: listesiz 27.

**Kod okuması:**
- **Toplu arşiv:** `archiveDrafts` her seçili için sırayla mevcut `PUT sessions/{id}` `{ requestedStatus: 'archived', expectedVersion }`; 409 `planning_session_not_empty` mesaja yansıyor; yeni yazma ucu yok.
- **Satır seçimi:** yalnız boş taslakta (`isEmptyDraft`).
- **`CanReadAll`:** Web denetleyicisinde `crm.visit-plan.read-all` → Temsilci süzgeci yalnız o zaman.
- **Hedefler:** düz tablo (DataTable yok); `visit-planning.css` yalnız VP sayfalarında.

**CT sabotajı:** Temsilci süzgeci koşulu `@if (true)` → 1 kırmızı (`The_list_filter_has_fixed_statuses_visible_labels_and_a_rep_filter_for_read_all_only`). Dosya yedekten geri yüklendi.

**Bilinçli sapmalar (ajan raporu, CT kabul):**
- boş hafta metni (motorla doğru);
- eczane tablosunda uydurma sıklık yok;
- kurum adı kendi kartında;
- "Hedefleri kaydet" başlık kartında;
- renkler CSS değişkeniyle;
- ülke adı tarayıcının `Intl` bölge adlarından;
- arşivliler yalnız durum süzgeciyle.

**E4:** kullanıcı + CT:
- mockup v3 yan yana;
- ürün paneli tek / toplu;
- boş hafta;
- süzgeç;
- toplu arşiv (kullanıcı onayıyla);
- yeni plan.
