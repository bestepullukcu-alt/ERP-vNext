# Windows hazırlığı — CT birleştirmesi

2026-11-20 (Asia/Baku). Bağımsız aday E2 UYGUN kaydı `ccbd4f6d1aa73de53710dd41d6b5822bcf221fc7` içinde; kanıt yeni normal395/395/35Mongo/3hedef ve G5/G6 derlenmiş RED/özgün GREEN çiftleridir. Yedi işçi mutantı ve kaynak/metot/Unix koruması ayrıca denetlenmiştir.

Yalnız doğrulanmış `fee7d0132e3862ded7025465e311f263e068f7aa` CT dalına ordinary no-ff merge ile alındı. Merge `0b03b35075caba7e353e64f6f9fc7936f53d0d87`; birinci ebeveyn `754ae2724cf847383ecc6829850f1aadc3d614b2`, ikinci ebeveyn işçi commit'i; tree `bebcdcfe886f4abcbfc15f2a74711333093e47c8`. Fark yalnız üç onaylı test yolunda141ekleme/31çıkarma, çatışma yok. Güncel ana dal58eb6fa90, Web ve registry teslimleri aynı CT kaynağındadır. Root test/ürün kaynağı yazmadı.

Birleşik kapı PASS: bu kesin merge SHA'sının gerçek Git bağlamlı dış snapshot'ında bir taze full API koşusu395/395PASS,0FAIL/0SKIP;35Mongo/3hedefPASS. Değişmemiş CT üreticisi (base birinci ebeveyn), gerçek strict-skip ve known-failures çıkış0.26443kaynak bayt hashleri değişmedi; fresh DLL/aynı koşu395TRX storage bağı ölçüldü. Kendi5MongoPID/port/datadir/ACL kalıntısı0; süreç öldürülmedi. Önceki aday E2 veya sabotajlar tekrar edilmedi; bu yeni birleşmiş kaynağın normal ölçümüdür.

Ham kanıt `docs/records/audits/2026-11/pvg-dl12-prep-win-ct-integration-01/` içinde; TRXraw e222ad1acdb19205f3128a64f6653578c7454993f320fcad5753d1440d2c9ac1, GitLF4a92fc1e824f8a82e31976d2428cd0b6f29f27e0c1363a7c6d6a1363538559b6; raporrawf02903db55dd54d4d0650ef0c435fd3ea1fe7f43f349a703536be40c3bae3320. Büyük snapshot/index/orchestration dış kalıcı kanıt köküne hashleriyle korundu. CT kararı: INTEGRATED; modül/no-loss/E4/live kabulü değildir.

Normal çalışan geliştirme, 7 dil çeviri ve FE ayrı normal kapılardan geçecektir. Linux final H/checkout/TRX ve ikinci insan main merge kapıları sonraki tek PR için saklı; operasyon kapalıdır. İşçi worktree'leri ve eski WIP korunur.
