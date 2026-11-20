PROMPT İNCELEME KAYDI v1
wp: WP-0230-DL12-PREP-WEB
prompt: PVG-WP-0230-DL12-PREP-WEB-P v1.0
prompt-sha256: 744d6d165db98862f236e48899e60794ea32bf6ed2ad3adb81ea890a666b52c3
kaynak-paketi-sha256: 6ff8612999a78f72396d0775a517c5bf1ee49f6b9117fffd67d62cae7efe1211
yazar: PVG CT root
inceleyen: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9
model: gpt-6.2-astra
effort: high
kaynak commit: 97e0e7f92dd2efb8d8affd94294bddc84ac05a5f
kontrol sözleşme: açık — frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:163, frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs:41 ve AGENTS.md:34 alıntıları eşleşti. İş emrinin CI tarifi execution/domains/pharmacovigilance/domain-config.md:113–115 ile uyuşmuyor.
kontrol dosyalar: tamam — execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:1494–1503 ile üç yazma yolu eşleşti. Mevcut modül C# testi, tek pvgapi takma adlı proje referansı ve .github/workflows/phase1-gates.yml içindeki iki adım kapsamda. Üretim Razor, JS, MVC ve PVG kaynakları yalnız okunuyor.
kontrol ct-onayı: açık — docs/records/audits/2026-11/pvg-rev20-formal-review-01/ROUND-02-DECISION.md iki bağımsız incelemeyi ve normal dağıtım şartını kaydeder. execution/approvals/approvals.tsv:105 aynı paket için HIGH satırı içerir. Sabit Git blobunda pack_approval.slices sonucu None. CT aynı ref için check --deferred çıkış 1 ve dilim tablosu reddi bildirdi. Bu oturumun tam kontrolü GitHub üzerinden defter geçmişini doğrulayamadığı için daha erken çıkış 1 verdi.
kontrol kabul: açık — frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js:62–71 ve 320–340 gerçek FormData ve antiforgery başlığını üretiyor. Dört taşıyıcı denetimi, tek gerçek güncelleme ve depodan yeniden okuma, başarısız çocuk süreçte sıfır yazma ve dış kopyada sabotaj ölçülebilir. DOĞRULA 1 ve 4 onaylı CI/cache kurulumunun tam değerlerini denetlemiyor.
kontrol durma: tamam — frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs için gerçek host, alias veya RPC kurulamazsa, token onarımı gerekirse, süreç sızarsa veya sabotaj yeşil kalırsa iş durur. .antigravity/rules/git-safety.md ile uyumlu olarak işçi izinli üç yolu ve tam staged farkı denetleyip tek yerel commit ile CT’ye döner.
kontrol risk: tamam — execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:1433–1460 ve docs/records/audits/2026-11/pvg-rev20-formal-review-01/ROUND-02-DECISION.md doğrultusunda sentetik istek belirteci gerçek form ve başlıkla taşınır. Oturum çerezi ve JWT C# tarafında kalır. Üretim izinleri değişmez. E2 bağlantı kanıtı ve iki hazırlığın entegrasyonu ana işten önce, gerçek Linux CI ise son PR kapanışında aranır.
bulgu: engelleyici · açık · Paket onayı kapısı 97e0e7f92 üzerinde çıkış 1 veriyor. execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:2770 altında DL-12 satırı var, ancak araç tabloyu tanımıyor. CT tablo biçimini uzlaştırmalı. Paket baytları değişirse kesin sürüm inceleme ve HIGH onay kayıtları yenilenmeli, iş emri yeni Expected HEAD ve kaynak paketi özetine bağlanıp yeniden incelenmeli.
bulgu: engelleyici · açık · WEB v1.0:89 ve DOĞRULA 1,4 yalın npm ci ile iki adımın varlığını yeterli bırakıyor. CT, sabitlenmiş setup-node adımına cache: npm ve cache-dependency-path: frontend/Diten.Web/package-lock.json değerlerini, kurulum adımına frontend/Diten.Web içinde npm ci --no-audit --no-fund komutunu açıkça yazmalı. Yerel hazırlık komutu ve fark denetimi aynı sözleşmeyi ölçmeli. execution/domains/pharmacovigilance/domain-config.md:113–115 ve .github/workflows/web-js-tests.yml:38–46.
kritik-açık: 0
engelleyici-açık: 2
