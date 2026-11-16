# Eski 667 kuralın akıbeti — özet ve sahibe kalan kararlar

Tablo: `akibet.tsv` (667 satır, her kimlik bir kez; sütunlar: kimlik, akıbet, yer, neden, eski kaynak, eski kural, ilk geçiş
sınıfı, nasıl karara bağlandığı). Kaynak döküm: 12 Kasım kural envanteri (`rules.csv`). Kartlar: `.ees/kartlar/`, taslak 1.
Tarih 2026-11-16; ölçümler bu dalda, `origin/main` @ `6ef2c884b` üzerinde.

## 1. Boyut — eski ve yeni (ölçüm)

| | Satır | Bayt |
|---|---|---|
| Eski kule kartı | 932 | 101.350 |
| Eski SOP | 2.832 | 122.665 |
| `AGENTS.md` (yerinde kalır; çekirdek bölümü ileride `cekirdek.md`'den üretilir) | 309 | 21.877 |
| **Yeni çekirdek** (sınır 70) | **49** | 8.593 |
| **Yeni kule** (sınır 90) | **38** | 5.453 |
| **Yeni hat** (sınır 110) | **49** | 7.275 |
| **Yeni işçi** (sınır 60) | **23** | 3.661 |
| **Yeni denetçi** (sınır 50) | **13** | 2.005 |
| Yeni beş kart toplam (sınır 380) | **172** | 26.987 |
| Şablonlar + model seviyeleri (her işte okunmaz, gerektikçe) | 232 | — |

Kart satırları uzun (kural + neden tek satırda). Önerilen bayt bütçesi (uygulama planı A7): çekirdek 10.000, kule 10.800,
hat 13.200, işçi 7.200, denetçi 6.000; hepsi altında. Bir işçinin okuduğu: çekirdek + işçi kartı ≈ 12,3 KB ≈ 3,5 bin token (**tahmin**, bayt/3,5); bugün
kule kartı + SOP tek başına 224 KB ≈ 64 bin token (tahmin). İşçinin bugünkü 95–155 bin token'lık okumasının geri kalanı
paket, kural haritası ve kod okumasıdır; o kısım iş 6'da (yalnız ilgili bölümün verilmesi, H14) ölçülecek.

## 2. Akıbet sayıları

| Akıbet | Satır | İlk geçiş (12 Kas.) | Anlamı |
|---|---|---|---|
| Kartta | **207** | 154 | bir kart satırı kuralın özünü karşılıyor (yer sütununda satır kimliği: çekirdek 87, hat 91, kule 55, işçi 48, denetçi 19 atıf) |
| Araç zorluyor | **36** | 36 | yeni akışta da kalan araç (bağdaştırıcı denetimi 12, modül kimliği aracı 8, kod sahipleri 5, komut freni 3, liste sayfası denetimi 2, model seviye eşlemesi 2, mimari testler ve phase1 kontrolleri 4) |
| Başka yerde yaşıyor | **69** | (ilk geçişte "konusu kalmadı" içindeydi) | kartın konusu değil ama kural sürüyor: mühendislik standardı, ajan tanımı, beceri ya da şablon (yer sütununda hangisi) |
| Konusu kalmadı | **314** | 347 | emekli araçların ritüeli (onay defteri, istem kapısı, kule kuyruğu, kabul kaydı, durum raporu, takip sayfası), yerini yeni bir kart satırının aldığı eski kural (yer: "yerini aldı: …"), eski kule ritüelleri |
| Karar gerek | **41** | 130 | aşağıda, 4 konu |
| **Toplam** | **667** | 667 | |

"Konusu kalmadı" yalnız **yeni akış** için geçerlidir: eski hatlar yeni kartlara geçene kadar eski kart ve SOP'taki kurallar
yürürlükte kalır.

130 karar satırından 89'u sahibin 16 Kasım kararları ve kart ilkeleriyle çözüldü: push/PR izni (karar 4) 8 satır, inceleme
turu (karar 2) 2, PR sıklığı 9, dal ve çalışma ağacı 13, modül talebi ve plan 13, kural yürürlüğü 7, paket yazımı 9, karar
kaydı ve kule yargısı 11, entegrasyon 7, test koşu politikası ve doğrulama 8, başka sahibin modülü 5 (sayılar konu konu
yaklaşık; satır satır `akibet.tsv`, `karar_yolu` = "karar yeniden okundu").

## 3. Sahibe kalan kararlar (41 satır, 4 konu)

| # | Konu | Satır | Sade örnek | Önerim | Kim |
|---|---|---|---|---|---|
| 1 | SOP'taki mühendislik kontrol listeleri nereye taşınsın | 16 | Para hesaplayan bir ekran yazılırken "ondalık tip, para birimi kaynağı, yuvarlama, saat dilimi" listesi bugün yalnız SOP'ta; SOP arşive gidince bu liste hiçbir yerde kalmaz (standartlarda yok, ölçüldü). Aynısı veri göçü, sözleşme değişikliği, yüksek hacim, dışa aktarma, gözlemlenebilirlik ve akış profili A/B/C için. | Paket standardına (`module-pack-standard.md`) "koşullu bölümler" diye eklensin: paketi yazan ajan işin türüne göre ilgili bölümü doldurur, denetçi kontrol eder. Kartlara girmez (her işte gerekmiyor). Dosya korunan yolda olduğu için sizin onayınız gerekir. | sahip |
| 2 | MVP-6 (tedarik zinciri) hattı yeni akışa ne zaman ve nasıl geçsin | 17 | Bu hattın kendine özgü kuralları var: Mac ve Linux şeritleri, dört şablon, "yalnız kendi kulesi kuyruğa yazar", Codex yazamaz, sürüm kayıtlı derleme ortamı (BASE-STACK). Genel akışa uymuyorlar. | Hat şimdilik eski düzenle sürsün; pilot geçince hattın sahibi geliştiriciyle birlikte geçiş günü seçilsin. Geçişte kuyruk ve defter kuralları kalkar; derleme ortamı kaydı gerekiyorsa hattın alan ayarına taşınır. | hattın sahibi geliştirici + sahip (hat açma/kapama M3) |
| 3 | Modül sonu belgeleri ve kapanış kayıtları hangileri zorunlu | 4 | Modül bitince canlı kullanım veri listesi, iş akışı panosu, modül durum izleyicisi ve backlog güncellenecek mi? Bugünkü sahip kararı altı belge diyor; yönetici "elle kayıt yok, kayıtlar araçtan" diyor. | Zorunlu liste yöneticinin şart tablosuyla (iş 2) birlikte kesinleşsin; o güne dek hat kartı "belge ajanı yazar, liste açık karar" der (H23). Durum izleyicisi elle değil araçla güncellensin. | sahip + yönetici (iş 2) |
| 4 | Onaylayıcı listesi: kim "listelenen kişi", yedek onaylayıcı, hesap değişince güncelleme | 4 | Kart "paketi listelenen kişi GitHub'da onaylar" diyor ama liste bugün emekli olacak onay defteri dosyasında. | Şimdi karar gerekmiyor: iş 4 (GitHub onayına geçiş) bu dört satırı kendi kararıyla kapatır. "Başka kişi birleştirir" kuralı kartta (H25). | sahip, iş 4'te |

## 4. Benim yorumumla çözülenler (sahip göz atsın; itiraz edilirse karar listesine döner)

1. **Hattın açılması paket taslağı iznidir** (K12): eski kule temiz keşiften sonra söz beklemeden paket yazdırıyordu; eski
   çekirdek "araştırma kod izni değildir" diyordu. Yeni: hattı kişi açar (M3), açılış taslak iznidir, kod izni değildir.
2. **Dal adı** (H8): üç ayrı şema yerine `AGENTS.md` §9 biçimi + dilim eki: `feature/<alan>/<mod-kimliği>-dl<N>-<kısa-ad>`.
3. **Aynı anda iş sayısı** (H12, H10): eski "en çok iki alt ajan" yerine onayladığınız "başlamış iş en çok 3"; yük eşiği
   eski kuralınızdan (Mac 12, Windows %80).
4. **Test koşu politikası** (İ7): iki çelişen kural yerine kademeli koşu; geniş takım PR'dan önce bir kez, tam koşu CI'da.
5. **MVP planı yetki sırasının dışında** (Ç3): paket standardındaki mevcut kural (§16f) çekirdeğe alındı.
6. **Kanıt tanımı** (Ç21): "kanıt depoda olmalı" yerine yöneticinin 12 Kasım yanıtına göre CI çalıştırması + çalıştırma
   kaydı; ham kanıt depoya girmez; "yerel dal ve sohbet kanıt değildir" korundu.
7. **Modül bitti tanımı** (H24): iki farklı tanım yerine tek tanım.
8. **Kulenin acil küçük düzeltme istisnası kalktı** (eski SOP §3.1): kule kod yazmaz; düzeltme de bir iştir.
9. **Eski rapor ritüelleri kalktı** (ilerleme çubuğu, sekiz tür sözcüğü, sabit "tam rapor", kalan istem tahmini): yerini
   modül bloğu, özet satırı ve durum sayfası aldı. Soru biçimi kuralları `sablonlar/sahibe-soru.md`'de toplandı.
10. **Dilim başına PR** (H5, H25): eski "modül sonunda tek PR" kuralı, onayladığınız "paket onayı o dilim için PR izni"
    kararıyla yerini dilim başına PR'a bıraktı.

## 5. Yöneticiye açık sorular (kart metnini bekletmiyor)

- Hat kartının ATF adı: "Reporter" mı, "Team Lead (tek hat) + Reporter" mı (rehber §4)?
- `.ees/` klasör adı onaylanıyor mu (PR'dan önce teyit)?
- Çalıştırma kaydının IMS saklama süresi (CI varsayılanı 90 gün, doğrulanmalı).

## 6. Denetim kanıtı (bu dal)

- Satır sınırları: yukarıdaki tablo (`wc -l`); hepsi sınırın altında.
- Mimari testler `.ees/` eklenmişken: `dotnet test tests/architecture/…ArchitectureTests.csproj` → 229/229 yeşil.
- Belge yolu bekçisinin `.ees/`'i taradığının kanıtı (sabotaj): `.ees/sablonlar/_sabotaj.json` içine beş klasör dışına giden
  bir `docs/` yolu yazıldı → `DocsPathGuardTests` kırmızı (1 başarısız / 49, dosya ve satır adıyla); dosya silindi → 49/49
  yeşil. Bekçi `.md` dosyalarını taramıyor; `.ees/` altındaki `.json`, `.html`, `.py` dosyaları tarandığı için gelecekteki
  şema ve betik dosyaları bu kurala tabidir. Testte gevşetme gerekmedi.
- `sync_adapters.py --check` → `OK 13 adapter(s) match their sources` (`.ees/` yönetilen klasör değil).
- Kör kontrol: `kor-kontrol.md`.
