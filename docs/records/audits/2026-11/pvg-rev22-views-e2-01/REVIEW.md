# WP-0230-DL7-CI-VIEWS-01 bağımsız kesin commit E2 incelemesi

**UYGUN** · Dört görünümün PageCode üreticisi için kritik 0, engelleyici 0. İnceleme alanı rev22 AC46/49 dört görünüm dilimidir. Ürün yazarı pvg_r22_views_writer, bağımsız inceleyen pvg_r22_views_e2. İstenen model gpt-6.2-astra ve effort high. Gerçek model/effort verilemiyor.

## Kaynak ve yetki

- Dal feature/pvg/mod-0230-r22-page-help, işçi checkout C:/Users/epenc/.codex/worktrees/pvg-r22-views/ERP-vNext-git. Başta ve sonda temiz. Commit 3af28aa145ee175d82ec8f0b188f2eee46b6d4ff, tree 409a5bf61d393588fb2fcb53aa23a296a4c13091, parent b0dcca109997320e55659d7f41c489396338e15c.
- İncelenmiş v1.1 prompt c1ed47e427bd6ac890c4a3b43adf5195850d3db0e3d4e0ed75bf3923fda577f1. Parent üzerindeki 77 kaynaklık paketin blob listesinden yeniden hesaplanan SHA-256 879866cd87e960cdf84f05c64d2767e5872d563f745e669adc36894f30db08cd. Modül paketi d20813b2a4290bc299d29c4c72fbb7c64c095908294213b820533e6fafeafd0b, domain 0aa1706ebd4fd1d434b7b9fec78e8c8043c7340076f3d935544dd4853d5f2298, sahip kaydı 0fa566c24df9a3d0910e1ebd1b4febc68711d92ca22e64dbc754535fe217e0d7. Son normal dispatch capture ve doğrulayıcı 0, footer e5bc519144933acafcabe4d5b50bc15ab715da14ace313acc68ccdbc5c3c2089. Normalgate taze çalıştırılmadı.
- Git delta yalnız dört mevcut .cshtml dosyasında birer atama. Her canonical blobdan ilgili tek satır çıktığında parent blobu bayt bayt elde edildi. Author staged tam diff ile commit tam diff birebir aynı, SHA-256 2ed9970fecba2f9462c7d98352b0da997372ba9eb2a76454caf852d2573dd024. Tam yollar, blob ve disk hashleri scope.json ile candidate.diff içinde. Controller, helper, shell, izin, kaynak, JS, proje, görünüm iş mantığı veya diğer izlenen dosyalarda commit deltası sıfır.
- Derlenen bağımlılık ve executable kaynakları 18949 izlenen Git blobundan ayrı, boş bir çalışma dizinine kimlikleri tek tek kontrol edilerek alındı. Son restorasyonda kaynağın tümü yeniden ölçüldü. İşçi checkout ve CT repo salt okunur tutuldu. Yalnız dış QA kopyasına SDK çıktıları ve bir görünümün geçici negatif örneği yazıldı.

## Kesin sürümde yeni ölçümler

SDK 8.0.428, Node 22, SHA doğrulanmış 2085-dosyalı yerel feed, boş özel paket önbelleği ve test çıktı dizini kullanıldı. Çevrimdışı scoped restore 0. Diten.Web.Tests normal SDK/Razor derlemesi ve mevcut filtre 0. Çalıştırılan 48 olgu geçti, hata 0, atlanan 0. Author TRX ile 48 test kimliği ve adının birebir aynı olduğu doğrulandı. Taze TRX b67468c198606bd9433aad775fa3be8f7bc494a061e3c4ad81de2ffabcf4c02f. Test DLL c3d2f023fb6d466c59b0b6edc25f096fcf3e9256271e4474afacabcc3f31d98e / MVID 8124a307-7e61-403b-a498-10a2fba5cf7d. Web DLL 91817439d39e2d214ee6679d0c4b7408af48b3b7b9b53c80ff3e01614e6e46fc / MVID 8c497fca-3a10-4bb8-b34f-440d1e8e80a1. Test binindeki Web kopyasının hash'i eşit. PE CodeView ile PDB GUID eşleşir. Dört Razor, shell/panel, controller ve iki helper dahil dokuz Web belgesi ile beş test kaynağının compiler checksumları doğrudan 3af28aa145ee175d82ec8f0b188f2eee46b6d4ff Git baytlarına eşittir. pe-observed.json ayrıca yedi uydu DLL'nin dosya ve runtime MVID/SHA eşleşmelerini taşır.

| Gerçek GET görünümü | Atama satırı | Gerçek HTML panel kodu | HTTP | İngilizce içerik |
|---|---:|---|---:|---|
| Index | 5 | CASEINTAKETRIAGE | 200 | PREPARING |
| Create | 7 | CASEINTAKETRIAGECREATE | 200 | PREPARING |
| Edit | 7 | CASEINTAKETRIAGEEDIT | 200 | PREPARING |
| Details | 5 | CASEINTAKETRIAGEDETAILS | 200 | PREPARING |

Dışarıdaki Render.cs ve ViewEvidence.csproj incelendi ve taze derlendi. Harness derlemesi 0 hata ile tamamlandı. Test projesinin assets dosyasındaki göreli proje yolları dış dizinden çözümlenemediği için altı uyarı kaydedildi. Explicit Diten.Web referansı, aynı derlemenin hash/MVID doğrulaması ve gerçek HTTP çıktısı kullanılan assembly dosyasını bağlar. Üretim Razor ya da izin/CurrentPage algoritması kopyası içermez. Gerçek MVC controller kendi görünümü seçer. Create ve Edit gerçek PvgCaseIntakeFormViewModel taşır, Index ve Details'ın controller modeli null. Sentetik tenant kullanıcı sadece ilgili read/create/update claim'ini taşır. TestServer'a yalnız GET gider. İsimli HTTP istemcileri bellek içi iletim kullanır. Menü cevabı boş, diğer fixture GET okumaları 404. İstekten veya controller sözlüğünden PageCode verilmez. Eşleşen PVG menüsü sıfır, ölçülen yazma isteği sıfır. Her koşu için panel HTML'si ayrıca bir HTML ayrıştırıcısıyla doğrulandı. Canlı veritabanı veya kalıcı depo anlık görüntüsü ölçümü yok.

_LayoutTenantShell.cshtml satır 698, gerçek CurrentPage.Resolve(Context, ViewData) sonucunu doğrudan _PageHelpPanel'e gönderir. Panel satır 55–56 gerçek kodu data-page-code olarak yazar. Controller ViewResult sözlüğünde render sonrası gözlenen ayrı sonuç null. Üretim görünümünün etkisi gerçek panel çıktısında ve aynı harness ile omission/restorasyonda gözlendi.

### Bağımsız negatif

Edit'in yalnız PageCode satırı özel kaynak kopyasında çıkarılarak o görünüm tam parent blobuna döndürüldü. Normal Razor derlemesi 0, aynı Edit GET 200, doğru view/model, menü ve yetki koşulları korunurken panel kodu boş. Aynı assertion exit 42. Red Web DLL bd38b03f982b41dded6704905150a5cad59c25d8ce910c91c0549d62e7050de5 / MVID 4d07af01-4cd7-4024-8366-66b00e24b6b9. Satır kesin commit baytlarına geri konuldu. Derleme 0, aynı GET 200, aynı assertion 0, gerçek panel CASEINTAKETRIAGEEDIT. Restore edilmiş Web DLL/MVID başlangıçla birebir aynı. Aynı harness ae3e8320a33407b33d02ac2793021000c307bdfafda7633244b00fc9a5a267d9 / 0f9456ad-30bc-41ee-94a2-8ba1f28d1ff3. Ayrıntılar fresh-render-binding.json, bağımsız HTML'ler ve derleyici/host argv/çıkış/logları içindedir.

### Yazarın kanıtı ve sınırı

Mühürlü ZIP 60e333aefcc2586f17b336e74c06bd82b724cdc5b4965dc0702f72c49766f0c3. Manifest 51fd4d9f3879f5487217d7e4aff88696a7c99a84dff3a9814a7ba693f612e0f3, 302 listelenmiş dosya ve manifest dahil 303 arşiv girişi. Çakışma, mutlak yol ve üst dizine çıkma kontrolünden sonra her üyeyi açmadan okuyup manifest ve dış kanıt baytlarıyla karşılaştırdım. 31 komut makbuzunda stdout/stderr hashleri doğrulandı. Dört ayrı omission/restore çifti için tek değişen görünüm hash'i, normal derleme exit 0, aynı harness, farklı Web DLL/MVID, GET 200, gerçek panel metadatası, assert exit 42→0 ve boş menü/override yokluğu doğrulandı. Index, Create ve Details çiftleri yazar ölçümü üzerinden incelendi. Edit ayrıca yukarıda bağımsız olarak tekrarlandı. Yazarın ilk 48 testi commit öncesi çalıştı. TRX 69cf5df71481e6fe729e7a5889f26a7bc6a03f6428410498ce613b20c575ffcf, Web a65c1079fc51f44ea9499e44b61d875280b4fabcdb3e4d290cab12dcef978e2b, test c831d3323b36d11a5f625e2e2ee487e158f3f5b4a57bc52f17d2ba2e4fb7c662. Bu sonuçların tekrar derlenmiş kesin aday sonuçlarından ayrı kanıtı author-audit.json içindedir.

### DataTable ve açık işler

DataTable doğrulayıcısının yazarın değişiklik öncesi/sonrası stdout baytları birebir aynı, her iki exit 1. Bu incelemenin kesin kaynakta taze koşusu da exit 1, kök dizinin metni normalize edildiğinde aynı rapor. OD-PVG10/AC14 kapsamında 12 mevcut bulgu: server export, beş BulkDelete/BulkDeleteConfirm/AreYouSure/Export/Import köprü girdisi, select-all, bulk yapılandırma/seçim/endpoint/tetikleme, clear-selection. Data-mode, _TableSkeleton, _Filter, _IndexL10n include ve form-model kategorilerinde yeni sapma 0. Yeni muafiyet yok.

Ürün deltası için açık kritik/engelleyici bulgu 0. Ayrı birleşik kabul ve sevk kapıları açık. Bu izole branch'te dört İngilizce panel PREPARING döner. Onaylı metinle 4×7 gerçek yerel içerik ve RoleAssignments tüketicisi, CT no-ff entegrasyonu, yeni final SHA üzerinde Linux full JS ve tüm required CI/Phase1/ui-compliance, AC45 pvg-api Ubuntu22.04 aynı job targeted/full TRX ve commit/tree/checkout/event/run/attempt bağı, farklı insan HIGH inceleme ve E4/E5 bekler. Tek PR #460. Ürün commit/push/merge, kuyruk veya kabul güncellemesi yapılmadı.

CT'nin ayrı shared-worker gözlemi yerel Windows full JS'de 5577 olgudan 5564 geçti, 13 başarısız ve 0 atlanan, exit 1. CT'nin ilettiği tanıda H82'de aynı 269 durum ve 13 hata, canonical LF'de 11 düzelme ile iki Windows yol-ayırıcı hatası bildirildi. Bu inceleme shared full JS koşusunu yeniden yürütmedi. Yerel exit 1 korunur, birleşik Linux final CI ve PR kapısı açıktır.

### Teknik iz ve temizlik

İlk arşivle kaynak alma denemesi Windows satır sonu farkını yakaladı ve derlemeden durdu. Git cat-file ile ham blob okuması bunu düzeltti. İlk VSTest çağrısı özel artifacts tabanını bulamadı ve test başlatmadan sonlandı. VSTEST_ARTIFACTS_BASE_DIRECTORY özel dizine ayarlanıp aynı kaynak, filtre ve test projesiyle başarılı koşu alındı. Kaynak ya da test değişmedi. Komutlar, stdout, stderr, çıkışlar, TRX, PDB ve render kanıtları saklandı. Gizli değer kalıbı taramasında ham test çıktısı veya HTML'de JWT/oturum/antiforgery değeri bulunmadı.

Geçici kaynağın doğrulanmış mutlak yolu C:/Users/epenc/AppData/Local/Temp/e2v41b6. Temizlik öncesinde reparse point 0 ve bu geçici dizini komut satırında taşıyan SDK/testhost 0. Beklenen komutların PID/çıkışları cleanup.json içinde. Yalnız bu dizin kaldırıldı ve yokluğu ölçüldü. Kanıt kökü C:/Users/epenc/.codex/visualizations/2026/11/20/01a1f7d8-bf54-7703-ae91-a8ba29312b0d/e2v-3af28aa-41b6.
