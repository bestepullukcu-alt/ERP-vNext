hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 729ac90976f8038b7d6e7c39b0348a51be485999ae7e094c46aea87264bcdab5
model: verilemiyor
effort: verilemiyor
reviewer_session: /root/pvg_rev20_blind_formal_review2
kaynak_commit: 3fcc08ddce520d0d277f275cb1b5662f88d60eb5

F-1 | ORTA | Paketin 1580–1585, 1880–1885 ve 1951–1966. satırları istemciden gelen eski başlıkları hâlen yetkili, JWT doğrulamasını eksik ve eski izin ailesini yürürlükte gösteriyor. PvgCaseIntakeTriageEndpoints.cs:591–621 doğrulanmış tenant_id ile X-Tenant-Id eşleşmesini arıyor ve aktörü doğrulanmış kullanıcıdan alıyor. PvgIntakeDraftApplicationService.cs:7–11 güncel izin ailesini kullanıyor. CT-QUEUE.tsv içinde DL4B entegrasyonu kayıtlı. Bu ifadeler tarihsel gözlem olarak tarihlenmeli ve DL-12 işçisinin koruyacağı güncel sözleşme gösterilmeli. | açık | K5 | 2026-11-23
F-2 | ORTA | AC-42(f) gömülü satır sonlarını kanıt dizisinde ölçüyor. SourceReference için PvgIntakeDraftValidator.cs:218–221 yalnız kırpılmış uzunluğu denetliyor ve PvgIntakeDraftApplicationService.cs:834 özgün metni saklıyor. _Form.cshtml:71 ise tek satırlı input kullanıyor. Harfler arasında CR/LF içeren geçerli bir kaynak referansı, yalnız DOM değeri geri gönderilirse sessizce değişebilir. Gerçek API ile oluşturulan bu örneğin dokunulmamış Edit kaydında birebir korunduğu ve DOM değerini kullanan sabotajın testi düşürdüğü ölçülmeli. | açık | K5 | 2026-11-23
F-3 | ORTA | AC-41(b) alan politikası reddinden sonra liste yanıtında 15 değerin görünmemesini istiyor, ancak verili bir kaydı döndüren başarılı liste çağrısını şart koşmuyor. PvgCaseIntakeApiContractTests.cs:561–568 izinli ayrıntıyı okuyor, listeyi reddedilen bağlamda çağırıyor. Böylece boş ret yanıtı liste ölçüsünü geçebilir. Aynı kiracıda izinli liste için 200, beklenen kimliğin dönmesi, öğenin yalnız intakeDraftId/status/version taşıması ve ayrıntı verisinin yanıtın hiçbir yerinde bulunmaması birlikte ölçülmeli. | açık | K5 | 2026-11-23

sonuç: KOŞULLU

Blueprint 8.8 içindeki Blueprint_Data satırı MOD-0230 kimliğini ve Case Intake & Triage adını doğruluyor. Yedi sayfa dili arşivde mevcut. Sahip kararı ve domain-config iki hazırlık işinin dosya sınırlarını belirliyor. Ana geliştirme işinin dağıtımı iki hazırlığın bağımsız E2 doğrulamasına ve entegrasyonuna, kesin sürüm onaylarına ve kendi dağıtım kapısına bağlı. Operasyonel kullanım kapısı kapalı.

ZIP SHA-256 33d2a95b227a6b511fecbddccdb577789595126d02fa5e587032c4552762dadb doğrulandı. Manifestin 281 benzersiz girdisinin tamamı, hedef paket özeti ve domain SHA-256 914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c eşleşti. Hüküm yalnız ilk satırdaki paket özetine bağlıdır.

AGENTS.md §6.1’in atıf yaptığı code-style, docs-organization ve cross-module-change-consent kural dosyaları arşivde yoktu. Eski incelemeler ve CT değerlendirme dosyaları açılmadı. PvgCaseIntakeEditKeepsStoredDataTests.cs gelecekte oluşturulacak dosyadır. Build, test, sabotaj, Windows Mongo ve Linux CI koşulmadı.
