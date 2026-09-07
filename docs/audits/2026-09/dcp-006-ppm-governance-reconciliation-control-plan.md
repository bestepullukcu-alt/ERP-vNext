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
- `CONFLICT`: repository kuralı ile iş kararı çelişiyor; yönetim kararı gerekir

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

1. Portfolio
2. Initiative
3. Program
4. Project/Workspace
5. Investment Case
6. Benefit Commitment
7. PPM-owned ortak lifecycle/gate/identity davranışları
8. İzin verilmiş typed external integrations
9. Navigation/frontend/localization
10. Integrated golden flow ve regression closure

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
| Eski kod → onaylı model eşlemesi | Açık | Enum/alan/migration değişikliği yapılamaz |
| Bağımsız teknik inceleme | Açık | Governance pack coding baseline değildir |
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

İlk yürütülecek çalışma Aşama 0 ve Aşama 1'dir: güncel `main` üzerinde altı PPM sınıfının salt-okunur exact
envanteri ve eski → onaylı model eşleme matrisi. Bu tamamlanmadan DCP-006, MOD-0117 kodu, enumlar veya veri
modeli değiştirilmez.
