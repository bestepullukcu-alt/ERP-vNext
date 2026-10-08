# WORK PACKAGE — WP-VP-4I · Sabit + yol, RTL, Hedefler kartları ve etiketler (küçük düzeltme)

> **CT (SoR), 2026-10-08.** WP-VP-4G / 4H canlı kontrolünden (E4) çıkanlar.
> - **Kullanıcı (2026-10-08):**
>   - "Küçük sapmaları da düzeltelim."
>   - "Hedefler kısmındaki hesap kartları biraz mockup'tan sapmış gibi, ona da bak."
>   - Kurum türü, uzmanlık ve il etiketleri de bu pakette.
> - İki bölüm, paralel: **4I-BE** (CRM motoru) ve **4I-WEB** (Web + veri betiği). Ayrı worktree'ler.
>
> **Çalışma yeri:** `C:\tmp\vp-4i-be` (dal `wp/vp-4i-be`) ve `C:\tmp\vp-4i-web` (dal `wp/vp-4i-web`), test dalı başından. Commit dala, push YOK.

## Canlıda görülen (plan `23b1706a`, 41. hafta)
- Kurumların konumu:
  - 01 ve 03 NOLU: Konya;
  - 018 KLİNİK: İstanbul;
  - 06 NOLU TOKİ: Şanlıurfa.
- Başlangıç düzeni: Perşembe Konya kümesi, Cuma TOKİ.
- SERHAT KOKULU (Konya) "Yalnız bu doktor" ile Cuma'ya sabitlendi:
  - HÜSEYİN ve SEDAD Perşembe'de kaldı. 4G kararlı yerleşim **doğru**.
  - Ancak TOKİ 42. haftaya kaydı ve nedeni **`capacity_full`** ("hafta dolu") yazdı. Cuma'da yalnız 8 dk iş vardı.
- **Kök neden:**
  - `DayBalancer.AssignAroundPins` serbest ziyareti taban gününde tutarken yalnız dakikaları sayıyor; sabitli kümeyle arasındaki **yolu saymıyor**.
  - Gün rotası kurulunca Konya → Şanlıurfa yolu güne sığmıyor ve ziyaret rota katmanında taşıyor. Taşmanın nedeni o katmanda `capacity_full`.

## 4I-BE — CRM motoru

### 1. Sabitli günde kümeler arası yol
- `AssignAroundPins` 3. adımı: bir günde sabitli küme varken o güne bağlı serbest ziyaretlerin kümesi sabitli kümeye **yakın değilse** (`NearTravelMinutes` dışı), günün toplamına kümeler arası yol eklenir (4G light-day kuralındaki yol hesabıyla aynı).
- Toplam bütçeyi aşıyorsa uzak serbest küme günden çıkar ve 4G kurallarıyla yeniden yerleşir: yakın gün → boş gün → az dolu gün (yol dahil) → yoksa `no_near_day`.
- Canlı vaka için beklenen: TOKİ Perşembe'ye de Cuma'ya da sığmaz (ikisi de Konya) → 42. haftaya kayar, nedeni **`no_near_day`**.

### 2. Rota katmanında yol taşması nedeni
- Gün rotası kurulurken bir ziyaret **yol süresi yüzünden** mesaiye sığmıyorsa, kaydırma nedeni `capacity_full` değil, `no_near_day` olur.
- `capacity_full` yalnız haftanın hiçbir gününde ziyaret süresine yer kalmadığında yazılır.

### Acceptance (4I-BE)
1. Canlı vakanın kopyası (Konya ×2, İstanbul, Şanlıurfa; Konya doktoru Cuma'ya sabit) → TOKİ'nin nedeni `no_near_day`. Perşembe'deki diğerleri yerinde.
2. Sabitli kümeye yakın serbest ziyaret aynı günde kalır (yol eklenmez).
3. Yol yüzünden rota taşması → `no_near_day`. Gerçekten dolu hafta → `capacity_full`.
4. Determinizm; 4G testleri değişmeden yeşil.
- **Sabotaj:** yol eklemeyi kaldır → test 1 kırmızı (kırmızı kanıt, geri al).

## 4I-WEB — Web + veri betiği

### 3. Şerit kartı (mockup `flex:0 0 128px`, `text-align:left`)
- Kart içeriği **başa hizalı**, ortalanmış değil. RTL'de sağa.
- Başlık satırı kırılmaz: "41. Hafta" + "BUGÜN" tek satırda. BUGÜN küçük yazı (11px); gerekirse başlık `white-space:nowrap`.

### 4. Gün satırı sağ sütunu
- Mockup gibi kısa sayı: "6 / 57" ("ziyaret" sözcüğü `title`'da). Boş süre rozeti aynı satırda, kırılmaz.
- Gün adı sütunu Arapçada kırılmasın ("الخميس 8 أكتوبر"): `minmax(96px, auto)` ya da `nowrap`.

### 5. İngilizce ay kısaltması
- "28 Sept" → **"28 Sep"**.
- `format.js` İngilizcede gün önce sırayı koruyarak "Sep" verir. `en-GB` "Sept" veriyor; başka bir İngilizce kültür ya da `formatToParts` kullanılabilir. Koruma testindeki "`'en-US'` yok" kuralı bozulmaz.

### 6. Hedefler — hesap kartları (mockup satır 403–406)
- **Satır 1:** ad solda; tür rozeti **en sağda** (`justify-content:space-between`, `white-space:nowrap`). Rozet renkleri: Klinik çivit (mockup `#e0e2f3` / `#0b1a8c`), Hastane camgöbeği (`#d7f5fc` / `#028aa6`). Tema sınıfı kullanılacaksa en yakın karşılık.
- **Satır 2:** il · "x / y seçili" · **"N bu hafta" amber** (mockup `#b27800`). Ayrı aralıklı öğeler (`gap:10px`), " · " noktaları yok. 13px, ikincil renk.
- **Seçili görünüm yalnız açık hesapta:** açık bg (`#f3f4fb`) + başta 3px lacivert çizgi (RTL'de sağda). Şimdi plandaki her hesapta sol çizgi var ve açık hesabın yazısı mor; mockup'ta ikisi de yok.
  - Plandaki hesap "x / y seçili" ile anlaşılır. Sıralamada plandakiler yine en üstte.
- **Satırdaki kırmızı "×" kalkar** (mockup'ta yok). Hesabı plandan çıkarma, **Seçilenler** grubunun başlığına taşınır ("Kaldır": o hesabın tüm doktorlarını çıkarır; mevcut kaldırma akışı).
- **"Bölge dışı ekle":** beyaz, kesik çizgili kenar, `+` simgesi (mockup). Şimdiki sarı uyarı düğmesi değil. Pencere ve akış aynı.
- Liste kutusu mockup gibi kenarlıklı, en fazla 560px, satır aralarında ince çizgi.

### 7. RTL'de veri metinleri (bidi)
- Arapçada rakam ya da noktalama ile başlayan Latin adlar ters dönüyor:
  - "018 KLİNİK" → "KLİNİK 018";
  - "03 NOLU … ASM." → "NOLU … ASM 03";
  - "75.YIL …" → "YIL … .75".
- Visit Planning'de her veri metni (kurum, doktor, ürün, dönem adı, temsilci) `<bdi>` ya da `dir="auto"` ile sarılır. Ortak bir yardımcı olmalı (ör. `VisitPlanningFormat.bidi(text)` → escape edilmiş `<bdi>`); dağınık düzeltme değil.

### 8. İl adı etiketi
- Şimdi il, kodun son parçası ("TR-34-ISTANBUL" → "ISTANBUL", `details.js` `accCity`).
- İl **referans etiketinden** gösterilir (dile göre: "İstanbul", "Şanlıurfa"). İl seti `reference-labels` uç noktasına ek alan olarak eklenir (mevcut tür / uzmanlık gibi; Web denetleyicisi, yeni CRM yazma ucu yok).
- Etiket yoksa son parça, Türkçe kurallarla baş harf büyük ("Istanbul" değil; `toLocaleUpperCase('tr')` / `toLocaleLowerCase('tr')`). Asıl çözüm etiket.

### 9. Tür / uzmanlık / il Türkçe etiketleri — veri betiği
- "Clinic", "Family Medicine", "Pediatrics" Türkçe arayüzde İngilizce görünüyor.
- Önce kök nedeni belirle (rapora yaz): etiket setinde `tr` etiketi mi yok, yoksa Web yanlış dili mi okuyor? Web hatasıysa düzelt.
- Etiket eksikse betik yaz: `scripts/data-load/add_tr_reference_labels.py`.
  - Kapsam: kurum türü, tıbbi uzmanlık, il setlerinde eksik `tr` etiketleri.
  - Diğer 6 dil (en, fr, es, zh, ar, ru) eksikse onları da ekler (tenant modülleri 7 dil).
  - **Varsayılan kuru çalışma** (dry-run): neyi ekleyeceğini listeler. `--apply` yalnız kullanıcı çalıştırır.
  - Yol: mevcut BRD yazım akışı (set → version → values, maker-checker / publish kuralları; `brd-catalog-loader` notu).
  - Türkçe adlar diakritikli ("Aile Hekimliği", "Çocuk Sağlığı ve Hastalıkları", "Klinik", "Hastane", "İstanbul", "Şanlıurfa").
- **Ajan veritabanına yazmaz.** Betik ve kuru çalışma çıktısı rapora.

### Acceptance (4I-WEB)
1. Şerit kartı başa hizalı; başlık tek satır.
2. Gün satırı "6 / 57" + rozet aynı satır; Arapça gün adı kırılmaz.
3. `en` → "28 Sep–2 Oct".
4. Hesap kartı:
   - rozet sağda;
   - amber "bu hafta";
   - seçili görünüm yalnız açık hesapta;
   - satırda "×" yok;
   - Seçilenler grup başlığında "Kaldır" var ve çalışır (mevcut akış);
   - "Bölge dışı ekle" kesik çizgili.
5. `bidi` yardımcısı: kurum / doktor / ürün adları `<bdi>` içinde (tarama testi).
6. İl referans etiketinden; etiket yoksa Türkçe baş harf.
7. Betik varsayılan kuru çalışma; `--apply` olmadan yazmaz (test ya da kod okuması).
8. Yeni metin anahtarları 7 dilde; argümansız Localizer değerinde `{0}` yok.
- **Sabotajlar:** `bidi` sarmalamayı kaldır → test 5 kırmızı · kartta seçili görünümü plandaki her hesaba ver → test 4 kırmızı.

## KORU / YAPMA (iki bölüm)
- Yeni CRM yazma komutu yok (listesiz 27).
- 4D / 4E / 4F / 4G / 4H davranışları:
  - taşıma;
  - sabit;
  - kararlı yerleşim;
  - ürün adı;
  - biçimleyici;
  - boş hafta;
  - onay sonrası yer.
- Rota tasarımı aynı. UAS-001. 7 dil. Kiracı sınırı. Ajan veritabanına yazmaz. Kullanıcının oturum açık sekmesine enjeksiyon yok.

---

## §36.1 Agent Prompt — 4I-BE (paste-ready) — DISPATCH: owner (4I-WEB ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4I · 4I-BE — sabitli günde kümeler arası yol + rota taşması nedeni
Repository: C:\tmp\vp-4i-be (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4i-be · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4I-pin-travel-rtl-account-cards.md — "Canlıda görülen" + 4I-BE (madde 1–2). Bağlam: WP-VP-4E ve 4G §37 (DayBalancer.Assign / AssignAroundPins, LightDayLoadRatio, NoNearDay, NearTravelMinutes; VisitPlanningEngine hafta planı + gün rotası).
NE: (1) AssignAroundPins: sabitli günde sabitli kümeye yakın olmayan serbest küme için kümeler arası yol gün toplamına eklenir; aşarsa uzak serbest küme çıkar, 4G kurallarıyla yeniden yerleşir (yakın → boş → az dolu yol dahil → no_near_day). (2) Gün rotasında yol yüzünden mesaiye sığmayan ziyaretin kaydırma nedeni no_near_day; capacity_full yalnız gerçekten yer yokken.
KORU/YAPMA: yeni yazma komutu yok (27); 4E/4G kuralları ve testleri aynen yeşil; göç/seed/grant/indeks yok; TenantId; mobil yalnız ek alan (yeni alan yok).
DOĞRULA (E2): CRM (2451/0/5 tabanı) · Web 792/0 (dokunulmaz) · mimari 27. Testler 4I-BE Acceptance 1–4 (canlı vakanın kopyası: Konya 37.90/32.49 ve 37.90/32.46, İstanbul 41.06/28.99, Şanlıurfa 37.19/38.78); sabotaj 1 (kırmızı kanıtla, geri al). dotnet test -o kullanacaksan çıktı klasörü REPO İÇİNDE olsun (bin/Debug/<ad>; repo dışı klasör kök-bulucu testleri yanlış kırmızı yapar).
Commit: "fix(crm): WP-VP-4I-BE — count inter-cluster travel on a pinned day; travel overflow says no_near_day" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — 4I-WEB (paste-ready) — DISPATCH: owner (4I-BE ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4I · 4I-WEB — şerit/gün satırı, İngilizce ay, Hedefler hesap kartları, RTL bidi, il etiketi, TR etiket betiği
Repository: C:\tmp\vp-4i-web (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4i-web · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4I-pin-travel-rtl-account-cards.md — 4I-WEB (madde 3–9). Mockup: execution/domains/commercial-suite/work-packs/mockups/visit-planning/visit-planning-v2.decoded.html (hesap kartı satır 397–410, şerit ~579, gün satırı ~625). Bağlam: WP-VP-4H §37 (format.js, weeks.js, details.js sol liste). Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/** · Resources/Views/CRM/VisitPlanning/** · Controllers/CRM/VisitPlanningController.cs (reference-labels) · scripts/data-load/.
NE: (3) şerit kartı başa hizalı, "41. Hafta"+BUGÜN tek satır. (4) gün satırı "6 / 57" + boş süre rozeti aynı satır; Arapça gün adı kırılmaz. (5) en ay "Sep" (gün önce; 'en-US' literal yok). (6) hesap kartı mockup: rozet en sağda (Klinik çivit / Hastane camgöbeği), 2. satır il · x/y seçili · amber "N bu hafta" (noktasız, gap), seçili görünüm YALNIZ açık hesapta (bg + başta 3px çizgi, RTL'de sağda), satırdaki "×" kalkar → Seçilenler grup başlığında "Kaldır" (mevcut akış), "Bölge dışı ekle" beyaz kesik çizgili + "+". (7) RTL bidi: ortak yardımcı (ör. VisitPlanningFormat.bidi → escape'li <bdi>) ile kurum/doktor/ürün/dönem/temsilci adları. (8) il referans etiketinden (reference-labels'a il seti ek alan; yoksa Türkçe baş harf). (9) "Clinic"/"Family Medicine" kök nedeni (etiket yok mu, Web yanlış dil mi) → Web hatasıysa düzelt; etiket eksikse scripts/data-load/add_tr_reference_labels.py (tür, uzmanlık, il; eksik tr + diğer 6 dil; VARSAYILAN DRY-RUN, --apply yalnız kullanıcı; BRD set→version→values akışı; diakritikli Türkçe). VERİTABANINA YAZMA.
KORU/YAPMA: 4D/4E/4F/4H davranışları aynı; Rota tasarımı aynı; backend CRM'e dokunma (4I-BE paralel); yeni yazma ucu yok; UAS-001; 7 dil + TR diakritik; argümansız Localizer değerinde {0} yok; kullanıcının oturum açık sekmesine harness/mock enjekte etme.
DOĞRULA (E2): Web (792/0 tabanı) · CRM 2451/0/5 (dokunulmaz) · mimari 27; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad> (REPO İÇİNDE). Testler 4I-WEB Acceptance 1–8; sabotaj 2 (kırmızı kanıtla, geri al). Betiğin kuru çalışma çıktısını rapora koy.
Commit: "fix(web): WP-VP-4I-WEB — strip/day-row polish, en month, account cards per mockup, RTL bidi, city labels, TR label script" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, kök neden (madde 9), mockup'tan bilinçli sapmalar, elle denenecekler. §22 TÜRKÇE. K13.
```
