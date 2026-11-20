PROMPT İNCELEME KAYDI v1
sonuç: UYGUN
wp: WP-0230-DL12-REGISTRY-01
prompt: PVG-WP-0230-DL12-REGISTRY-01-P v1.2
prompt-sha256: 91579bd5efa030488c0d219cd60242440d1fb4ad08e59ca2e241f3d12f2c6938
kaynak-paketi-sha256: 7859db7b871a3b3af8d4efd94c327370b7327e65117bd75b4c7675d745b8c1cc
yazar: PVG CT root
inceleyen: /root/pvg_dl12_prep_web_verify
model: verilemiyor (istenen gpt-6.2-astra)
effort: verilemiyor (istenen high)
kaynak commit: fff584a3675fb0273872b0dca9896d26da749f8f

kontrol sözleşme: tamam — docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md:25 ve tests/known-failures.tsv:4–8 karşılaştırıldı. Prompt :74 artık kimlikleri onay belgesinde bulunmayan bir listeye değil, onayın işaretlediği sabit ledger blob’undaki aynı beş kayda bağlar; başka hücre veya kimlik değişikliği açılmaz.

kontrol dosyalar: tamam — tests/known-failures.tsv:4–8 tek yazma yolundaki beş fixed kayıttır. execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py:18–27,48 sabit baseline’den yalnız beş rerun_trigger hücresindeki literal token çıkarımını kabul eder; diğer hücreler, kayıtlar, status ve fingerprint korunur.

kontrol ct-onayı: tamam — docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md:3–6,25–31 WP kimliğini decided insan onayına, dar XMC istisnasına ve normal prompt/dağıtım kapısına bağlar. İşçi yerel commit sınırı bellidir; push, PR, main merge ve operasyon yetkisi açılmaz.

kontrol kabul: tamam — execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py:28–48 gerçek CLI validate/check çıkışlarının ikisini de 0, tek eski tokenın üretim validator’ında bellek içi RED sonucunu ve sabit Web TRX’in CLI öncesi/sonrası immutable kaynak içeriğiyle eşitliğini zorunlu kılar. Üretici LF SHA256 1fe0e58cf55d7566dee5a994b636a54dc128819a769743b5634a262a2ed5e0f9 ve TRX LF SHA256 d8e4459f1779fee3cdcd68b49cecf6949d13f00df459dbc136a11633c8646e91 yeniden doğrulandı. docs/records/audits/2026-11/pvg-dl12-registry-01/baseline-red-v1.1.json aynı kaynak paketinde korunuyor.

kontrol durma: tamam — execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py:20–24 kaynak biçimi ve tek-token önkoşullarını durdurarak korur; docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md:27 normal kapı şartını korur. Prompt :79’daki başlangıç SHA’sı artık header ile aynı tam fff584a3675fb0273872b0dca9896d26da749f8f değeridir; :84 hash/kirlilik/kapsam/kanıt uyuşmazlığında durmayı açıkça ister.

kontrol risk: tamam — scripts/known_failures.py:334–359 ve tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs:60–63 değişmez kaynaklardır. Beş fixed kayıt düzeltmesi gerçek XML taramasını veya validator’ı gevşetmez; korunmuş 62/62 TRX yeni test koşusu, ürün kabulü veya ana DL-12 E2 sonucu gibi sunulamaz.

bulgu: engelleyici · kapandı · v1.1 DOĞRULA/1 içindeki hedefSHA690 artığı v1.2 :79’da tam Expected HEAD ile değiştirildi.
bulgu: engelleyici · kapandı · v1.1 NASIL bölümündeki mevcut olmayan onay-belgesi test listesi atfı v1.2 :74’te sabit ledger’ın onaylı satır 4–8 kimliklerine bağlandı.
kritik-açık: 0
engelleyici-açık: 0

not: Gerçek v1.2 dosyasının ham SHA256’sı yukarıdaki değerle aynı; CR sayısı 0. Yirmi kaynak Git blob’u ve kaynak-paketi digest’i yeniden ölçüldü. v1.1→v1.2 tam içerik farkı yalnız sürüm etiketi ve belirtilen iki düzeltmedir; kabul üreticisi, TRX bağı ve kapsam değişmemiştir. Önceki CRLF/raw-hash uyuşmazlığı v1.2’de yoktur. S-54 okuyucusu salt okunur incelendi; dosya yazımı, gate, kabul üreticisi, build veya test çalıştırılmadı. Bu inceleme normal dispatch izni değildir.
