# Mobil notu — hekim raporu: endikasyon potansiyeli ne zaman sorulur (2026-10-14)

> **Kimden:** CRM (W4-FIX-b ajanı; CT mobil sayfasına taşıyacak). **Kime:** Android ekibi.
>
> **Kapsam:** WP-VW-W4 · W4-FIX-b (yerel E4 bulgusu F-5). Ayrıntı: [WP-VW-W4-report-v2](../../WP-VW-W4-report-v2.md) §2.2, §2.5 ve "W4 yerel E4 bulguları".
>
> **Kural:** mevcut uçlar ve alanlar değişmedi. Bir pre-fill alanı ve bir uyarı kodu eklendi.

## 1. Değişen kural
Bir ürünün **endikasyon potansiyeli** (endikasyon × profil başına aylık hasta, ya da "Bu endikasyonlarda hasta görmüyor") yalnız şu iki koşul birlikte sağlanınca sorulur ve zorunlu olur:
1. Değer vadesi gelmiş olmalı (90 gün) ya da temsilci "güncelle" demiş olmalı (`potential.updated = true`).
2. Ürünün **onaylı endikasyon × profil satırı** olmalı (Kavram Grafiği; pre-fill `indicationRows[].items` boş değil).

İkinci koşul YENİ. Satırı olmayan ürüne "hasta görmüyor" demek yanlış beyandı ve doktorun potansiyel sınıfını / fırsatını bozuyordu.

Değişmeyenler:
- Doktorun aylık toplam hasta sayısı (`doctor.totalMonthlyPatients`, profil adımı) ve 90 gün kuralı.
- Pay kovası (`shareBucketCode`), her ürün için yine zorunlu.
- Satırı olan ürünlerde eski kural aynen geçerli: 90 gün, endikasyon toplamı ≤ doktor toplamı, sınıf ve fırsat matrisi.

## 2. Pre-fill: `productSections[].potentialAsked` (yeni, salt okunur)
`GET api/crm/visit-report/v2/prefill?plannedVisitId=…` yanıtında, hekim raporunda her ürün bölümüne `potentialAsked` (bool) eklendi.

| değer | anlamı | istemci |
|---|---|---|
| `true` | satır var ve değer vadesi gelmiş (ya da taslakta `updated`) | endikasyon satırlarını + "hasta görmüyor" düğmesini zorunlu göster |
| `false` | satır yok, ya da vadesi gelmemiş | satır yoksa yalnız bilgi notu ("endikasyon listesi tanımlı değil; doktorun toplamı profil adımında"); satır varsa son değer + "güncelle" |

- İstemci kendi hesabını YAPMASIN; bu bayrağı kullansın. Satır var mı sorusu için `indicationRows[].items.length > 0` okunabilir.
- Temsilci "güncelle"ye basarsa (`potential.updated = true`) yalnız **satırı olan** üründe soru açılır.
- Alan sunucuya geri gönderilmez; gönderilirse yok sayılır ve hiçbir yazmada saklanmaz.
- Eczane raporunda bu alan yoktur (`null`).

## 3. Gönderim (`POST v2/submit`)
- Satırı olmayan ürün için `missing[]` içinde artık `{ section: "product", field: "potential" }` gelmez.
- Satırı olmayan ürün için gövdede yine de `potential` gelirse (`rows` ya da `noPatientsInIndications = true`):
  - potansiyele yazılmaz, raporda saklanmaz (`potential` boşaltılır);
  - gönderim başarılı olur;
  - yanıtın `data.warnings[]` dizisine `potential_ignored_no_indications:{productId}` eklenir.
- Pay kovası aynı üründe yine kaydedilir.

**Yeni uyarı kodu:** `potential_ignored_no_indications:{productId}`. Bu bir hata değil, bilgidir. Uygulama bilinmeyen uyarı koduna çökmemeli.

## 4. Eski davranışla fark
| durum | önce | şimdi |
|---|---|---|
| satırsız ürün, vadesi gelmiş | `missing[]`'de `potential`; temsilci "hasta görmüyor" demeye zorlanıyordu | sorulmaz, zorunlu değil |
| satırsız ürün, gövdede `noPatientsInIndications = true` | potansiyele `true` yazılıyor, 90 gün yeniden başlıyordu | yok sayılır + uyarı; kayıt ve 90 gün değişmez |
| satırlı ürün | — | aynı |

Var olan rapor ve potansiyel kayıtlarına geriye dönük düzeltme yapılmadı.
