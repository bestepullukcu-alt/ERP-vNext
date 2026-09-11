# DCP-006 PPM Governance Reconciliation Control Plan

> 2026-09-08 teslimat notu: Bu belge PPM Control Tower koordinasyon planıdır; bulunduğu çalışma
> klasörü belge sahipliğini belirlemez. Mevcut plan bu feature teslimatına alınmıştır; henüz main'e
> merge edildiği iddia edilmez. Tarihsel ölçümler ve yerel kanıt yolları kendi tarihlerine aittir;
> yerel ekler diğer makinelerde mevcut varsayılmaz. Güncel karar durumu §10'da tutulur.
> Bu kayıt bütçe entegrasyonu veya yeni governance özellikleri için uygulama yetkisi vermez.

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

**Kullanıcıyla mutabık sayfa bazlı teslim düzeni — 2026-09-10:** İlk kullanıcı kabulü Portfolio'nun
liste/create/edit/detail ve onaylı owner aksiyonlarını kapsayan kullanılabilir dilimi içindir;
tek düğme veya yalnız backend kabulü değildir. Sıra: Portfolio → Initiative → Program →
Project/Workspace → Investment Case → Benefit Commitment → bütünleşik regresyon. Mevcut kod
korunarak ilerlenir; DWS/BPM ayrı kapsam olarak kalır.

- Her dilimde teknik kontrol → kullanıcı eski/yeni ekran kabulü → Claude bağımsız uygunluk
  incelemesi → gerekli düzeltme ve yeniden test → anlamlı tek teslimat/PR sırası uygulanır.
- Kullanıcı kontrolü ve mevcut dilimin teslimat kapanışı olmadan sonraki sayfanın geliştirmesine
  başlanmaz. Bağımsız altyapı CT kuyruğu kendi sahipliğinde yürüyebilir.
- Push/merge, deployment veya production activation değildir. Her Git yazma ve canlıya alma
  işlemi ilgili yetki kapısını ayrıca karşılar. Canlıya geçişte mevcut veri uyumluluğu,
  yapılandırma/bağımlılıklar, geri dönüş yöntemi ve yayımlama sonrası doğrulama gerekir.
- Portfolio ilk hedefi kısmi Draft kullanımıdır. Gizlilik/kayıt erişimi çözülmeden gerçek veride
  Draft kullanımı; bütçe ve diğer activation koşulları kapanmadan Active geçişi açılmaz.
  Kullanılabilir dilimin kabulü, Portfolio'nun bütün kapsamının tamamlandığı anlamına gelmez.
- Güvenli bağımsız canlı dilim çıkarılamıyorsa blocker bildirilir; kullanıcıyla sıra değişikliği
  kararlaştırılmadan sonraki sayfaya geçilmez ve test başarısı production hazır oluş diye sunulmaz.

Bu süreç mutabakatı açık iş seçimlerini, module pack uygulama kapısını veya production yetkisini
kapatmaz. Yeni worktree veya küçük dosya başına PR açılmaz; mevcut plan ve çalışma alanı korunur.

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

Altı sınıfın sabit-main envanteri ve dar correction alındı. 2026-09-08'de iletilen beş Portfolio iş kararı
§10.3'e işlendi; sıradaki iş eksik kaynakları ve §10.8 teknik sözleşmelerini kapatmaktır. Envanter sıfırdan
tekrarlanmaz; uygulama hazırlığında yeni main farkları ayrıca ölçülür. Bu taslak için repository
implementation authority verilmedi; MOD-0117'nin önceki sınırlı onayları kendi kapsamlarında korunur.

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

<a id="portfolio-first-delivery-control"></a>

### 10.3 Portfolio İlk Teslimat Kararları — 2026-09-08 / DRAFT / NON-EXECUTABLE amendment

**WP: PPM-PORTFOLIO-GOV-01 / prompt v1; dar correction v2: 2026-09-09 / PPM-GOV–DEV / Profile B.** Önceki öneri tablosunun iş kararı
durumu bu bölümle güncellendi. Kaynak: kullanıcının ilettiği, **Natig Yusubov / CEO'ya atfedilen**
8 Eylül 2026 tarihli “Portfolio İlk Teslimat Kararları” yanıtı. Bağımsız imza doğrulaması yapılmadı;
v1.5.7 ZIP güncellenmiş veya formal olarak imzalanmış sayılmıyor. §2'deki ZIP/checker kanıtları tarihseldir,
bu turda yeniden çalıştırılmadı. Yanıt iş kararlarını aktarır; repository implementation authority vermez.

Eşleşen sözleşme: [MOD-0117 Portfolio taslak amendment](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-first-delivery-draft).
Pack `review` olarak kalır; önceki dilim/onaylar korunur; bu amendment `draft`, `production_authority: none`.
Buradaki finans **OD-04/07** ve gizlilik **OD-05**, dış PPM Governance Pack kararlarıdır; DCP-006'nın aynı
numaralı kapıları değildir. Bu kayıt bunları kapatmaz.

Correction v2'de kullanıcının 8 Eylül yanıtından ilettiği aktivasyon şartı açıkça korunur:
“Strategic objective, accountable owner, funding ceiling and review frequency all set.” Stratejik hedef,
accountable owner, finans koşulu ve Review Frequency birlikte sağlanmalıdır. Bu iş gereksinimi mevcut kodda
uygulanmış sayılmaz; aktivasyon zorunluluğu Draft kaydında aynı alanların zorunlu olduğu kararı değildir.
Finans koşulu §10.5 typed-reference ve dış OD-04/07 sınırına tabidir; PPM-native Funding Ceiling yetkisi yok.

| Konu | İletilen kesin iş kararı | Açık bağımlılık / kabul sınırı |
|---|---|---|
| Bütçe | İlk teslimat Draft kayıt yönetimi olabilir. MOD-0136'da doğrulanmış bütçe/fon taahhüdüne typed reference kurulmadan Draft → Active kapalı kalacak. Dış OD-04/07 kapanmadan PPM-native Funding Ceiling oluşturulmayacak. | Stratejik hedef, accountable owner, doğrulanmış finans koşulu ve Review Frequency aktivasyonda birlikte gerekir; ilgili erişim/onay şartları da korunur. Finans kaynağı/approved sürüm/güncellik sözleşmesi açık. Draft teslimatı tam Portfolio kabulü değildir. Tek entegrasyon kaydı §10.5. |
| Review Frequency | Aktivasyondan önce gözden geçirme sıklığı belirlenmiş olmalı; iş gereksinimi mevcut. | Birim, kontrollü değerler, varsayılan, saklama tipi ve create/edit veya ayrı aksiyon yerleşimi OPEN; bu detaylar için kaynak kararı doğrulanmadı. Draft-save zorunluluğu çıkarılmaz. Otomatik takvim/bildirim/Workflow işi kararı yok. |
| Gizlilik | SOP-0029 esas; görme hakkı değiştirme/onaylamadan ayrı. Yönetici dış OD-05 kapanana kadar en kısıtlayıcı seviyeyi istemiştir. | Gerçek seviyeler ve rol-görünürlük eşlemesi sağlanmadığından karar uygulanabilir değildir. Draft da erişim politikası gerektirir. Seviye/rol/yalnız oluşturan görür kuralı/fallback uydurulmaz. |
| Performance Status | Portfolio owner manuel **Ahead of Plan / On Track / At Risk / Off Track** seçer; zorunlu gerekçe ve tam audit. | Business label'ları runtime kodu, default veya authoritative vocabulary sağlayıcısı seçmez. Owner/actor doğrulaması açık; code/provider/değişiklik yönetişimi tasarımı SOP-0004/0029 kaynaklarını beklemez. Risk/erişim kabul kapıları korunur; otomatik hesaplama §10.7'de ayrı OPEN. |
| Portfolio Risk | Portfolio owner SOP-0004 ölçeğinden manuel değerlendirir; gerekçe ve tam audit zorunlu, otomatik birleştirme yok. | Uygulanabilir gerçek ölçek/sürüm doğrulanamadı: OPEN. Değer üretilmez; owner doğrulaması ayrı teknik bağımlılıktır. |
| Capacity Allocation | Expected, mandatory değil; ilk teslimatta serbest metin açıklaması. | Rezervasyon, kişi ataması veya kapasite hesabı üretmez. Boş olması tek başına Draft kaydını engellemez. Gerçek kaynak modeli/entegrasyonu §10.7'de OPEN. |

SOP numarası aramasındaki QMS/PV kayıtları ve migration manifest referansları, Portfolio için doğru SOP
sürümünü/ölçeğini veya erişim matrisini kanıtlamaz; onların değerleri aktarılmadı. Kaynak sahiplerinden
SOP-0029'un uygulanabilir seviyeleri/rol matrisi ve SOP-0004'ün uygulanabilir onaylı ölçeği beklenir.
Form/aksiyon yerleşimi ve Golden seçimi mühendislik tasarımıdır; bilinen iş kararları yeniden oylatılmaz.

### 10.4 Sonraki teslimat ve sahiplik

- Beş iş kararı §10.3'te kayıtlı; kaynak/sözleşme kapıları §10.5–10.8'de açık tutulur. Taslağın yazılması
  bu eksiklerin kapanmasını beklemez; uygulama ise açık bağımlılıkları atlayamaz.
- Claude: Organization/FU02, Platform, Auth ve Workflow kendi alanında kalır. §10.8 yalnız gereken teknik
  sözleşme kanıtları/PPM tasarım önerileridir; code/pack/görev emri veya yeni shared-module sahiplik ataması değildir.
- Codex: PPM bağları, Portfolio davranışı ve ekran sözleşmesinin kendi alanında hazırlanması. Tek aktif
  worktree/single-writer düzeni korunur; bu tur yalnız iki belgeyi değiştirir.
- İlk Draft teslimatı, güvenli Draft kayıt yönetimi ile sözleşmesi kapanmış manuel değerlendirme ve
  açıklama davranışının kısmi kabul hedefidir. Gizlilik/owner/vocabulary bağımlılıkları nedeniyle şimdi
  implementation-ready alt küme veya exact runtime allowlist oluşturulmaz.
- Hedefin dışında: tam Portfolio kabulü/aktivasyon, finans SoR'u, performans otomasyonu, risk birleştirme,
  gerçek kaynak entegrasyonu, diğer PPM yüzeyleri, WorkCenter onay/lifecycle sahipliği ve migration.
- Eksik kaynaklar, teknik mutabakat, mevcut kayıt uyumluluğu ve exact kapsam tamamlanıp ilgili amendment
  ayrıca onaylanmadan ve açık repository implementation authority verilmeden yeni kod başlamaz. Önceki
  sınırlı pack onayları bu taslağa genişletilmez veya geri alınmaz.
- İlerideki teslim: teknik kabul → kullanıcı ekran kontrolü → Claude uygunluk incelemesi → aynı teslimatta
  correction → yönetici merge'i. Bu governance turunda stage/commit/push/PR/merge veya ayrı merge talebi yok.

<a id="portfolio-budget-integration"></a>

### 10.5 Bağlantı backlog'u — Portfolio ↔ MOD-0136 Bütçe

**Durum: AÇIK — Draft/aktivasyon iş kararı §10.3'te kayıtlı; dış OD-04/07 ve executable MOD-0136
owner sözleşmesi bekleniyor.** Kullanıcı bu bağlantının ayrı takip edilmesini onayladı; bu kayıt uygulama
yetkisi vermez ve finansal sahipliği PPM'ye taşımaz. Bütçe modülünün tek başına tamamlanması bu entegrasyonu kapatmaz.

| Takip alanı | Tanım |
|---|---|
| İş | Portfolio'yu MOD-0136'da doğrulanmış bütçe/fon taahhüdüne typed reference ile bağlama ve aktivasyonda doğrulama; PPM-native Funding Ceiling oluşturma yetkisi yok |
| Tek takip kaynağı | Bu bölüm; aynı iş için kopuk ve mükerrer backlog açılmaz |
| İş kararı önkoşulu | §10.3 Draft/aktivasyon kararı kayıtlıdır; dış PPM paketi OD-04/07, finansal custody ve tutar/para birimi/sürüm anlamı hâlâ netleşmeli; bu OD'ler repo DCP-006 OD numaralarıyla karıştırılmaz |
| Teknik önkoşul | MOD-0136 sahibi ile kullanılabilir typed-reference/read/validation sözleşmesi, izinler ve hata davranışı doğrulanmalı; hazır API veya olmayan veri varsayılmaz |
| Sağlayıcı sorumluluğu | Bütçe sahibi authoritative veriyi ve doğrulama sözleşmesini sağlar; exact kapsam ve görev sahibi koordinasyonda atanacak |
| Tüketici sorumluluğu | MOD-0117 Portfolio bağlantıyı tüketir, mutation sırasında gerekli iş koşullarını yeniden kontrol eder; ikinci bütçe veri sahibi oluşturmaz |
| Açılma tetikleyicisi | İlgili finans kararları veya MOD-0136 sözleşmesi hazır olduğunda bu kayıt yeniden ele alınır; bu ifade zamanlanmış otomatik izleme değildir |
| Kapanış kanıtı | Onaylı sözleşme + implementation referansı + hedef entegrasyon/regresyon testleri + kullanıcı kabulü; yalnız Budget/Portfolio build'i veya ekran varlığı yeterli değildir |
| Portfolio etkisi | Gelecekteki delta: doğrulanmış bütçe/fon taahhüdü typed reference olmadan Draft → Active kapısı açılmaz; stratejik hedef, accountable owner, Review Frequency ve ilgili erişim/onay koşulları da korunur. Mevcut kod bu geçişe izin verir, yeni bütçe engeli uygulanmış değildir. Kısmi Draft teslimatı bu bağlantıyı veya tam Portfolio kabulünü kapatmaz |

Asgari kabul kapsamı:

- Doğru tenant, Portfolio, bütçe kimliği ve onaylı sürüm eşlemesi.
- Tutar/para birimi ve güncellik kurallarının finans sahibiyle belirlenmiş sözleşmeye uygunluğu.
- Yetkisiz ve cross-tenant erişimin reddi; eksik/silinmiş/geçersiz referansın güvenli ele alınması.
- Kaynak kullanılamadığında tahmini/sentetik bütçe veya koşulsuz aktivasyon yok; sözleşmeye uygun fail-closed davranış.
- Aktivasyon sırasında yetki ve bütçe koşulları yeniden doğrulanır; değişen sürüm ve eşzamanlılık senaryoları testlenir.
- Bütçe ana verisi PPM'ye kopyalanmaz; izinli referans/kanıt kapsamı exact amendment'ta belirlenir.

2026-09-08: MOD-0117'nin [Portfolio taslak amendment'ı](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-first-delivery-draft)
bu tek kayda bağlandı. MOD-0136 pack'i değiştirilmedi; sahibiyle karşı referans ve executable sözleşme
mutabakatı hâlâ açık. Yeni bütçe backlog'u oluşturulmadı.
Her Portfolio kullanıcı kabulü ve ilgili Budget teslimatı kapanışında bu kayıt kontrol edilir. Başka bir
iş tamamlandığı için otomatik kapanmaz; iptal/defer kararı verilirse gerekçe ve Portfolio etkisi kaydedilir.


### 10.6 Portfolio dar kanıtı, form ayrımı ve korunacak temel

İnceleme tabanı: `e72701fa565942187e7dd85c1e92fdcc25080969`, 2026-09-08, kullanıcının belirttiği
`/Users/alitufanoglu/ERP-vNext-codex-current`. Başlangıç `codex/ppm-es-recovery-closure`, temiz; izinli
`codex/ppm-portfolio-first-delivery` dalı aynı HEAD'de oluşturuldu. Uzak main güncelliği iddia edilmez.
Altı sınıf yeniden denetlenmedi; pack amendment'ının dar kanıt tablosundaki Portfolio dosyaları okundu.

| Ayrım | Kanıt / taslak kararı |
|---|---|
| Mevcut form | Code, Name, Description = **3** kullanıcı girdisi; sırasıyla required/max 64, required/max 200, optional/max 2000; mevcut normalizasyon/tenant uniqueness korunur. |
| Önerilen form girdisi | Capacity Allocation açıklaması optional; create/edit'e yerleşirse sayılır, toplam bundan türetilmez. Hedef envanter, metin limiti ve yerleşim OPEN. |
| Ayrı aksiyon | Performance Status + gerekçe, Portfolio Risk + gerekçe ve lifecycle; create/edit sayımına katılmaz. Değerlendirmelerin geçerli lifecycle durumları/DTO/API/permission tasarımı açık. |
| Salt okunur/sistem | Lifecycle, teknik Version, ID/tenant/tarih/audit, son değerlendirme ve owner modülden okunan veri sayılmaz. |
| Review Frequency | Aktivasyon iş gereksinimi; birim, kontrollü değerler, varsayılan, saklama tipi ve create/edit veya ayrı aksiyon yerleşimi OPEN. Yalnız create/edit kullanıcı girdisi olursa sayılır; Draft-save zorunluluğu veya otomasyon çıkarılmaz. |
| Stratejik hedef referansı | MOD-0352 / ESBP iş alanına aittir; aktivasyonda gerekir. Portfolio'ya uygun executable provider/version/access sözleşmesi doğrulanmadı/OPEN; Organization sözleşmesinden ayrı. Salt okunur sağlayıcı verisi sayılmaz. |
| Organization referansı | MOD-0288 / Organization-FU02 Claude alanında; typed identity/access/selection sözleşmesi OPEN. Stratejik hedef otoritesini sağlamaz; salt okunur veri sayılmaz. |
| Accountable owner | Aktivasyonda gerekir; PPM kayıt sorumluluğu ve authoritative person/account/access doğrulaması ayrılır. CreatedBy owner kanıtı değildir. Yerleşim ve Draft-save zorunluluğu OPEN. |
| Ertelenmiş bağlantı | Bütçe/fon taahhüdü §10.5; gerçek kaynak entegrasyonu §10.7. Funding Ceiling alanı eklenmez. |
| Gizlilik tasarımı | Gerçek SOP-0029 policy/seviye eşlemesi olmadan selector/kod/fallback yok. VisibilityPolicyKey varlığı uygulanmış OD-05 kanıtı değildir. |

**Hedef form alan sayısı OPEN; hedef Golden seçimi OPEN.** 13/15 kullanılmaz. Mevcut 3 alan Slim ile
uyumludur; hedef envanter kesinleşince ≤8 Slim, >8 Compact uygulanır. Pack'in composite `form_field_count: 8`
değeri ve diğer dilimlerin Golden kararları değişmedi. Tenant `_LayoutTenantShell`, DataTable v2 ve 7 dil
sözleşmesi korunur; bunlar bu turda yeniden test edilmiş sayılmaz.

- `Portfolio.CanTransitionTo` ve `PortfolioService.Transition` mevcutta Draft → Active geçişini kabul eder.
  Stratejik hedef, accountable owner, doğrulanmış funding ve Review Frequency şartları **gelecek deltadır**;
  Portfolio entity/service'te Review Frequency uygulanmış değildir. İlgili erişim/onay şartları korunur.
- `PortfolioService` genel izin + tenant lookup kullanır; genel update izni owner kanıtı değildir.
  Liste/detay ve mutasyonlarda SOP-0029 erişimi, kayıt bazlı owner/actor doğrulaması yeni bağımlılıklardır.
- PPM'nin yerel EntityBase'i, server tenant/actor, UTC tarihleri, soft-delete, expected-Version CAS ve
  mutation + local audit-intent transaction temeli korunur. Mevcut minimal audit, gerekçeli tam değerlendirme
  geçmişini kanıtlamaz; before/after, gerekçe, actor/time/version ve izinli payload/saklama sözleşmesi gerekir.
- Soft-delete InvestmentCase dependency/fence denetimleri korunur. Audit yazımı hatasında yerel rollback
  temeli genişletilir; commit sonrası dış audit yayım hatasıyla aynı olaymış gibi raporlanmaz.
- Mevcut **Active/Archived** kayıtların okuma/güncelleme/değerlendirme/referenceability ve arşiv davranışı,
  eksik tarihsel funding/policy dahil, **PPM iş/teknik sahiplerinin** açık uyumluluk kararıdır; erişim/finans
  sahipleriyle yalnız ilgili dış koşullar için mutabakat gerekir. Otomatik Draft'a düşürme,
  grandfathering, classification default'u, backfill veya migration yetkisi yok; tarihçe/bağlar korunur.
- PPM lifecycle sahibidir; MOD-0023 onayı yürütür; WorkCenter onay veya lifecycle sahibi olmaz.

<a id="portfolio-technical-work-split"></a>

### 10.7 Gerçek iş ayrımı — teknik mutabakat, 2026-09-09

Bu tek tablo önceki ertelenmiş-iş tablosunu geliştirir; bütçe yalnız §10.5'te takip edilir. Claude D1–D10
listesi iş emri veya onaylı Platform geliştirme listesi olarak devralınmadı. Mevcut sözleşmenin ilgili
mekanizma için yeterli olması bütün Portfolio davranışının hazır olduğunu göstermez.

Sınıf anahtarı (yalnız tablo kısaltması, yeni iş/modül kimliği değil):

- **A:** Mevcut sözleşme yeterli; PPM bağlantısı/tasarımı gerekiyor.
- **B:** Ortak sözleşmede kanıtlanmış dar eksik.
- **C:** İş kararı veya kaynak belge eksik.
- **D:** Yalnız seçilirse gerekecek geliştirme.
- **E:** Ertelenmiş, ilk Draft teslimatını engellemeyen iş.

Etki sütununda **C/R/E = Draft create/read/edit**, **M = manuel assessment**, **Akt = activation**.
“Doğrudan engel yok” yalnız o satırın etkisidir; gizlilik/owner bypass'ı veya implementation-ready alt küme
değildir. Bütün kapanış kanıtları gelecekteki beklentidir; bu turda test edilmedi.

| İhtiyaç | Mevcut kanıt | Eksik olan | Sınıf | Sahip | Engellediği davranış | Kapanış kanıtı |
|---|---|---|---|---|---|---|
| PPM yerel mutation/history/intent/Version | PortfolioService + PpmUnitOfWork transaction ve MongoRepository CAS mevcut; tam assessment geçmişi yok | PPM değerlendirme/gerekçe modeli, tam yerel iş geçmişi, atomik yazım ve mutation re-check tasarımı | A | PPM iş/teknik sahipleri | C: yeni alan/model; R: izinli history projection; E: atomik yeni alanlar; M: model/history gerekli; Akt: PPM uygulama/CAS gerekli | Onaylı PPM model + tenant/owner, stale-version, yerel audit hatasında rollback ve tekrar-etki testleri; ortak API yeniden yazımı değil |
| Accountable owner ilişkisi ve principal tipi | Genel update izni owner değil; FU02 kayıt sorumluluğu sağlamaz; User/Person/Position farklı anlamlar | PPM ilişki/cardinality/geçerlilik kararı; ayrıca typed principal ve authoritative doğrulama seçimi, Draft requiredness | C | PPM iş/teknik sahibi; MOD-0288/Auth yalnız kendi referans sözleşmeleri | C: owner kaydı/requiredness tasarımı açık; R: owner tabanlı erişim açık; E: owner doğrulaması açık; M: doğrulanmış owner şart; Akt: accountable owner şart | İlişki ve principal kararları ayrı onaylı; tenant/aktiflik/vekâlet/hesap bağı ve yetkisiz-owner örnekleri |
| Kimlik adapter'ında hata ayrımı | AuthServiceUserReferenceValidator lookup-validation yolunda non-success/invalid ve kesinti/parse aynı 404; güvenli ret var | Seçilen kimlik doğrulama yolunda kesin ret ile unavailable/indeterminate ayrımını koruyan dar sözleşme | B | Mevcut adapter için Platform; authoritative kimlik cevabı Auth; PPM consumer mapping | C/E: bu yol seçilen owner/referansta ayrım eksik; R: bu doğrulamaya bağlı erişimde eksik; M/Akt: aynı bağımlılıkta doğru ret/kesinti ayrımı gerekli. Bağımsız tüm CRUD için yeni endpoint önkoşulu değil | Mevcut yolu düzeltme veya yeterli başka mevcut sözleşme seçimi; invalid/cross-tenant/kesinti/timeout örnekleri ayrı; PPM HTTP/UX mapping kanıtı |
| SOP-0029 sınıflandırma ve erişim | Genel AuditService redaction var; sınıflandırma/rol matrisi veya onun enforcement'ı kanıtlanmadı | Gerçek seviyeler, rol–görme/değiştirme/onaylama matrisi ve bunun authoritative policy'ye bağlanması | C | Belge sahibi / OD-05 sahibi; PPM ve erişim sahibi kendi enforcement sınırları | C/R/E: Draft dahil güvenli erişim kararı kapanmadan kabul yok; M/Akt: erişim kapısı. Hayali en-kısıtlı veya creator-only fallback yok | Onaylı SOP/matris ve işlem bazlı policy mapping; deny/indeterminate, server-side list/detail/write testleri; kod gereği matrise göre belirlenir |
| Performance Status kontrollü tanımı | Dört business label onaylı; runtime kodu/kalıcılığı/provider seçilmedi | Code mapping, değişiklik yönetişimi ve set/scope/version/sunum bağının onayı; §10.8.1 önerisi PPM iş tanımı + MOD-0048 yayını | C | PPM iş/teknik sahibi; önerilen MOD-0048 yayın sözleşmesi sahipleri | C/R/E: yalnız bu veri/aksiyonla ilgili model/sunum; M: performans değerlendirmesi bu kararı bekler; Akt: yeni performans zorunluluğu çıkarılmaz. Tasarım SOP-0004/0029'u beklemez | Onaylı kontrollü-set kararı, kaynak-sürüm ve eksik/uyumsuz değer testleri; dört etiket kod/default veya değişmezlik kanıtı değildir |
| Mevcut BRD tüketimi — önerilen yaklaşım | published-values/values endpoint ve payload mevcut; ExceptionBehavior + DI + published query marker mevcut (§10.8) | PPM setCode/scope/version/as-of/izin ve label–code binding; onaylı published set verisi kanıtı | A | PPM tüketici; MOD-0048 mevcut yayın sözleşmesi sahibi | C/R/E: yalnız seçilmiş vocabulary tüketimine bağlı yüzey; M: önerilen BRD tüketiminde binding gerekir; Akt: yalnız bağlanan vocabulary koşulu. Yeni HTTP hata altyapısı genel blocker değil | Mevcut Response<T>/payload tüketimi ve no_published_version/ret/kesinti mapping örnekleri; özel resolver timeout semantiği genellenmez |
| SOP-0004 Portfolio risk ölçeği | Manuel owner değerlendirmesi kararı var; uygulanabilir onaylı ölçek/sürüm yok | Gerçek ölçek ve authoritative değer eşlemesi | C | SOP belge sahibi; PPM tüketici | C/R/E: yalın metadata'ya ayrı zorunluluk getirmez, risk görünümü/girdisi bekler; M: risk değerlendirmesi bloke; Akt: kaynakta olmayan yeni risk önkoşulu çıkarılmaz | Onaylı uygulanabilir ölçek/sürüm, gerekçe/owner/audit ve eksik vocabulary testleri; değer uydurulmaz |
| Asgari ortak audit kanıtı | Pack §8 altı alanlı Minimal Mutation Audit v1; ayrıca idempotent HTTP append ve genel redaction mevcut | PPM-owned tam iş geçmişi ile asgari ortak event'in eşlemesi/minimizasyonu; mevcut credential/runtime kanıt kapıları korunur | A | PPM kaynak/minimizasyon; MOD-0021 taşıma sözleşmesi | C/E: yeni mutation kanıtı; R: history okuma erişimi; M/Akt: atomik yerel history/intent ve uygun ortak kanıt. HTTP before/after yokluğu tek başına engel değil | Yerel history ve minimal event ilişkisinin onayı; mevcut altı alanı değiştirmeyen payload, rollback ve commit sonrası retry kanıtı |
| HTTP audit BeforeState/AfterState genişlemesi | İç model/redactor destekliyor; GovernedAuditAppendRequest Metadata taşıyor, before/after taşımıyor | Ancak tam snapshot'ın ortak HTTP audit'e taşınması ayrıca seçilirse kapsam/şema/gizlilik değişikliği | D | İleride seçilirse Platform taşıma sahibi + PPM minimizasyon | C/R/E/M/Akt: şu anda bağımsız zorunlu blocker değil; seçilen yeni taşıma gereksinimine özgü | Gerekçeli seçim + §8 ile açık uzlaştırma/onay + bounded payload/redaction/uyumluluk kanıtı; sınırsız Metadata yok |
| Kesin instance okuma / şablon sabitleme | GET workflow/instances/{id}; izin/tenant lookup; başlangıçta Published/Immutable TemplateVersionId zorunlu | PPM'nin kesin WorkflowInstanceId'yi saklama/okuma ve response eşleme tasarımı | A | PPM tüketici; MOD-0023 mevcut endpoint sahibi | C/R/E: Draft için onay koşulu varsayılmaz; M: ayrı onay zorunluluğu eklenmez; Akt: approval-dependent işlemde PPM bağlantısı gerekli, tam sonuç için sonraki satır | Exact-ID, tenant/izin/non-leaking 404 ve şablon-versiyon/PPM-versiyon ayrımı kanıtı; TaskApprovalService veya latest-by-object tüketilmez |
| Tam PPM onay bağı/sonuç sözleşmesi | Instance+tenant+object var; işlem ve PPM sürümü için tam bağ yok; DTO'da CompletedAt var fakat tam approver/karar gerekçesi/işlem/sürüm attestation'ı yok | Kesin instance–tenant–kayıt–işlem–PPM sürümü, authoritative preparer/approver ve karar sonucu semantiği | B | MOD-0023 ↔ PPM sözleşme sahipleri | C/R/E: Draft metadata için yeni onay şartı değil; M: otomatik onay şartı değil; Akt: mevcut politikaya göre onay gereken aktivasyon/geçiş kapanamaz | Onaylı bilateral outcome/binding sözleşmesi; yanlış işlem/sürüm/instance ve preparer-approver örnekleri; tam karar zamanını genel CompletedAt'tan türetmeme |
| Sonucun PPM'de tekrar uygulanmasını önleme | PPM CAS/transaction temeli var; Workflow retry/idempotency bu iş etkisini garanti etmez | PPM tüketim/idempotency, yetki/sürüm/iş koşulu re-check ve atomik business mutation tasarımı | A | PPM | C/R/E: yalın Draft CRUD'a dış Workflow önkoşulu eklemez; M: yalnız sonuç tüketen davranış seçilirse; Akt: onay sonucunu uygulama kapanamaz | PF-AC12: tekrarlı/eşzamanlı teslimde ikinci etki/Version/history yok; yerel rollback ve dış taşıma hatası ayrı |
| Instance-task query, yeni outcome taşıması veya ObjectRef kodlaması | GET tasks instance filtresi almıyor; task DTO'da ActionedBy var; ObjectRef opaque; exact-instance GET zaten var | Yalnız seçilen outcome tasarımının gerektirdiği dar endpoint/encoding değişikliği; mevcutlar önce değerlendirilecek | D | Seçim sonrası MOD-0023 ↔ PPM; görev atanmadı | C/R/E/M: bağımsız blocker değil; Akt: tam sonuç ihtiyacı B satırında, özel çözüm zorunlu değil | Seçilmiş tasarım; canonical encoding, exact-ID, start/retry/duplicate-instance, sürüm değişimi ve re-check kanıtları birlikte |
| Push / polling / sonuç okuma zamanlaması | Exact query var; §10.8.1 açık apply isteğinde okuma önerir; otomatik completion uygulaması/polling/push seçilmedi | Önerilen on-demand okumanın kullanıcı davranışı/teknik sözleşme onayı; push yalnız ayrıca seçilirse geliştirme | D | PPM tüketici ve seçilen MOD-0023 taşıma sahibi | C/R/E/M: doğrudan engel yok; Akt: onay sonucu tüketimi için yöntem gerekli, push yokluğu tek başına blocker değil | Seçilmiş yöntem + tekrar teslim/kayıp cevap/toparlanma kanıtı; exactly-once teslim iddiası yok |
| Çok adımlı sıralı akış runtime kabulü | Motor kaynakta mevcut; rapor test koşmadı | Onaya bağlı hedef akışın gerçek uçtan uca kabul kanıtı; yeni motor gereksinimi kanıtlanmadı | A | MOD-0023 sağlayıcı + PPM entegrasyon kabul sahipleri | C/R/E/M: yeni onay şartı üretmez; Akt: kullanılan onay senaryosunun kabulünü engeller | İleride yetkili testte gerekli sıralı akış ve SoD/sonuç/replay kanıtı; test yokluğu “motor yok” değildir |
| Review Frequency | Aktivasyonda belirlenmiş olmalı; mevcut Portfolio kodunda yok | Birim, kontrollü değerler, default, saklama ve form/aksiyon kararı | C | PPM iş/teknik sahibi; kaynak karar varsa belge sahibi | C/E: Draft zorunluluğu açık; R: truthful boş/bilinmeyen gösterim tasarımı; M: bağımsız engel değil; Akt: sıklık belirlenmeden geçiş yok | Kaynak/karar izi + eksik sıklıkta aktivasyon reddi; otomatik takvim/bildirim/Workflow işi çıkarılmaz |
| Strategic Objective | MOD-0352 / ESBP kanonik owner; legacy kod hazır provider kanıtı değil | Portfolio'ya uygun executable typed provider/version/access sözleşmesi | C | MOD-0352 iş/teknik sahibi + PPM tüketici | C/E: Draft requiredness uydurulmaz, seçilen link doğrulanmalı; R: yalnız izinli kaynak; M: bağımsız engel değil; Akt: stratejik hedef şart | Bilateral provider/tenant/version/ret-kesinti ve activation kanıtı; Organization/FU02 ile birleştirilmez |
| Portfolio–Budget | İş kararı ve mevcut §10.5 kaydı; mevcut kod Draft→Active'e izin verir | Dış OD-04/07 ve MOD-0136 finans/typed-reference koşulları | C | MOD-0136 finans sahibi + PPM tüketici | C/R/E: Draft için funding zorunluluğu çıkarılmaz; M: bağımsız engel değil; Akt: doğrulanmış funding olmadan yasak gelecekteki deltadır | Yalnız §10.5 kapanış kanıtı; bu satır yönlendirmedir, ikinci backlog değildir |
| Active/Archived uyumluluğu | Önceki §10.6 kapsamı; otomatik migration yetkisi yok | Eksik tarihsel owner/policy/funding/frequency verisinde işlem bazlı uyumluluk kararı | C | PPM iş/teknik sahipleri; erişim/finans sahipleri kendi koşulları için | C: geçmiş kayıt dönüşümü yok; R/E/M: mevcut kayıtların etkilenen davranışı kabul bekler; Akt: tarihsel veriye grandfathering yok | Onaylı uyumluluk matrisi + tarihçe/bağ/tenant/CAS/terminal-state kanıtı; default/demotion/backfill yok |
| Performance otomasyonu | İlk teslimat manuel; kaynak/formül yok | Onaylı kaynak/formül/güncellik ve manuel sonuç ilişkisi | E | PPM süreç sahibi + Portfolio owner; metric provider ayrıca belirlenir | C/R/E/M/Akt: otomasyon ilk Draft'ı doğrudan engellemez; manuel sözleşme kapıları korunur | Kaynak provenance + deterministik formül/eski-eksik veri örnekleri + kullanıcı kabulü |
| Risk birleştirme | İlk teslimat manuel; aggregate kararı yok | Ölçek sonrası aggregate kaynak/ağırlık/formül/güncellik kararı | E | PPM süreç sahibi + ölçek/kaynak risk sahipleri | C/R/E/M/Akt: otomatik aggregate ilk Draft'ı doğrudan engellemez; manuel risk ölçeği ayrı kapı | Onaylı örnek hesap, izlenebilir kaynak, eksik/çelişkili veri/tenant testleri + kullanıcı kabulü |
| Gerçek kapasite/kaynak entegrasyonu | İlk teslimat optional açıklama; rezervasyon/atama/hesap yok | Kaynak SoR'u, birim/zaman/allocation ve typed entegrasyon kararı | E | PPM business owner + kaynak sahibi (exact modül/kişi atanmadı) | C/R/E/M/Akt: gerçek entegrasyon ilk Draft'ı doğrudan engellemez; açıklama zorunluya çevrilmez | Provider–consumer, birim/zaman/version/concurrency/yetki kanıtı ve metinden rezervasyon üretilmemesi + kullanıcı kabulü |

### 10.8 PPM sorumlulukları ve ortak servis sözleşmeleri

PPM; Portfolio değerlendirmesi/gerekçesinin iş modelini, mutation + ilgili yerel history/audit intent +
Version'ın atomik kaydını ve Workflow sonucunun uygulanmasındaki idempotency/CAS'i sahiplenir. Mutation
anında yetki, kayıt sürümü ve iş koşullarını yeniden kontrol eder. Workflow idempotency'si PPM mutation
idempotency'sinin yerine geçmez; tekrar teslim aynı iş etkisini ikinci kez uygulamamalıdır. Tam bir kez
teslim garantisi yoktur. Yerel transaction hatası rollback, commit sonrası dış audit taşıma hatası ise
mutabık kalınmış durable retry/consumer davranışıdır. Bunlar Claude'a devredilen işler değildir.

**Kaynak ve doğrulama sınırı:** 2026-09-09'da kullanıcının ilettiği düzeltilmiş Claude raporu
(/Users/alitufanoglu/.codex/attachments/03215463-4a21-469d-b99b-dbb8508123a7/pasted-text.txt) veri olarak
okundu. Raporun HEAD'i b6fcfe97; kendi main karşılaştırması bu worktree için güncellik kanıtı değildir.
Aşağıdaki dar kaynak yolları burada e72701fa565942187e7dd85c1e92fdcc25080969 üzerinde de okundu.
Test yürütülmedi; rapordaki D1–D10 onaylı görev listesine dönüştürülmedi.

Doğrulanmış sözleşmeler ve düzeltilen çıkarımlar (dosyalar services/Diten.Platform/src/ altındadır):

- WorkflowDefinitionsController (Diten.Platform.API/Controllers/) GET /api/v1/workflow/instances/{id:guid}
  endpoint'ini WorkflowPermissions.InstancesView ile sunar; GetWorkflowInstanceByIdHandler non-leaking 404
  döner. TaskApprovalService iç servisi PPM dış sözleşmesi değildir. EvaluateWorkflowTransitionGateHandler
  GetLatestByObjectRefAsync kullanır; kesin onay bağı yerine geçmez.
- Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/ içindeki
  WorkflowTaskTransitionSupport approve/reject yolunda ActionedBy'ı işlem aktörüyle doldurur; hazırlayan
  değildir. Diğer task aksiyonları da alanı yazar. StartedBy başlatan ve mevcut submitter SoD girdisidir;
  authoritative preparer yerine geçmez. StartWorkflowInstanceHandler Published/Immutable template version
  zorunluluğunu uygular. TemplateVersionId, PPM iş kaydı sürümü değildir.
- Diten.Platform.Application/Features/Workflow/WorkflowModels.cs: WorkflowInstanceDto.CompletedAt ve
  LastTransitionAt mevcut. Genel tamamlanma zamanı ile tam onay sonucu/karar zamanı aynı kanıt değildir.
  DTO, işlem ve ilgili PPM sürümü ile tam approver/gerekçe bağını sağlamaz. Instance+tenant+object kanıtı
  tam beşli bağ değildir; ObjectRef'e işlem/sürüm gömmek tek başına çözmez.
- Diten.Platform.API/Controllers/Platform/PlatformAuditAppendController.cs:
  POST /api/v1/platform/audit/events, platform.audit.events.append izni, tenant mismatch reddi ve idempotent
  append/Duplicate sonucu mevcut. Diten.Platform.Application/Features/Audit/AuditAppendApiModels.cs içindeki
  GovernedAuditAppendRequest before/after taşımıyor; bu otomatik API genişletme görevi değildir.
  AuditService.BuildPayload before/after/Metadata'yı SensitiveFieldRedactor ile genel desenler üzerinden
  maskeler; SOP-0029 sınıflandırma/rol görünürlüğü kanıtı değildir. Kaynak minimizasyonu PPM'de kalır.
- Diten.Platform.API/Controllers/BusinessReferenceDataController.cs:
  GET /api/v1/reference-data/sets/{setCode}/published-values (scope_key) ve /values
  (scope_key/version/as_of_date/include bayrakları), Platform.BusinessReferenceData.Consumer.Read izniyle
  mevcut. Response<T> published payload: SetCode, VersionNumber, PublishedAt,
  Items(Code, Label, Description, IsActive, SortOrder, Attributes).
  Diten.Platform.Application/Features/BusinessReferenceData/BusinessReferenceDataExceptionBehavior.cs ve
  Application DependencyInjection kaydı mevcut; Queries/GetBusinessReferenceDataPublishedValuesQuery
  IBusinessReferenceDataRequest uygular. no_published_version 404, tanımlı dependency hataları 503 gibi
  coded mapping vardır; bilinmeyen hata global pipeline'a bırakılır. Özel resolver timeout eşlemesi bütün
  published-values arızalarına genellenmez. PPM set/scope/version/data binding eksikliği ayrı konudur.
- Diten.Platform.Infrastructure/Services/Auth/AuthServiceUserReferenceValidator.cs:
  GET /api/users/{userId}/lookup-validation için caller bearer/tenant iletilir. Geçersiz referans,
  non-success ve yakalanan transport/parse kesintisi aynı 404'e düşer; fail-closed var, hata ayrımı yok.
  Caller cancellation yeniden fırlatılır. FU02 Portfolio kayıt sorumluluğunu sağlamaz.

<a id="portfolio-four-design-decisions"></a>

#### 10.8.1 Dört somut tasarım önerisi — PROPOSED / DRAFT / NON-EXECUTABLE

2026-09-09: Önceki seçenek özeti, her tasarım için tek önerilen yaklaşım ile somutlaştırıldı.
Ayrıntılı kanonik tasarım [MOD-0117 dört öneri bölümündedir](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-four-design-proposals).
Tasarımların bütünü DECIDED değildir; yalnız atayıcı iş rolü §10.11 ile APPROVED BUSINESS DECISION oldu.
Aşağıdaki yeni model/alan kavramları **önerilen, mevcut değil**.
Mevcut onaylı iş etiketleri ve owner-only manuel değerlendirme kararı yeniden oylamaya açılmadı.
Tek iş sınıflandırması §10.7'de, tek bütçe kaydı §10.5'tedir.

1. **Sorumluluk:** PPM'de tek etkin atama; ilk teslimat için adlandırılmış insan **User** hesabı önerilir.
   User doğrudan işlemi yapan hesapla karşılaştırılabilir; Person hesap değişiminden bağımsız kişiyi,
   Position ise geçerli görev sahibinin ayrıca çözülmesini gerektiren makamı temsil eder. Aktif hesap
   doğrulaması PPM sorumluluk atamasının yerine geçmez. Draft'ta 0–1, aktivasyonda tam bir uygun owner;
   devirde eski/yeni geçerlilik aralıklarının CAS/history ile atomik değiştirilmesi önerilir. Draft'ta
   opsiyonellik ve hesap düzeyinde sorumluluk iş onayı ister. Atayıcı persona 2026-09-10'da tek başına
   APPROVED BUSINESS DECISION oldu: portföy yönetimi/PMO adına yetkilendirilmiş kişi. Atama ayrı kontrollü aksiyon;
   genel edit veya owner olma atama yetkisi değildir. Değerlendirme yalnız uygun etkin owner + işlem izni +
   SOP-0029 erişimiyle yapılır. Devre dışı/expired/belirsiz principal değerlendirme ve aktivasyonu durdurur;
   otomatik yerine-atama/demotion/vekâlet yok. Eski typed atama ve actor attribution korunur; ileride
   Person/Position seçilirse yeni bağlı atama açılır, geçmiş yeniden yorumlanmaz. [Tam owner önerisi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-owner-proposal).
2. **Performans sözlüğü:** İş anlamı PPM'de, hedef kontrollü değer yayını **MOD-0048'de**. Domain kuralına
   uygun varsayılan budur; kalıcı PPM-local lookup/enum istisnası önerilmez. Code kimliktir, label sunumdur.
   Set/scope/version/code binding ve yayın izi PPM'de tutulur; set kodu/runtime kod/published veri mevcut
   sayılmaz. Mevcut published-values ile seçim, /values version/as_of_date/include_deprecated ile tarihsel
   çözümleme değerlendirilir. Yeni kayıt güncel uygun published sürümde aktif değer kullanır; seçimden
   sonra sürüm değişirse sessiz remap yerine yeniden seçim önerilir. Eski kod yeniden anlamlandırılmaz;
   retirement eski değerlendirmeyi değiştirmez. Yetkili backend options/sunum üretir; frontend hardcoded
   liste veya doğrudan servis erişimi kullanmaz. Tarihsel çözümleme, yayın geçerlilik penceresi ve yedi dil
   kanıtı açık teknik binding'dir; ancak mevcut mekanizma karşılamazsa dar provider değişikliği gerekir.
   SOP-0004 beklenmez; SOP-0029 kapısı korunur. [Tam vocabulary önerisi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-performance-proposal).
3. **Geçmiş/audit:** PPM'de değiştirilmeyen iş geçmişi girdileri, her değerlendirme türünün kendi son girdisine
   bağlı güncel değer; risk değerlendirmesi performans sonucunun üzerine yazmaz; düzeltme yeni gerekçeli girdi önerilir. Önerilen kayıtta Portfolio/tenant, assessment kimliği/türü,
   etkin owner ataması ve typed principal, gerçek actor/UTC zaman, eski/yeni code–scope–vocabulary sürümü,
   kaynak/sunum izi, gerekçe, önceki/yeni PPM Version ve local auditIntentId bulunur. Aynı değerin yeni
   gerekçeli değerlendirmesi yeni review sayılır; aynı isteğin tekrarı ikinci review değildir. Mutation,
   history/güncel projection, local intent, request receipt ve CAS tek atomik sınırda kalır.
   Ortak payload §8'in altı alanlı Minimal Mutation Audit v1'i olarak korunur; geçmiş aynı auditIntentId
   üzerinden ilişkilendirilir. Ortak event'e tam geçmiş/snapshot/gizli gerekçe veya yeni alan eklenmez.
   Yerel hata rollback; commit sonrası taşıma hatası mevcut sabit EventId/bytes ile retry/DLQ/replay'dir,
   iş değerlendirmesi yeniden uygulanmaz. Saklama/erişim politikası uydurulmaz.
   [Tam history/audit önerisi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-assessment-history-proposal).
4. **Onay uygulaması:** PPM-held kesin WorkflowInstanceId ile mevcut GET, yetkili kullanıcının açık
   uygulama isteği sırasında okunur; completion otomatik aktivasyon oluşturmaz. Polling/push zorunlu
   seçilmedi. Tam tenant/kayıt/işlem/dondurulmuş PPM sürümü ve authoritative preparer bağlanır.
   Hazırlayan için öneri: dondurulan talebin iş içeriğini hazırladığını açıkça beyan ederek yetkili
   gönderimi yapan insanın authenticated kimliği; bu iş tanımı ayrıca onay ister. StartedBy/CreatedBy
   veya istemciden gelen kimlik bunun yerine kullanılamaz. Ambiguous start'ta aynı başlatma denemesi
   reconcile edilir; kör ikinci instance yok. Mevcut DTO'nun kimlik/object/status/template/time bilgisi
   tam karar aktörü/gerekçesi/adım sonucu/işlem/sürüm/finality kanıtı değildir. Her ara business Version
   değişikliğinde eski talep superseded tutulur ve yeniden onay önerilir; eski sonuç yeni sürüme taşınmaz.
   Uygulama yetki/erişim/owner/sürüm ve aktivasyonun dört koşulunu yeniden doğrular. Consumption receipt,
   etki, Version, history ve intent atomiktir; tekrar/eşzamanlı sonuç ikinci etki üretmez. Exact GET + CAS
   dağıtık transaction değildir; kararın finality/revocation/freshness koşulu ortak sözleşmede kapanır.
   [Tam onay uygulama önerisi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-approval-application-proposal).

**Karar özeti — yeni blocker/backlog değil; yukarıdaki PROPOSED tercihlerin onay yüzeyi:**

| Karar | Önerim | Neden | Kim onaylamalı | İlk Draft teslimatına etkisi | Açık kanıt/sözleşme |
|---|---|---|---|---|---|
| Accountable responsibility ve principal | PPM-owned tek etkin atama, adlandırılmış insan User; Draft 0–1, activation 1; yetkili persona ayrı atama/devir yapar, yalnız uygun owner değerlendirme yapar | İş sorumluluğu PPM'de kalır; ilk teslimatta kanıtlanmamış Person/Position→hesap çözümü varsayılmaz | PPM iş/süreç sahibi: hesap düzeyinde sorumluluk, cardinality, Draft opsiyonellik ve devir; atayıcı persona APPROVED BUSINESS DECISION (§10.11), açık madde değil; Auth/PPM teknik sahipleri: doğrulama/izin sözleşmesi | Draft requiredness öneridir; owner yokken assessment/activation yok; SOP-0029 erişimi tüm ilgili Draft işlemlerinde korunur | §10.7 owner ve kimlik adapter satırları: named-human/aktiflik/tenant/reference eligibility, ret–kesinti ayrımı; mevcut kayıt davranışı aynı uyumluluk satırında |
| Performance vocabulary | PPM iş tanımı + MOD-0048 kontrollü yayın; stable code ve explicit scope/version; yeni değerlendirmede güncel aktif değer, tarihsel anlam korunur | Domain Ownership Boundaries ile uyum; ikinci kalıcı lookup sahibi yaratmaz; dört iş etiketi değişmez | PPM iş sahibi: anlam/değişiklik-emeklilik sorumluluğu; MOD-0048 yetkili steward: yayın; teknik sahipler: scope/version/locale ve save-time binding | Performans aksiyonu yayın ve binding kanıtını bekler; yalın Draft metadata için yeni assessment zorunluluğu yok; SOP-0004 tasarımı bekletmez | §10.7 Performance/BRD satırları: gerçek set/code/scope/yayın verisi, eski sürüm/retirement/locale/freshness; provider değişikliği yalnız kanıtlı yetersizlikte |
| Tam yerel geçmiş + asgari ortak kanıt | PPM append-only assessment; son girdi güncel değer; düzeltme/yeni gerekçe yeni review; aynı request retry'ı değil. Aynı auditIntentId ile §8 altı alanına bağlanır | İş anlamı/gizli gerekçe PPM'de kontrollü kalır; minimal event ve history birbirini izler, tam geçmiş taşıma genişlemesi gerekmez | PPM iş sahibi: düzeltme/yeni review anlamı; mevcut SOP/veri politikası sahipleri: erişim/saklama; PPM teknik sahibi: atomik model ve korelasyon | Manuel değerlendirme history/owner/vocabulary sözleşmesiyle birlikte kabul edilir; ortak HTTP before/after bağımsız Draft blocker değildir | §10.7 yerel mutation/audit ve SOP-0029 satırları; §8 sabit payload/taşıma/credential kapıları; saklama ve Active/Archived uyumluluğu mevcut kayıtlarda açık |
| Kesin onay sonucu ve uygulama | Yetkili açık apply isteğinde exact-instance GET; attested request preparer; her ara business Version değişiminde yeni onay; atomik tüketim receipt'i | Sonuca yakın doğrulama ve açık kullanıcı kontrolü; eski onayın farklı kayda/işleme/sürüme veya ikinci etkiye dönüşmesini önler | PPM iş/onay süreç sahibi: preparer tanımı, açık uygulama ve yeniden onay davranışı; MOD-0023/PPM teknik sahipleri: binding/outcome/start-recovery/finality | Draft metadata veya manuel assessment'a yeni onay zorunluluğu getirmez; sadece onay gerektiren activation/geçiş bu sözleşmeyi bekler | §10.7 exact-instance, tam onay bağı, replay ve koşullu taşıma satırları; mevcut CompletedAt tam sonuç değildir; PF-AC12 ve §10.5 finans koşulu korunur |

**Mevcut açık girdilere yönlendirme:** SOP-0029 matrisi → §10.7 SOP-0029 satırı; SOP-0004 ölçeği →
aynı tablonun risk satırı; Review Frequency birim/değer/default/yerleşimi → Review Frequency satırı;
strateji sağlayıcısı → Strategic Objective satırı; finans → yalnız [§10.5](#portfolio-budget-integration);
mevcut Active/Archived kayıtlar → §10.6 ve §10.7 uyumluluk satırı. Bunlar yeniden açılmış veya çoğaltılmış
kararlar değildir. Form alan sayısı/Golden OPEN kalır; atama, assessment ve apply ayrı aksiyon önerileri,
history/current-value sunumu salt okunur, create/edit girdileri ise nihai yerleşime göre sayılacaktır.

**Claude ile ileride mutabık kalınacak dar sözleşmeler — görev gönderimi değil:**
User yolu seçilirse mevcut doğrulamanın named-human/aktiflik/tenant uygunluk kanıtı ve ret–kesinti ayrımı;
MOD-0048 için mevcut yayın/tarihsel çözümleme/scope-version-locale ve save-time validity sözleşmesi ile
gerçek yayın verisi; MOD-0023 için exact-instance'a bağlı tam outcome/preparer/işlem/PPM sürümü,
start/retry-recovery ve finality/revocation/freshness semantiği. Önce mevcut dış tüketici sözleşmelerinin
yeterli kısmı kullanılacak; yalnız karşılanamayan bölüm dar provider deltası olarak ayrıca hazırlanabilir.
Yeni audit before/after endpoint'i, task query'si, push motoru veya FU02 sorumluluk modeli istenmiyor.
PPM ilişkisi, tam history, CAS ve at-most-once iş etkisi Claude'a devredilmiyor.

**Strategic Objective ayrı bağımlılıktır:** kanonik iş alanı sahibi **MOD-0352 — Enterprise Strategy
Management / ESBP**; kanıt [registry MOD-0352 satırı](../../../../execution/registries/module-id-registry.md)
ve [ESBP domain-config Ownership Boundaries](../../../../execution/domains/enterprise-strategy-business-performance/domain-config.md#ownership-boundaries)
(goals/objectives/cascade). MOD-0288 Organization/FU02 Claude tarafında kalır; onun sözleşmesi Strategic
Objective sağlayıcısı değildir. Portfolio'ya uygun, onaylı, tenant-safe executable Strategic Objective
provider/version/access/failure sözleşmesi **doğrulanmadı / OPEN**. Legacy Enterprise Strategy kodu veya
kanonik sahiplik bu sağlayıcıyı hazır yapmaz; sözleşme MOD-0352 iş/teknik sahibiyle ele alınır. Yeni API,
module ID, sahiplik kararı veya ES uygulama görevi yoktur.

Active/Archived uyumluluğu PPM iş/teknik sahiplerinin kararıdır (§10.6–10.7); ilgili erişim/finans
koşullarında o sahiplerle mutabakat gerekir. SOP-0029 seviye/rol matrisi ve SOP-0004 ölçeği belge sahiplerini
bekler; finans sözleşmesi §10.5'te MOD-0136 sahibindedir. Correction hiçbir tarafa implementation görevi
atamaz. Mesaj/görev gönderilmedi. Dört tasarım bu iki belgede **PROPOSED** olarak somutlaştırıldı;
yalnız atayıcı iş rolü §10.11 ile onaylandı. Gerçek
runtime uygulama hazırlığı, §10.8.1 karar tablosundaki iş tercihleri yetkili PPM sahibince açıkça onaylanınca
bu seçime bağlı teknik sözleşme/alan-aksiyon kapsamının hazırlanmasıyla açılır; bilinen dört performans
etiketi yeniden sorulmaz ve yeni denetim turu gerekmez. Hazırlık kod yetkisi değildir: ilgili §10.7
kaynak/erişim/sözleşme ve mevcut kayıt uyumluluk kapıları kapanmalı, exact kapsam ayrıca onaylanmalı ve
kod başlangıcı için açık implementation authority verilmelidir. Review Frequency aktivasyon koşulu
korunur; değer/default/Draft zorunluluğu bu önerilerle kararlaştırılmaz.

### 10.9 Gelecekteki kabul ve bu turun doğrulama sınırı

Pack amendment'ındaki **PF-AC01–PF-AC12** yerel kabul maddelerinin tamamı **PENDING / NOT RUN**:

| Gelecekteki senaryo | Beklenen kanıt |
|---|---|
| Genel update izni var, owner değil / owner belirsiz | Değerlendirme server-side reddedilir; durum, gerekçe geçmişi ve Version değişmez. |
| Cross-tenant read/write/assessment/lifecycle/typed link | Diğer tenant kaydı 404; veri/kimlik sızmaz veya değişmez. |
| Eksik/uyumsuz vocabulary | İlgili değerlendirme kaydedilemez; hardcoded label-as-code, default/rating/fallback yok. |
| Gerekçesiz performans veya risk | Boş/whitespace gerekçe reddedilir; geçerli manuel sonuç tam gerekçe/önce-sonra/actor/time/version audit'i taşır. |
| Stale-version | Aynı sürümle yarışta tek geçerli commit; diğerine 409; sessiz overwrite veya başarılı mutation audit'i yok. |
| Yerel audit/history yazım hatası | Mutation + Version + history/intent atomik rollback; commit sonrası transport hatası durable retry/idempotency'dir. |
| Bütçe bağı olmadan veya geçersiz/eski bağla aktivasyon | Active oluşmaz; provider kesintisinde güvenli ret. Geçerli bütçe varken bile stratejik hedef, accountable owner veya Review Frequency eksikse aktivasyon reddedilir; PPM mutation anında dördünü ve ilgili onay/erişim şartlarını yeniden kontrol eder. Draft-save zorunluluğu çıkarılmaz. |
| Gizlilik belirsizliği, Draft dahil | İlgili okuma/yazma güvenle reddedilir; kayıt açıklanmaz. See hakkı change/approve vermez; hayali seviye/creator-only fallback yok. |
| Capacity açıklaması boş/dolu | Zorunluluk uydurulmaz; metin rezervasyon/kişi ataması/hesap üretmez. |
| Active/Archived/soft-deleted uyumluluğu | Onaylı davranış matrisi, tenant/CAS/tarihçe/bağ/dependency/terminal-state korunur; otomatik dönüşüm yok. |
| Kullanıcı yüzeyi | Kesin form sayısı, ayrı aksiyon, doğru salt okunur veri, tenant layout, DataTable v2, 7 dil ve yetkisiz içerik ifşasının önlenmesi kanıtlanır. |
| Tekrarlanan/eşzamanlı Workflow sonucu — PF-AC12 | Aynı authoritative sonuç PPM'de ikinci iş etkisi, Version artışı veya başarılı mutation history'si oluşturmaz. Mutation + tüketim/idempotency kaydı + ilgili local history/audit intent + Version atomiktir. Yeni etki öncesi yetki/sürüm/iş koşulları yeniden kontrol edilir; instance/tenant/kayıt/işlem/sürüm bağı uyuşmayan sonuç uygulanmaz. Yerel rollback ve commit sonrası dış audit taşıma hatası ayrı testlenir; Workflow idempotency veya teslim sayısı bu kanıtın yerine geçmez. |

Yeni endpoint/DTO/permission/HTTP eşlemesi §10.2'ye uygun biçimde invalid request, kesin ret, görünmez/kayıp
kayıt, state conflict ve authoritative dependency kesintisini ayırmalıdır; açık kodlar keyfî 400/409'a
indirgenmez. Bu liste implementation allowlist, test yürütme sonucu veya teslimat kabulü değildir.

Bu governance turunun kontrolleri: Master 8.1/registry ile mevcut canonical MOD-0117 preflight'ı **PASS**;
handoff öncesi `git diff --check` ve yalnız izinli iki belgenin tam diff incelemesi. Build/runtime/browser/DB
ve migration/index/seed testi **uygulanmaz**. Kod değişmedi; yeni bütçe/gizlilik/owner kontrolleri veya manuel
değerlendirmeler uygulanmış/test edilmiş sayılmaz. Sonuç **taslak hazır**; açık sözleşmeler ve repository
implementation authority yokluğu nedeniyle bu amendment için kod başlatılamaz.


<a id="portfolio-isolated-test-control"></a>

### 10.10 İzole Domain/Application test dilimi — 2026-09-10 / NOT SELECTED — implementation not authorized

**CONTROL TOWER durum düzeltmesi:** 14 dosyalık detached dilim uygulama için seçilmedi.
PortfolioDraftState + ayrı service + test store, mevcut Portfolio uygulamasından bağımsız ikinci bir
davranış modeli oluşturur; testlerinin geçmesi gerçek Portfolio teslimatını ilerletmiş sayılmaz.
Aşağıdaki kapsam, dosya listesi ve test kapıları tarihsel öneri olarak korunur; bu durum düzeltmesi
önceki koşullu uygulama/onay ifadelerinin önüne geçer. Önerilmiş onay metni artık çalıştırılacak sonraki
adım değildir; kod/test yetkisi verilmez ve onay istenmez.

Sonraki gerçek geliştirme mevcut Portfolio entity/service zincirini genişletmelidir. Test doubles
yalnız dış bağımlılıkları temsil etmeli; ikinci Portfolio aggregate'i veya alternatif persistence
gerçeği yaratmamalıdır. Önceki Portfolio tasarımları, bağımlılık kararları, scoped onaylar, pack review
ve production_authority: none korunur.

Kullanıcı mevcut PROPOSED tasarımdan izole test dilimi **hazırlanmasını** kabul etti. Bu kayıt kod/test
çalıştırma yetkisi, şirket rol/erişim politikası onayı, gerçek kullanıcıya atama/onay yetkisi, kurumsal
preparer kararı, SOP-0029/SOP-0004 kapanışı veya production kabulü değildir. Pack review, önceki scoped
onaylar ve production_authority: none korunur. Yeni module/FU/rapor/backlog yoktur.

Kanonik dilim [MOD-0117 izole test bölümünde](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-isolated-test-slice);
[exact gelecek dosya listesi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-isolated-test-files)
ve [olumlu/olumsuz test matrisi](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-isolated-test-matrix)
aynı bölümde tutulur. Bu kontrol satırı ikinci bir iş envanteri değildir.

**İlk dilim:** mevcut Portfolio runtime'ına bağlı olmayan immutable Draft değerlendirme modeli ve
testlerin doğrudan kurduğu, runtime'a kaydedilmeyen Application servisi. Tek etkin owner, gerekçeli devir,
ownerless Draft, owner-only assessment, ayrı performans/risk history, gerekçeli append-only düzeltme,
Version ve request-replay sözleşmeleri yalnız test senaryosu olarak doğrulanır. Missing/Denied/Indeterminate
dış otorite sonucu ilgili işlemi etki bırakmadan reddeder. Genel edit yetkisi assignment/assessment izni
veya owner kimliği yerine geçmez.

**Aktivasyon sınırı:** yalnız owner eksikliğinin/uygunsuzluğunun reddini gösteren negatif kontrol.
Geçerli owner kontrol örneği bile OutsideSlice döner; hiçbir test Active state, gerçek aktivasyon sonucu
veya approval receipt'i üretmez. Tam preparer/onay sözleşmesi, Workflow start/exact-result tüketimi,
gerçek strateji/funding/frequency doğrulaması ve provisioning kapsam dışıdır.

**Mimari izolasyon kararı:** Application/DependencyInjection.cs mevcut MediatR ve validator assembly
taraması yapıyor. Dolayısıyla yeni handler/validator ekleyip "DI dosyasına dokunmadık" demek yeterli değil.
Dilim plain DTO + constructor port + doğrudan servis çağrısı kullanır; IRequest/handler/notification,
FluentValidation validator, hosted worker, initializer veya registration hook üretmez. Bu, yalnız
kayıtsız test dilimi için açık istisnadır; gelecekteki runtime CQRS standardını değiştirmez.
Portfolio.cs, EntityBase.cs, PortfolioService.cs, controller ve bütün DI dosyaları korunur.

**Test doubles ve kanıt sınırı:** Kimlik/yetki/vocabulary ve state-store uygulamaları yalnız mevcut
Diten.PpmService.Tests assembly'sindeki internal TestOnly sınıflarıdır. Opaque sentetik referans
kimlikleri/provenance kullanılır; risk seviyesi/değeri/puanı, SOP ölçeği, gizlilik seviyesi, gerçek
MOD-0048 set/kod/yayın sürümü uydurulmaz. Pozitif risk-channel testi yalnız ayrı history/rationale
saklama davranışını kanıtlar, kullanılabilir risk ölçeğini değil. Runtime/src altında somut fake
provider, fallback, lookup listesi, izin veya seed yoktur.

Application yalnız immutable candidate + history + yerel intent + receipt paketini compare-and-commit
portuna sunar. Deterministik test store'u eski snapshot'ı mutasyondan korur; pre-publication hatası sıfır
etki, commit sonrası kayıp cevap aynı receipt'ten toparlanma senaryosudur. Bu kanıt Mongo transaction,
durable outbox, process-crash dayanıklılığı veya gerçek entegrasyon kanıtı değildir. Minimal Mutation
Audit v1 ve mevcut taşıma kapıları değişmez; gerçek event yayımı yapılmaz.

**Exact gelecek kapsam:** Kanonik listede **14 yeni** Domain/Application/test dosyası; hiçbir mevcut kod
dosyası, csproj, solution veya paket değişikliği yok. Mevcut bu iki belge yalnız scoped onay ve gerçek
test kanıtı için güncellenebilir. Kod yazımı için ayrıca açık kullanıcı onayı gerekir; bu turda yalnız
iki belgenin ilgili bölümleri değişir. İlk test dilimi diğer PROPOSED fiziksel dosya fikirlerinin veya
Portfolio runtime allowlist'inin tamamını devralmaz.

Mevcut unit-test projesinin Api/Infrastructure referansları bulunduğu açıkça kaydedildi. Yalnız
Diten.PpmService.Tests.PortfolioDraftEvaluation namespace'i seçilir; API host, service-provider kurulumu,
Infrastructure/Persistence kaydı, HttpClient/Mongo veya gerçek veri çağrısı yapılmaz. Transitive build
ile runtime başlatma aynı şey değildir. İzolasyon bu sınırlarla sağlanamıyorsa exact engel bildirilir;
yeni proje/paket/config veya runtime değişikliğiyle sessizce aşılmaz.

**Gelecek test kapıları — tamamı PENDING / NOT RUN:** Pack'teki 12 satırın olumlu/olumsuz/race örnekleri;
test doubles'ın assembly aidiyeti ve source→test referansı bulunmaması; runtime discovery/registration
olmaması; mevcut DI/controller/manifest/permission/config dosyalarının baseline'a göre değişmemesi;
yalnız exact kapsam; full diff + git diff --check. Testler ve build bu hazırlık turunda çalıştırılmadı.

**Önceki sonraki-adım kaydı geçersiz:** Pack'teki tarihsel onay metni ve bu detached dilimin kodlanması/
testlerinin çalıştırılması artık sonraki iş değildir. Durum NOT SELECTED — implementation not authorized;
onay istenmez. Gerçek kullanım kapıları ve mevcut bağımlılık kararları değişmez.

Gerçek persistence/provider/frontend devamı mevcut §10.7 satırlarında kalır: yerel mutation/audit,
owner/kimlik, BRD vocabulary, SOP-0029, SOP-0004, tam onay bağı/replay, Review Frequency, Strategic Objective,
Active/Archived uyumluluğu. Finans yalnız [§10.5](#portfolio-budget-integration). Yeni mükerrer blocker yok.
PF-AC01–PF-AC12'nin gerçek sistem kabulü bu bellek içi testlerle kapanmaz.

Tamamlanınca yalnız "izole Domain/Application proposed davranış testleri geçti" denebilir; "Portfolio
tamamlandı", "güvenli browser kabulü geçti", "entegrasyon çalışıyor" veya "gerçek policy/risk ölçeği
doğrulandı" denemez. Bu tarihsel önerinin olası test başarısı gerçek Portfolio teslimatında ilerleme
sayılmaz. Ayrı küçük PR hedeflenmez. Sonraki birleşik teslimat için güncel main mutabakatı ayrıca gerekir;
bu tur fetch/merge/main güncelliği iddiası veya stage/commit/push/PR yoktur.


<a id="portfolio-owner-assignment-control"></a>

### 10.11 Owner atayıcı kararı ve mevcut Portfolio uygulama sınırı — 2026-09-10

**APPROVED BUSINESS DECISION — yalnız atayıcı iş rolü:** Kullanıcı karar yetkisini teyit ederek
“Portfolio sorumlusunu portföy yönetimi/PMO adına yetkilendirilmiş kişi atar” önerisini açıkça onayladı.
Atayıcı persona §10.8.1 açık maddelerinden çıkarıldı. Sistem yöneticisi veya mevcut owner otomatik
atama/devir yetkisi kazanmaz; mevcut PMO rolü/permission/grant varsayılmaz. User principal, Draft
cardinality/devir, diğer PROPOSED tasarımlar, gizlilik/risk/preparer ve runtime/production onaylanmadı.
Pack review, production_authority: none ve önceki scoped onaylar korunur.

[Mevcut Portfolio owner sınırı](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-owner-assignment-boundary)
tek kanonik ayrıntıdır; [5 mevcut + 8 yeni exact dosya](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-owner-assignment-files)
burada yeniden envanterlenmez. Portfolio.cs + mevcut PortfolioService/command zinciri genişletilir;
mevcut repository/UoW/Mongo/audit kullanılır. Yeni sahip olunan owner girdisi ikinci aggregate veya
collection değildir. Ayrı atama/devir aksiyonu genel edit/değerlendirme yetkisinden ayrıdır.
Kimlik/kayıt erişimi/PMO adına yetkilendirme kanıtı yoksa işlem kapalıdır. Genel list/detail'e owner/history
eklenmez; güvenli Draft/browser veya activation kabulü iddia edilmez.

**Kalan kod şeklini belirleyen iki seçim:** (1) adlandırılmış insan User + Draft 0–1 önerisi;
(2) aynı yetkilendirilmiş PMO kişisinin gerekçeli, server anında, yalnız Draft'ta devir yapabilmesi;
owner kaldırma, zamanlama/backdate ve Active/Archived mutasyonu dışarıda önerisi. Bunlar ayrıca
seçilmeli; atayıcı rolü veya genel “iş sahibi onayı” yeniden istenmez.

**Dar teknik kapılar:** Claude ile yeni owner-action permission anlamı/literal ve Platform manifest
eşleşmesi; authoritative kimliğin named-human/aktiflik/tenant/atanabilirlik kanıtı ve kesin ret–kesinti
ayrımı; actor+record+operation erişimi/PMO yetkilendirmesi. Protected Platform/Auth/shared ve frontend
manifest temasları pack'te exact dosyalarla gösterildi; PPM allowlist'ine dahil değil. Altyapı CT'ye
iletilen dar işin güncel sahiplik ve bekleme durumu aşağıdaki koordinasyon kaydındadır.
SOP-0029 erişim kaynağı olumlu gerçek kullanım için zorunlu; kodun null-provider ile ret yolu bunu
kapatmaz. SOP-0004/vocabulary/preparer/finans/frequency bu owner kod sınırının bağımsız önkoşulları
yapılmaz; kendi mevcut §10.7 ve §10.5 gerçek kullanım kapılarında kalır.

**Test ve yetki sınırı:** Testler mevcut Portfolio'yu; entegrasyon testleri aynı PortfolioService ve
gerçek repository/UoW/audit/Mongo zincirini doğrular. Yalnız dış kimlik/erişim/permission/entitlement
cevapları test double olabilir; state store/UoW/aggregate ikamesi yoktur. Test-owned Mongo süreç/DB
çalıştırma ayrıca açık yetki ister; şimdi test/build/runtime çalıştırılmaz. Kanonik bölümdeki dar kod
başlangıcı metni henüz verilmiş onay değildir ve test DB/provisioning/production yetkisi içermez.
Yalnız ilgili unit testleri geçmesi bu işin persistence/entegrasyon kabulünü tamamlamaz.

§10.10'daki 14 dosyalık detached dilim **NOT SELECTED — implementation not authorized** olarak aynen
kalır; yeni sınır o dilimin yeniden açılması değildir. Önceki tasarımlar ve bağımlılık kararları korunur;
bu owner kapsamı için §10.8.1'deki dört tasarımın toplu onayı önkoşulu çıkarılmaz. Yeni rapor/backlog,
küçük governance PR'ı veya ayrı teslimat yoktur; aynı Portfolio feature branch'inde kalınır.

#### Altyapı CT koordinasyonu — 2026-09-10

**Durum: altyapı CT'de beklemede.** Kullanıcının ilettiği altyapı CT yanıtına göre AuthService/PSS
değişikliklerinin tek yazarı altyapı CT'dir. Bu iş başka bir yürütme sohbetine gönderilmeyecek;
PPM tarafından Auth/Platform korumalı yollarına müdahale edilmeyecek.

- Otomatik grant dışlama ve PPM modül-kodu harf tutarsızlığı altyapı kusuru olarak altyapı CT
  kuyruğuna alınmıştır; PPM'den bağımsız planlanacaktır. Resolver kabulünün sessizce genişlememesi
  koşulu korunur. Kuyruğa alınması uygulama, test veya teslimatın tamamlandığı anlamına gelmez.
- `ppm.portfolios.assign-owner` katalog eklemesi **bekliyor**: izin adı MOD-0117 / DCP-006 kapsamında
  onaylı governance genişletmesine bağlanmalı; Platform `PpmManifestProvider` deltası aynı teslimata
  veya açık bir teslim sırasına alınmalıdır. Auth canonical listesi tek başına yeterli değildir.
  Bu koordinasyon kaydı önerilen izni onaylamaz, pack statüsünü veya uygulama yetkisini değiştirmez.
- Altyapı CT, yanıt anında bu iş için henüz dalda uygulama bulunmadığını ve önceki sohbet
  çıktılarının depoya aktarılmadığını bildirmiştir. Bu, iletilen durum bilgisidir; burada bütün
  dalların bağımsız denetlendiği veya runtime grant durumunun ölçüldüğü iddia edilmez.
- Kapanış kanıtı: altyapı CT'nin teslimat/checkpoint referansı ve kabul testleri; izin eklemesi
  için ayrıca onaylı governance referansı ile Platform/Auth discovery-katalog teslim sırası.
  Kimlik, hedef kullanıcı uygunluğu ve actor–Portfolio–işlem erişimi sağlayıcısı açık kalır;
  bu Auth düzeltmeleri onların veya güvenli browser kabulünün yerine geçmez.

Yeni mükerrer backlog/PR açılmaz. Bu kayıt mevcut Portfolio teslimat takibinin parçasıdır;
runtime, gerçek grant/provisioning, commit/push veya production yetkisi vermez.

#### Dar CT governance handoff ve kayıt erişimi — 2026-09-11

Kanonik permission ve operation karar tablosu [MOD-0117 owner sınırında](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-owner-assignment-boundary)
tutulur; DCP-006 aynı kararı yeniden üretmez. Tablo \`ppm.portfolios.assign-owner\` iznini yalnız
Assign/Transfer ile sınırlar, generic edit/assessment/read yerine geçmediğini ve named-human /
SOP-0029 provider kapılarını ayrı tutar. Creator-only, owner/member veya PMO-all-access varsayımı
yoktur; policy/provider belirsizliğinde PPM fail-closed kalır.

| CT'ye iletilecek dar konu | Mevcut durum | CT'den beklenen tek teyit / sonraki teslim |
|---|---|---|
| Scoped amendment statü kapısı | MOD-0117 \`review\` ve \`production_authority: none\`; PPM pack statüsü yükseltilmiyor. | Claude'un 2026-09-11 teyidi: parent review kalabilir; own approved/ready FU origin SHA ile okunur. Bu teyit FU onayı veya implementation yetkisi değildir. |
| Permission discovery/catalog | Otomatik grant dışlama iş kararı 2026-09-11'de kullanıcı tarafından onaylandı; kanonik kapsam [MOD-0117 onay kaydındadır](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-owner-explicit-grant-decision). Uygulama ve katalog yayını tamamlanmış sayılmaz. | FU'nun kendi onayı ve CT write scope'u sonrasında aynı literal için explicit-grant modelinin ve automatic-grant negatiflerinin Auth/Platform teslim sırasını verir; gerçek grant/provisioning yapmaz. |
| Kimlik ve record-access provider | Named-human/tenant/active/assignable ve SOP-0029 operation-bound access sözleşmeleri açık kalır. | CT yalnız kendi Auth/Platform temasını ve kanıt sınırını belirtir; PPM gizlilik politikasını veya insan hesabı kuralını icat etmez. |

CT, push edilmiş sabit governance SHA oluştuğunda iki belgeyi \`git show <sha> -- <path>\` ile
main merge beklemeden okuyabilir. PPM governance kaydı CT'nin ilgili Auth/Platform commitlerinden önce
veya aynı anda main'e girmelidir. SHA henüz üretilmedi. Commit/push/PR/merge ve production activation
ayrı kapılardır; bu turda hiçbiri yapılmamıştır.

**FU ve CT teslim sırası — 2026-09-11:** [MOD-0117-FU01](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-FU01-portfolio-assign-owner-permission-publication.md)
yalnız `ppm.portfolios.assign-owner` permission yayımı için kullanıcı onaylı bir follow-up’tır. Parent
MOD-0117 `review`, FU `approved` ve `production_authority: none` kalır. Auth/Platform uygulaması yalnız
altyapı CT’nin kendi kesinleştirilmiş write scope’u ve test kapılarıyla yürür; gerçek grant/provisioning,
kimlik/kayıt erişimi politikaları, browser kabulü ve production bu onayın dışındadır. PPM consumer literal’i
ve ilgili contract beklentisi mevcut Portfolio çalışma ağacında zaten vardır; yeniden uygulama işi değildir.
Platform `PpmManifestProvider`/testi, Auth `PpmPermissionCatalog`/resolver testleri ile BL-359
automatic-grant dışlama ve BL-360 case-consistency altyapı CT’nin ayrı write scope’udur. CT, own
approved/ready FU’nun origin’e push edilmiş sabit SHA’sını
`git show <sha>:execution/domains/portfolio-delivery/module-packs/MOD-0117-FU01-portfolio-assign-owner-permission-publication.md`
ile checkout yapmadan okuyabilir; parent review bunu engellemez. Bu gelecekteki teslim yöntemidir; SHA
henüz yoktur. FU ayrı küçük PR değil, mevcut anlamlı Portfolio teslimatıyla gider. Named-human/assignable
target ve SOP-0029 record-access sözleşmeleri izin yayımını engellemez; gerçek owner kullanım kabulünü engeller.


<a id="portfolio-user-delivery-control"></a>

### 10.12 Portfolio ilk kullanıcı teslimatı — tek toplu onay önerisi / DRAFT / NON-EXECUTABLE

> **SCOPED USER IMPLEMENTATION APPROVAL — 2026-09-10:** Kullanıcı dört alanlı Portfolio ekranı ve
> Draft'ta sorumlu atama/devir geliştirmesini açıkça onayladı; mevcut Active/Archived read-only,
> lifecycle ve silme kapalı seçimini kabul etti. Aşağıdaki tarihsel DRAFT/onay-bekliyor ifadeleri
> bu seçilen PPM geliştirme sınırı için bu kayıtla aşılmıştır; toplu taslak metnin tamamı verilmiş
> onay gibi okunmaz. Diğer PROPOSED politikalar, permission katalog yayını, gerçek kullanıcı grant'i,
> Auth/Platform değişikliği, provisioning, ortak/production DB ve canlıya alma kapsam dışıdır.
> Pack review, önceki scoped yetkiler ve production_authority: none korunur.
> Test koşulu: açıkça belirlenmiş, repository kurallarına uygun disposable profil.
> Seçilen Mongo profili mevcut PpmMongoCollection / MOD-0117-disposable-Mongo + PpmDisposableMongo:
> yalnız test-owned loopback süreçleri, dinamik port >=27022, geçici dbpath ve sabit
> diten_ppm_integration_tests DB; test başına tenant izolasyonu. Ortak/production bağlantısı veya
> uygulama config'i tüketilmez. Unit/JS kontrolleri DB'siz disposable çalışma çıktılarıyla sınırlıdır.
> CT external endpoint/schema ve güvenli katalog checkpoint'i olmadan runtime authority adapter/DI
> veya frontend assign-owner manifest yayını yapılmaz; eksik authority işlemleri kapalı tutar.


Kanonik kapsam [MOD-0117 tek kullanıcı teslimat bölümündedir](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-user-delivery-bundle).
Bu bölüm önceki analizleri yeniden açmaz; liste/create-edit/details/owner atama-devir tek kabul
paketinde birleşir. Önceki owner-only ayrı onay metni yerine bu toplu metin değerlendirilir.
Atayıcı persona APPROVED BUSINESS DECISION kalır; başka hiçbir karar veya implementation/production
yetkisi bu yazımla yükselmez. Pack review ve önceki scoped onaylar korunur.

| İlk kullanıcı yüzeyi | Toplu öneri |
|---|---|
| Create/edit | Code required/max64, Name required/max200, Description optional/max2000, Capacity Allocation açıklaması optional/max2000: **4 alan → Golden Slim**. Capacity teknik limit önerisidir; rezervasyon/hesap yok. |
| Liste/details | Yalnız authoritative erişimli kayıtlar; details güncel GET ve ayrı history-read kararı; Active/Archived izinli read-only. Slim quick view, Compact ayrı sayfalar yok. |
| Owner atama/devir | Ayrı User seçimi + gerekçe; User principal/Draft0–1/gerekçeli-anlık-Draft-only/owner kaldırma ve zamanlama dışlama **PROPOSED**. Genel admin/update/current-owner otomatik atama hakkı değildir. |
| Kapalı işlemler | Aktivasyon, diğer lifecycle/delete ve assessment bu ilk kabul dışında; doğrudan API bypass'ı reddedilir. Finans/strateji/owner/frequency/approval gereklilikleri korunur. |
| Sistem / dış kaynak | Id/tenant/actor/Version/RequestId/UTC/lifecycle/policy kanıtı form alanı değildir. SOP seviyesi, risk ölçeği, Review Frequency veya funding default'u icat edilmez. |

**Gerçek veri kapısı:** SOP-0029 uygulanabilir politika ve page/create/record/edit/history karar sağlayıcısı
yoksa ilgili okuma/yazma kapalıdır. Create için sınıflandırmayı dört alan üzerinden authoritative
çözmek mümkün değilse create kapalı kalır; beşinci alan veya creator-only/en kısıtlı fallback eklenmez.
Kimlik/scoped candidate/atanabilir insan hesabı sözleşmesi olmadan owner aksiyonu olumlu çalışmaz.
Dört-alan formu güvenli gerçek kullanım hazır iddiası değildir; tam Portfolio formu ileriki kapsamdır.

**Tek permission governance önerisi:** ppm.portfolios.assign-owner Tier-3, Assign/Transfer için aynı
kontrollü izin; generic edit/assessment alias'ı veya PMO hardcoded rol kontrolü değil. User/role grant
vermez. Önce §10.11'deki **mevcut altyapı CT kuyruğunda** auto-grant/case kusurları ve negatif kanıtlar;
sonra aynı literal ile Platform PORTFOLIOS aksiyonu/testi ve Auth tekil canonical kayıt eşleşmesi;
ardından doğrulanmış identity/record-access provider sözleşmesi ve PPM adapter bağlaması.
Auth/Platform kodu altyapı CT'nin tek yazarlı alanıdır; başka sohbete devir/görev mesajı yok.
Kuyruk kaydı veya önerilen katalog deltası tamamlanmış iş değildir. Finans yalnız §10.5; diğer
kaynakların sahipleri ve açık kapıları §10.7'de aynen kalır, mükerrer backlog açılmaz.

[Exact PPM/backend/frontend/test aday kapsamı](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-user-delivery-files)
**53 dosya: 35 mevcut + 18 yeni**. PpmController, ppm-crud, DtoMapping shared temasları açıkça kapsamda
ve yalnız Portfolio dalı/hook'u ile sınırlı; diğer PPM regressions zorunlu. Protected Auth/Platform/ortak
altyapı dosyaları PPM allowlist'inde değildir. Yeni endpoint/provider yolları öneridir; CT dış sözleşmesi
ve config taşıma bilgisi hazır varsayılmaz. Mevcut Gateway wildcard yeterli görünüyor; route değişim
yetkisi yok. İkinci aggregate/store, repo config/secret/index/migration/seed veya yeni rapor yok.

Teknik test → kullanıcının **eski/yeni ekran kontrolü** → Claude uygunluk incelemesi → aynı teslimatta
düzeltmeler → **tek anlamlı Portfolio teslimatı/PR**. Sonraki sayfa bu kabul ve teslimat kapanmadan
başlamaz. CT/security/identity kapıları kullanıcı kabulünden önce kapanır; test doubles yalnız dış
bağımlılıkların test cevaplarıdır, gerçek browser provider'ı yerine konmaz. Gerçek repository/UoW/CAS/
audit ve test-owned Mongo kanıtı gerekir; unit/DOM testleri bunun veya kullanıcı kabulünün yerine geçmez.
Mongo entegrasyon testleri yalnız repository kurallarına uygun, açıkça belirlenmiş disposable test
profilinde çalışabilir; ortak veya production veritabanı kullanılamaz. Uygun profil belirlenmemişse
Mongo testi başlatılmaz. Mevcut fixture'ın varlığı profil seçimi veya test çalıştırma onayı değildir;
bu koşul pack statüsünü ya da implementation/production yetkisini değiştirmez.
Canlıya alma ayrıca açık yetki, veri/rollback uyumluluğu ve güvenlik kontrolü ister.

Tek seferde değerlendirilecek kapsamlı onay metni kanonik bölümde tutulur. Metin şu anda verilmiş onay
değildir. Bu tur yalnız mevcut iki belge değiştirildi; kod/build/test/servis/DB/provisioning ve Git
yazma işlemi yapılmadı. §10.10 detached dilim NOT SELECTED — implementation not authorized kalır.

**Uygulama kanıtı — 2026-09-11; kabul açık:** [Kanonik scoped checkpoint](../../../../execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md#portfolio-user-delivery-bundle)
içinde bu turun sonuçları kayıtlıdır. Dört alanlı Slim ve Draft owner atama/devir mevcut zincirde
uygulandı; 42 kod/test + bu iki mevcut belge aday kapsam içinde değişti. Yeni detached model yok.
Seçili Portfolio unit/Application 29/29, yeni disposable Mongo 12/12, frontend JS 12/12 ve Slim
statik proxy kontrolü 64/64; backend/frontend build başarılı. Tam unit 386/389 ve mevcut Mongo
regresyonu 16/19: exact listenin atladığı PpmEntitlementAuthorizationTests.cs ve
MongoPersistenceIntegrationTests.cs authority'siz create bekleyen testleri uyarlama gerektiriyor;
bu iki dosya sessizce kapsama eklenmedi. Mimari kontrol 16/17; kalan DB-010 bulgusu değiştirilmeyen
Platform PpmAuditRetentionPolicySeedMongoTests.cs / DisposableStandaloneMongo.cs dosyalarındadır.
Test atlama, güvenlik bypass'ı veya DB istisnası eklenmedi.

**Dar test kapsamı onayı ve sonucu — 2026-09-11:** Kullanıcı bu iki mevcut dosyayı exact allowlist'e
ekledi: \`PpmEntitlementAuthorizationTests.cs\` ve \`MongoPersistenceIntegrationTests.cs\`. Değişiklik
yalnız dış authority test cevapları, fixture kurulumu ve create/CAS beklentileriyle sınırlı kaldı;
ürün kodu, Auth/Platform, DI, configuration ve Mongo test profili değişmedi. Full PPM unit
\`389/389\`, entitlement sınıfı \`27/27\`, mevcut MongoPersistence sınıfı \`20/20\` ve yeni Portfolio
Mongo sınıfı \`12/12\` geçti. Olumlu correlation create \`201\` ile entitlement → mutation → audit →
dispatch zincirini korur; duplicate \`409\`, cross-tenant \`404\`, stale \`409\` ve authority-yok
\`503\` retleri veri/audit etkisi bırakmaz. Test double yalnız dış record-access kararını temsil eder;
gerçek Mongo repository/UoW/CAS/rollback yerine geçmez.

**Ayrı bulgu, değişiklik yok:** \`Portfolio_delete_and_investment_create_never_commit_an_orphan\`
Portfolio silmesi artık koşulsuz \`409\` olduğundan gerçek delete–create yarışını doğrulamaz; mevcut
\`204 && 201\` assertion'ı imkânsız sonucu denetler ve sahte güvence üretebilir. Bu test sessizce
silinmedi veya zayıflatılmadı. Uyarlama, dar test onayının dışında ayrı lifecycle/test kararı ister.

CT API/kimlik/erişim ve güvenli katalog kapıları açık kaldığından runtime authority adapter/DI ve
frontend assign-owner manifest yayını yapılmadı. Sorumlu/form onayı kurumsal gizlilik/risk/preparer
politikası, gerçek grant/provisioning veya production onayı değildir. Kullanıcı browser kabulü ve
Claude incelemesi henüz yapılmadı; sonraki sayfa/PR başlatılmadı. Pack review ve
production_authority: none korunur. Ayrıntı ve exact dar regresyon eksiği kanonik checkpoint'tedir.
