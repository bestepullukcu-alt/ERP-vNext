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

| Modül | Kapsam cümlesi | Kanıt seviyesi | Kanıtın yeri | CT kararı | Sıradaki tek eksik |
|---|---|---|---|---|---|
| MOD-0183 | Sevkiyat oluştur/listele/geçiş, tenant + RBAC | E4 | `docs/records/audits/2026-09/mod-0183-r1-evidence/@4a8d4d4b` | WP Done (SOP §29.1) | canlı ingress doğrulaması |

Kurallar:

- **Kapsam cümlesi** tek cümledir ve neyin kabul edildiğini söyler; "modül tamamlandı" demek değildir.
- **Kanıt seviyesi** yalnız `E0`–`E5`'ten biridir (SOP §23).
- **Kanıtın yeri** `depo-yolu@commit` biçimindedir. Başka bir şey kanıt değildir (bkz. §3).
- **CT kararı** yalnız SOP §29'daki seviyelerden biridir: WP Done · Module Done · Capability Done · Release Done —
  ya da `karar yok`.
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
