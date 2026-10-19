# CT-DRA-01 — Dev_Readiness aktarımı ve sürüme bağlı onay: alan ve kural sözleşmesi

> **Durum:** taslak — sahip onayı ve Codex incelemesi bekliyor. Kod yazılmadı.
> **Kapsam:** yalnız Control Tower araçları ve `.antigravity` kuralları. Ürün kodu, başka ekiplerin modülleri ve
> MVP-6 yok.
> **Ölçüm tabanı:** main `01f1080fd` (2026-10-19).
> **Dayanaklar:** Blueprint master 8.7 `Dev_Readiness` sayfası (CEO kuralı, 2026-10-16); REP-001 v2 kabul kaydı
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

### 2.4 Önerilen aşama kuralları (kanıt kapısı, Excel'inkiyle aynı yönde)

| Önerilen aşama | Gereken kanıt |
|---|---|
| (satır yok) | modül için kanıt yok |
| In Development | modülün bir CT kuyruğunda yürüyen satırı var (yalnız kayıtlı kuyruklar; `execution/delivery/ct-queue-registry.tsv`) |
| Dev Complete | yayın kapsamı paketine bağlı en az bir kabul kaydı; `commit` main'de |
| Tested | yayın kapsamı paket sürümünde `module-check` çıkışı 0: bütün zorunlu AC'ler "geçti" |
| Verified | Tested + kapsayan her kayıtta `verifier` ≠ `implementer` ve ≠ Owner + Verified Date |
| Released | hiç önerilmez (yayın kanıtı kaydı yok) |

AC kimliği olmayan eski biçimli paket "ölçülemez" diye hata listesine girer, aşama önerilmez.

### 2.5 Çıktı

TSV sütunları: `Module ID`, `Önerilen aşama`, `Gerekçe`, `Eksik kanıt`, `Code link`, `PR link (türetilmiş)`,
`Test evidence`, `Verification evidence`, `Verified By`, `Verified Date`, `Kaynak kayıtlar (yol@commit)`.
Hata listesi ayrı bir dosyadır. Çıktının başında main commit'i ve üretim zamanı yazar.

### 2.6 Koruyucular (sabotajla gösterilecek)

Aşağıdakilerin her birinde adı belli bir test kırmızı olmalı:
- Tek bir kabul edilmiş iş paketi modüle Tested önerirse.
- Koşu kaydı tek başına Tested önerirse.
- `verifier` = `implementer` iken Verified önerilirse.
- Owner bilinmezken Verified önerilirse.
- Kanıtı olmayan modül tabloya girerse.
- Released önerilirse.
- Excel'in operasyonel bir sütunu çıktıya girerse.

## 3. Sürüme bağlı onay (APR)

### 3.1 Onay ayrı dosyada

- Onaylar paketin içinde değil, `execution/approvals/approvals.tsv` dosyasında tutulur. Dosyaya yalnız satır eklenir;
  bir paketin en son satırı geçerlidir.
- Sütunlar: `pack` (yol), `pack_sha256` (paket dosyasının **tamamı**; onay dışarıda tutulduğu için alan ayıklamaya
  gerek yok), `risk` (LOW / HIGH), `risk_reason`, `approver`, `approver_role`, `approved_at`, `card_sha256`, `note`.
- Paket bir bayt bile değişirse özet tutmaz. Araç işi dağıtmaz ve **"yeniden onay gerekli"** der; görünür bir fark
  özeti verir. Biçim değişikliği de yeniden inceleme ister; bu bilerek seçildi (Codex: kısmi özet, bağımlılık ya da
  güvenlik değişikliğini kaçırabilir).

### 3.2 Risk sınıfı

- Risk geliştirici tarafından **gerekçeyle beyan edilir.**
- Araç değişen yollara ve içeriğe bakıp riski yalnız **yükseltebilir**, düşüremez. Belirsizlikte risk HIGH sayılır.
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
- Kartın SHA-256'sı onay satırına yazılır.
- Kart okumayı kanıtlamaz; okunacak şeyi kısaltır.

### 3.5 GitHub katmanı (sonra eklenir)

- Sahip kararı (2026-10-19): ilk sürüm **GitHub hesabı olmadan** çalışır. Bu sürümde onaylayan adı beyandır.
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

- Kural **yürürlük tarihinden (KARAR D5)** sonra onaylanan paketler için geçerlidir.
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

## 4. Görüşler (2026-10-19)

- **Codex:** iki öneriye de KOŞULLU onay verdi. Bu sözleşme şunları içeriyor:
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
- **Sahip:** onay sistemi ilk sürümde GitHub hesabı olmadan; CODEOWNERS sonra. MVP-6 kapsam dışı.

## 5. Sahibin kararları

| # | Karar | Önerilen |
|---|---|---|
| D1 | Owner bilgisinin kaynağı | domain → kişi listesi, `approvers.tsv` ile aynı dosya |
| D2 | Onaylayıcılar: domain sorumlusu ve yedeği | sahip verir; uydurulmaz |
| D3 | Yayın kapsamı kaynağı | `execution/registries/release-scope.tsv` |
| D4 | Doğrulama tarihi | yeni REP-001 kayıtlarına isteğe bağlı `verified_at` |
| D5 | Onay kuralının yürürlük tarihi | sözleşme onayından sonraki ilk iş günü |
| D6 | GitHub ayarları ve hesaplar | sonra |

## 6. Sıra ve tahmin

1. Sözleşmenin onayı (sahip + Codex).
2. DRX aracı ve testleri: 1–2 gün.
3. Onay okuyucusu, onay dosyası ve kart: 1–2 gün.
4. Risk yükseltme ve karşı örnek testleri: 1–2 gün.
5. GitHub katmanı: hesaplar geldiğinde, yarım gün.

Tahmin; kesin değil. Ölçülmeyen: Excel'e yapıştırmanın kapılarla gerçek davranışı (Excel açılmadı) ve sahibin
listeleri.
