# Ortak regresyon kaydı — salt okunur kök neden

2026-11-20. Araştırmacı `pvg_dl12_readiness`, ürün veya kayıt dosyası değiştirmedi. CT bağımsız görüşü ayrıca istiyor.

`origin/main@2de950edcaeb9d8a153465e5f29d7c71eeb31144` ve CT `5b81b89a2fb43c63300e0f10ca5f5654e7d13750` ağaçlarında izlenen `*.xml` sayısı 0. İki ref'te `tests/known-failures.tsv` blob'u aynı: `6038f88ef70db0bedfbde183527f8022b9ca0c64`; guard da aynı. Son 28 XML, `d354900b44a93e22321c96c5edf21c6ca453fa73` (2026-11-16, EES archive batch 1) ile silinmiş; dört eski UI taslağının yedi dildeki audit `SharedResource.*.resx.fragment.xml` dosyalarıdır.

Ledger satır 4–8 beş `fixed` kaydın rerun_trigger alanında `*.xml` kalmış. `scripts/known_failures.py:355–359` her glob'un mevcut izlenen bir dosyaya eşleşmesini ister; :623–634 suite ayrımından önce bütün kaydı doğrular. Dolayısıyla `Diten.Web.Tests` filtresi de durur. Ana dal hükmü test tekrarıyla değil sabit Git ağaçları ve aynı kod koşuluyla çıkarılmıştır.

Tek en küçük öneri: yalnız bu beş fixed satırın rerun_trigger hücresinden `,*.xml` öğesini kaldırmak. Diğer tetikleyiciler, hücreler, statüler, parmakizleri, guard ve `DocsPathGuardTests.cs` XML taraması korunur. Sahte XML ekleme, alternatif kayıt veya guard gevşetme yoktur. Ortak ledger PVG'nin izinli yolları dışındadır; ayrı sahip/kapsam onayı alınmadan uygulanmaz. Bu belge onarım veya izin kaydı değildir.
