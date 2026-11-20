# Ortak kayıt düzeltmesi — CT kırmızı tabanı

2026-11-20. İzin `pvg-mod0230-extra-scope-approval-01.md@a2586b89dd87dd99a51e85ff29560fc5fd1656cc`; uygulama henüz başlamadı. Ayrı işçi ağacı `pvg-dl12-registry`, aynı HEAD'de temiz kuruldu; normal prompt kapısını bekliyor.

CT kabul üreticisi `execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py`, değiştirilmemiş gerçek `scripts/known_failures.py` CLI'ını çalıştırır. Kabul için kaynak baseline'ine göre yalnız beş yetkili hücrenin tam farkı, `validate` 0, korunmuş bağımsız Web TRX'ine `check --suite Diten.Web.Tests` 0 ve üretim `validate` fonksiyonuna bellek içi tek eski tokenın geri konmasıyla gerçek tracked dosyalar üzerinden RED gerekir. Bellek deneyi CLI'nın yerine geçmez; dosya/guard değişmez ve sonuç onarım uygulanmış gibi gösterilmez.

Son kesin betikle gerçek kırmızı taban: kabul üreticisi çıkış 1; CLI `validate` çıkış 1, CLI `check` çıkış 2; bellek içi tek eski tokenı üretim validator'ı reddetti. Ham komut/çıktı ve betik SHA'sı `baseline-red.json` içinde. Önceki betik denemesi aynı normal CLI 1/2 sonucunu verdi; son üreticide bellek sabotajı yalnız tek token olacak şekilde kesinleştirilip kaynak değiştiği için ölçüm yenilendi. Hiçbir ledger hücresi değiştirilmedi.

Bu kanıt yalnız kayıt düzeltmesinin tabanıdır; Web testleri tekrar koşulmadı, yeni kabul/entegrasyon veya dağıtım PASS iddiası yoktur. İşçi aynı kesin üreticiyi final adayda yeniden çalıştıracaktır. Betik global ayar, ağ, üretim kaynak veya kendi test/guard kaynağı değiştirmez.
