# PVG altıncı dalga — bağımsız doğrulama: WP-0230-DL4D (AC-34)

> Control Tower kaydı, 2026-10-30. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır. Entegrasyon ya da kabul kaydı değildir.
> İş CT dalına birleştirildi (kart S-76). Dalga sonu PR'ı ile `main`'e girecek; `INTEGRATED` geçişi o PR birleştikten sonra yazılır.

## Teslim

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl4d-r1` (işçi oturumu pvg-dl4d) |
| İş commit'i | `4fbd30936`; HEAD^1 = Expected HEAD `99b5049fa` |
| CT dalına birleştirme | `edfde7292` (merge; ebeveynler `1165d58c3`, `4fbd30936`). PVG ve `ocelot.json` için `git diff 4fbd30936 edfde7292` boş. |
| DoR | `docs/records/audits/2026-10/pvg-wave6-dor-01/DOR.md` |
| Prompt | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL4D-v1.6.md` (sha256 `17324510…`); dağıtım kapısı 2026-10-30 çıkış 0 |

## Bağımsız ölçüm: KABUL EDİLEBİLİR

Ölçümü işi yazmayan bir CT ölçücüsü yaptı (alt ajan). Ölçüm ayrı bir worktree'de, `edfde7292`'de, ayrılmış HEAD ile yapıldı. Paket sha256'sı orada `ee175d00…` (revizyon 13, `main`'de onaylı).

- **Kapsam.**
  - Yalnız izinli üç dosya değişti:
    - `PvgCaseIntakeTriageEndpoints.cs`: +25 −1. Silinen satır yalnız `"delete"` satırına virgül eklenmesi.
    - `PvgCaseIntakeOptionsTests.cs`: +524.
    - `PvgServiceApiHostTests.cs`: +4. Üç kesin rota listesine options girişi ve tek bir yöntem iddiası.
  - `git diff --check` temiz.
  - Korunan yolların hiçbiri değişmedi.
- **Kod.**
  - `GetOptions` yalnız `PvgIntakeOptionSets`'ten okuyor; listeler `StringComparer.Ordinal` sırasında. Api projesinde seçenek kodu kopyası yok (grep 0).
  - Depo, izin kapısı ve servis bulucu çağrısı yok.
  - Eşleme `.RequireAuthorization()` taşıyor.
  - Kiracı denetimi `From(httpContext, requireActor: false, requireCorrelation: false)` ile yapılıyor. Geçersizse liste ucuyla aynı engelleme satırı çalışıyor. `From` ve `ToHttpResult` değişmedi.
  - `options` ayrılmış parça kümesine eklendi. `ocelot.json` değişmedi.
- **Testler.** Her `dotnet` komutundan önce yük ölçüldü; 1 dakikalık yük 1.79–3.72 arasındaydı.

  | Süit | Geçen | Başarısız | Atlanan |
  |---|---|---|---|
  | pvg-api (Api.Tests) | 298 | 0 | 0 |
  | pvg-regpv-base | 210 | 0 | 0 |
  | pvg-meddra-coding | 40 | 0 | 0 |
  | pvg-signal-management | 43 | 0 | 0 |
  | pvg-case-processing | 23 | 0 | 0 |

  - Altı adlı testin 11 satırı geçiyor.
  - `known_failures.py check` sonucu: pvg-api ve pvg-regpv-base için bilinen 0, yeni 0.
- **Sabotajlar.** Prompt'un bütün sabotajları uygulandı; her biri beklenen testi kırmızıya çevirdi, sonra geri alındı. Uygulanamayan sabotaj yok. Ek olarak ürün rolü sırası sabotajı da kırmızı verdi.

  | Sabotaj | Kırmızıya dönen testler |
  |---|---|
  | S1a, S1b, S1c | T1 ve T6 |
  | S2 | T2 |
  | S3 | T3 |
  | S4a | T4 |
  | S4b | T1, T5 ve T6 |
  | S5a, S5b, S5c | T5 (ve T1/T6) |
  | S6a | T6'nın PUT, triage ve route satırları |
  | S6b | sekiz test, `PvgServiceApiHostTests`'teki üç rota testi dahil |

  Son yeşil koşu: 298/298. Worktree temiz bırakıldı.

## Notlar (DÜŞÜK, kabulü etkilemez)

- **N-1:** `PvgCaseIntakeOptionsTests.cs:190-202` (`ValidUpdateJson`). S6a'da PUT satırı 404 yerine 400 aldı; gövde bugünkü güncelleme doğrulamasından geçmiyor. Koruma yine kırmızı verdiği için ayırt edici, ama PUT satırı servise kadar giden yolu ölçmüyor. Takip: AC-28 ya da DL-2 işinde test gövdesi güncel sözleşmeye çekilebilir.
- **N-2:** Rota metadata testleri `RequireAuthorization` varlığını ölçmüyor; bu ölçümü yalnız T3 yapıyor. Paket ölçümü T3'te istiyor. Bilgi notu.

## Ölçülmeyenler

- Expected HEAD'deki taban test sayısı ayrıca koşulmadı (287 = 298 − 11, çıkarım).
- E4 canlı kanıt bu ölçümün kapsamı değil. CT koşusu `docs/records/audits/2026-10/pvg-dl4d-live-01/run-02` (commit `c09dd1d74`, kod `edfde7292`, çıkış 0).
