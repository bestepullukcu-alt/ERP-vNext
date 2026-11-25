PROMPT İNCELEME KAYDI v1
sonuç: UYGUNDEĞİL
wp: WP-0230-DL12-REGISTRY-01
prompt: PVG-WP-0230-DL12-REGISTRY-01-P v1.1
prompt-sha256: 3cea965644d1924141dde15cac6de17b317a23c5ef0cc8afb470592887aa4bae
kaynak-paketi-sha256: 7859db7b871a3b3af8d4efd94c327370b7327e65117bd75b4c7675d745b8c1cc
yazar: PVG CT root
inceleyen: /root/pvg_dl12_prep_web_verify
model-effort: verilemiyor (istenen gpt-6.2-astra/high)
kaynak commit: fff584a3675fb0273872b0dca9896d26da749f8f

kontrol sözleşme: sorun — docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md:25 yalnız sabit ledger blob’u ve satır 4–8 kapsamını tanımlar; prompt NASIL:74 ise burada bulunmayan tek tek test adlarıyla karşılaştırmayı zorunlu tutar. Karşılaştırma hedefi aynı kesin ledger’ın tests/known-failures.tsv:4–8 kayıtlarına bağlanmalıdır.

kontrol dosyalar: tamam — tests/known-failures.tsv:4–8 kaynak blob6038f88ef70db0bedfbde183527f8022b9ca0c64 içindeki beş fixed kayıttır; execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py:18–27 ve :48 yalnız beş hücrenin beklenen literal token çıkarımıyla eşitliğini zorunlu kılar. Diğer hücreler ve kaynaklar yazma kapsamı dışındadır.

kontrol ct-onayı: tamam — docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md:3–6,25–31 bu WP’yi decided insan onayına, tek ledger yoluna ve normal prompt/dağıtım kapısına açıkça bağlar; XMC istisnası dar kapsamlıdır, operasyon ve yayın yetkisi açılmaz.

kontrol kabul: tamam — execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py:28–48 gerçek CLI validate/check çıkışlarını, immutable a2586b89 kaynağındaki gerçek Web TRX içeriğinin önce/sonra eşitliğini ve üretim validator’ında bellek içi tek eski token RED ölçüsünü birlikte zorunlu kılar. Üretici LF SHA256 1fe0e58cf55d7566dee5a994b636a54dc128819a769743b5634a262a2ed5e0f9 ve korunmuş TRX LF SHA256 d8e4459f1779fee3cdcd68b49cecf6949d13f00df459dbc136a11633c8646e91 doğrulandı. docs/records/audits/2026-11/pvg-dl12-registry-01/baseline-red-v1.1.json:1 kaydında normal CLI 1/2, guard_red=true ve TRX bağı aynı sürüme aittir.

kontrol durma: sorun — docs/records/audits/2026-11/pvg-dl12-registry-01/BASELINE.md:3 ve kesin kaynak commit’iyle karşılaştırıldığında prompt DOĞRULA:79 içindeki “hedefSHA690” talimatı header’daki fff584a3675fb0273872b0dca9896d26da749f8f ile çelişir. Başlangıç SHA uyuşmazlığında durmayı zorunlu tutan prompt :84 nedeniyle işçi bu çelişkiyi yorumlayarak aşmamalıdır.

kontrol risk: tamam — scripts/known_failures.py:334–359 tekrar önerisini yalnız open kayıtlara uygular ve mevcut tracked yollarla glob doğrulamasını korur; tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs:60–63 XML uzantısını gerçek taramada tutar. Beş fixed kayıt düzeltmesi guard/status/fingerprint gevşetmesi veya yeni Web testi başarısı sayılmaz.

bulgu: engelleyici · açık · Prompt :79’daki hedefSHA690 artığı kesin Expected HEAD fff584a3675fb0273872b0dca9896d26da749f8f ile değiştirilmeden başlangıç doğrulama talimatları tutarlı değildir.
bulgu: engelleyici · açık · Prompt :74’teki “onay kaydındaki test adları” karşılaştırma hedefi kaynak onay belgesinde yoktur; mevcut izin genişletilmeden kesin ledger satırlarına veya aynı beş gerçek test adına bağlanmalıdır.
kritik-açık: 0
engelleyici-açık: 2

not: Yirmi kaynak Git blob’u ve paket digest’i doğrulandı. Önceden okunan kural/guard kaynaklarının bu commit’teki blob eşitliği kontrol edildi; TRX’te 62 gerçek sonuç ve 62 passed görüldü. Dosya yazımı, build, test, kabul üreticisi veya dağıtım kapısı çalıştırılmadı. S-54 okuyucusu scripts/prompt_gate.py:1176–1249 üzerinden salt okunur incelendi.

not: prompt-sha256 yukarıda istenen kanonik LF baytlarına bağlıdır. Mevcut CT dosyasının CRLF raw SHA256’sı 70004e6ebc345bbb42b4288b7d591812378336c1a1164d478c618b3ab9ee2d74. review-check :2445 ve try_review :1237 ham bayt hash’i kullandığından LF kaydı doğrudan bu CRLF dosyayla denemek ayrıca hash uyuşmazlığı verir; okuyucu değiştirilmemelidir. Bu kayıt normal dispatch izni değildir.
