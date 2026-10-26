# TEST-HIZ-01 · Platform: eşzamanlı plan modülü askıya alma yarışı, düzeltme ve inceleme (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, 2026-10-26, "platform düzelt" (XMC-001 onayı).
> **Kök neden:** önceki kayıt `2026-10-26-test-hiz-01-koruyucu-testler.md` §3.
> Yerel koşular yük kuralıyla yapıldı: 1 dk yükü 12'nin üstündeyse bekle; tek proje, `--filter`.

## 1. Düzeltme

- **Önceki akış:** `SuspendPlanLineAsync` kapı kararını bir okumayla veriyor (`RefuseProjectionDisableAsync`), var olan override'ı ikinci bir okumayla arıyordu. Birinci istek ikisinin arasında commit ederse ikinci istek o taze override'ı süresi dolmuş sanıp devralıyordu. Sonuç: iki 204, tek satır, çift denetim kaydı ve çift sürüm artışı.
- **Yeni akış:** `RefuseProjectionDisableAsync` karar verdiği satırları da döndürüyor; handler yalnız bunları kullanıyor. İkinci okuma yok.
- **Karşı istekler artık ne alıyor:**
  - Kapıdan önce okuyan istek insert eder ve benzersiz indekse takılır → 409 `ENTITLEMENT_STALE`.
  - Sonra okuyan istek kapıda reddedilir → 409 `ENTITLEMENT_ACTION_NOT_OFFERED`.
  - Süresi dolmuş override'ı devralma, okunan sürümle yapılıyor. Rakip araya girerse → 409 `ENTITLEMENT_STALE`.
- **Testler** (test host'unun deposuna kanca: bir okuma döndükten sonra öteki istek sonuna kadar koşar):
  - `A_second_suspend_that_read_before_the_first_one_committed_is_refused_as_stale_and_writes_nothing`
  - `A_second_takeover_of_an_expired_override_that_read_before_the_first_one_committed_is_refused_as_stale` (kapalı ve açık override; açık olanda yer iadesi tam 1)

## 2. Ölçüm

| Koşu | Yük | Sonuç |
|---|---:|---|
| Düzeltilmiş kod, sınıf | 2,37 | 48/48 yeşil |
| S1: ikinci okuma geri | 6,63 | 3 kırmızı: yeni testlerin üç durumu |
| Düzeltilmiş kod, sınıf, sonra | 10,59 | 48/48 yeşil |
| `FullyQualifiedName~Entitlement` | 10,91 | 282/282 yeşil |
| Sınıf × 10 (ilk test eklendikten sonra) | 4,3–7,8 | 10/10 yeşil |

S1, üretim dosyasına tek satırlık sabotaj. Dosya sonra bayt bayt geri yüklendi (`cmp`).

## 3. Bağımsız inceleme (alt ajan, yazarın sonucu verilmeden): UYGUN

İnceleyici kendi yoklamalarını yazdı. Her senaryo hem yeni hem eski kodla koşuldu:
- **A, E:** ikinci istek okuduktan sonra birinci commit ediyor; E'de devralınan override kapalı ve açık. Yeni kod 204 + 409 STALE; eski kod iki 204.
- **B, C, D:** okuma sırası farklı (açık işlemin içinde, commit'ten sonra): yeni ve eski kod aynı.
- **G:** tek askıya alma: aynı.
- **H:** kiracı ayrımı ve partner 403: aynı.
- **I:** kanca varsayılan arayüz üyesini iletiyor.
- **S (stres):** 200 tur gerçek eşzamanlılık: iki kodda da 0 çift 204. Ayırt eden kanıt deterministik yoklamalar.

**Bulgular**
- 🟡 **Önceden var, bu değişiklikle gelmedi.** Süresi dolmuş override'ı devralırken ikinci istek birincinin açık işlemine denk gelirse 500 alıyor (`PlatformTransactionUnavailableException`). Nedeni: `TenantModuleEntitlementRepository.UpdateAsync` WriteConflict'i (112) eşzamanlılık hatasına çevirmiyor; `CreateAsync` çeviriyor. Ayrı iş.
- 🟡 **Süresi dolmuş override yolunun testi yoktu.** Bu kayıttaki ikinci test eklendi.
- 🟢 **Kanca okunup ayrıca temizleniyordu.** `Interlocked.Exchange` ile tek adıma çevrildi.
- 🟢 **Olay sayılmadı.** Test sunucusu olay yazmıyor (`SilentEvents`), bu yüzden çift olay sayılamadı. Denetim kaydı ve sürüm sayıları aynı işlemin içinde yazılıyor.
