# PVG dördüncü dalga — bağımsız doğrulama: WP-0230-DL6 ve WP-0230-DL3D

> Control Tower kaydı, 2026-10-27. Bu kayıt kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Entegrasyon ya da kabul kaydı değildir.
> İki iş, sahibin "tek-pr" kuralı gereği PR açılmadan CT dalına birleştirildi (kart S-76). Dalga sonu PR'ı ile `main`'e girecekler. `INTEGRATED` geçişi o PR birleştikten sonra yazılır.

## Teslim (kart S-63, S-76)

| WP | İşçi dalı | İş commit'i | Taban (HEAD^1) | CT dalına birleştirme |
|---|---|---|---|---|
| WP-0230-DL6 | `feature/pvg/wp-0230-dl6-r1` | `bacaa077f` | `17b7ff220` | `f483536bc` (CT dalı birinci ebeveyn) |
| WP-0230-DL3D | `feature/pvg/wp-0230-dl3d-r1` | `48efc3847` | `7e4a50918` | `420473857` (CT dalı birinci ebeveyn) |

`git merge-tree` iki dalın birbiriyle ve CT dalı ucuyla birleşimini önceden ölçtü: üç birleşimde de çakışma yok.

## WP-0230-DL6 (AC-4) — KABUL EDİLEBİLİR

Ölçüm yazardan bağımsız bir CT ölçücüsü tarafından yapıldı.

- Kapsam: yalnız izinli dört dosya değişti; `git diff --check` temiz.
- AC-4 taraması paketteki komutla birebir koşuldu: tam 10 satır, hepsi `pvg.case-intake-triage.*`. `pvg.mod0230` geçen satır yok.
- "Yalnız önek" ölçümü: tabandaki iki test dosyasına `sed` ile önek değişimi uygulandı; sonuç daldaki dosyalarla bayt bayt aynı (37 satır).
- Testler (yük 5,5–7,5): RegPvBase 210 (209 + P1), Api 228; CaseProcessing 23, MeddraCoding 40, SignalManagement 43; `known_failures` 0 ve 0.
- Ölçücünün kendi sabotajları, her biri adı verilen testleri kırmızıya çevirdi:
  - Route sabitini eski aileye döndürmek: 10 test.
  - Read ↔ Update değer takası: 31 test (şekil testi beklendiği gibi yeşil kaldı; bunu P1 yakalıyor).
  - Fazladan bir anahtar: şekil testi ve P1.
  - Bir sabiti `pvg.mod0231` önekine çevirmek: 4 test.

## WP-0230-DL3D (AC-10) — KABUL EDİLEBİLİR

- Kapsam: izinli dört dosya değişti. `Program.cs` farkı boş. Depoya fikstür dosyası yazılmadı. Commit edilmiş yapılandırmada `ApprovalReference` yok.
- Host kuralı:
  - Kuralların sırası 1 → 2 → 3.
  - Biçim `^RTA-MOD0230-[0-9]{3}\z`; `CultureInvariant`, harfe duyarlı, kırpma yok.
  - Ret iletisi sabit metin; değer iletiye ya da health yanıtına yazılmıyor.
- Testler (yük 5,1–8,2): Api 262/262, düz ve tr-TR; RegPvBase 209; diğer PVG süitleri aynı; `known_failures` 0 ve 0.
- Ölçücünün kendi sabotajlarının her biri kırmızı verdi:

| Sabotaj | Kırmızı olan |
|---|---|
| Üçüncü kuralı kaldırmak | A1, A2, A6 |
| Değeri `Trim()` ile denetlemek | A2'nin boşluk satırları |
| Biçim regex'ini gevşetmek | A2'nin 7 satırı |
| Kimliği künye yerine gövdede de kabul etmek | C2 |
| Kayıt dizini yokken istisna atmak | C1, C1b ve 96 tarama fikstürü |

- Bilgi notları:
  - Prompt'un madde 14(d)'si o dosyada 4 satır bekliyordu; doğrusu 3. Dördüncü satır öteki test dosyasında; işçi doğru yaptı.
  - C2'nin "yanlış yer" satırı `docs/records/notes` kullanıyor.
  - Regex'te `$` yerine `\z` kullanılmış; bu daha sıkı.

## Mimari süit

Her iki işte de 3 kırmızı test var ve ikisi tabandakiyle aynı: `AuditTrailStandardTests` ×2 ve `DocsPathGuardTests`. İletiler iki koşuda bayt bayt aynı. Kaynakları:
- `Diten.Platform`'un iki imza komutu,
- `docs/analysis` yolu.

İkisi de `main`'den geliyor; `tests/known-failures.tsv` güncel değil. PVG ile ilgisi yok. Durum "Yönetici · Ofis" aracılığıyla kurulum ve Platform sahibine bildirildi.

## CT dalında birleşik ölçüm

DL6 ve DL3D CT dalına birleştikten sonra (`420473857`), yük 4,3–4,6 iken:

| Ölçüm | Sonuç |
|---|---|
| Api | 262/262 |
| RegPvBase | 210/210 |
| `known_failures` | 0 / 0 |
| AC-4 taraması | 10 satır, hepsi yeni aile |

## Kanıt seviyesi

İki iş de E2 kanıt üretti. Gerekli seviye DL-6 için E5, DL-3 için E3. Kabul, canlı kapı açılana kadar açık kalır.
