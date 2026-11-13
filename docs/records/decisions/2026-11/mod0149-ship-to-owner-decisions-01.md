---
status: taslak — sahip (Beste) okuması bekliyor (CT-APR-01 onayıyla birlikte); bu belge yeni bir karar vermez, alınmış kararları kayda geçirir
decided: 2026-11-13 (K-12, K-13, K-16, K-18, K-19; Q-DEPOT-SOR), 2026-11-13 (K-14, tüketici WMS)
module: MOD-0149
work_packages: MOD-0149 sevkiyat adresi eki (pack §32, DL-3; DL-4 taslağı §32.10), CRM-ACCOUNT-LOOKUP 1.1.0 taslağı
decision_ids: K-12, K-13, K-14, K-15 (öneri), K-16, K-17 (açık → DL-4), K-18, K-19 (CT önerisi; Beste kabul 2026-11-13)
---

# MOD-0149 sevkiyat adresi eki — sahip kararları K-12…K-19

> **Karar sahibi:** commercial-suite sahibi (Beste, K2); K-14 tüketicinin (WMS ekibi) kararıdır.
> **Neden bu kayıt:** Bağımsız inceleme `docs/records/reviews/MOD-0149/2026-11-13-0a9fd4ef.md` F-3: kararlar pakette ve
> sözleşmede yalnız ad ve tarihle anılıyordu. Bu dosya o kaynaktır; paket §32.8 ve sözleşme taslağı §8 / x-record buraya
> atıf verir.
> **Sınır:** Bu kayıt push, PR, birleştirme, kod ya da dağıtım izni değildir.

## Kaynaklar

- **Tüketici isteği (2026-11-13):** WMS ekibinin Beste'ye yazılı isteği "Çıkış sevkiyatı için CRM account adresinin
  okunması" (MOD-0178, çıkış sevkiyatı formu v2): alıcıyı CRM hesabından seçmek, kimlik + adres kopyası tutmak; seçenekler
  (a) CRM-ACCOUNT-LOOKUP 1.1 ve (b) ayrı `ship-to` ucu; ülke / şehir kod mu ad mı; çoklu teslimat adresi; izin anahtarı;
  tarih; MVP-6 ile aynı kaynak. Metin CT · CRM oturumunda (780dee5f) sahip tarafından yapıştırıldı; repoda dosyası yok.
  Not: MOD-0178 paketi (`execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md`) bugün
  bu okumayı içermez; tüketici "aday (istek)" sayılır (inceleme F-3).
- **K-12…K-19:** CT · CRM oturumunda (780dee5f) sahibe sorularak alındı; ilk yazılı hâli sözleşme taslağı commit'leri
  `91195fdc3` (K-12, K-13), `cfd441181` (K-18), `65d9c92ba` (K-16, ekran), `f359493a5` (K-14) ve paket `cd7327923`.
  Oturumun soru-cevap kaydı repoda yok; bu kayıt metni kaynak alır ve sahibin okumasıyla kesinleşir.

## Kararlar

| # | Konu | Karar | Tarih / sahip |
|---|---|---|---|
| K-12 | Tasarım | CRM-ACCOUNT-LOOKUP 1.1 içinde, yalnız ekleme olarak ayrı uç U-4 `GET /api/crm/accounts/{id}/ship-to`; U-1 / U-3'e adres eklenmez | 2026-11-13, Beste |
| K-13 | İzin anahtarı | `crm.account-ship-to.read`; `crm.account.read` ve `crm.account.lookup-validation` U-4'e yetmez | 2026-11-13, Beste |
| K-14 | Aktif olmayan hesaba sevkiyat | Yeni sevkiyatta yalnız `active` hesap seçilir; seçimden sonra pasifleşen hesapta mevcut sevkiyat sürer; WMS kayıt ve "sevke hazır" adımında U-4'ü yeniden okur ve uyarır. CRM'den yalnız `status` istenir | 2026-11-13, WMS ekibi (tüketici) |
| K-15 | Görünen ad | Öneri: U-4 kod döner; tüketici Reference Data'dan (`country`, `city`, `district`) çözer | öneri (karar değil) |
| K-16 | Ana adrese posta kodu | Eklenecek (`Account.PostalCode`), DL-4 içinde | 2026-11-13, Beste |
| K-17 | Ülke / şehir / ilçe kodlarının yazmada doğrulanmaması | Açık → DL-4'te yeni ve değişen adreslerde Reference Data doğrulaması | açık |
| K-18 | Çoklu teslimat adresi | Yapılacak (DL-4); ekran mockup'sız, hesap detayının mevcut sekme yapısında standart liste + form; sözleşme gövdesi bugünden liste | 2026-11-13, Beste |
| K-19 | Yeni anahtarı alan CRM rolleri | CRM Admin alır; öteki CRM rolleri almaz | KAPANDI (Beste, 2026-11-13; CT önerisi, sahip "uygun" dedi). Pack §32.8 "ÖNERİ (CT) — CT-APR-01 onayıyla kesinleşir" yazar; pack sürümü (8749bc2a) incelendiği hâliyle kalsın diye değiştirilmedi, CT-APR-01 onay satırıyla karşılanır |
| Q-DEPOT-SOR | Ecza depoları ve dağıtıcılar | Ecza depoları (toptancı) CRM Account, tip `wholesaler`; ürünümüzü bir pazarda bizim adımıza dağıtan firma `distributor` (Blueprint 8.8: Customer (Account) SoR = MOD-0149; partner katmanı MOD-0282). Kendi depolarımız (warehouse) CRM'de değil | 2026-11-13, Beste |
