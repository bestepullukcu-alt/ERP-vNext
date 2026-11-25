hedef: docs/roadmap/plans/pvg-mod0230-windows-acl-scope-proposal-2026-11-20.md @ cde6effd06f482a1e5a3027d842d41856fea7679dfe34109603e5409eb0a5d47
model: gpt-6.2-astra
effort: high
reviewer_session: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9
kaynak_commit: 5b81b89a2fb43c63300e0f10ca5f5654e7d13750

F-4 | ORTA | Öneri 104–107, Linux testinin doğrudan son PR head SHA’sında çalışmasını istiyor. service-tests.yml:16–21,159–160 içindeki varsayılan checkout, pull_request olayında PR’ın sentetik merge commit’ini çalıştırır. Olağan yeşil PR koşusu bu yüzden yazılı SHA şartını tek başına kanıtlamaz. CI dosyasını değiştirmeden kesin head şartı korunabilir. Mevcut workflow_dispatch ile son PR dalında çalışan pvg-api işinin checkout SHA’sı son PR head SHA’sıyla eşleştirilmeli ve aynı koşunun TRX’inde hedef testin geçtiği, tam süitte 0 başarısız ve 0 atlanan olduğu doğrulanmalı. | açık | K5 | 2026-11-23

Önceki F-1 kapandı. Hem dizin listeleme önkoşulu hem gerçek tarayıcı çağrısı aynı kilitli dizinden gelen UnauthorizedAccessException’ı gerektiriyor. Önceki F-2’nin zamanlama koşulu kapandı. Windows E2, entegrasyon ve feature başlangıcı ilerleyebiliyor. Unix kanıtı son PR birleşimini ve WP kapanışını bağlıyor. F-4, bu kanıtın hangi commit için geçerli olduğuyla ilgili. Önceki F-3 kapandı. E2, 58920519’dan son adaya kadar iki WIP dosyasıyla ACL metodunu birlikte kapsıyor. Altı sabotajın her biri için ayrı derleme, isimli testte RED, özgün adayda GREEN ve kaynak hash çifti isteniyor.

Tek yeni insan onayı üçüncü dosyadaki tek test metodunun gövdesiyle sınırlı. Eski kirli ağaç korunuyor. Kayıtlı iki dosyalık patch’in temiz ağaca aktarılması, yeni pack/domain sürümü, HIGH incelemesi ve normal dağıtım kapısının 0 sonucundan sonra geliyor. Patch’i bellekte yeniden uyguladım. Her iki dosyanın kanonik LF içeriği patch’teki Git blob kimliğiyle, CRLF disk özeti de donmuş WIP kaydıyla eşleşti. Metin checkpoint veya kırmızı testle commit istisnası istemiyor.

Resmî ZIP’in 8.488.973 baytlık boyutu, SHA-256 özeti, manifestteki 301 dosyanın tamamı ve hedef belge doğrulandı. Pack ve domain özetleri bildirilen bazla eşleşti. Arşivdeki Windows TRX’i 395 yürütülen, 394 geçen, 1 başarısız ve 0 atlanan test gösteriyor. ZIP tam .NET referans paketini içermiyor. Gerçek Windows ACL reddi ve geri yüklemesi bu incelemede denenmedi. Hüküm yalnız kapsam önerisine aittir. İnsan onayı, MP onayı ve dağıtım kapısı PASS kaydı bekliyor.

sonuç: KOŞULLU
