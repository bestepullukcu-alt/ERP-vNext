---
record_id: TREASURY-PRODUCER-ANSWER-TRM11-01
kind: kaynak proje cevabı (ERP-vNext Treasury tarafı) — TRM-11 "MOD-0289 ve MOD-0217 kırık testlerinin nedeni ve doğru commit"; onay değildir
received_at_local: 2026-10-26
received_from: kaynak proje tarafı; sahip (K1) sohbete yapıştırdı
measured_by_producer_at: kaynak depo `/Users/natig/Projects/ERP-vNext`, kanıt klasörü `docs/records/audits/2026-10/treasury-build-evidence-20261023/` (salt okuma — üreticinin beyanı; dosya değiştirilmedi, yeni test koşulmadı)
cited_evidence: SUMMARY-v1.2.md sha256 `99e22ac790dbf8d5…` (son değişiklik commit `9998654d`); runs/R3/run-record.json sha256 `b9e53a36846fb5a4…`; runs/R3/Diten.MdmService.Application.Tests.csproj.trx (Treasury CT varlığını ölçtü, içeriği okumadı)
queue: treasury-intake TRM-11
---

# TRM-11 cevabı (özet; kaynak metin sohbette)

**Sonuç:** iki paketin işlem güvenceleri için doğrulanmış tam yeşil koşum yok.

## (a) Kırmızıların nedeni (üreticinin beyanı)

| Paket | Bulgu |
|---|---|
| MOD-0289 — 61 test | Fixture kurulumu eksik. F02'nin 43 testi sahipli Mongo koşullarında duruyor (output-root, veritabanı öneki, replica-set adı ve sahiplik işareti sağlanmamış); PAIR'ın 18 testinde `PAIR_TEST_MONGO_URI` eksik. Ürün koduna ulaşmadıkları için işlem güvencelerini doğrulamıyorlar. |
| MOD-0289 — kalan 56 başarısızlık | İkisinde doğrudan controller çağrısı admission filtresini atlıyor, kimlik kontrolü istisna üretiyor. **53 ContractBootstrap + 1 SeedBaseline hatasının kök nedeni belirlenmemiş**; yalnız ortam sorunu sayılamaz. |
| MOD-0217 — 18 koşmayan test | `MOD0217_CONTRACT_AGGREGATE_TEST_URI` ve `MOD0217_CALLBACK_TX_TEST_MONGO_URI` koşum ortamında yok. |
| MOD-0217 — 6 başarısız test | Üçünde aynı adlı indekslerin `unique` / `sparse` özellikleri çakışıyor; ikisinde 200/404 yerine 503; birinde değişmezlik karşılaştırması bozuluyor. Test sınıfları aynı sabit veritabanını paylaşıyor, bazıları indeksleri kasıtlı değiştiriyor — testler arası etkileşim güçlü olasılık, **altısının doğrulanmış kök nedeni değil**. |

## (b) Doğru referans commit

S = `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (bağlanan kaynak ve koşumlar bu commit'e ait). R = `e4c93c29…` alternatif yeşil
referans **olamaz**: MOD-0289'un 86/86'sı farklı kaynak / kapsamdır, S'deki F02 / PAIR güvencelerini doğrulamaz; `218acaf5` ile
bağlı dosyaların aynı olması bağımlılıkların ve test davranışının aynı olduğunu kanıtlamaz.

## (c) Yeşil commit + komut

İstenen işlem kapsamının tamamı için doğrulanmış yeşil commit + komut **yok**. MOD-0217'nin kayıtlı başarısız koşumu:
`dotnet test services/DitenMdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj --no-restore -c Debug`
(ortam ve seçenekler `runs/R3/run-record.json`).

## Recovery için sonuçlar (Treasury CT)

1. **MOD-0217:** sözleşmede işlem atomikliği "kaynakta testle doğrulanmış" sayılmaz. Pack yazılırsa işlem güvenceleri yalnız
   Recovery'nin kendi gerçek replica set testleriyle kabul edilir (fixture'lar tam, indeks değiştiren testler yalıtılmış). D19'un
   TRM-11 koşulu bu cevapla karşılandı; pack hâlâ "Document Management" kapısının bir modüle çözülmesini ve MDM domain sahibinin
   onayını bekler (D19/1).
2. **MOD-0289:** mevcut pack zaten kaynak güvencelerini kabul etmiyor (kod alma izni yok, D27/5; işlem ve çit AC'leri DL-1 / DL-2
   kabul kapılarında Recovery testleriyle). Cevap bunu destekler; pack değişikliği gerekmez.
3. Kaynak koşumlarındaki 503 ve indeks çakışması bulguları, Recovery testlerinde paylaşılan veritabanı ve sabit indeks adı
   tasarımından kaçınmak için WP prompt'larında hatırlatılır (lane kuralı: her suite kendi Mongo URI'si).
