# Developer Enablement

## Purpose
`developer-enablement` domain'i, urun davranisi tasimayan fakat gelistirme hizini, standartlasmayi ve tekrar kullanimi artiran referans modul, scaffold kit ve delivery baseline'larini yonetir.

## Operating stance
- Bu domain production business capability sahibi degildir.
- Buradaki module pack'ler "gelistirme referansi" olarak kullanilir.
- Referanslar olgunlasmadan `.antigravity` altina kural olarak tasinmaz.
- Her referans, hangi senaryo icin ornek oldugunu acikca tanimlar.
- Frontend projesi icindeki `_reference` istisnasi haric, tum referans ownership'i bu domain altinda toplanir.

## In-scope examples
- Golden reference modul kit'leri
- Veri yogunluguna gore referans module varyantlari
- CRUD, DataTable, details, form ve CQRS baseline'lari
- Scaffold ve turetme script'leri
- Gelecekte eklenecek tum yeni reference module'ler

## Out-of-scope
- Canli business domain modulleri
- Production menu ownership
- Domain business objects
- `.antigravity/**` altindaki kalici kurallar

## Domain package contents
- `domain-config.md` — domain sinirlari ve repo kapsami
- `module-packs/` — her referans modul icin ayri execution dosyasi
- `decisions/` — naming, tasima ve referans stratejisi kararlari
- `controls/` — gelistirme sureci icin domain-ozel kontroller

## Initial direction
Ilk modul `Golden Reference Slim` olacaktir. Bu modul tamamlandiginda, gelecekteki moduller icin:
- kucuk/orta veri yogunluklu referans
- buyuk veri yogunluklu referans
- karmasik form/details referansi

gibi ek baseline'lar ayni domain altinda kataloglanabilir.
