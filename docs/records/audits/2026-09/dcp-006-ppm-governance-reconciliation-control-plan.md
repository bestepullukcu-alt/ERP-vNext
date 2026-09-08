# DCP-006 PPM Governance Reconciliation Control Plan

## 1. Amaç

Bu plan, PPM Governance Pack v1.5.7 içindeki iş kararlarının güncel `main` üzerindeki gerçek kodla
karşılaştırılmasını, doğru modül sahiplerine ayrılmasını ve unutulmadan kontrollü biçimde teslim edilmesini
izler.

Bu belge:

- DCP-006'nın veya module pack'lerin yerine geçmez.
- Kod geliştirme, commit, push, PR, migration veya production activation yetkisi vermez.
- Yüzde tahmini üretmez; dosya, alan, durum, geçiş, kontrat ve test kanıtı üzerinden ilerler.
- Eski Enterprise Strategy arayüzünü otorite değil, parity ve kullanıcı ihtiyacı kanıtı olarak kullanır.
- SAP/Oracle benzeri dış benchmark'ları doğrudan kopyalanacak gereksinim değil, öneri ve eksik-risk kontrolü
  olarak kullanır.

## 2. Kaynak ve otorite sırası

Repository içindeki uygulama kararlarında sıralama değişmez:

1. İlgili module pack
2. Domain config
3. `AGENTS.md`
4. `.antigravity/` mühendislik kuralları
5. PPM Governance Pack v1.5.7 iş kararları
6. Legacy Enterprise Strategy ve dış ERP benchmark'ları

İncelenen dış kaynak:

- `PPM_Governance_Pack_v1.5.7_DRAFT_2026-09-07.zip`
- ZIP SHA-256: `b70ab64b27c9d8f60c08017afbe9c94c319e548840284f8bc05762377fe00c6e`
- Paket bütünlüğü: manifest/hash/top-level dosya denetimi temiz; checker `130/130 PASS`
- İş kararları: `business_baseline = true`
- Coding baseline: `false`
- Repository implementation authority: `false`

Belgedeki eski üç-koşul/dört-koşul ifadesi şu güvenli yorumla sabitlenmiştir:

1. Business baseline tamamlandı.
2. Bağımsız teknik inceleme yapılmadı.
3. Yetkili belge imzası atılmadı.
4. Repository implementation authority verilmedi.

## 3. Kapsam

### 3.1 Doğrudan kapsanan alanlar

Governance Pack yalnız 1.3'ü kapsamaz. Doğrudan şu sınırları etkiler:

| DCP-006 alanı | Doğrudan etki |
|---|---|
| 1.3 Portfolio, Investment and Value | Portfolio, Investment Case, Benefit Commitment ve Budget/Scenario/Outcome typed-link sınırları |
| 1.4 Delivery and Execution | Initiative, Program, Project ve Project'e bağlı Workspace sınırı |

### 3.2 Kapsamadığı alanlar

- MOD-0354 DWS'nin tam yapısal modeli Governance Pack tarafından tanımlanmaz.
- MOD-0355 BPM ve 1.6 Business Process and Operational Management bu paketin doğrudan kapsamı değildir.
- MOD-0023 workflow/approval motoru, MOD-0024 task, MOD-0031 evidence, MOD-0288 organization ve diğer
  paylaşılan modüller PPM içinde yeniden uygulanmaz.
- 1.6 ancak MOD-0355 module pack'i ve DCP-006 entegrasyon kapıları üzerinden ele alınır.

## 4. Değişmez iş kararları

- Altı governed class: Portfolio, Initiative, Program, Project, Investment Case, Benefit Commitment.
- Dashboard türetilmiş görünüm; ayrı kalıcı veri sahibi değildir.
- Workspace, Project'e bağlı çalışma yüzeyidir; yedinci governed class değildir.
- Governance hedefi 25 ortak alan, 62 modüle özel alan, 11 durum ve 55 geçiştir.
- Initiative kendi execution lifecycle'ını çalıştırabilir; Program/Project üretimi ayrı ve tekrar edilebilir PG2
  Gate Event'tir.
- PPM gate gereksinimini ve sonucunu sahiplenir; MOD-0023 approval yürütmesini sahiplenir.
- Planned benefit MOD-0117'ye, actual/realized outcome MOD-0072'ye aittir.
- Budget/version MOD-0136'ya; scenario/comparator MOD-0138'e aittir.
- PPM yabancı modül verisini kopyalamaz; typed reference kullanır.
- Funding Ceiling alanı OD-04 ve OD-07 kapanmadan oluşturulmaz.
- Capex/Opex alanı OD-04 ve OD-11 kapanmadan oluşturulmaz.
- NPV/IRR/Payback/Discount Rate alanları OD-11 kapanmadan oluşturulmaz.
- Mevcut Expected Financial Return ve Risk Adjusted Scenario alanları OD-11'e bağlıdır.
- Cancellation ve Closure ayrı dokuz-kodlu kontrollü vocabulary kullanır; `other` için açıklama zorunludur.

## 5. Yürütme sırası

### Aşama 0 — Current-main preflight

- `origin/main` güncelliğini doğrula.
- Aktif worktree ve dirty dosyaları salt-okunur say.
- Başka geliştiricinin worktree veya branch'ine dokunma.
- DCP-006, MOD-0117 pack ve repository gerçekliği arasındaki eski bilgileri listele.

Çıkış kanıtı: base SHA, worktree envanteri ve stale-governance listesi.

### Aşama 1 — Eski koddan onaylı modele exact eşleme

Altı sınıfın her biri için aşağıdakileri güncel `main` kodundan çıkar:

- Entity/aggregate ve alanlar
- Enum/status/record-state değerleri
- Lifecycle geçişleri ve yetki kontrolleri
- İlişkiler ve cardinality
- API endpoint/DTO/validation
- Mongo persistence/index/concurrency/soft-delete/tenant izolasyonu
- Frontend liste/create/edit/details/action yüzeyleri
- Manifest, navigation, localization ve permission bağlantıları
- Unit/integration/architecture/browser test kanıtı

Her satır yalnız şu kararlardan birini alır:

- `KEEP`: mevcut hâli onaylı modelle uyumlu
- `MAP`: isim/semantik dönüşüm gerekiyor
- `ADD`: onaylı modelde var, kodda yok
- `DEFER`: açık determination veya owner modülü bekliyor
- `EXCLUDE`: legacy/mock/ikinci SoR olduğu için taşınmayacak
- `CONFLICT`: uzlaştırılacak fark; önce teknik eşleme, son iş cevabının önceki metni açıklaması veya gerçek
  yeni sahip kararı olarak ayrılır. Her fark otomatik olarak yöneticiye yeniden sorulmaz.

Bu aşamada kod, enum, veri veya module pack değiştirilmez.

### Aşama 2 — Sahip modüle ayırma

Her `ADD/MAP/DEFER/CONFLICT` satırı tek sahibine yönlendirilir:

- MOD-0117 PPM-owned davranış
- MOD-0136 Budget/version
- MOD-0138 Scenario/comparator
- MOD-0072 Outcome/value
- MOD-0354 DWS
- MOD-0355 BPM
- MOD-0023 Workflow/approval
- MOD-0024 Task
- MOD-0021 Audit
- MOD-0031 Evidence
- MOD-0028/MOD-0029 Document
- MOD-0048 Reference data
- MOD-0288 Organization/person/position
- MOD-0059–0061 KPI/scorecard

Çıkış kanıtı: hiçbir davranışın iki SoR'a verilmediği sahiplik matrisi.

### Aşama 3 — Governance reconciliation

Salt-okunur eşleme tamamlandıktan sonra:

1. DCP-006 içindeki güncelliğini kaybetmiş durum ve sıra bilgileri amendment ile düzeltilir.
2. MOD-0117 module pack'e yalnız PPM-owned uygulanabilir farklar eklenir.
3. Başka modül sahipleri için ayrı backlog/module-pack girdileri hazırlanır; PPM pack'ine kod yetkisi olarak
   sokulmaz.
4. External governance pack'in iş kararları repository kurallarını geçersiz kılamaz.

### Aşama 4 — Tek toplu karar ve uygulama yetkisi

Yöneticiye dosya başına veya küçük değişiklik başına ayrı PR çıkarılmaz. Aşağıdaki paket tek karar turunda
sunulur:

- Eski kod → onaylı model eşleme matrisi
- KEEP/MAP/ADD/DEFER/EXCLUDE/CONFLICT sonuçları
- Sahiplik matrisi
- DCP-006 amendment diff'i
- MOD-0117 amendment diff'i
- Açık OD ve dış-modül blocker'ları
- Önerilen uygulama dilimleri ve PR sınırları

Kod ancak ilgili module pack `approved/ready-for-dev` olduğunda ve açık repository implementation authority
verildiğinde başlar.

### Aşama 5 — Uygulama dilimleri

Küçük dosya PR'ları yerine anlamlı ve test edilebilir dilimler kullanılır:

1. Altı sınıfın exact eşlemesini kapat; sonraki dilimin ihtiyaç duyduğu PPM-owned ortak lifecycle/gate ve
   kayıt bazlı sorumluluk ve MOD-0023 onay sözleşmelerini belirle. Kayıt sorumluluğunun sahibi henüz kesin
   MOD-0288 veya yeni pack olarak atanmış değildir. Tüm Platform'u yeniden geliştirme.
2. Portfolio — backend, frontend, navigation/localization, ilgili entegrasyon ve kabul aynı teslimatın parçasıdır.
3. Initiative — mevcut Core v2 korunarak onaylı model farkları ve browser/UI eksikleri tamamlanır.
4. Program — aynı uçtan uca kabul düzeni.
5. Project/Workspace — aynı uçtan uca kabul düzeni.
6. Investment Case — yalnız onaylı finans kapsamı; gereken MOD-0136/0138 sözleşmeleri ilgili davranıştan önce.
7. Benefit Commitment — planned/realized sınırı; gereken MOD-0072 sözleşmesi ilgili davranıştan önce.
8. Integrated golden flow ve regression closure.

MOD-0288-FU02 Organization matris/custom-field işi ayrı sahipliktedir; PPM'nin kayıt bazlı sorumluluk
çözümlemesini tamamlamaz ve bütünüyle PPM'nin önkoşulu değildir. Kaynak veya sözleşme kesişimi ölçülmeden
paralel yazım güvenli kabul edilmez. MOD-0354 DWS ve MOD-0355 BPM ayrı kapsamlarıyla sonraki planlama
dilimleridir; PPM paketi bu modüllerin tam gereksinim listesini sağlamaz.

Bir dilim bağımsız olarak güvenli değilse yapay biçimde bölünmez. Her PR güncel `main` tabanında hazırlanır;
eski branch doğrudan merge edilmez.

### Aşama 6 — Kabul ve kapanış

Her tamamlanan kullanıcı yüzeyinde:

1. Control Tower repository ve test kanıtını inceler.
2. Kullanıcı eski ve yeni ekranı yan yana kontrol eder.
3. Eksik görülen özellik doğrudan kabul edilmez; Blueprint, doğru sahip modül ve dış ERP benchmark'ı ile
   değerlendirilir.
4. Kullanıcı kabulünden sonra bağımsız Claude incelemesi Platform bağlantısı, tenant/RBAC, sol menü/manifest,
   Gateway, localization, shared-module sınırı ve regression yönünden yapılır.
5. Ana kabul kapıları geçmeden modül `done` veya yüzdeyle tamamlandı ilan edilmez.

## 6. Açık kapılar

| Kapı | Durum | Etki |
|---|---|---|
| Eski kod → onaylı model eşlemesi | db2e2ef2 tabanında rapor ve correction alındı; açık sahiplik/kararlar §10'da | Rapor uygulama veya migration yetkisi değildir |
| Bağımsız teknik inceleme | Kısmi: envanter ve Portfolio sözleşme incelemesi alındı | Nihai tasarım/runtime kabulü ve coding baseline tamamlanmadı |
| Yetkili belge imzası | Açık | Formal belge onayı yoktur |
| Repository implementation authority | Açık | Yeni governance farkları kodlanamaz |
| OD-01 ve OD-02 | Açık | Governance pack'e göre genel teknik/configuration başlangıç kapısı kapanmamıştır |
| OD-04/07/11/12 | Açık | Finans ve benefit alanları belirlenen sınırlar içinde kapalıdır |
| MOD-0023/0031/0288 ve diğer owner kontratları | Kısmi/açık | İlgili PPM geçişleri fail-closed kalır |

## 7. İlerleme raporu biçimi

Paydası kanıtlanmadan yüzde verilmez. Her büyük aşamadan sonra şu sayılar raporlanır:

- Governance hedefindeki toplam alan/durum/geçiş sayısı
- Main'de birebir bulunan sayı
- MAP gereken sayı
- ADD gereken sayı
- DEFER/CONFLICT sayısı
- Backend, frontend, persistence ve test kanıtı bulunan satır sayısı
- Main dışında fakat korunması gereken commit/uncommitted iş sayısı
- Tamamlanan ve açık kabul kapıları

Yüzde ancak eşleme matrisi tamamlandıktan ve payda sabitlendikten sonra ayrıca hesaplanabilir.

## 8. İlk sıradaki iş

Altı sınıfın sabit-main envanteri ve dar correction alındı. Sıradaki iş §10'daki Portfolio iş kararlarını
tek turda kapatmak ve teknik sözleşmelerin exact kapsamını hazırlamaktır. Envanter sıfırdan tekrarlanmaz;
uygulama hazırlığında yeni main farkları ayrıca ölçülür. Henüz runtime implementation yetkisi verilmedi.

## 9. 2026-09-07 koordinasyon ve teslimat güncellemesi

### 9.1 FU02 uygulama promptu incelemesi

İncelenen Organization worktree: `/private/tmp/ERP-vNext-mod0288-fu02-pack`.
Checkpoint: `162bd959b38fbc0c88404e8d1b06a6c6420338db`; çalışma ağacı temiz.
`db2e2ef2..162bd959` farkı yalnız bir module pack dosyasıdır; bu branch'teki yeni FU02 runtime
teslimatı sıfırdır. Bu, mevcut MOD-0288'in yapılmamış olduğu anlamına gelmez.

Karar: PPM sınırı §21.4'te doğru ayrılmıştır; fakat uygulama öncesi pack iç tutarlılığı kapatılmalıdır.
Bu inceleme yeni uygulama yetkisi vermez ve Organization pack'ini değiştirmez.

Toplu correction girdileri:

- §8 karar 1 / §21.4 onayı değiştirmiyor; §16 AC4, §18, §20 ve §21.1 girişinde eski işlevsel-hat
  onayı ifadeleri kalmış. Aynı geçerli karar bütün normatif bölümlere uygulanmalı.
- §12 bilinçli aynı-ebeveyn seçimini kabul ediyor; §13 aynı primary/secondary hedefi 400 olarak listeliyor.
- §8/18 kesin `organization.field-definition.read/manage` anahtarlarını ayrı değerler olarak tanımlarken
  §14 eski `platform.organization-units.*` önerilerini taşıyor. Değer yazma ve matris yetkileri de açık olmalı.
- §5 yeni handler/validator dosyalarını “corresponding” diye bırakıyor; mimari test yolunu sonraya erteliyor.
  Exact path iddiası bu haliyle tamamlanmış değil. §17 build yolunda `Api` / `API` çelişkisi sürüyor.
- Alan başına ayrı value entity/collection anlatımı ile gömülü array + `$elemMatch` anlatımı uzlaştırılmalı.
  Tenant-first indeksler, uniqueness, soft-delete, Classification okuma/filtre yetkisi ve gerçek sorgu şekli
  birlikte tanımlanmalı. İndeks sayısının azlığı query performans kanıtı değildir.
- `IsQueryable` §8'de UI bayrağı; §12/16'da server-side filtre kapısı. Tek davranış seçilip testlenmeli.
- Tanım sayısı sınırının değeri veya belirleme mekanizması, supported types/operators ve paging sınırları
  somutlaştırılmalı. Grafik eşzamanlılık çözümü süreçler arası güvenliği göstermeli; yalnız process-local
  kilit veya bağımsız belgeleri tekrar okumak tek başına kanıt sayılmaz.
- §21.4'te MOD-0018'in hiç yazılmadığı / delegation'ın hiç tüketilmediği çıkarımı pack statüsünden
  yapılmamalı. Mevcut authorization kodu ve `DelegateWorkflowTaskHandler` vardır; PPM'ye uygunluk ayrı ölçümdür.
- Mutasyon testi istenirse paylaşılan çalışan servis kaynağı sabote edilmemeli. İzinli disposable test
  kopyası veya test aracının geçici çıktısı kullanılmalı; bu yeni kalıcı geliştirme worktree'si değildir.

### 9.2 İş sırası ve sahiplik

1. Organization sahibi yukarıdaki FU02 correction'larını tek turda toparlar; PPM incelemesi FU02'nin
   bütün uygulamasını beklemek zorunda değildir. Ortak Platform dosyaları için single-writer korunur.
2. Altı sınıfın db2e2ef2 tabanındaki eşleme raporu ve correction teslim alındı; §10 bu aşamanın güncel durumudur.
3. Kayıt → rol → kişi, vekâlet/geçerlilik, preparer/approver ayrımı ve çoklu/sıralı onay ihtiyaçları mevcut
   MOD-0288/MOD-0023/MOD-0018 koduna karşı incelenir. Kullanılabilir parçalar yeniden yazılmaz.
4. Toplu governance mutabakatından sonra ilk kullanıcı modülü Portfolio, sonra Initiative, Program,
   Project/Workspace, Investment Case ve Benefit Commitment'tır (§5 Aşama 5).
5. DWS ve BPM kendi existing-code/legacy/owner incelemeleriyle devam eder; kapsam netleşmeden tam teslimat
   yüzdesi veya kesin prompt sayısı verilmez.

### 9.3 Tek çalışma alanı ve Git teslimat düzeni

- Codex geliştirmesi için tek aktif, önceden belirlenmiş worktree kullanılır; her prompt yeni worktree açmaz.
- Büyük geliştirmeler kullanıcıya verilen promptlarla başka sohbette yürütülür; aynı worktree'ye tek yazıcı
  atanır ve sohbetler sırayla çalışır. Control Tower bu sırada salt-okunur inceler.
- Claude kendi çalışma alanında kalır; onun branch'i veya runtime'ı devralınmaz.
- Yerel checkpoint commitleri anlamlı geri dönüş noktalarıdır. Push aynı feature branch'i günceller;
  push başına PR açılmaz. Yedek push küçük commitler içerebilir, yöneticinin merge etmesini gerektirmez.
- PR sınırı dosya sayısı değil test edilmiş iş bütünüdür. Governance correction'ları ve uygulama düzeltmeleri,
  ilgili yetki hiyerarşisi elverdiğinde aynı teslimatta toplanır. Zorunlu önce-merge kapısı varsa saklanmaz.
- Kullanıcı ve Claude incelemesindeki düzeltmeler aynı feature branch / PR üzerinde biriktirilir.
- Merge yöneticidedir. Merge sonrası dirty iş korunarak güncel main doğrulanır; aynı worktree'de sonraki
  feature branch'e geçilir. Merge beklerken sonraki dilimin salt-okunur analizi yapılabilir.
- Bu plan güncellemesi stage, commit, push, PR veya merge yetkisi vermez.

### 9.4 Oranlar ve prompt tahminlerinin anlamı

Modülün tamamlanma yüzdesi yalnız ortaklaşa kabul edilmiş gereksinim satırlarının kanıtlı kabulüyle
hesaplanır. Entity/view varlığı veya geçen test sayısı tek başına modül yüzdesi değildir. 25 ortak ve
62 özel alan, tüm alanların tüm sınıflara uygulanacağı anlamına gelmez; applicability eşlemesi gerekir.

FU02 için yalnız incelenen branch'in yeni runtime teslimatı 0'dır; ana Organization modülünün oranı değildir.
Altı PPM sınıfının entity ve liste/form dosyaları ağaçta vardır; yeni governance hedeflerine göre kabul
oranları henüz ölçülmemiştir. Bu hücreler 0 olarak raporlanmaz.

Prompt tahmini bir ana görevin analiz/uygulama/inceleme-correction turlarıdır; mesaj sayısı, süre garantisi
veya PR sayısı değildir. İlk hazırlık (eşleme + toplu mutabakat) yaklaşık 2–3 tur; FU02 correction + backend
uygulama + inceleme için 3–5 tur bir ön planlama aralığıdır. Kalan modül tahminleri exact eşleme sonrası
yeniden kalibre edilir; açık owner kararları ve yeni scope bu aralıklara otomatik dahil değildir.

## 10. Portfolio mutabakatı — envanter sonrası güncel karar kaydı

Bu bölüm önceki geçici form sayısı, sahiplik ve sıradaki iş ifadelerini günceller. Yalnız koordinasyon
kaydıdır; module pack, kaynak kod, servis veya Git teslimat yetkisi değildir.

### 10.1 Kanıt ve kapanan değerlendirmeler

- Denetim tabanı: `db2e2ef2781d94ca5bbc56ee419f1ac88125c6d9`. Bu tarihsel sabit taban güncel uzak main
  olarak sürekli varsayılmaz; uygulama preflight'ında yeni farklar ayrıca doğrulanır.
- Altı sınıfın entity, application/CQRS, persistence, API ve frontend zinciri mevcut. Yeniden yazım yok.
- Envanter raporu: `/Users/alitufanoglu/.codex/attachments/099c8cb7-9f59-4a23-bb6b-ca6dd8537898/pasted-text.txt`.
- Dar correction: `/Users/alitufanoglu/.codex/attachments/4f9ea35d-6412-4189-ab14-212c70edd164/pasted-text.txt`.
- Claude teknik kapanışı bu konuşmada teslim alındı; kaynak raporun varsayımları aşağıdaki sınırlarla
  kabul edildi. İletilen raporlar yeni test yürütüldüğü anlamına gelmez.
- DWS yapısal hiyerarşi/dependency/baseline sahibidir; schedule baseline, milestone tarihi/gerçekleşmesi
  ve deliverable kabulü DWS'ye yüklenmez. Bu davranışların executable owner sözleşmesi açık; Portfolio
  sırasını engellemez ve ES retrofit'ini iş emrine dönüştürmez.
- Portfolio mevcut formu 3 girdidir: Code, Name, Description. Hedef 13/15 sayısı geri çekildi.
  Kullanıcı seçimi, sistemden türeyen veri, owner'dan okunan bilgi ve ayrı aksiyon girdisi ayrılınca
  gerçek alan sayısı belirlenir; ardından <=8 Slim, >8 Compact uygulanır. Tasarım seçimi iş kararı değildir.
- GUID/display reference ve teknik CAS/onaylı iş sürümü ayrı tutulur; yöneticiye yeniden oylatılmaz.
- 15/55 yalnız başlangıç/hedef durum adlarının eşleşmesidir; PPM tamamlanma oranı değildir.
- Mevcut öneri listesi exact implementation allowlist değildir; karar sonrası yollar tek tek belirlenecek.

### 10.2 Onay ve kimlik sözleşmesinin zorunlu sınırları

1. PPM lifecycle sahibidir; MOD-0023 onayı yürütür; WorkCenter yalnız gerçek approval işini gösterir.
2. Authoritative preparer ve StartedBy ayrı anlamlardır. Preparer zorunluluğu istemcinin alan gönderip
   göndermemesinden değil güvenilir PPM işlem/politika sözleşmesinden belirlenir. Eksik preparer için
   StartedBy fallback'i yoktur. Tarayıcıdan serbest kimlik beyanı kabul edilmez.
3. Diğer tüketicilerin mevcut SoD davranışı sessizce değişmez. PPM ve legacy sözleşme ayrımı, istemcinin
   daha zayıf profili seçerek PPM denetimini atlamasına izin vermez. Devir/onayda kontroller sürdürülür.
4. Hatalı istek, durum çakışması ve authoritative bağımlılık kesintisi ayrı HTTP sonuçlarıdır;
   400/409/503 exact sözleşmede belirlenir. Her eksik/belirsiz kimlik otomatik 400/409 sayılmaz.
5. WorkflowInstanceId kesin workflow'u seçer ama tek başına karar bağı değildir. Tenant, kayıt, talep
   edilen işlem ve ilgili PPM sürümüyle eşleşme gerekir. Latest-by-object ve TemplateVersionId bu
   kayıt sürümü bağının yerine geçmez.
6. Workflow tarafı idempotency, PPM mutation idempotency'sinin yerine geçmez. PPM'de onay tüketim/uygulama
   tekilliği, lifecycle değişikliği, CAS ve gerekli audit aynı atomik sınırda korunmalıdır.
7. Polling seçilmedi; sonuç teslim yöntemi ve instance-ID ile sorgulamanın executable API'si açık.
   Mevcut EvaluateWorkflowTransitionGate bu yeni sözleşmeyle eşdeğer ilan edilmez.
8. Sıralı çok adımlı motor kodda mevcut; yayınlama ve uçtan uca kabulü doğrulanmadı. Yeni motor yok;
   paralel AND yalnız açık gereksinim varsa ele alınır. Beş dev kaydı geçmişte hiç kullanılmadığını kanıtlamaz.
9. PersonReference ve auth hesabı ayrı kavramlardır. Tipli bağ, cardinality/uniqueness, authoritative
   tenant doğrulaması ve iki yaşam döngüsünün kontrolü birlikte gerekir; tek alan bunları sağlamaz.
10. Kayıt-kapsamlı sorumluluk ile sıralı/paralel onay bağımsızdır. PPM-owned rol ile ortak atama mekanizması
    ayrılmadan yeni module/pack sahipliği veya PositionAssignment'ın birebir kopyası kararlaştırılmaz.

### 10.3 Tek toplu iş kararı listesi — öneriler, henüz onay değil

| Konu | Control Tower önerisi | Karar sahibi / engellediği iş |
|---|---|---|
| Funding Ceiling ve aktivasyon | OD-04/07 kapanana kadar schema'ya alan ekleme; finans şartını atlama. Draft kayıt yönetimi ayrı kısmi kabul olabilir, tam Portfolio kapanışı değildir. Aktif kullanım hedefleniyorsa owner sözleşmesini önceliklendir. | CFO/Group Finance; istisna istenirse yetkili governance sahibi. Draft → Active engeli. |
| Confidentiality OD-05 | Yeni seviye adları uydurma; onaylı seviyeler ile her seviyenin kimlere hangi erişimi verdiği birlikte tanımlansın. Yetki yalnız UI gizleme ile uygulanmasın. | Atanmış OD-05 sahibi; erişim ve nihai kabul. |
| Performance Status | Otomatik hesaplama kaynağı yoksa, açık owner onayıyla tarih/gerekçe/audit taşıyan manuel review değerlendirmesi öner. Otomatik hesaplanmış gibi gösterme. | PPM süreç sahibi / Portfolio Owner; değerlendirme davranışı. |
| Portfolio Risk | İlk dilimde onaylı SOP-0004 ölçeği üzerinden manuel, auditli review öner; otomatik aggregate risk iddiası üretme. Ölçek henüz doğrulanmadıysa değer uydurma. | PPM süreç sahibi / Portfolio Owner; rating kaynağı ve güncelleme yetkisi. |
| Capacity Allocation | İlk dilimde allocation motoru değil, kaynak modelinin yerini tutmadığı belirtilen serbest açıklama öner. Kaynak rezervasyonu veya kapasite hesabı üretme. | PPM business owner; ilk dilim kapsamı. |

Form/aksiyon yerleşimi, DTO, API, hata eşlemesi ve Golden seçimi repo kurallarıyla önerilecek teknik
tasarımdır; yöneticiye gereksiz mühendislik sorusu olarak gönderilmez. Strategic Objective ve Organization
seçimlerinin gerçek provider/kimlik/erişim sözleşmeleri ayrıca teknik kapıdır; mock seçenekle kapatılamaz.

### 10.4 Sonraki teslimat ve sahiplik

- Önce §10.3 tek karar turu; bu sırada yalnız izinli salt-okunur teknik kapsam hazırlığı yapılabilir.
- Claude: Organization/FU02 kendi alanında kalır; MOD-0023/kimlik ve gereken Platform deltaları onunla
  koordine edilir. Bu liste kendisine implementation veya commit emri değildir.
- Codex: PPM bağları, Portfolio davranışı ve ekran tasarımı. Büyük uygulama için kullanıcıya başka
  sohbette çalıştırılacak prompt verilir; tek aktif Codex worktree ve single-writer korunur.
- Sahiplik/karar sonrası toplu governance amendment ve exact allowlist hazırlanır; açık uygulama
  yetkisi olmadan kod başlamaz. Her küçük correction için ayrı PR hedeflenmez.
- Teslim: teknik kabul → kullanıcı ekran kontrolü → Claude uygunluk incelemesi → aynı feature teslimatında
  correction → yönetici merge'i. Yalnız plan değişikliği için şimdi push/PR açılmaz.

### 10.5 Bağlantı backlog'u — Portfolio ↔ MOD-0136 Bütçe

**Durum: AÇIK — iş kararı ve executable owner sözleşmesi bekliyor.**
Kullanıcı bu bağlantının ayrı takip edilmesini onayladı; bu kayıt uygulama veya finansal sahiplik kararı
değildir. Bütçe modülünün tek başına tamamlanması bu entegrasyonu kapatmaz.

| Takip alanı | Tanım |
|---|---|
| İş | Portfolio'nun bütçe tavanını onaylanmış authoritative bütçe kaynağına bağlama ve PPM koşullarında kullanma |
| Tek takip kaynağı | Bu bölüm; aynı iş için kopuk ve mükerrer backlog açılmaz |
| İş kararı önkoşulu | Dış PPM paketi OD-04/07, finansal custody ve tutar/para birimi/sürüm anlamı netleşmeli; bu OD'ler repo DCP-006 OD numaralarıyla karıştırılmaz |
| Teknik önkoşul | MOD-0136 sahibi ile kullanılabilir typed-reference/read/validation sözleşmesi, izinler ve hata davranışı doğrulanmalı; hazır API veya olmayan veri varsayılmaz |
| Sağlayıcı sorumluluğu | Bütçe sahibi authoritative veriyi ve doğrulama sözleşmesini sağlar; exact kapsam ve görev sahibi koordinasyonda atanacak |
| Tüketici sorumluluğu | MOD-0117 Portfolio bağlantıyı tüketir, mutation sırasında gerekli iş koşullarını yeniden kontrol eder; ikinci bütçe veri sahibi oluşturmaz |
| Açılma tetikleyicisi | İlgili finans kararları veya MOD-0136 sözleşmesi hazır olduğunda bu kayıt yeniden ele alınır; bu ifade zamanlanmış otomatik izleme değildir |
| Kapanış kanıtı | Onaylı sözleşme + implementation referansı + hedef entegrasyon/regresyon testleri + kullanıcı kabulü; yalnız Budget/Portfolio build'i veya ekran varlığı yeterli değildir |
| Portfolio etkisi | Bütçe tavanı zorunlu kaldığı sürece Draft → Active kapısı bu iş tamamlanmadan açılmaz. Kısmi Draft teslimatı bu bağlantıyı veya tam Portfolio kabulünü kapatmaz |

Asgari kabul kapsamı:

- Doğru tenant, Portfolio, bütçe kimliği ve onaylı sürüm eşlemesi.
- Tutar/para birimi ve güncellik kurallarının finans sahibiyle belirlenmiş sözleşmeye uygunluğu.
- Yetkisiz ve cross-tenant erişimin reddi; eksik/silinmiş/geçersiz referansın güvenli ele alınması.
- Kaynak kullanılamadığında tahmini/sentetik bütçe veya koşulsuz aktivasyon yok; sözleşmeye uygun fail-closed davranış.
- Aktivasyon sırasında yetki ve bütçe koşulları yeniden doğrulanır; değişen sürüm ve eşzamanlılık senaryoları testlenir.
- Bütçe ana verisi PPM'ye kopyalanmaz; izinli referans/kanıt kapsamı exact amendment'ta belirlenir.

İlgili MOD-0117 ve MOD-0136 pack/teslimat planlarına bu bölümün bağlantısı, sahiplerinin izinli toplu
amendment turunda eklenmelidir. **Bu turda o belgeler değiştirilmedi; karşılıklı referans işi açık.**
Her Portfolio kullanıcı kabulü ve ilgili Budget teslimatı kapanışında bu kayıt kontrol edilir. Başka bir
iş tamamlandığı için otomatik kapanmaz; iptal/defer kararı verilirse gerekçe ve Portfolio etkisi kaydedilir.
