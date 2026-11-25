# PVG CT — ana dal güncellemesi

2026-11-20 (Asia/Baku). `git fetch origin main` başarılı; önceki `2de950edcaeb9d8a153465e5f29d7c71eeb31144`, yeni `58eb6fa901683296a0049a06910dc4b12e970950`. PR #455 ve #457'nin 108 dosyalık değişikliği MDM banka master ve AGR kayıt kodu kararını içerir. CT bu farkın dosya listesini okudu; PVG pack/domain, hazırlık yolları ve kullanılan kapı betikleri değişmedi.

Yalnız `chore/ct/pvg-ct-01` dalına ordinary no-ff merge: `ff4609ac81f85e0fd4398aace2808bf5dd10b6e2`. Çatışma yok. Ortak yedi RESX kaynağı Git tarafından birleştirildi; CT başka modülün ürününü düzenlemedi. Ana dalın kendi korunan kural değişikliği merge ile taşındı; CT yeni kural değişikliği yapmadı.

Windows E2 hâlâ kendi değişmez `fee7d0132e3862ded7025465e311f263e068f7aa` / `addfabdae2d2a0e415b4ce77383bbc806ca9f59a` kaynağını ölçer. Bu main güncellemesi o eski koşuyu yeni birleşik koşu olarak sunmaz. Windows teslimi bağımsız doğrulandıktan sonra CT birleşik API kapısı yeni CT kaynağında çalışacaktır. Web'in önceki 62/62 kanıtı kendi `78804713` kaynağına bağlı kalır; son ürün/PR kontrolleri güncel birleşik kaynağı ölçer.

Push/PR/main'e commit yok. Ana DL-12 iki PREP tamamlanıncaya kadar BLOCKED; operasyon kapalıdır.
