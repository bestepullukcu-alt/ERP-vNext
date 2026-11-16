# Paket standardı — kısa biçim (`kisa-v1`)

> **Durum:** sürüm 1, sahip onaylı (2026-11-16). Yalnız `pack_standard: kisa-v1` künyeli paketlere uygulanır; eski standart
> (`.antigravity/rules/module-pack-standard.md`) eski paket ve hatlar için yerinde kalır.
> Şablon: `.ees/sablonlar/paket.md` · koşullu bölümler (açılınca okunur): `.ees/standartlar/kosullu-bolumler.md` · süreç:
> `.ees/sablonlar/is-sureci.md`. Her kural: **kural** — *neden*.

## 1. İlke
Paket bir **sözleşmedir**: neyin yapılacağını, sınırını ve neyle kabul edileceğini söyler. Nasıl yapılacağını (katman, dosya
düzeni, korunan yollar) standartlar, altın referans ve mimari testler söyler.Geçmiş ayrı dosyadadır, onayı etkilemez.

## 2. Kurallar
- **KP1. İki dosya:** `<ID>-<kısa-ad>.md` (sözleşme) ve `kararlar/<ID>-<kısa-ad>.karar.md` (kararlar, revizyon geçmişi,
  inceleme cevapları, kayıtlı sapmalar; `module-packs/` altında `kararlar/` klasöründe). Onay yalnız sözleşmeye bağlıdır; işçi
  karar dosyasını okumaz, denetçi ilk turda okumaz. — *Eski paketlerde geçmiş %19'a varıyor; incelemelerin %64'ü ilk onaydan
  sonraki yeniden inceleme. Klasör ayrı: araçlar `module-packs/*.md` desenini tarıyor, kardeş dosya paket sayılırdı.*
- **KP2. Yedi çekirdek bölüm, sırası sabit** (§4); başka bölüm yalnız koşullu bölümdür. — *Serbest başlıklar aracın aradığını
  bulamamasına yol açıyor.*
- **KP3. Türetilebilen yazılmaz:** katman, dosya ve Razor düzeni, genel korunan yollar, varsayılan testler (§7), izin anahtarı
  biçimi. Modüle özgü dokunmama sınırı bölüm 3 "girmeyen" sütununa yazılır. — *Aynı bilgi başka yerde duruyor.*
- **KP4. Kabul ölçütü bugünkü makinece okunan biçimde yazılır, satırında dilimini ve planlanan testin türünü taşır** (§4.5).
  Etiket (tam paket kimliğiyle `[MOD-nnnn-FUnn:AC-n]`) numaradan türediği için pakete yazılmaz. Test sözcüğü planı söyler; varlığının ve
  geçtiğinin kanıtı teslimde `scripts/ac_coverage.py report` çıktısıdır (H22). Testle
  ölçülemeyen ölçüt `test: canlı tur` yazar. — *Etiket testin varlığını kanıtlamaz.*
- **KP5. Hata yolu kabul ölçütüdür, ayrı bölüm değil:** her beklenen hata (409, 403, 404, 503) bir ölçüt satırıdır. — *İki yerde
  yazılan davranış çelişiyor.*
- **KP6. Koşullu bölüm yalnız işin türü gerektiriyorsa açılır** (künyede `conditional:`); açılmayan yazılmaz; açılan soruları tek tek cevaplar. — *En büyük eski engel nedeni "eksik alan kuralı".*
- **KP7. Açık karar onaydan önce kapanır ya da bir dilim kapısına bağlanır.** Sözleşmenin herhangi bir yerindeki `TBD` (koşullu
  bölüm dahil) bölüm 7'de bir satırdır; satırı olmayan `TBD` onaya gitmez. Teslim edilmiş kodla eski paket çelişiyorsa fark da bölüm 7'ye yazılır. — *Hat kartı H3: belirsizlik paket aşamasında çözülür;
  işçi karar dosyasını okumaz.*
- **KP8. Boyut sınırı dilim başınadır, uyarıdır** (§5). — *Tek toplam sayı büyük modülü haksız uyarır.*
- **KP9. Büyük artış takip paketidir:** onaylı pakete yeni MVP yeteneği ya da yeni süreç getiren dilim eklenmez, `MOD-nnnn-FUxx`
  açılır (numara klasöre ve `verify_module_id.py` ön kontrolüne bakılarak verilir, Ç27). Eski (büyük) pakete yeni dilim
  gerekirse paket büyütülmez, kısa biçimli takip paketi açılır; eski paket atıfla kalır, yeniden numaralanmaz; künye
  alanları (`golden_reference`, `form_field_count`) üst paketten alınır. — *Onay paketin tamamına bağlı.*
- **KP10. Paketi `ees-module-pack-author` yazar, yazmayan denetçi denetler; bir düzeltme turu, sonra kişi karar verir** (H4, H19).
- **KP11. Künyede ad değişikliği yok, yalnız ekleme var** (§3). — *Eski araçlar adları okuyor.*
- **KP12. İş süreci süreç başına bir kez yazılır:** bir iş sürecinin ilk paketinden önce tek sayfalık süreç hazırlanır;
  ilk paketle **aynı PR'da ve aynı onayla** gelir, ayrı kapı yoktur. Süreç varsa paket yalnız atıf yapar. Teknik modül ve
  üst paketinin süreci olan takip paketi muaftır; üst paketin süreci yoksa takip paketi o sürecin ilk paketidir ve süreci yazar.
  İlk dönemde yalnız uyarı. Ayrıntı: `is-sureci.md` şablonu. — *Eksik iş kuralı ve iç çelişki eski engellerin çoğunun kökü.*

## 3. Künye
Bugünkü alanlar (`id`, `name`, `domain`, `service`, `shell`, `golden_reference`, `data_mode`, `entity_base`, `status`, `owner`,
`branch`, `started`, `target`, `form_field_count`, …) aynen kalır. Dört alan eklenir:

| Alan | Değer |
|---|---|
| `pack_standard` | `kisa-v1` |
| `plan_row` | `<plan sürümü> · <MVP kimliği> · MUST\|SHOULD\|COULD`; ya da `plan satırı yok — <neden>` (değer uydurulmaz) |
| `business_process` | `SUR-<ALAN>-<nn> adım <a-b>`; ya da `yok — teknik: <gerekçe>`; ya da `yok — takip paketi: üst paketin süreci` |
| `conditional` | açılan koşullu bölümlerin kısa adları, ör. `[zaman, durum-akisi]`; hiçbiri yoksa `[]` |

`owner` rol adı olabilir (depoya kişi adı yazılmaz). Künyedeki `TBD` (dal, tarih) açık karar sayılmaz; KP7 gövdeyi kapsar. Plan depoda değildir: bağ yalnız
**biçim** olarak denetlenir; plana üç alan istenir: öncelik, bağlı süreç, sahip rol. Süreç kimliği
`SUR-<ALAN>-<nn>`; alan kısa adı `AGENTS.md` §9'dadır, orada olmayan alan için önerilen kısa ad PR'da sorulur (listeye şimdi
eklenmez).

## 4. Bölümler — tam bu başlıklar, bu sırayla
İkinci düzey başlıklar tam bunlardır; koşullu bölümler 7'den sonra `## K-<ad>. <Ad>` olarak, `conditional:` sırasıyla gelir.

1. **`## 1. Ne ve neden`** (≤10 satır): amaç (bir cümle), kullanıcı rolleri, paketin süreçte karşıladığı adımlar. Örnek senaryo
   yazılmaz (sürecin E2E satırına atıf). Servis sapması: bir cümle + `adr:`.
2. **`## 2. Kapsam`**: kapsam ve kapsam dışı listeleri; kapsam dışı satırı nereye gittiğini söyler (takip paketi, başka modül,
   gelecek MVP). Sahip olunan nesneler tek satır.
3. **`## 3. Bağlar`**: tablo — kaynak (süreç · plan · başka modül · var olan kod) · atıf · girenler · girmeyenler. Başka modüle
   dokunuş: hangi modül, okuma/yazma, sahibinin yazılı onayı + bağlantısı (XMC-001, H16), dokunulan yol.
   "Girmeyen" sütunu modüle özgü dokunmama sınırını taşır. Kimliği olmayan yüzey adıyla yazılır; var olan kod ve üst paket de birer satırdır.
4. **`## 4. Sözleşme`**: dört alt tablo, her satırın son sütunu **dilim** (`DL-n`):
   nesneler ve uç noktalar (ad · tür · açıklama · dilim) · alanlar (nesne.alan · tip · zorunlu · kural · dilim; bileşik index
   `<Nesne>.(index)` satırı; işlem düzeyindeki doğrulama alan değil ölçüttür) · izinler (anahtar · tür · kim · uç nokta · dilim) ·
   denetlenen olaylar (komut — sistemin kendiliğinden yazması dahil, kullanıcı "sistem" · işlem adı · yol ya da istisna sınıfı +
   gerekçe · dilim; AUD-001). Varsa beşinci: yayımlanan olaylar ve bildirimler (kod · ne zaman · kime · alanlar · dilim). Ürün
   değişmezleri (kiracı, soft delete, ağ geçidi, dil) yazılmaz, yalnız istisnası. `AC-n` bu bölümde tablo satırı olmaz.
5. **`## 5. Acceptance Criteria`** (başlık İngilizce; okuyucu bunu arar): bugünkü biçim aynen, sonuna dilim ve test eki:
   `- [ ] AC-<n> (zorunlu|isteğe bağlı) — <gözlenebilir davranış> · dilim: DL-<n> · test: <tür>`.
   Bölümde yalnız bu biçimde madde olur (başka madde, tablo ya da `AC-` geçen başka satır paketi okunamaz yapar); `AC-`
   ardından yalnız sayı; kimlik tekil, silinmez, yeniden numaralanmaz; en az bir zorunlu; belirsiz söz yok. Test türleri: birim, entegrasyon,
   sözleşme, uçtan uca, görünüm, canlı tur. Bir ölçüt tek dilime düşer (yayılan davranış bölünür). Hata yolları burada (KP5). Sürecin her E2E senaryosu bir ölçüttür, senaryonun bütün adımlarının teslim
   edildiği ilk dilimdedir (eski pakette teslim edildiyse bölüm 3 "girmeyen" satırına yazılır): `AC-n (zorunlu) — SUR-…/E2E-k: … · dilim … · test: uçtan uca`. Takip paketi AC-1'den başlar; eski
   paketin kimliksiz ölçütüne metinle atıf yapılır. İşaret kutusu ilerleme ölçüsü değildir. Sabotaj kanıtı çalıştırma kaydına girer
   (İ6); bozmanın yolu açık değilse ölçüt metnine tek cümle.
6. **`## 6. Teslim dilimleri`**: eski onay aracının okuduğu biçim — başlık, ilk üç sütun ve doldurulmuş hücre kuralı değişmez.
   Sütunlar: `dilim · ad · kapsadığı bölümler · süreç adımı · plan · ölçütler · bağımlı · kanıt`. Koşullu bölüm başlığın tamamıyla yazılır (`K-zaman. Zaman`). İlk sütun `DL-n` (DL-1 çekirdek
   dilimdir); "kapsadığı bölümler" paketin başlık adlarından, numarasız ve virgülle (`Sözleşme, Acceptance Criteria`); "ölçütler"
   yalnız numara (`1–9`); hücre boş, `—` ya da tek başına `yok` olmaz (`bağımsız`, `teknik` yaz).
   Bölümde HTML yorumu, girintili satır, alt başlık yoktur. Her dilim bir süreç adımına ya da teknik gerekçeye bağlanır.
7. **`## 7. Açık kararlar`**: tablo `# · Karar · Kim · Hangi dilimi bekletir`. Her satır bir dilime bağlı (KP7).

## 5. Boyut sınırları (yalnız uyarı)
- **Ortak kısım** (künye + bölüm 1, 2, 3, 7): hedef ≤ 8 KB.
- **Dilim başına** (o dilimin bölüm 4 satırları + ölçütleri + koşullu bölüm satırları): hedef ≤ 6 KB, uyarı > 10 KB.
- **Dilim sayısı > 8:** takip paketine böl. **İş süreci:** ≤ 9 KB, ≤ 90 satır.
- Karar dosyası sınırsız. İşçinin okuduğu (ortak kısım + kendi dilimi) hedef ≤ 14 KB.
- Uyarı engel değildir: yazar bölmeyi önerir ya da karar dosyasına "neden bölünmedi" yazar. Sayılar deneme hattıyla bir kez düzeltilir.

## 6. Koşullu bölümler — ne zaman açılır (soruları ve en az içerik: `kosullu-bolumler.md`)
`para` tutar, fiyat, kur, vergi · `zaman` görünen tarih, gün, hafta, süre · `veri-degisikligi` şema, referans veri ya da var olan
kayıt değişiyor · `sozlesme-degisikligi` başka modülün tükettiği uç nokta, olay, veri biçimi değişiyor · `yuksek-hacim` binlerce
kayıt · `disa-aktarma` dosya, rapor, başka sisteme veri çıkıyor · `gozlem` arka plan işi, olay tüketicisi, servisler arası çağrı ·
`hassas-veri` kişisel, sağlık, finans, gizli veri · `ekran` arayüz · `durum-akisi` durum değişiyor ya da iki işlem aynı kaydı
değiştirebilir · `rota` yeni ağ geçidi rotası · `dis-sistem` banka, e-posta, takvim · `mevzuat` yasal kural.

## 7. Varsayılan testler — her pakette geçerli, pakette yazılmaz
Paket yalnız istisnasını (ör. `GlobalEntity`) ya da ekini yazar. Etiket taşımazlar.

| # | Ne | Kim / ne ölçer |
|---|---|---|
| V1 | başka kiracının kaydı 404; üst veri sızmaz | her yeni varlık için entegrasyon |
| V2 | izinsiz kullanıcı 403; iskelet çizilmez, yönlendirme yok (UAS-001) | entegrasyon (+ görünüm) |
| V3 | her uç noktada `[HasPermission]` | mimari testler |
| V4 | silme yalnız soft delete; silinen kayıt listede ve okumada yok | entegrasyon |
| V5 | her yazma komutu denetim olayı yazar ya da istisna sınıfı taşır (AUD-001) | entegrasyon + denetçi |
| V6 | görünen metin kaynak dosyasında; kiracı 7, platform 2 dil | `resx-check`, `l10n-agent` |
| V7 | ön yüz servise ağ geçidinden gider, port uydurulmaz | mimari testler, denetçi |
| V8 | ekran varsa liste doğrulayıcısı ve sayfa yardımı defteri | `verify_datatable_page.py`, `page-help-ledger.test.js` |

## 8. Denetim
`scripts/pack_contract_check.py --base origin/main` (ya da `--all`, ya da dosya yolu) yalnız uyarı üretir (çıkış 0), yalnız
`pack_standard:` alanı olan paketlere bakar (U0–U8).
