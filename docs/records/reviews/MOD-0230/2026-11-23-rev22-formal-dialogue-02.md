hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ d20813b2a4290bc299d29c4c72fbb7c64c095908294213b820533e6fafeafd0b
istenen_model: gpt-6.2-astra
istenen_effort: high
gerçek_model: verilemiyor
gerçek_effort: verilemiyor
reviewer_session: /root/pvg_pr460_ui_diagnosis
inceleme_türü: yazardan ayrı oturumda resmî diyalog yeniden incelemesi; önceki olumlu sonuç yeni baytlara aktarılmadı
bağımsızlık_sınırı: Fransızca açıklayıcı etiket önerisine önceki katkım vardır; bu ifade için bağımsız dilbilimsel sertifika veya yedi dilde native doğrulama vermiyorum

zip: MOD-0230-review-61d23ff89-since-82bc77393.zip
zip_boyutu: 8004724 bayt; beklenenle eşit
zip_sha256: 8692171682fdc5503ec2db7137fb88ce70bc2249840887f6d0d4a081c6332ae2; doğrulandı
manifest: 238 benzersiz dosyanın 238 hash’i doğrulandı; yinelenen üye, yinelenen manifest satırı, eksik veya manifest dışı dosya yok; MANIFEST.sha256 ile toplam 239 üye
paket_sha256: d20813b2a4290bc299d29c4c72fbb7c64c095908294213b820533e6fafeafd0b; doğrulandı
domain_sha256: 0aa1706ebd4fd1d434b7b9fec78e8c8043c7340076f3d935544dd4853d5f2298; doğrulandı
bildirilen_kaynak_commit: 61d23ff8951f96a29854c23bd035dc42b3004a09; ZIP dışı Git nesnesi okunmadı

önceki_F-1: kapalı. Önceden eksik olan üretim tüketicileri ve render yardımcısı yeni ZIP’te mevcut ve kaynakları okundu. _LayoutTenantShell.cshtml:445,698 gerçek CurrentPage.Resolve sonucunu yardım bileşenlerine aktarıyor. CurrentPage.cs:93-99 açık ViewData PageCode değerine öncelik veriyor. _PageHelpPanel.cshtml:27-46 ilgili yardım anahtarlarını okuyup başlığı NavNames.Page üzerinden çözüyor; NavNameLocalizer.cs:38-55 gerçek SharedResource Nav.Page anahtarına gidiyor. PVG manifestindeki dört sayfa IsNavigationVisible:false; Platform GetTenantNavigationMenuQueryHandler.cs:96-111 görünürlük filtresini uyguluyor ve boş sayfa grubunu eklemiyor. LayoutRenderer kaynağı da mevcut. DCP-001 ve uygulama durum kaydı eklenmiş; FU9 sahiplik bağlamı karşılaştırılabildi. Böylece önceki bulgunun üretim bağını incelemeyi önleyen girdi eksikliği giderilmiş.

önceki_F-2: kapalı. Paketin 41-44 satırlarındaki yeni açıklama iki eski auth iddiasını açıkça tarihî kaynak fotoğrafı olarak sınırlıyor ve güncel üretim yollarını gösteriyor. ZIP’te PvgServiceApiHost.cs:71-72,153-161 kimlik doğrulama/yetkilendirme hattı, endpointlerin RequireAuthorization çağrıları ve MVC controller’ın RequirePage denetimleri bu açıklamayla tutarlı. Açıklama yeni auth işi veya kapsam izni oluşturmuyor.

korunum: CHANGES.diff yeni paket üzerinde tersine uygulanınca rev21 kaynak hash’i ea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4 elde edildi. AC-1…AC-45 metinleri değişmemiş; tek Acceptance Criteria bölümü ve benzersiz AC-1…AC-50 mevcut.

kapsam: Kanonik scope.json 27 benzersiz üretim yolu içeriyor; 23 ortak dosya ile dört PVG görünümü ayrık. İçerik 154 yeni değer ve bir mevcut Fransızca değer değişimidir. Yedi dilde her biri 22 yeni anahtar, dolu değerler ve eşleşen kümeler mevcut; yeni non-English değerler İngilizce karşılıklarının birebir kopyası değil. Listelenen yeni anahtarlar ZIP’teki ilgili mevcut kaynaklarda henüz bulunmuyor. Uygulanmamış olmaları başarı veya paket kusuru sayılmadı.

içerik_bağı: SOURCE-MANIFEST.json içindeki yedi ham zarfın base64 içerikleri, boyutları, ham hash’leri ve LF hash’leri doğrulandı. Kanonik kapsam/içerik dosyaları ham kaynakların LF gösterimleriyle eşit. İnsan onayı kaydının 0fa566c24df9a3d0910e1ebd1b4febc68711d92ca22e64dbc754535fe217e0d7 hash’i paket/domain atıflarıyla eşleşiyor.

yetki: Paket/domain kesin XMC, içerik, sunum ailesi ve tek-yazıcı sınırını aynı biçimde tanımlıyor. FU9/platform-team sahipliği korunuyor; noktasal kullanıcı onayı diğer FU9 işleri, ortak dosyaların bütünü veya başka domain servisine yetki vermiyor. İnsan kapsam onayı ile son-bayt resmî HIGH/prompt/dağıtım kapıları ayrılmış. Operasyonel runtime, yeni-SHA push ve main merge bu kapsam kaydıyla açılmıyor.

uygulanabilirlik: Mevcut ActionVerbs köprü biçimi ve üretim ACTION_FAMILIES satırları iki eklemeye uygun. Mevcut fiil testleri üretim köprüsünü okuyup gerçek resolveFamily fonksiyonunu çağırıyor. Yardım metinleri sayfa amacı, kapalı kullanım durumu ve sentetik veri sınırını açıklıyor; başarılı kaydetme veya operasyonel triage/routing vaadi içermiyor.

kabul_ve_negatif_kanıt: AC-46…AC-50 gerçek dört görünüm/CurrentPage/yardım tüketimini, yedi dili, üretim köprüsünü, tam kapsam farkını ve dış karalama kopyasında bağımsız eksiltme kanıtlarını istiyor. Kaynak araması veya kopyalanmış kuralla başarıya izin vermiyor. Eski altı dosyalı FE üreticisi yeni kapsamın kanıtı yapılmıyor. E2 bütünleştirme E4/E5 kabulü yerine geçmiyor.

son_kaynak_CI: Her yeni H için zorunlu güncel CI ve değiştirilmemiş AC-45 Linux checkout/ebeveyn/ağaç/TRX bağı yeniden isteniyor. Önceki H82 koşuları yeni kaynak için kullanılmıyor. ZIP’teki eski CI gözlem kayıtları bu sınırı koruyor; ilk Phase1 başarısızlığı başarılı tekrar nedeniyle silinmiyor. SOURCE-BOUND-PVG-CI.json içindeki gömülü C commit metninin Git nesne SHA1 hesabı d4aff06421eaf9e3a64f8d462b671cee40c1f88e ile eşleşti; bu, gelecekteki kaynak veya çalışma başarısı değildir.

açık_bulgu: yok

inceleme_sınırı: Yedi CaseIntakeTriageIndex sayfaya özgü RESX dosyası ZIP’te bulunmuyor; yeni Nav.Page değerlerinin mevcut sayfa başlıklarıyla birebir eşitliği bu girdide doğrulanmadı. Kesin yeni değerler insan onayına bağlı içerik dosyasında mevcut; son kaynak gerçek çok dilli görünüm doğrulaması hâlâ zorunlu. Mevcut LayoutRenderer kültürü en olarak ayarlıyor; mevcut yardımcı/testlerin tek başına çalıştırılması yedi dilde AC-46 başarısı sayılmaz. Paket zaten ayrıca gerçek çok dilli üretim tüketimi kanıtını gerektiriyor.

kanıt_sınırı: Ham CI logları ve TRX/artifact içeriklerinin tamamı ZIP’te yok; kompakt kayıtlar okunabildi. Bunlardan yeni çalışma başarısı veya bağımsız ham-TRX doğrulaması çıkarılmadı. Ürün, negatif senaryolar ve gelecekteki CI bu incelemede çalıştırılmadı. Eski inceleme sonuçları karar dayanağı yapılmadı.

işlem_sınırı: Yalnız verilen ZIP okundu. Repo/ZIP dışı kaynak, ağ, build/test/restore, dosya yazımı, Git değişikliği veya alt ajan kullanılmadı. Sonuç, eksik normal HIGH/prompt/dağıtım kayıtlarının yerine geçmez ve ürün/operasyon kabulü değildir.
sonuç: UYGUN
