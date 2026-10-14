# Mobil notu — eczane raporu: rakip ürünleri stok GSKU'larından, GSKU araması ürün adıyla (2026-10-14)

> **Kimden:** CRM (W4-FIX-d ajanı; CT mobil sayfasına taşıyacak). **Kime:** Android ekibi.
>
> **Kapsam:** WP-VW-W4 · W4-FIX-d (yerel E4 2. tur bulguları G-8, G-9). Ayrıntı: [WP-VW-W4-report-v2](../../WP-VW-W4-report-v2.md) "W4 yerel E4 — 2. tur bulguları".
>
> **Kural:** uç adresleri ve alan adları değişmedi. Bir yanıtın içeriği genişledi ve bir uyarı kodu eklendi.

## 1. GSKU araması (MDM `GET api/gskus?search=`)
- Arama artık şunlardan birinde eşleşir:
  - GSKU kodunun **herhangi bir kısmı** (önce yalnız koddan başlayan eşleşme vardı);
  - global ürün **adı**;
  - global ürün **kodu**.
- Büyük / küçük harf ayrımı yoktur; Türkçe İ / ı / i / I birbirinin yerine geçer. Örnek: `konset` ve `KONSET`, KONSET'in bütün ambalajlarını bulur.
- Sayfalama (`pageNumber` / `pageSize` ≤ 100) ve kiracı sınırı aynıdır.
- Eczane stok / satış satırına GSKU eklerken ve numune GSKU listesinde aynı uç kullanılır; yalnız eşleşen kayıt sayısı artar.

## 2. Eczane raporu ön doldurma (`GET api/crm/visit-report/v2/prefill`)
`competitors[]` (ürün başına MDM rakip listesi) önce yalnız **planın ürünleri** için gelirdi. Artık eczane raporunda iki kaynaktan gelir, bu sırayla:
1. planın ürünleri;
2. kayıtlı taslaktaki **stok ve satış satırlarının GSKU'larının global ürünleri**.

Plan dışı / ürünsüz eczane ziyaretinde taslağa stok GSKU'su girildiyse, o ürünün rakip listesi de gelir.

Taslağa sonradan yeni bir GSKU eklenirse, o ürünün rakip listesini istemci kendisi okur: MDM `GET api/mdm/competitor-products/lookup?productId=` (yetki `mdm.competitor-products.read`; Web de böyle yapıyor).

## 3. Tamlık kuralı ve öneri ürünü (`POST v2/submit`)
- "Rakip ya da rakip yok" sorusu (`missing[]` içinde `pharmacy` / `competitors`) ve temsilcinin rakip önerisinin MDM'de bağlandığı ürün, AYNI üründen belirlenir:
  - **planın ilk ürünü**;
  - o yoksa **ilk stok satırının GSKU'sunun ürünü**;
  - o da yoksa ilk satış satırının GSKU'sunun ürünü.
- Hiçbiri yoksa eski davranış geçerlidir: soru sorulmaz; öneri raporda bekler ve `competitor_proposal_pending:{ad}` uyarısı döner.
- **Yeni uyarı kodu:** MDM GSKU → ürün eşlemesini okuyamazsa:
  - eski kural uygulanır (yalnız planın ürünleri);
  - gönderim yine başarılı olur;
  - `data.warnings[]` dizisine `pharmacy_products_unavailable` eklenir.

  Bu bir bilgidir, hata değildir; uygulama bilinmeyen uyarı koduna çökmemeli.

## 4. Eski davranışla fark
| durum | önce | şimdi |
|---|---|---|
| ürünsüz eczane ziyareti + stok GSKU'su | rakip listesi yok, öneri bekliyor | rakip listesi GSKU'nun ürününden; öneri MDM'ye o ürünle gider |
| plan ürünü var | planın ilk ürünü | aynı |
| MDM GSKU okunamıyor | — | eski kural + `pharmacy_products_unavailable` |
| GSKU araması "konset" | boş | KONSET ambalajları |
