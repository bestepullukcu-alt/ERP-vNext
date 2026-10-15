---
description: "REP-001 — Durum raporu ve kanıt standardı: sayılar elle yazılmaz, kanıt depoda durur, terim uydurulmaz"
---

# Durum Raporu ve Kanıt Standardı

Bu standart, **birden fazla modülün durumunu bildiren** raporların biçimini ve bu raporlardaki kanıtın nerede
duracağını tanımlar. Tek bir iş paketinin raporu SOP §22'dir, tek bir doğrulama SOP §37'dir; bu dosya onların
yerini almaz, "dokuz modül nerede" sorusunun cevabını standarda bağlar.

> **Otorite:** `.antigravity/` katmanı (AGENTS.md §1). Kanıt seviyeleri SOP §23'te, "Done" seviyeleri SOP §29'da
> tanımlıdır; bu standart onlara atıf yapar, yenisini icat etmez.

---

## 1. Neden var

2026-09-18'de gelen bir durum raporu şu satırları taşıyordu: "Pack mevcut %100 — 9/9", "Bağımsız doğrulanmış
bounded runtime %22,2 — 2/9". Ölçüldü:

- "bounded runtime" ve "bounded CT kabulü" terimleri depoda hiçbir yerde tanımlı değildi.
- Kanıt bağlantıları başka bir makinedeki çalışma kopyasını ve bir `/tmp` klasörünü gösteriyordu.
- Rapordaki iki modülden birinin kodu depoya hiç gönderilmemişti; ilgili dal ana daldan 400 commit geriydi.

Yani rapor ne doğrulanabiliyor ne de yanlışlanabiliyordu. Bu standart, aynı raporun tekrar yazılmasını değil,
**okunabilir ve kontrol edilebilir** olmasını sağlar.

---

## 2. Rapor biçimi (zorunlu)

Çok modüllü her durum raporu tek bir tablo taşır. Sütunlar sabittir:

| Modül | Kapsam cümlesi | Kanıt seviyesi | Kanıtın yeri | Karar | Tamamlanma | Sıradaki tek eksik |
|---|---|---|---|---|---|---|
| MOD-0183 | Sevkiyat oluştur/listele/geçiş, tenant + RBAC | E4 | `docs/records/audits/2026-09/mod-0183-r1-evidence/@4a8d4d4b` | accepted | WP Done (SOP §29.1) | canlı ingress doğrulaması |

Kurallar:

- **Kapsam cümlesi** tek cümledir ve neyin kabul edildiğini söyler; "modül tamamlandı" demek değildir.
- **Kanıt seviyesi** yalnız `E0`–`E5`'ten biridir (SOP §23).
- **Kanıtın yeri** `depo-yolu@commit` biçimindedir. Başka bir şey kanıt değildir (bkz. §3).
- **Karar** künyedeki `decision` değeridir: `accepted` · `rejected` · `conditional` — ya da `karar yok`.
- **Tamamlanma** yalnız SOP §29'daki seviyelerden biridir: WP Done · Module Done · Capability Done · Release Done —
  ya da `yok`. Karardan türetilmez (SOP §29.5).
- **Geçiş notu (CT-OPS-03 → CT-OPS-04):** `scripts/status_report.py` bugün eski biçimi üretir: tek "CT kararı"
  sütunu, içinde `decision` değeri. İki sütunlu çıktı CT-OPS-04'ün yeni kipiyle gelir; o zamana kadar betiğin çıktısı
  eski uyumluluk çıktısıdır. Yalnız yerelde duran bir pilotun çıktısı yayımlanmış kanıt değildir (§3).
- **Sıradaki tek eksik** bir cümledir. "Modülün bitmesi için yalnız bu kaldı" demek değildir; sıradaki adımdır.
- **Yüzde yazmak serbesttir, ama tek başına yasaktır.** Bir yüzde ancak altındaki satır listesiyle birlikte yazılır;
  liste yoksa yüzde silinir.

---

## 3. Kanıt nerede durur

- Kanıt **depoya işlenir ve gönderilir**. Yeri: `docs/records/audits/<yyyy-aa>/` (bkz. `docs-organization.md`).
- Rapor kanıtı **`yol@commit`** diye gösterir; commit, kanıtın üretildiği ağaçtır.
- **Kanıt sayılmayanlar:** kişisel makine yolu (`/Users/<kişi>/...`), geçici klasör (`/tmp`, `/private/tmp`),
  yalnız yerelde duran dal, ekran görüntüsü olmayan "çalıştı" cümlesi, sohbet çıktısı.
- Kanıt bir test koşusuysa: koşunun çıktısı (trx/log/json) kaydın içinde durur; "testler yeşildi" tek başına E0'dır.

---

## 4. Bayatlık

- Kanıt koşusundan **önce** dal ana dalla senkronlanır.
- Rapor, dalın ana daldan kaç commit geride olduğunu yazar: `git rev-list --count <dal>..origin/main`.
- Çok geride bir dalda koşan test, bugünkü sistemi değil o günkü sistemi kanıtlar; CT bunu kanıt saymaz.

---

## 5. Terim disiplini

- Yeni statü adı **uydurulmaz**. Kullanılabilecek kelimeler: `E0`–`E5` (SOP §23), SOP §29'daki Done seviyeleri,
  module pack durumları (`draft` · `approved` · `ready-for-dev` · `in-progress` · `review` · `done`).
- Yeni bir terime gerçekten ihtiyaç varsa önce SOP'a ya da bu dosyaya yazılır, sonra raporda kullanılır.
- Kısaltma kullanıldıysa raporun kendi içinde bir kez tanımlanır ve standart karşılığı yazılır.

---

## 6. CT karar kaydı künyesi

Her CT karar kaydı (`docs/records/audits/<yyyy-aa>/...md`) dosyanın başında şu künyeyi taşır:

```yaml
---
module: MOD-0183
work_package: WP-MVP6-LOGISTICS-01
evidence_level: E4
decision: accepted        # accepted | rejected | conditional
commit: 4a8d4d4b
---
```

Künye insan için değil, §7'deki betik için vardır: kararların listesi elle sayılmaz.

### 6.1 Künye v2 — yeni Control Tower'lar (CT-OPS-03)

Katı kuyruklu (kayıtta `strict=yes`) Control Tower'ların kabul kayıtları bu künyeyi taşır. Eski (v1) künyeler yerinde
geçerlidir; göç yoktur. Künye kapalı bir bloktur; yalnız aşağıdaki alanlar, her biri tek satırda ve bir kez yazılır;
değerde `#` olmaz (eski okuyucu onu keser). Örneğe açıklama eklenmez:

```yaml
---
format: rep-001-v2
module: MOD-0151-FU01
work_package: WP-ORNEK-01
queue: ornek-ct
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/ornek/work-packs/WP-ORNEK-01.md@0123456789abcdef0123456789abcdef01234567:6
decision: accepted
completion: WP Done
conditions: -
commit: 0123456789abcdef0123456789abcdef01234567
evidence: docs/records/audits/2026-10/ornek/run.log@0123456789abcdef0123456789abcdef01234567
acceptance_list: docs/records/audits/2026-10/ornek/kabul-listesi.md@0123456789abcdef0123456789abcdef01234567
date: 2026-10-15
supersedes: -
verifier: bağımsız doğrulayıcının kimliği
implementer: geliştirme ajanının kimliği
---
```

| Alan | Kural |
|---|---|
| `module` | Tam kimlik (`MOD-nnnn`, `MOD-nnnn-FUn` ya da `ortak:…`); FU ana modüle indirilmez; kuyruk satırının modülüyle aynı |
| `work_package`, `queue` | Kuyruk satırının kimliği ve kayıtlı kuyruk |
| `evidence_level`, `required_evidence_level` | `E0`–`E5`; elde edilen seviye gerekli seviyenin altında olamaz; gerekli seviye E0 olamaz (SOP §23, §23.1) |
| `required_evidence_source` | `yol@commit:satır`. Dosyanın künyesinde tek bir `status` vardır ve şu değerlerden biridir: `approved` · `ready-for-dev` · `in-progress` · `review` · `done`; dosya bu WP'yi (`work_package`) gösterir ya da bu modülün paketidir; satır gerekli seviyeyi tek başına söyler. Eksik ya da belirsizse kabul durur |
| `decision` | §6'daki anlamıyla `accepted` · `rejected` · `conditional` |
| `completion` | WP Done · Module Done · Capability Done · Release Done · `yok`. Karardan türetilmez; `conditional` ve `rejected` kararda `yok` |
| `conditions` | Yalnız `conditional` kararda: her koşul `ne · sahip · YYYY-AA-GG`, birden çoksa `;` ile. Diğerlerinde `-` |
| `commit` | Doğrulanan kod ağacının commit'i. Kaydın kendisi ayrı bir commit'te durur; kuyruğun `record` sütunu onu `yol@commit` ile gösterir |
| `evidence`, `acceptance_list` | Commit'lenmiş `yol@commit`; kabul listesi §6.2 biçimindedir |
| `supersedes` | `-` ya da aynı kuyruk + WP + modülün önceki kaydının yolu. Döngü olmaz; iş başına tek yetkili kayıt vardır. En yüksek E seviyesi ya da dosya sırası seçim ölçütü değildir |
| `verifier`, `implementer` | Farklı olur (SOP K13). Beyandır, kimlik doğrulaması değildir |

Muafiyet (SOP K19) kabul kaydını otomatik geçirmez; sahip, son tarih ve kanıtla ayrı değerlendirilir.

```bash
python3 scripts/acceptance_record.py check <kayıt yolu>@<commit> --queue <kuyruk> --wp <WP> --module <modül>
```

Çıkış 0 yalnız kayıt ACCEPTED'e izin veriyorsa verilir. `ct_queue.py validate` katı kuyrukta her ACCEPTED satırı için
aynı denetimi çalıştırır. **Sınır:** betik kaydın ve kanıtın bütünlüğünü denetler; testleri yeniden koşmaz. Kabul
listesinin ve kanıtın anlamca yeterli olduğuna bağımsız doğrulayıcı ve CT karar verir.

### 6.2 Kabul listesi satır biçimi

| ID | Ne kontrol edilir | Neden | Nasıl | Beklenen sonuç | Ekran mı | Zorunlu | Gerçek sonuç | Sonuç | Kanıt |
|---|---|---|---|---|---|---|---|---|---|

- İlk yedi sütunu `product-owner` yazar; son üç sütunu bağımsız doğrulayıcı doldurur.
- `Ekran mı` ve `Zorunlu`: `evet` ya da `hayır`. `Sonuç`: `geçti` · `kaldı` · `denenmedi` · `N/A — <gerekçe>`.
- `geçti` satırında gerçek sonuç yazılıdır ve kanıt `yol@commit:satır` ya da içinde kriter kimliği geçen `yol@commit`
  olur. Gerçek sonuçsuz ya da kanıtsız "geçti" kanıt değildir.
- Başlıktan sonra ayırıcı satır gelir; tablonun dışında ölçüt satırı kalmaz; boş liste olmaz.
- `accepted` karar için zorunlu ölçütlerin hepsi `geçti` ya da gerekçeli `N/A` olur.
- Mevcut senaryo dosyaları değişmez; bu biçim yeni kabul listeleri içindir.

---

## 7. Tabloyu betik üretir

`scripts/status_report.py` module pack durumlarını, karar kaydı künyelerini ve git'teki geri kalmışlığı okuyup
§2'deki tabloyu basar:

```bash
python3 scripts/status_report.py --packs "execution/domains/*/module-packs/*.md"
python3 scripts/status_report.py --ref origin/feature/mvp6-logistics --packs "execution/domains/supply-chain-execution/module-packs/*.md"
```

Geliştirici tablo yazmaz, betiği çalıştırır ve çıktısını rapora yapıştırır. Betiğin bulamadığı şeyi **uydurmaz**:
kanıt kaydı yoksa hücre `kayıt yok` yazar. `kayıt yok` gören CT, sayıyı değil eksiği konuşur.

---

## 8. CT ne yapar

- Bu biçimde olmayan çok modüllü rapordaki sayıları **okumaz**; biçimi ister.
- Kanıtı §3'e uymayan satırı `E0` sayar.
- Tanımsız terim gören CT, terimin SOP karşılığını sorar; tahmin etmez.
