# CT-DRA-01 — Dev_Readiness aktarımı ve sürüme bağlı onay: alan ve kural sözleşmesi

> **Durum:** v2 — sahip kararları alındı (2026-10-19). **§3 onay (APR) v1 uygulandı:** `scripts/pack_approval.py`,
> `execution/approvals/`, `scripts/prompt_gate.py` dağıtım kapısı. Bağımsız inceleme (2026-10-19) 3 engel + 4 düzeltme
> buldu, hepsi uygulandı: iptal edilen onayın tabanı, insan adımının main çizgisinde ölçülmesi, aynı kişinin hesapları
> (`person`), listenin merge'den önceki hâli, silinen POLICY-START, yalnız-ekleme içerik denetimi, kanonik yol, başlığa
> bağlı risk (gerçek geçmişte yüksek risk %63 → %39). **§2 Dev_Readiness (DRX) ertelendi:** main'de REP-001 v2
> kaydı yok (0, ölçüldü); ilk kayıt main'e girince yazılır (backlog BL-656). Onaylayıcılar: rol + GitHub hesabı; junior
> yüksek riskli işi onaylayamaz.
> **Kapsam:** yalnız Control Tower araçları ve `.antigravity` kuralları. Ürün kodu, başka ekiplerin modülleri ve
> MVP-6 yok.
> **Ölçüm tabanı:** main `01f1080fd` (2026-10-19).
> **Dayanaklar:** Blueprint master 8.7 / 8.8 `Dev_Readiness` sayfası (8.8'de depo içe aktarma sütunları Z–AD) (CEO kuralı, 2026-10-16); REP-001 v2 kabul kaydı
> (`scripts/acceptance_record.py`); paket onay okuyucusu (`scripts/ct_queue.py` `pack_approval_limit`); sahip ve Codex
> görüşü (2026-10-19, bu belgenin §4'ünde).

## 1. Neden

- **Dev_Readiness:** 8.7'de her modülün aşaması (Not Started … Released) ve kanıt bağlantıları bekleniyor. 364 modülün
  hepsi "Not Started", çünkü Excel'i hazırlayan depodaki kanıtı görmedi. Kanıt depoda (kabul kayıtları) duruyor.
- **Onay:** geliştiriciler paket ve DCP onaylarını okumadan veriyor; sorunlar sonradan çıkıyor. Bugünkü okuyucu
  yalnız paketin `status` / `approval_status` alanına bakıyor. Onaylayanı, onaylanan sürümü ve riski bilmiyor
  (ölçüldü: `scripts/ct_queue.py@01f1080fd` satır 1207–1248).

## 2. Dev_Readiness aktarımı (DRX)

### 2.1 Yön ve sınır

- **Tek yön:** depo → Excel. Araç yalnız okur. Excel'e yazmaz, Excel'i okumaz.
- **Öneri tablosu üretir, aşama atamaz.** Excel'in `Stage` hücresini insan seçer; Excel'in kapıları uyumsuz satırı
  `GATE FAIL` olarak işaretler.
- **Excel'in operasyonel alanlarına karşılık sütun üretilmez:** Release Scope, AI kararları, Last Updated ve
  Change Note. Böylece yapıştırma bu alanları boş hücreyle ezemez.
- **Kanıtı olmayan modül tabloya girmez.** Kanıt yokluğu "Not Started" yazmak demek değildir; o modülün Excel satırına
  dokunulmaz.
- Eksik ya da çelişkili kayıtlar ayrı bir **hata listesine** çıkar.

### 2.2 Bir satırın kimliği

Bir öneri satırı dört şeyle tanımlanır: **modül + yayın kapsamı + paket sürümü + değerlendirilen kod sürümü**
(Codex, 2026-10-19). Tek bir iş paketinin kabulü modülü "Verified" yapmaz; `module-check` "AC coverage only, never
Module Done" der (`scripts/acceptance_record.py@01f1080fd:16`).

**Yayın kapsamı kaynağı — KARAR D3.** Hangi paket sürümünün bir modülün yayın kapsamını tanımladığını bugün hiçbir
dosya söylemiyor. Öneri: sahibin tuttuğu `execution/registries/release-scope.tsv`. Sütunlar: `module`, `release`
(ör. R1), `pack` (yol@commit). Bu dosya olmadan araç Tested ve Verified önermez.

### 2.3 Alan eşlemesi

| Excel sütunu | Kaynak | Kural | Durum |
|---|---|---|---|
| Module ID | REP-001 `module` | Blueprint_Data'da bulunmalı | ölçülür |
| Owner | **KARAR D1** | Paketin `owner` alanı kişi ya da ekip olabilir (`module-pack-standard.md:75`); kabul kaydındaki `implementer` Owner değildir | kaynak yoksa boş + hata listesi |
| Code / PR link | REP-001 `commit` (doğrulanan kod) | commit bağlantısı. PR numarası main'deki merge mesajından okunur ve **"merge mesajından türetildi, GitHub'da doğrulanmadı"** etiketi taşır | ölçülür / türetilir |
| Test evidence link | REP-001 `evidence` (run-record) | Yalnız yayın kapsamı paket sürümünün **bütün zorunlu AC'leri** kabul listesinde "geçti" ise; tek satır yetmez. `run_record`, komutun ölçütü sınadığını kanıtlamaz (`scripts/run_record.py@01f1080fd:23`) | ölçülür |
| Verification evidence link | REP-001 kaydının yol@commit'i | `decision: accepted`; `verifier` ≠ `implementer`; ayrıca CEO kuralı **Verified By ≠ Owner** (D1 gerekir) | ölçülür |
| Verified By | REP-001 `verifier` | beyan niteliğinde; kimlik doğrulaması değildir | beyan |
| Verified Date | **KARAR D4** | REP-001 `date` doğrulama tarihi olarak tanımlı değil. Yeni kayıtlar için isteğe bağlı `verified_at` alanı önerilir; yoksa boş | tanım gerekir |
| Release evidence link | — | Bugün yayın kanıtı kaydı yok; hiç önerilmez | yok |

### 2.4 Önerilen aşama kuralları (v2)

Aşamalar REP-001'in mevcut alanlarına bağlanır: `completion` (`WP Done` / `Module Done` / `Capability Done` /
`Release Done`), `decision`, `commit`, `evidence`, `verifier`, `implementer`, `module_pack`
(`scripts/acceptance_record.py@01f1080fd:54–59`). "Modül kaydı": modülün, yayın kapsamındaki paketlerine bağlı ve
`completion` değeri `Module Done`, `Capability Done` ya da `Release Done` olan kabul kaydı.

| Önerilen aşama | Gereken kanıt |
|---|---|
| (satır yok) | modül için kanıt yok |
| In Development | modülün kayıtlı bir CT kuyruğunda yürüyen satırı var. Araç Excel'i okumadığı için "Excel kapısı geçti" demez; Excel'in bu aşamada istediği alanları (Owner, Last Updated, Change Note) "Excel'de doldurulmalı" sütununda sayar |
| Dev Complete | `decision: accepted` bir **modül kaydı** var ve `commit`'i main'de. Tek bir `WP Done` kaydı yetmez |
| Tested | Dev Complete ve ek olarak: (a) yayın kapsamındaki her onaylı paket sürümünde `module-check` çıkışı 0; (b) hiçbir zorunlu AC **yalnız N/A ile** kapsanmıyor (`module-check` gerekçeli N/A'yı kapsam sayar — `scripts/acceptance_record.py@01f1080fd:834–837,863–866`; tek başına N/A ile kapsanan zorunlu AC listelenir ve Tested'i durdurur); (c) modül kaydının `commit`'i kapsayan her kaydın `commit`'ini içerir (git ataları) ve modül kaydının `evidence` koşusu o commit üzerindedir. Farklı kod sürümlerinin kanıtı birleştirilmez |
| Verified | Tested ve ek olarak modül kaydında `verifier` ≠ `implementer`, `verifier` ≠ modülün Owner'ı (D1), `verified_at` dolu (D4) |
| Released | hiç önerilmez (yayın kanıtı kaydı yok) |

**Tested ile Verified arasındaki fark:** Tested, geliştirme tarafının yayın kapsamındaki bütün kanıtının tek bir kod
sürümünde tamam olduğunu söyler. Verified, bağımsız bir kişinin aynı sürümü kabul ettiğini, kimin ve ne zaman
olduğuyla söyler. Excel'in tek hücresine modül kaydının `verifier` ve `verified_at` değeri yazılır; kapsayan diğer
kayıtlar "Kaynak kayıtlar" sütununda listelenir. `verifier` bir beyandır, kimlik doğrulaması değildir.

AC kimliği olmayan eski biçimli paket "ölçülemez" diye hata listesine girer, aşama önerilmez.

### 2.5 Çıktı (Master 8.8'in içe aktarma sözleşmesine göre)

Master 8.8, `Dev_Readiness` sayfasına depo için beş giriş sütunu ekledi; AE sütunu insanın seçtiği `Stage` ile öneriyi
karşılaştırır (`MATCH` ya da `REVIEW: repo suggests …`). Araç yalnız bu beş sütunu, `Module ID` anahtarıyla üretir:

| Excel | Sütun | Araçtaki karşılığı |
|---|---|---|
| A | `Module ID` | anahtar (yapıştırma için; Excel'de zaten var) |
| Z | `Repo Suggested Stage` | §2.4'teki önerilen aşama (Excel'in aşama listesindeki yazımla) |
| AA | `Suggested At` | üretim tarihi |
| AB | `Source Commit` | çıktının üretildiği main commit'i (40 hane) |
| AC | `Suggested Evidence` | kanıt bağlantıları: modül kaydı, test kanıtı (yol@commit), commit; PR bağlantısı "türetildi" etiketiyle |
| AD | `Suggestion Reason` | gerekçe ve eksik kanıt; Excel'de doldurulması gerekenler (Owner, Last Updated, Change Note) |

`Stage` (G) ve insanın doldurduğu hiçbir sütun üretilmez. Hata listesi ayrı bir dosyadır. Çıktının başında main commit'i ve
üretim zamanı yazar.

### 2.6 Koruyucular (sabotajla gösterilecek)

Aşağıdakilerin her birinde adı belli bir test kırmızı olmalı:
- Tek bir kabul edilmiş iş paketi modüle Tested önerirse.
- Koşu kaydı tek başına Tested önerirse.
- `verifier` = `implementer` iken Verified önerilirse.
- Owner bilinmezken Verified önerilirse.
- Kanıtı olmayan modül tabloya girerse.
- Released önerilirse.
- Excel'in operasyonel bir sütunu çıktıya girerse.
- Yalnız `WP Done` kayıtları Dev Complete önerirse.
- Bütün zorunlu AC'ler N/A iken Tested önerilirse.
- Aynı paketin farklı kod sürümlerindeki kanıtları birleştirilip Tested önerilirse.
- Modül kaydının commit'i kapsayan bir kaydın commit'ini içermezken Tested önerilirse.
- `verified_at` yokken Verified önerilirse.

## 3. Sürüme bağlı onay (APR)

### 3.1 Onay ayrı dosyada

- Onaylar paketin içinde değil, `execution/approvals/approvals.tsv` dosyasında tutulur. Dosyaya yalnız satır eklenir;
  bir paketin en son satırı geçerlidir.
- Sütunlar: `pack` (yol), `pack_sha256` (paket dosyasının **tamamı**; onay dışarıda tutulduğu için alan ayıklamaya
  gerek yok), `action` (`approve` / `revoke`), `risk` (LOW / HIGH), `risk_reason`, `approver`, `approver_role`,
  `approved_at`, `card_sha256`, `note`.
- **Yalnız ekleme, git geçmişiyle ölçülür:** araç dosyanın main'deki geçmişinde her commit'in yalnız satır eklediğini
  denetler. Silme, değiştirme ya da yeniden sıralama içeren bir commit varsa dosyanın tamamı "güvenilmez" sayılır ve
  hiçbir onay geçmez. İptal, `action: revoke` satırıyla yapılır.
- **Eski onay tekrar kullanılamaz:** onay, satırındaki `pack_sha256` paketin **şimdiki** özetine eşitse geçerlidir.
  Paket geri alınıp eski özete dönse bile, o özetten sonra bir `revoke` satırı varsa onay geçmez.
- Paket bir bayt bile değişirse özet tutmaz. Araç işi dağıtmaz ve **"yeniden onay gerekli"** der; görünür bir fark
  özeti verir. Biçim değişikliği de yeniden inceleme ister; bu bilerek seçildi (Codex: kısmi özet, bağımlılık ya da
  güvenlik değişikliğini kaçırabilir).

### 3.2 Risk sınıfı

- Risk geliştirici tarafından **gerekçeyle beyan edilir.**
- Araç değişen yollara ve içeriğe bakıp riski yalnız **yükseltebilir**, düşüremez. Belirsizlikte risk HIGH sayılır.
- **Karşılaştırma tabanı:** paketin son geçerli onaylı sürümü; yoksa paketin main'e ilk girdiği sürüm. Hedef: paketin
  şimdiki sürümü ve paketin "Repo Scope" bölümündeki yollar. Hesaplanan risk onaydaki riskten yüksekse onay geçersizdir
  (HIGH'a yükselen iş, eski LOW onayla ilerleyemez).
- HIGH tetikleyicileri:
  - yeni modül ya da kimlik;
  - başka domain'in yolu;
  - izin, yetki ya da güvenlik anahtarları;
  - veri taşıma ya da şema değişikliği;
  - gateway (`ocelot.json`);
  - korunan yollar (`.antigravity/**`);
  - dış entegrasyon.
- LOW: aynı domain içinde, yukarıdaki tetikleyicilerden hiçbirine dokunmayan takip işi.

### 3.3 Onaylayan

- **KARAR D2.** Onaylayıcı listesi `execution/approvals/approvers.tsv` dosyasında tutulur. Sütunlar: `domain`, `risk`,
  `primary`, `backup`, `github_login` (isteğe bağlı; §3.5).
- HIGH riskte onaylayan listede olmalı ve paketi yazan kişi olmamalıdır.
- LOW riskte geliştiricinin kendi onayı geçerlidir, ama satır yine yazılır.
- Tek bir yönetici yerine domain sorumlusu ve yedeği tanımlanır; darboğaz böyle önlenir.

### 3.4 Onay kartı

- Araç onaydan önce tek sayfalık bir kart üretir: kapsam, dokunulan modüller ve yollar, açık kararlar (TBD dahil),
  🔴 kritik maddeler.
- Kart paketin SHA-256'sını içerir; kartın SHA-256'sı onay satırına yazılır. Araç kartı yeniden üretip özeti karşılaştırır;
  elle değiştirilmiş kart geçmez.
- Kart okumayı kanıtlamaz; okunacak şeyi kısaltır.

### 3.5 GitHub katmanı (sonra eklenir)

- Sahip kararı (2026-10-19): ilk sürüm **GitHub hesabı olmadan** çalışır. **Güven sınırı (açıkça):** bu sürüm yalnız
  beyan tutarlılığını sağlar; onaylayanın kimliğini doğrulamaz. Geliştirici listedeki başka birinin adını elle yazabilir.
- **İlk sürümdeki insan adımı (türetilmiş):** HIGH onay satırı main'e bir PR merge commit'iyle girmelidir. Merge commit'in
  yazarı (GitHub'ın kaydettiği merge eden hesap; ölçüldü: `01f1080fd` yazarı merge eden hesap) onay satırını ekleyen
  commit'in yazarından farklı olmalıdır. Bu bilgi git'ten çevrim dışı okunur ve "türetildi, GitHub'da doğrulanmadı"
  diye etiketlenir. Hesap ↔ kişi eşlemesi `approvers.tsv`'dedir (rol + GitHub hesabı; depo açık olduğu için gerçek ad
  yazılmaz).
- Hesaplar verildiğinde CODEOWNERS şunları korur:
  - `execution/approvals/**`;
  - onaylayıcı listesi;
  - kontrolü yapan betikler (`scripts/ct_queue.py`, `scripts/acceptance_record.py`, onay aracı);
  - `.github/workflows/**`;
  - CODEOWNERS dosyasının kendisi.
- Depo yöneticisinin GitHub'da "Require review from Code Owners" ve dal korumasını açması, atlama yetkisini kapatması
  gerekir (Codex: yalnız onay dosyasını korumak yetmez).
- GitHub onayı kimliği güçlendirir; metnin okunduğunu kanıtlamaz.

### 3.6 Geçiş

- Kural, main'e giren sabit bir **geçiş kaydıyla** yürürlüğe girer: `execution/approvals/POLICY-START.md` ve onun
  main'e girdiği commit (KARAR D5). Bir paket bu commit'ten sonra değiştiyse ya da onaylandıysa yeni kurala tabidir.
  Geçmiş bir tarih yazmak eski-kural istisnası sağlamaz; ölçüt tarih değil commit sırasıdır.
- Eski onaylar geriye dönük düşürülmez; eski Control Tower'lar durdurulmaz
  (`control-tower-operating-card.md@01f1080fd:27–28`).
- Eski bir paketin kapsamı esaslı biçimde değişirse yeni onay gerekir.

### 3.7 Koruyucular (sabotajla gösterilecek)

Aşağıdakilerin her birinde adı belli bir test kırmızı olmalı:
- Paket değişti ama onay eski özetle geçti.
- HIGH risk listede olmayan biri tarafından onaylandı.
- HIGH risk yazan tarafından onaylandı.
- Beyan LOW ama yol HIGH tetikleyicisine dokunuyor ve LOW kaldı.
- Onay satırı yokken iş dağıtıldı.
- HIGH'a yükselmiş bir işte eski LOW onay kullanıldı.
- Onay dosyasında bir satır silindi, değiştirildi ya da yeniden sıralandı.
- `revoke` edilmiş bir özet, paket geri alınınca yeniden geçti.
- Elle değiştirilmiş kart ya da onaylayıcı listesi geçti.
- Onaylayanla yazarın aynı kişinin iki takma adı olduğu durum (`approvers.tsv`'de aynı rol + hesap).
- HIGH onay satırı, onu yazan hesabın kendi merge'üyle main'e girdi.
- Geçiş commit'inden önceye tarih yazılarak eski kural istisnası istendi.

## 4. Görüşler (2026-10-19)

- **Codex (bağımsız inceleme, sahip kararı değildir):** iki öneriye de koşullu görüş verdi; v1 taslağına ikinci kez
  KOŞULLU görüş verdi (2026-10-19). v2 bunları işliyor: Dev Complete için açık modül kaydı, Tested'de N/A politikası ve
  ortak kod sürümü, Tested–Verified farkı, ilk sürümün güven sınırı ve insan adımı, risk tabanı, yalnız-ekleme ve iptal,
  karta bağlı özet, geçiş kaydı, ek karşı örnekler. v1 için işlenenler:
  - iş kabulü ≠ modül hazırlığı;
  - koşu kaydı ≠ test kanıtı;
  - Owner ≠ implementer;
  - Excel alanlarını ezmeme;
  - tam paket özeti;
  - riskin yalnız yükseltilmesi;
  - domain sorumlusu ve yedeği;
  - onaylayıcı listesi ve betiklerin de korunması;
  - PR bağlantısının "türetildi" etiketi;
  - Tested için yayın kapsamının bütün gerekli testleri.
- **Sahip:** onay sistemi ilk sürümde GitHub hesabı olmadan; CODEOWNERS sonra (PR #153 ile eklendi). MVP-6 kapsam dışı.

## 5. Sahibin kararları

| # | Karar | Önerilen |
|---|---|---|
| D1 | Owner kaynağı | `approvers.tsv`'de rol + GitHub hesabı; modül bazında istisna satırı olabilir (domain sorumlusu otomatik Owner sayılmaz) |
| D2 | Onaylayabilenler | "dört göz": HIGH işi, listede olan ve yazandan farklı biri onaylar (CEO, kıdemli geliştirici ya da geliştirici); LOW işi yazan onaylar |
| D3 | Yayın kapsamı | yeni tablo yok: modülün yayını Blueprint 8.7'den okunur; kapsam = modülün ve takip paketlerinin main'deki onaylı paket sürümleri (çok paketli kapsam desteklenir) |
| D4 | Doğrulama tarihi | REP-001 v2'ye isteğe bağlı `verified_at` alanı; okuyucu tanımsız alanı reddettiği için (`scripts/acceptance_record.py@01f1080fd:181–185`) okuyucu ve öz-test birlikte güncellenir; eski kayıtlar geriye doldurulmaz |
| D5 | Yürürlük | `execution/approvals/POLICY-START.md`'nin main'e girdiği commit; eski onaylar bozulmaz |
| D6 | GitHub hesapları ve ayarları | **yapıldı (dosya):** `.github/CODEOWNERS` PR #153 ile main'de (2026-10-19); GitHub `codeowners/errors` boş. **Açık (depo ayarı, ölçüldü 2026-10-19):** squash ve rebase merge hâlâ açık; "Require review from Code Owners" ayarı yönetici yetkisi olmadan okunamadı |
| D7 | İlk sürümün güven sınırı | beyan tutarlılığı + türetilmiş insan adımı (merge eden ≠ yazan); kimlik doğrulaması GitHub katmanıyla gelir |
| D8 | Tested–Verified ayrımı | §2.4'teki tanım |

## 6. Sıra ve tahmin

1. Sözleşmenin onayı (sahip + Codex).
2. DRX aracı ve testleri: 1–2 gün.
3. Onay okuyucusu, onay dosyası ve kart: 1–2 gün.
4. Risk yükseltme ve karşı örnek testleri: 1–2 gün.
5. GitHub katmanı: hesaplar geldiğinde, yarım gün.

Tahmin; kesin değil. Ölçülmeyen: Excel'e yapıştırmanın kapılarla gerçek davranışı (Excel açılmadı) ve sahibin
listeleri.
