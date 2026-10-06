# KP-5b mockup analizi — Marka Kitleri + Görsel Kütüphanesi

> **CT, 2026-10-02.**
> - **Kaynak:** `kp-5b-brand-assets-prototype.html` (kullanıcının yaptırdığı mockup).
> - **Çözülmüş dosyalar:** `kp-5b-screens.decoded.html` (S — şablon 482–1021, mantık 1023–1403), `kp-5b-script-5ff6651c.decoded.js` (D — veri + metinler), `kp-5b-script-5dbc863d.decoded.js` (genel çalıştırma kodu).
> - **Ölçüt:** `BRIEF-kp-5b-brand-kit-asset-library.md` §1–§8 ve `docs/decisions/DEC-SCMM-05-brand-kit-approved-asset-placement.md`.
> - Analiz alt ajanla yapıldı; kritik iddialar CT tarafından kaynakta doğrulandı (S:1131, 1271, 1274, 1332, 1345).

## 1. Özet
Kapsamın büyük kısmı karşılanmış: marka kiti listesi + 6 sekmeli ayrıntı (palet, logolar, yazı tipleri / kurallar, önizleme, sürüm, nerede kullanılıyor), görsel galerisi, iki yollu yeni görsel, görsel ayrıntısı, nedenli ortak seçici, Belge Yönetimi + MDM ekleri, boş / yükleniyor / hata / yetkisiz / belge yetkisi yok durumları.

**Eksik / sorunlu:** menü ekranı çizilmemiş; 7 dil, sağdan sola ve tema yalnız veri düzeyinde; onayda kp-studio kararları (K1 tek kanal, kişi bazlı SoD) geri gitmiş; birkaç iş kuralı brief ile çelişiyor (alternatif metinde İngilizce yedek, süresiz lisans girilemiyor, görselde arşiv durumu yok).

## 2. Kapsama (brief §1–§8)
| Madde | Durum | Not |
|---|---|---|
| §1 / §8-1 Menü "İçerik Stüdyosu" | ✗ | Çizilmemiş; grup adı yalnız sayfa yolunda (S:1093). Uygulamada mevcut menü / manifest deseni yeterli — yeni tur gerekmez. |
| §2.1 Kit listesi (sütun, filtre) | ✓ | S:536–543, 1117–1123 |
| Ürün başına tek aktif | ◐ | Onayda eski aktif "yerini aldı" olmuyor (S:1274) → uygulamada KP-5a kuralı. |
| §2.2 Kimlik (ürün kilitli, MDM bilgi amaçlı) | ✓ | `gpLabel`, `lockedAfterCreate`, `infoOnly` (S:859–865) — DEC-SCMM-05 ile uyumlu |
| Palet (7 dil ad, HEX, rol, metin / zemin, kontrast) | ✓ | WCAG 4.5:1 + geçersiz HEX (S:1162–1177); sürükle / ok ile sıralama |
| Logolar (3 varyant, kütüphaneden, en küçük boyut, boşluk payı, zeminler) | ✓ | S:762–764; boşluk payı "% logo yüksekliği", 4 zemin türü (D:99–100) |
| Yazı tipleri, tipografi (en küçük 10 pt, ölçek), notlar | ✓ | S:770–797 |
| Önizleme (açık / koyu) | ✓ | S:798–815 |
| Sürüm geçmişi | ◐ | Sabit 3 satır |
| Onay paneli | ◐ | Ret yorumu zorunlu ✓, gönderen paneli görmez ✓; **SoD role göre** (S:1268), Görev Merkezi notu yok (K1 geri gitmiş); ek onay adımı işlevsiz, onay tek adımda aktif ediyor (S:1274) |
| Kit eylemleri | ◐ | Yerini aldı / arşivde eylem yok; "Yeni sürüm" yalnız bildirim |
| §3.1 Galeri | ✓ | Kart alanları, lisans uyarısı (≤30 gün / doldu / süresiz), filtre, arama. Küçük resim metin yer tutucu; ülke rozetleri ham kod |
| §3.2 Yeni görsel (iki yol, sürüm sabitleme, "yeni sürüm var") | ✓ | S:633–660, 1251 |
| Alanlar, 7 dil alternatif metin zorunlu | ✓ | Gönderimde `altCount<7` engeli (S:1326–1333) |
| Otomatik boyut, düşük çözünürlük, desteklenmeyen tür | ✓ | <1200 px, PSD (S:1313–1314) |
| §3.3 Görsel ayrıntısı | ✓ | "Etkilenen N yol" bandı; sayılar metne sabit |
| Görsel eylemleri | ◐ | **`archived` durumu yok** (IMG_ST: draft / review / approved / expired / withdrawn) |
| §4 Ortak seçici | ◐ | Nedenli gri öğeler ✓; bağlam pencerede **değiştirilebiliyor**; logo alanında tür=logo zorlanmıyor; yalnız marka kitinden açılıyor |
| §5 Belge Yönetimi + MDM ekleri | ✓ | S:909–955 |
| §6 Durumlar | ◐→✓ | Boş / yükleniyor / hata / yetkisiz (iskelet yok) / belge yetkisi yok / incelemede kilitli / süresi dolmuş / ülkeye kapalı / alternatif metin yok |
| §7 7 dil + RTL / tema / klavye / tablet | ✗ / ◐ | Arayüz yalnız TR / EN; tema renkleri sabit; ürün L10n / tema altyapısında çözülür (kp-studio'daki gibi) |

## 3. Mockup'ın eklediği / çelişen kurallar
| # | Mockup | Durum | CT önerisi |
|---|---|---|---|
| E1 | Alternatif metinde **İngilizce yedek**: dil eksik listesinde değilse EN metin o dil için yeterli (S:1131, 1345) | Brief ile çelişir ("o dilde alternatif metin var") | **Yedek yok.** Seçici, yolun dilinde metin ister. Oluşturmada zorunluluk: görselin izinli ülkelerinin içerik dillerinin hepsi (ülke kısıtı boşsa 7 dil). |
| E2 | Lisans bitişi **her zaman zorunlu** (S:1332), ama örnekte süresiz kurum içi görseller var (`lic:null`) | Kendi içinde çelişki | Kaynak "kurum içi" ise **"süresiz" işareti**; diğer kaynaklarda bitiş zorunlu. |
| E3 | Görselde `archived` yok | Eksik | **`withdrawn`** = kararla yayından çekilir (gerekçe zorunlu, kullanan yollara uyarı); **`expired`** = tarihle otomatik; **`archived`** = kullanılmayan kaydın kaldırılması (kullanımdaysa engellenir). |
| E4 | Onay tek adımda aktif; ek adım (Medikal / Hukuk) çizilmiş ama çalışmıyor; geçmişte onaycı hep "Regülasyon" | Onaycı kararı verilmedi | Kullanıcı kararı (aşağıda K-1, K-2). |
| E5 | SoD **role** göre; karar kayıt sayfasında, Görev Merkezi notu yok | kp-studio K1 + KP-5a kuralına aykırı | **Uygulamada KP-5a kuralı:** tek kanal = MOD-0023 görevi (Görev Merkezi), kişi bazlı SoD (yazar / gönderen karar veremez). Mockup turu gerekmez. |
| E6 | Kit gönderiminde doğrulama yok (ters logo boş, onaysız logo, renk adları eksik) | Eksik | **Engelleyici kontrol listesi:** ana logo dolu ve onaylı görsel; en az bir metin + bir zemin rengi; kontrast geçer; renk adları 7 dil. Diğer logo varyantları uyarı. |
| E7 | Aktif kit doğrudan arşivlenebiliyor | Ürün kitsiz kalabilir | Aktif yol kullanıyorsa arşiv **engellenir**; değilse uyarıyla izin. |
| E8 | Seçicide bağlam değiştirilebilir; logo alanında her tür seçilebilir | Kural gevşek | Bağlam **kilitli** (çağıran verir); logo alanı yalnız `logo` türü. Marka kiti logoları için bağlam = ürün; ülke kısıtı olan görsel logo olamaz (uyarı). |
| E9 | Yükleme: PNG / JPG / SVG / WEBP, ≤ 25 MB, "Belge Yönetimi › Tanıtım görselleri" klasörü | Belge Yönetimi bugün SVG / WEBP kabul etmiyor, küçük resim / boyut üretmiyor | Platform paketi (KP-5b-PLT): görsel türleri + içerik doğrulaması, boyut + küçük resim türevi, "Tanıtım görselleri" koleksiyonu. |
| E10 | Kod önekleri `BLG-`, `ŞBL-` (ASCII değil), `HATA-503`, ham ülke kodları | Uygulama ayrıntısı | Kodlar ASCII; hata metinleri yerelleştirilmiş; ülke adları referans setinden. |
| E11 | İki sürüm ekseni: görsel kaydı sürümü + sabitlenen belge sürümü | Uygun | Benimse. |

## 4. Mockup verisinden alan modeli (özet)
- **BrandKit:** `id (BK-…)`, ürün (Global Product + MDM marka bilgi), `ver`, `st` (draft / review / active / superseded / archived), `colors[] {hex, role (primary / secondary / accent / text / background), text, bg, n{7 dil}}`, `logos[] {v: main / mono / inv, asset (VRL-…), minPx, minMm, clear (% yükseklik), bgs{light, dark, photo, color}}`, yazı tipleri (başlık / gövde + yedek, isteğe bağlı dosya), tipografi (`minFont`, `scale{h1, h2, h3, body}`), `notes`, kullanım.
- **ImageAsset:** `id (VRL-…)`, `ti{dil}`, `type` (photo / diagram / logo / icon / chart), `st`, `cn[]` (boş = tümü), `pr[]`, kullanım hakkı (`src` agency / stock / internal, `licType`, `holder`, `licStart`, `lic` bitiş | süresiz), dosya (`doc BLG-…`, `ver` sabit, `newVer`, `w`, `h`, `fmt`, `size`), `alt{dil}`, `credit`, `tags[]`, kullanım (`paths`, `pages`, `kits`).
- **Seçici gerekçeleri:** onaylı değil, süresi doldu, ülkeye kapalı, ürün için izinli değil, alternatif metin yok, yayından çekildi.

## 5. Kullanıcı kararları (paketlemeden önce)
- **K-1 Marka kiti onaycısı:** tek adım. Kim? (CT önerisi: **Regülasyon tek adım** — KP-REG akışıyla aynı pozisyonlar; ayrı marka sorumlusu pozisyonu yok.)
- **K-2 Görsel onaycısı:** tek adım Regülasyon mı, tam MLR mı? (CT önerisi: **Regülasyon tek adım** — görsel, yolun içinde zaten tam MLR'den geçiyor; burada denetlenen lisans / ürün / ülke uygunluğu.)
- **K-3 Onay şablonu:** marka kiti ürün başına, görsel çok ülkeli → ülke şablonu (`KP-REG-{CC}`) uymuyor. CT önerisi: tek **küresel** şablon (`KP-BRAND`, `KP-ASSET`), aynı Regülasyon adımı.
- **K-4 E1–E3, E6–E8** önerilerinin kabulü.
- **K-5 Yükleme yolu:** İçerik Stüdyosu'ndan yükleme (Belge Yönetimi'ne kaydeder) + mevcut belge seçme — ikisi de (mockup gibi).

## 6. Paketleme önerisi
1. **KP-5b-PLT (Platform, Belge Yönetimi):** görsel türleri (PNG / JPG / SVG / WEBP) + içerik doğrulaması, boyut metadata'sı, küçük resim / önizleme türevi, "Tanıtım görselleri" koleksiyonu, görsel dosyanın yetkili okuması.
2. **KP-5b (CRM):** `BrandKit` + `ImageAsset` aggregate'leri (KP-5a yaşam döngüsü deseni), onay (MOD-0023, K-1..K-3), çözümleme (`brand-kits/resolve?productId`), seçici araması (bağlam + gerekçe), kullanım okuması, kontrol listesi.
3. **KP-5b-UI (Web):** iki liste + ayrıntılar + yeni görsel + ortak seçici + Belge Yönetimi / MDM ekleri; menüde "İçerik Stüdyosu" grubu (KP-5a sayfaları da taşınır).
4. **KP-5b-CFG:** canlı şablonlar + yetki script'i.

Sıra: PLT ∥ CRM → UI → CFG.

## 7. Kullanıcı kararları (2026-10-02) — "kabul paketle"
- **K-1:** marka kiti onayı = **Regülasyon, tek adım**.
- **K-2:** görsel onayı = **Regülasyon, tek adım** (yol içinde tam MLR ayrıca).
- **K-3:** iki **küresel** şablon: `KP-BRAND`, `KP-ASSET` (ülke şablonu değil), Regülasyon adımı.
- **K-4:** E1–E8 önerileri kabul (alternatif metinde yedek yok; kurum içi süresiz lisans; withdrawn / expired / archived ayrımı; kit gönderim kontrol listesi; kullanımdaki aktif kit arşivlenemez; seçici bağlamı kilitli, logo alanı yalnız logo türü). E5: KP-5a kuralı (Görev Merkezi tek kanal, kişi bazlı SoD).
- **K-5:** iki yükleme yolu — İçerik Stüdyosu'ndan yükleme (dosya Belge Yönetimi'ne kaydedilir) + mevcut belgeyi seçme.
- Paketler: **KP-5b-PLT ∥ KP-5b** → KP-5b-UI → KP-5b-CFG.
