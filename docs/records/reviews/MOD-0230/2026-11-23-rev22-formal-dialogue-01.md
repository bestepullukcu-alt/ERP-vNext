hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 8d44e327f34185e15ed9f8d915d73b86d32f33e50ba88775a172b512743818ac
istenen_model: gpt-6.2-astra
istenen_effort: high
gerçek_model: verilemiyor
gerçek_effort: verilemiyor
reviewer_session: /root/pvg_pr460_ui_diagnosis
inceleme_türü: yazardan ayrı oturumda resmî diyalog incelemesi; önceki taslak görüşü son baytlara aktarılmadı
bağımsızlık_sınırı: Fransızca açıklayıcı etiket önerisine önceki katkım vardır; bu metin için bağımsız dilbilimsel sertifika veya yedi dilde native doğrulama vermiyorum

zip: MOD-0230-review-16ada4be3-since-82bc77393.zip
zip_boyutu: 7349571 bayt; beklenenle eşit
zip_sha256: 6ed67cc38c1a64e4cf7ff80a814832644ba64806ba16ebd278df42aa2486a634; beklenenle eşit
manifest: 198 benzersiz dosyanın 198 hash’i doğrulandı; yinelenen üye, yinelenen manifest satırı, eksik veya manifest dışı dosya yok; MANIFEST.sha256 ile toplam 199 ZIP üyesi
paket_sha256: 8d44e327f34185e15ed9f8d915d73b86d32f33e50ba88775a172b512743818ac; doğrulandı
domain_sha256: 0aa1706ebd4fd1d434b7b9fec78e8c8043c7340076f3d935544dd4853d5f2298; doğrulandı
bildirilen_kaynak_commit: 16ada4be36ed245b3e317be8cb3c184b3d4d29da; ZIP dışı Git nesnesi okunmadı

doğrulama: CHANGES.diff tersine kaynak metin üzerinde uygulanınca rev21 paketinin ea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4 hash’i elde edildi. AC-1…AC-45 korunmuş; tek Acceptance Criteria bölümü ve benzersiz AC-1…AC-50 bulunuyor.
doğrulama: Kapsam 27 benzersiz üretim yolu; 23 ortak dosya ve dört PVG görünümü ayrık. İçerik 154 yeni yerelleştirilmiş değer ve bir mevcut Fransızca değer değişimi. Yedi dilde anahtar kümeleri tutarlı, değerler dolu; önerilen yeni non-English değerlerde İngilizceyle birebir eşitlik yok.
doğrulama: SOURCE-MANIFEST.json içindeki yedi ham base64 zarfının hash ve boyutları doğrulandı. Kapsam/içerik JSON’larının CRLF ham kaynakları ile ZIP’teki kanonik LF gösterimleri anlamsal olarak eşit; paket/domain içindeki kanonik hash bağları doğru.
doğrulama: İnsan onayı kaydı kesin dosya/anahtar/metin/aile sınırını, FU9 noktasal XMC istisnasını ve adlandırılmış tek-yazıcı düzenini kapsıyor. FU9 sahipliği devredilmiyor. İnsan kapsam onayı; yeni resmî HIGH kaydı, normal prompt incelemesi veya dağıtım kapısı yerine geçirilmiyor.
doğrulama: CurrentPage.Resolve üretim kaynağı açık ViewData PageCode değerine öncelik veriyor. Mevcut ActionVerbs köprüsü ve ACTION_FAMILIES üretim dizisi önerilen iki eşleme ve mevcut success/dark satırlarıyla uyumlu. Mevcut testler üretim köprüsündeki fiilleri okuyup gerçek resolveFamily fonksiyonunu denetliyor.
doğrulama: Paket/domain operasyonel kapıyı kapalı tutuyor; yetki/grant, tenant bağlamı, API/store/audit, manifest/menu, CI/guard/ledger değişikliğine izin vermiyor. E2 bütünleştirme E4/E5 kabulü sayılmıyor. Her yeni H için güncel zorunlu CI ve AC-45 Linux checkout/ebeveyn/ağaç/TRX bağı yenilenmek zorunda.

F-1 | ORTA | execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:113-135,242-246 ve ZIP girdisi: Yeni yardım başlığı/PageCode çözümünün dayandığı üretim tüketicilerinin kaynakları ZIP’te yok: frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml, frontend/Diten.Web/Views/Shared/_PageHelpPanel.cshtml, frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs; gizli sayfa iddiasının gerçek eşleri services/Diten.PvgService/src/Diten.PvgService.Api/ModuleRegistration/PvgCaseIntakeTriageManifestProvider.cs ve services/Diten.Platform/src/Diten.Platform.Application/Features/Navigation/Handlers/GetTenantNavigationMenuQueryHandler.cs de yok. PageHelpShellTests’in çağırdığı LayoutRenderer kaynağı da yok. CurrentPage ve test kaynakları mevcut olmakla birlikte bu uçtan uca üretim bağının yerini tutmuyor. Etki: Dört görünüm ataması ve 28 başlık değerinin mevcut gerçek kabukta tüketildiğini, gizli menü durumuyla uyumunu ve test/render uygulanabilirliğinin 27 yol içinde kaldığını yalnız bu ZIP üzerinden bağımsız karşılaştıramıyorum. Çözüm: Bu mevcut kaynakları değiştirmeden yeni inceleme ZIP’ine ekleyin ve yeni ZIP hash’inden yeniden inceleyin; uygulamanın henüz yapılmamış olması bulgu değildir. | açık | K5 | 2026-11-23

F-2 | DÜŞÜK | execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:1233,2008: Korunan eski gövdenin bu satırları hâlâ güncel durum diliyle serviste JWT doğrulaması bulunmadığını ve MVC controller’ın yalnız Authorize taşıdığını söylüyor. ZIP’te PvgServiceApiHost.cs:71,154 UseAuthentication/AddJwtBearer; PvgCaseIntakeTriageEndpoints.cs:27-33 RequireAuthorization; PharmacovigilanceCaseIntakeTriageController.cs:60-82 RequirePage çağrıları mevcut. Paketin 1941-1951 satırları da güncel doğrulanmış principal sözleşmesini açıklıyor. Etki: Aynı pakette eski kaynak değerlendirmesi ile güncel kaynak birbirine karışıyor. Yeni 27 dosyalık kapsam auth/controller değişikliklerini yasakladığından bu bulgu yeni kapsamı genişletmez. Çözüm: Sonraki uygun belge revizyonunda bu iki eski iddiayı tarihî olarak açıkça etiketleyip güncel kaynak notuna bağlayın; mevcut kodda yeniden uygulama görevi çıkarmayın. | açık | K5 | 2026-11-23

eksik_atıflar: DCP-001-access-governance.md ve module-implementation-status.md ZIP’te yok; bunlara ilişkin ikincil sahiplik/takip iddiaları bağımsız doğrulanmadı. FU9 paketinin kendisi ZIP’te mevcut ve platform-team sahipliği ile ayrı adım koşulları doğrudan okunabildi.
kanıt_sınırı: Atıf verilen ham JS/UI CI logları, TRX’ler ve önceki çalışma raporlarının tamamı ZIP’te yok. Bildirilen eski koşu sayıları bu incelemede yeniden doğrulanmış çalışma sonucu sayılmadı. Eski inceleme sonuçlarına dayanılmadı; gelecekteki RED/GREEN ve son-kaynak koşulları kabul sözleşmesi olarak değerlendirildi.
işlem_sınırı: Yalnız verilen ZIP okundu. Repo/ZIP dışı kaynak, ağ, build/test/restore, dosya yazımı, Git değişikliği veya alt ajan kullanılmadı. Bu inceleme ürün uygulaması, dağıtım veya operasyonel kullanım izni değildir.
sonuç: KOŞULLU
