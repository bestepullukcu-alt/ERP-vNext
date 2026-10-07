# WORK PACKAGE — WP-VP-4C · Ziyaret Planlama ekranı (2/3): Hedefler sekmesi (doktor durumu, hızlı filtreler, ürün sütunu ve seçici, toplu uygula, seçim özeti)

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** mockup v2 ekran 04 Hedefler + doktor panelinin "Ürünler" sekmesi · [brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) §4 · [K-7 eki](mockups/visit-planning/BRIEF-ADDENDUM-K7-visit-products.md) · [v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md) (S-1…S-4 onaylı) · 3C / 3D uçları.
> - **Kullanıcı:** "Faz 4 … paketlemeye başla" (2026-10-07).
> - **Kapsam:** yalnız Web, Hedefler sekmesi (`targets.js`, 4B iskeletine bağlı).
> - **Ön koşul:** **4A ve 4B birleşmiş olmalı.**
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4c` (4B kabulünden sonra açılır), dal `wp/vp-4c`. Commit bu dala, push YOK.

## Kullanılacak uçlar (hazır)
- Kurumlar: `GET /CRM/VisitPlanning/api/my-accounts` (bölge kapsamı, `territoryStatus`, `activeContactCount`, `hasActiveContacts`, Türkçe arama).
- Doktorlar: `GET /CRM/VisitPlanning/api/my-accounts/{accountId}/doctors?planningSessionId=&quick=&search=&specialty=` (3D: required / done / remaining / lastVisitDate / dueThisWeek / segmentBadges / consent / inactive / outOfTerritory).
- Plan hedefleri: `GET …/sessions/{id}/targets` (3D + 4A `products`).
- Bağlı eczaneler: `GET …/accounts/related?accountIds=`.
- Yazma: mevcut oturum güncellemesi (`PUT …/sessions/{id}`; D9 `MergeSelection`). Doktorun `products` alanı: null = koru, [] = temizle (3C). **Yeni yazma uç YOK.**
- Ürün kataloğu: Knowledge'daki aramalı ürün vekili deseni (`global-product-options`), Ziyaret Planlama'ya aynı vekil (`/CRM/VisitPlanning/api/products?search=`; 100'lük sayfa, arama).
- Önizleme: süre / dağılım / sınır (3C `productDistribution`, `doctorsWithoutProducts`, `overflowProducts`, `portfolioStatus`; 3B `weekCapacity`).

## NE
### 1. Sol: kurumlar (brief §4)
- Yalnız temsilcinin bölgesindeki hesaplar (bölge yoksa K-5 sarı bant + tüm hesaplar).
- Tür yerel etiketle ("Hastane", "Klinik", "Eczane"; reference-labels).
- Satırda "x / y seçili · N bu hafta" (seçili doktor / aktif doktor sayısı; `dueThisWeek` sayısı).
- **"Bölge dışı ekle"** penceresi: arama → uyarı rozeti "bölge dışı" ile eklenir (mevcut akış korunur, görünüm mockup'a).

### 2. Orta: doktorlar
- Sütunlar: seçim, doktor, uzmanlık (yerel ad), **sıklık** ("dönemde N"; bilinmiyorsa "sıklık yok" rozeti), **yapılan / kalan**, **son ziyaret**, **Ürünler** (madde 4), durum rozetleri.
  - Durum rozetleri: izin engelli (seçilemez, ipucu), pasif, segment adı (salt okunur, K-4).
  - "BAĞLANTI" sütunu kalkar.
- **Uzmanlık filtresi:** sayılı çoklu seçim ("Gastroenteroloji (12)").
- **Hızlı filtreler:** "Bu hafta görülmesi gerekenler" (`quick=due`), "Hiç görülmeyenler" (`never`), "Tümü".
- "Tümünü seç" filtreye uyanları seçer.
- Bağlı eczaneler sekmesi (mevcut; görünüm mockup'a).

### 3. Sağ: seçim özeti
- "N doktor · M eczane · K hesap".
- **Tahmini süre:** "Bu hafta ≈ X saat (ürünlere göre) · haftalık sürenin %Y'si" (önizlemeden); aşımda uyarı.
- **Ürün dağılımı** (`productDistribution`, ör. "TUTUKON 16 · X 4") + "N seçili doktorda ürün yok" uyarısı.
- **"Seçilenler":** ad + kurum + uzmanlık, hesaba göre gruplu, tek tek kaldırılabilir (GUID yok).

### 4. Ürünler (K-7, S-1…S-4; mockup v2)
- **Ürünler sütunu:** çipler; renk = rol (tanıtım / hatırlatma), simge = kaynak (önerilen / son ziyaret / sizin seçiminiz). Onaylı içeriği olmayan tanıtım ürününde uyarı simgesi. Lejant.
  - Kaynak: önizlemedeki sıradaki ziyaretin kalemleri (`products[]` / `contentItems[].source`) + plandaki seçim (`targets.products`).
- Ürünü olmayan doktorda sarı "ürün yok" + **"Ürün seç"**.
- **Ürün seçici** (doktor panelinin "Ürünler" sekmesi; panelin "Dönem görünümü" sekmesi 4D'de — bu pakette panel iskeleti + Ürünler sekmesi):
  - portföy listesi (portföy tanımlı değil → bilgi notu + tüm ürünler, `portfolioStatus = undefined`), arama, "Tüm ürünler" geçişi;
  - ürün başına rol düğmeleri (varsayılan tanıtım); **önerilen (oyun) ürün kilitli**, rolü de kilitli (S-3; kilit ipucu "yalnız Planlanan Ziyaret ekranında çıkarılabilir");
  - **sınır göstergesi** "N / max" (`MaxPromo` / `MaxNonPromo` kapasiteden; mockup'taki "3" sabiti veriden);
  - **anlık süre** ("2 tanıtım + 1 hatırlatma + rapor ≈ 34 dk"; kapasite `VisitMinutes` formülüyle — önizleme yanıtından ya da plan ayrıntısındaki kapasite değerlerinden; sabit yazma);
  - "Bu ürün için onaylı içerik yok" uyarısı;
  - **oyun sınırı dolduysa:** "Sınır dolu — eklediğiniz ürün sığmıyor, sonraki ziyarete kalır" (3C §37 notu).
  - "Tamam" → oturum güncellemesi (yalnız o doktorun `products`; diğerleri null).
  - **S-1 metni:** "Değişiklikler bu doktorun onaylanmamış sonraki ziyaretlerine uygulanır."
- **Toplu uygula:** araç çubuğunda "Ürün uygula (N)" (seçim yoksa pasif) → aynı panel toplu kipte.
  - Seçilen ürünler seçili doktorların mevcut ürünlerine **eklenir** (birleşim; mevcutlar korunur; S-2).
  - Sınırı aşan doktor sayısı mesajda ("N doktorda sınır aşıldı; fazla ürünler sonraki ziyarete kalır").
- **Kurallar kutusu:** kaynak sırası ve karışık sıra maddeleri (mockup metni).

### 5. Davranış
- "Hedefleri kaydet" bugünkü gibi tam seçim gönderir, **ürünleri göndermez** (null = koru; 3C kuralı korunur).
- Onaylı / geçmiş hafta seçiliyken sekme salt okunur (4B durumu).

## KORU / YAPMA
- Backend'e dokunma. Gerekli alan yoksa raporla; 4A ya da ayrı küçük paket.
- **Yeni yazma uç YOK.** Tek istisna Web vekili (ürün arama, okuma).
- Rota ve 4B iskeleti değişmez (yalnız bağlan). Haftalar / doktor dönem görünümü 4D.
- Mockup stilleri kopyalanmaz; tema bileşenleri; 7 dil + Arapça sağdan sola; tarih biçimi aynı.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (4B sonrası), CRM (dokunulmaz), mimari (27). Build 0 hata. JS `node --check`.

**Yeni testler:**
1. Doktor tablosu sütunları 3D alanlarından; "BAĞLANTI" yok; izin engelli seçilemez.
2. Hızlı filtre `quick` parametresi; uzmanlık çoklu seçim sayılı.
3. Ürün çipleri: rol rengi + kaynak simgesi + içerik yok uyarısı (JS mantık birimi ya da kaynak testi).
4. Seçici: önerilen kilitli (rol de); sınır göstergesi veriden; "Tamam" yalnız o doktorun `products`'ını gönderir.
5. Toplu uygula: birleşim (mevcut korunur), sınır aşımı mesajı.
6. "Hedefleri kaydet" `products` göndermez (3C kaynak testi korunur).
7. Seçilenler GUID değil ad / kurum / uzmanlık.
8. Yeni anahtarlar 7 dilde.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Toplu uygulamada mevcut ürünleri ezen (birleşim değil) gönderim → test 5 kırmızı.
2. Önerilen ürünü kaldırılabilir yap → test 4 kırmızı.

### E4 (CT, fleet; kayıtlar kullanıcı onaylı)
- Memorial Şişli doktorları: sıklık / yapılan / kalan / son ziyaret.
- "Bu hafta görülmesi gerekenler" süzgeci.
- Oyunsuz doktora ürün seçimi → çipler + süre.
- Toplu uygula (2 doktor).
- Arapça sağdan sola.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (4A + 4B kabulünden SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4C · Ziyaret Planlama ekranı (2/3): Hedefler sekmesi (doktor durumu, hızlı filtreler, ürün sütunu ve seçici, toplu uygula, seçim özeti)
Repository: C:\tmp\vp-4c (worktree) · Branch: wp/vp-4c · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-4C-targets-tab.md — önce tamamını oku. Mockup: …/mockups/visit-planning/ (v2 standalone + decoded + BRIEF + BRIEF-ADDENDUM-K7 + iki analiz). 4A ve 4B §37'lerini oku (iskelet, alanlar). Ayrıca: …/WP-VP-3C-visit-product-list.md + …/WP-VP-3D-target-status-reads.md (uçlar ve alanlar) · frontend/Diten.Web/Views/CRM/VisitPlanning/** · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · frontend/Diten.Web/Controllers/CRM/{VisitPlanningController,KnowledgeController}.cs.
NE:
(1) Kurumlar: bölge kapsamı (K-5 bandı), tür yerel etiket, "x / y seçili · N bu hafta", Bölge dışı ekle penceresi.
(2) Doktorlar (3D my-accounts/{id}/doctors): seçim, ad, uzmanlık, sıklık ("dönemde N" / "sıklık yok"), yapılan/kalan, son ziyaret, Ürünler, rozetler (izin engelli seçilemez, pasif, segment salt okunur); BAĞLANTI yok; uzmanlık sayılı çoklu seçim; hızlı filtreler due/never/all; Tümünü seç filtreye göre; bağlı eczaneler.
(3) Seçim özeti: sayılar, tahmini süre + haftalık % + aşım uyarısı, ürün dağılımı + ürünsüz doktor uyarısı, Seçilenler ad/kurum/uzmanlık gruplu.
(4) Ürünler (K-7, S-1..S-4): çip sütunu (rol rengi, kaynak simgesi, içerik yok uyarısı, lejant), "ürün yok"+"Ürün seç"; doktor paneli iskeleti + Ürünler sekmesi (portföy yok notu + tüm ürünler, arama, rol düğmeleri, önerilen kilitli+rol kilitli, sınır N/max veriden, anlık süre VisitMinutes, içerik yok uyarısı, oyun sınırı dolu uyarısı, Tamam = yalnız o doktorun products, S-1 metni); toplu uygula (birleşim, sınır aşımı mesajı); kurallar kutusu; Web ürün arama vekili /CRM/VisitPlanning/api/products (okuma).
(5) Hedefleri kaydet products göndermez (null=koru); onaylı/geçmiş haftada salt okunur.
KORU/YAPMA: backend'e dokunma (eksik alan → rapor); yeni yazma ucu YOK (yalnız okuma vekili); Rota ve 4B iskeleti değişmez; Haftalar/doktor dönem görünümü 4D; mockup stilleri kopyalanmaz; 7 dil + RTL; tarih biçimi aynı.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web · CRM (dokunulmaz) · mimari (27); build 0 hata; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–8. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(web): WP-VP-4C — targets tab with doctor period status, quick filters, product chips/picker/bulk apply, selection summary" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), süre hesabının kaynağı, eksik backend alanları (varsa), elle denenecek durumlar. §22 TÜRKÇE. K13.
```
