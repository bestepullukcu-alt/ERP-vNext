# Resmî inceleme 24 — CT karşılaştırması ve aynı kapsam içindeki teknik düzeltmeler

Tarih 2026-11-20. İncelenen kaynak bd3a1c6390d92e1a2e3cc9a8a3052641d2fe63df, pack7f612c2c7d714a487318f0a39313e324f8c1afc1d7414dfa4b3e6e9eddcb2b4c.
Diyalog ve geçmişsiz son bakış raporları docs/records/reviews/MOD-0230/2026-11-20-rev20-formal-{dialogue,blind}-01.md içinde değiştirilmeden saklandı. İkisi de UYGUN DEĞİL; iki ayrı açık engel, bir orta ve bir küçük bulgu var. Sonraki olumlu rapor bunları bu hash için ezemez.

CT bulguları gerçek kaynakla karşılaştırdı ve kabul etti:

- Windows: guard testinin /opt/fake/mongod sentetik olumlu örneği Windows'ta fully-qualified değil; ResolveBinary'nin IsPathFullyQualified kontrolü doğrudur. Aynı zaten onaylı test dosyasında yalnız sentetik yolların platforma uygun tam yol olması netleştirilecek. Güvenlik assertion'ı, exists kontrolü ve 27017 yasağı değişmez.
- Hazırlık döngüsü: onay zaten bounded feasibility ardından feature diyordu; pack'in her ürün işçisi öncesi final feasibility isteyen cümlesi bunu kapalı döngü yapabiliyordu. Aynı DL12 içindeki PREP-WIN/PREP-WEB normal ürün prompt/HIGH/dispatch kapısından ayrı geçer, feasibility kendi çıktılarıdır. Parent feature bu iki E2 entegrasyonunu bekler; hazırlıklar parent'a bağımlı olmaz. --prepare izin değildir. Gerçek GitHub Linux CI son PR kapanış kapısıdır; yerel temiz aynı-prerequisite koşusu hazırlık kanıtıdır.
- Ofset: validator tarih sınırını UTC anına göre denetliyor, özgün offsetli DateTimeOffset kabul edilebiliyor. Mevcut kabul edilmiş değerlerin no-loss sözleşmesine ±ofset/yedi-kesir fixture eklenmeli; UTC tick anı korunur, yeni iş kuralı veya şema yok.
- Port: AGENTS §3 PVG5011'i zaten tahsis ediyor, launch.json web-verify5002 override taşıyor. Eski tarihçe korunur; güncel eksik tahsis/kurulum işi kapatılır, port/config kodu değiştirilmez.

Bu dört düzeltme yeni dosya, iş kuralı veya modül açmaz; kullanıcının doğrudan onayladığı rev20 Windows/test/CI hazırlığı ve mevcut tüm kabul edilmiş değerleri koruma kapsamını uygulanabilir kılar. Yazar yalnız pack/domain metnini netleştirir; insanın onayladığı kapsamı genişletmez. Son yeni baytlar yeniden resmî incelenmeden/HIGH kayda bağlanmadan işçi başlamaz. Kaynak7f'nin olumsuzları korunur; onay veya başarı uydurulmaz.

Blueprint ve yedi dil dosyalarının ZIP'e girmediği inceleme sınırı kaydedildi; sonraki resmî ZIP'e gerçek dosyalar explicit extra eklenecek. Kaynak kodu, test, CI, build, restore veya ürün çalıştırması bu CT geçişinde yapılmadı. Portable araç hazırlığı ayrı kayıt ve yalnız smoke'dur.
