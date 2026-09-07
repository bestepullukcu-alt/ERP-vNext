# PPM Governance Pack v1.0 — Değerlendirme, İtirazlar ve Karar Talepleri

**Kaynak paket:** `PPM_Governance_Pack_v1.0_DRAFT_2026-09-02.zip`  
**İnceleme amacı:** Paketin mevcut MOD-0117 yönetişimi ve uygulaması üzerindeki etkisini belirlemek  
**Kullanım sınırı:** Bu rapor bir uygulama veya production aktivasyon yetkisi değildir.

## Yönetici özeti

Paket incelendi. Altı PPM nesnesi, veri alanları, lifecycle geçişleri, roller ve stage gate'ler açısından güçlü bir gereksinim kaynağıdır. Ancak doğrudan yazılıma uygulanmadan önce aşağıdaki hususların açıklığa kavuşturulması gerekir.

Paket şu kapsamı tanımlamaktadır:

- 6 governed class
- 25 ortak alan
- 62 modüle özgü alan
- 11 durum
- 51 lifecycle geçişi
- 15 rol
- 8 stage gate
- 14 açık determination

Önerilen yaklaşım, paketi doğrudan koda aktarmak değil; mevcut MOD-0117 module pack'i, mevcut uygulama, eski Enterprise Strategy yüzeyleri ve dış ERP benchmark'larıyla kontrollü biçimde uzlaştırmaktır.

## 1. Belgenin otorite ve onay durumu

Manifest şu ifadeyi içeriyor:

> Draft — not for operational use.

Buna karşılık R009, değişiklik kayıtları ve checker sonuçları sekiz dokümanın kod ve UID tahsisinin tamamlandığını söylüyor.

### Karar talepleri

- Kod ve UID'ler kesin olarak tahsis edildi mi?
- Paket yalnız doküman kimliği mi kazandı, yoksa iş gereksinimi olarak da onaylandı mı?
- Hangi belge ve sürüm yazılım geliştirmesi için bağlayıcı kaynak olacak?
- `Draft` statüsü ne zaman ve hangi onayla kalkacak?
- Manifestteki “kod/UID tahsis edilmedi” ifadesi güncellenecek mi?

### İtiraz

Bu çelişki çözülmeden paket yazılım için doğrudan otorite kabul edilmemelidir.

## 2. Altı governed class

Paket şu altı nesneyi tanımlıyor:

1. Portfolio
2. Initiative
3. Program
4. Project
5. Investment Case
6. Benefit Commitment

### Karar talepleri

- Bunlar MOD-0117'nin kesin PPM-owned aggregate'ları mıdır?
- Project ile Workspace aynı nesne mi, yoksa Workspace ayrı bir kavram mı?
- Dashboard'un yalnız türetilmiş görünüm olması ve kalıcı veri sahibi olmaması onaylanıyor mu?

### Öneri

Dashboard ayrı entity yapılmamalı; verilerini altı yetkili nesneden okumalıdır.

## 3. Mevcut lifecycle ile yeni lifecycle çelişkisi

Mevcut MOD-0117 kodunda daha sade durumlar bulunuyor. Yeni paket çok daha geniş bir lifecycle tanımlıyor.

| Nesne | Mevcut uygulama | Yeni paket |
|---|---|---|
| Initiative | Proposed, Active, OnHold, Completed, Cancelled | Draft, Under Assessment, Proposed, Approved, Rejected, On Hold, Closed, Cancelled |
| Portfolio | Draft, Active, Archived | Draft, Active, On Hold, Closed; arşiv ayrıca `Record_State` |
| Investment Case | Draft, UnderAnalysis, Closed, Withdrawn | Draft, Under Assessment, Proposed, Approved, Rejected, Cancelled, Superseded, Closed |
| Benefit Commitment | Draft, Planned, Active, Closed, Cancelled | Draft, Proposed, Approved, Active, On Hold, Completed, Cancelled, Closed |

### Karar talepleri

- Yeni lifecycle'lar mevcut modeli tamamen mi değiştirecek?
- Mevcut `Active`, `Completed`, `Withdrawn`, `Planned` ve `Archived` kayıtları nasıl dönüştürülecek?
- Geriye uyumluluk ve veri dönüşüm tablosunu kim onaylayacak?
- Eski API sözleşmeleri ne kadar süre korunacak?

### İtiraz

Mevcut enum değerleri doğrudan değiştirilmemelidir. Önce açık bir eski → yeni durum dönüşüm tablosu hazırlanmalıdır.

## 4. Status ve Record_State ayrımı

Paket:

- iş durumunu `Status`,
- idari arşiv durumunu `Record_State`

olarak ayırıyor. `Archived`, lifecycle durumu değildir.

### Karar talepleri

- Bu ayrım tüm altı nesne için kesin midir?
- Soft delete, archive ve legal hold birbirinden ayrı mı yönetilecek?
- Arşivlenmiş kayıt okunabilir fakat değiştirilemez mi?
- Kullanıcı arayüzünde arşiv ayrı filtre olarak mı gösterilecek?

### Öneri

`Status`, `Record_State`, `IsDeleted` ve `Legal_Hold` birbirine karıştırılmamalıdır.

## 5. Investment Case sahipliği ve bağlantısı

Mevcut uygulamada Investment Case ağırlıklı olarak Portfolio'ya bağlıdır. Yeni paket ise Investment Case'in şu nesnelerden tam birine bağlanmasını öngörüyor:

- Initiative
- Program
- Project

### Karar talepleri

- Portfolio ilişkisi kaldırılacak mı, yoksa bağlı nesne üzerinden mi türetilecek?
- `Linked_Object_Type + Linked_Object_ID` kesin model midir?
- Bir Investment Case yalnız tek nesneye mi bağlanabilir?
- Initiative'den Project veya Program'a dönüşüm sırasında Investment Case nasıl taşınacak?
- Aynı nesne için aynı anda kaç Investment Case ve kaç onaylı baseline bulunabilir?

### İtiraz

Polymorphic ilişki yalnız string type + ID olarak bırakılmamalı; izinli tür ve tenant doğrulaması sunucu tarafından zorunlu tutulmalıdır.

## 6. Benefit Commitment sahipliği ve bağlantısı

Mevcut uygulamada Benefit Commitment, Investment Case'e bağlıdır. Yeni paket yalnız Program veya Project'e bağlanmasını öneriyor.

### Karar talepleri

- Investment Case bağlantısı tamamen kaldırılacak mı?
- Initiative veya Portfolio seviyesinde Benefit Commitment kesinlikle yasak mı?
- Benefit Commitment ile Investment Case arasında izlenebilirlik yine gerekli mi?
- `Benefit_Family_ID` kim tarafından oluşturulacak?
- Aynı benefit family içinde yalnız bir açık kayıt bulunması kesin kural mı?
- Gerçekleşen değerlerin sahibi MOD-0072 mi olacak?

### İtiraz

Bu karar mevcut veri modelini doğrudan etkiler. Veri migrasyonu ve cardinality kararı olmadan uygulanmamalıdır.

## 7. Kod sistemi ve kimlik üretimi

Paket değişmez, sistem tarafından üretilen üç harfli sınıf kodu tabanlı kimlik öneriyor. Ayrı bir değiştirilebilir `Business_Reference` da bulunuyor.

### Karar talepleri

- Kalıcı sistem kodunun kesin biçimi nedir?
- Altı sınıfın üç harfli kodları onaylandı mı?
- Kod üretimi merkezi bir hizmet tarafından mı yapılacak?
- Mevcut kullanıcı tarafından girilen `Code` alanları `Business_Reference` olarak mı kabul edilecek?
- Eski kayıtların kimlikleri korunacak mı?

### İtiraz

Yönetimden gelecek kod standardı beklenirken geçici kalıcı kod formatı üretilmemelidir. Mevcut `Code` alanı doğrudan yeni immutable System ID sayılmamalıdır.

## 8. Stage Gate PG0–PG7 modeli

Paket sekiz gate tanımlıyor ve gate'i bir alan değil, değiştirilemez bir `Gate Event` kaydı olarak görüyor.

### Karar talepleri

- PG0–PG7 kurumsal olarak kesinleşti mi?
- Bunlar mevcut G0–G7 kalite/build gate'leriyle aynı mı, ayrı mı?
- Her gate hangi PPM nesnesine uygulanacak?
- Gate kararı MOD-0023 Workflow üzerinden mi yürütülecek?
- Gate Event ve approval kayıtlarının sahibi MOD-0117 mi, MOD-0023 mü?
- Gate sonucu birden fazla kaydı değiştirdiğinde tek Mongo transaction zorunlu mu?

### İtiraz

Gate workflow motoru PPM içine kopyalanmamalıdır. PPM gate gereksinimini ve sonucunu sahiplenebilir; approval yürütmesini MOD-0023 sağlamalıdır.

## 9. Roller ve görev ayrılığı

Paket 15 rol, hazırlayan–onaylayan ayrımı ve bazı geçişlerde iki onay şartı tanımlıyor.

### Karar talepleri

- Bu roller MOD-0288 organizasyon/pozisyon kayıtlarına mı bağlanacak?
- Portfolio owner, Sponsor ve Decision authority sabit uygulama rolleri mi, kayıt bazlı sorumluluklar mı?
- Bir kişi aynı kayıt üzerinde hazırlayan ve onaylayan olabilir mi?
- Vekâlet ve delegasyon MOD-0023/MOD-0288 üzerinden mi yönetilecek?
- İki onay gerektiğinde sıralı mı paralel mi çalışacak?

### İtiraz

Bu roller yalnız JWT role adı olarak modellenmemelidir. Kayıt, tenant, organizasyon ve geçerlilik tarihi bağlamı gerekir.

## 10. WorkCenter sınırı

### Karar talepleri

- WorkCenter yalnız bekleyen gerçek approval/task öğelerini mi gösterecek?
- PPM lifecycle durumlarının WorkCenter'a kopyalanmayacağı onaylanıyor mu?
- Gate approval, geciken karar, benefit ölçümü ve closure aksiyonlarından hangileri WorkCenter öğesi üretir?

### Öneri

WorkCenter veri sahibi olmamalı; MOD-0023/MOD-0024 kaynaklı işleri göstermelidir.

## 11. Evidence, document ve audit sahipliği

Paket gate kararlarında kanıt, karar geçmişi ve değiştirilemez kayıtlar istiyor.

### Karar talepleri

- Evidence sahibi MOD-0031 mi?
- Controlled Document sahibi MOD-0028/MOD-0029 mu?
- Audit kaydı MOD-0021 üzerinden mi yürütülecek?
- Gate Event'in kanıt referansları typed reference olarak mı tutulacak?
- Kanıt silinirse veya erişilemezse gate geçmişi nasıl davranacak?

### İtiraz

Dosya, kanıt ve controlled document içerikleri PPM Mongo belgelerine kopyalanmamalıdır.

## 12. Bütçe, senaryo ve finansal model sınırı

Paket Portfolio funding ceiling, Investment Case capex/opex, risk-adjusted scenario ve finansal değerlendirme içeriyor.

### Karar talepleri

- Funding ceiling PPM alanı mı, MOD-0136 Budget bağlantısı mı?
- Capex/opex PPM'de tutulacak mı, Budget sürümünden mi okunacak?
- Scenario sahibi MOD-0138 mi?
- NPV, IRR, payback ve discount rate Investment Case alanı mı, ayrı finansal model mi?
- Para birimi ve kur dönüşümünün sahibi hangi modül?
- Approved financial baseline nerede tutulacak?

### İtiraz

MOD-0136 ve MOD-0138'in sahip olduğu veriler PPM'de ikinci doğruluk kaynağına dönüştürülmemelidir.

## 13. Benefit ve gerçekleşen değer sınırı

### Karar talepleri

- Planned benefit MOD-0117 Benefit Commitment'a mı ait?
- Actual/realized outcome MOD-0072'ye mi ait?
- PPM gerçek değeri kopyalamadan typed link üzerinden mi okuyacak?
- Numeric olmayan benefit türleri nasıl temsil edilecek?
- Shared benefit attribution nasıl yapılacak?

### İtiraz

Paket bu konuda açık kararların sürdüğünü söylüyor. OD-12 kapanmadan kapsam genişletilmemelidir.

## 14. Ortak ve modüle özel alanların UI dağılımı

Paket 25 ortak ve 62 modüle özel alan tanımlıyor; fakat bunların ekran yerleşimini tam olarak belirlemiyor.

### Karar talepleri

- Hangi alanlar create formunda bulunacak?
- Hangi alanlar yalnız details ekranında gösterilecek?
- Hangi alanlar lifecycle aşamasında zorunlu hâle gelecek?
- Hangi alanlar ayrı sekme, kart veya related-module görünümü olacak?
- Zorunlu alanların tamamı ilk kayıt sırasında mı, ilgili gate öncesinde mi istenecek?

### Öneriler

- Create ekranı sade tutulmalıdır.
- İlk kayıt için gereken minimum alanlar alınmalıdır.
- Diğer alanlar Details sekmeleri ve gate hazırlık ekranlarında tamamlanmalıdır.
- 8'den fazla kullanıcı alanı olan yüzeyler Golden Compact olmalıdır.

## 15. Eski Enterprise Strategy karşılaştırması

### Karar talepleri

- Eski Enterprise Strategy yalnız UX ve özellik keşif kaynağı olarak kullanılabilir mi?
- Yeni Governance Pack iş kuralı için daha yüksek otorite kabul edilecek mi?
- Eski sistemde bulunan fakat pakette olmayan alanlar otomatik reddedilmek yerine owner decision/backlog olarak mı değerlendirilecek?

### Öneri

Eski sistem doğrudan kopyalanmamalı; her alan `KEEP / MAP / BACKLOG / REJECT` kararından geçmelidir.

## 16. SAP/Oracle benchmark kullanımı

### Karar talepleri

- SAP/Oracle karşılaştırması yalnız boşluk analizi ve öneri amacıyla mı kullanılacak?
- Kurum gereksiniminde olmayan benchmark özellikleri otomatik kapsama alınmayacak mı?
- Benchmark sonucundaki yeni önerileri kim onaylayacak?

### Öneri

Governance Pack kurumsal gereksinim, eski Enterprise Strategy parity kaynağı, SAP/Oracle ise dış benchmark olarak kullanılmalıdır. Hiçbiri tek başına otomatik kod yetkisi vermemelidir.

## 17. Açık determination'lar

Paket 14 açık determination içeriyor; 7'si gate/blocker niteliğindedir.

### Talepler

- 14 kararın tamamı sahip, hedef tarih ve durumuyla paylaşılmalıdır.
- Hangilerinin yazılım geliştirmesini engellediği açıkça belirtilmelidir.
- Karar kapanmadan uygulanabilecek ve uygulanamayacak işler ayrılmalıdır.

### İtiraz

Gating determination açıkken ilgili davranış tahmin edilmemeli veya varsayılanla etkinleştirilmemelidir.

## 18. Veri migrasyonu ve geriye uyumluluk

### Karar talepleri

- Mevcut PPM test/veri kayıtları yeni modele nasıl taşınacak?
- Eski enum değerleri için dönüşüm matrisi kim tarafından onaylanacak?
- Dönüşemeyen kayıtlar karantinaya mı alınacak?
- API v2 korunup yeni model v3 olarak mı yayınlanacak?
- Rollback ve veri doğrulama planı nedir?

### İtiraz

Migration ve rollback planı olmadan mevcut alan veya lifecycle değeri kaldırılmamalıdır.

## 19. Mevcut geliştirmelerin korunması

Yeni paket, daha önce yapılan Initiative ve diğer PPM işlerinin tamamını değersiz kılmaz.

### Korunabilecek altyapılar

- Tenant izolasyonu
- Yetkilendirme
- Soft delete
- Optimistic concurrency
- Mongo transaction, outbox ve audit temeli
- CQRS ve katmanlı yapı
- DataTable ve localization altyapısı
- Same-origin proxy
- Fail-closed bağımlılıklar

### Yeniden uzlaştırılması gerekenler

- Entity alanları
- Lifecycle enum ve geçişler
- Entity ilişkileri
- Gate ve approval davranışları
- Form ve Details ekranları
- Kod üretimi
- Archive ve Record_State ayrımı

### Talep

Yeni plan hazırlanırken “her şeyi yeniden yaz” yaklaşımı yerine mevcut güvenli altyapının korunması onaylanmalıdır.

## 20. Yönetimden beklenen nihai onay paketi

Geliştirmeye devam etmeden önce aşağıdakiler istenmektedir:

1. Governance Pack'in bağlayıcı sürümü ve statüsü
2. Düzeltilmiş manifest
3. Altı entity'nin kesin sahipliği
4. Lifecycle ve eski → yeni durum dönüşüm matrisi
5. İlişki/cardinality kararları
6. PG0–PG7 gate sahipliği
7. MOD-0023, MOD-0024, MOD-0031, MOD-0072, MOD-0136, MOD-0138 ve MOD-0288 sınırları
8. Kod/UID üretim standardı
9. Açık determination listesi ve karar sahipleri
10. Migration, compatibility ve rollout yaklaşımı
11. UI alanlarının create, details ve gate ekranlarına dağıtılma ilkesi
12. Eski Enterprise Strategy ve SAP/Oracle benchmark kullanım sınırı

## Yönetici cevabından sonra önerilen büyük plan

1. ZIP içeriğini satır bazında mevcut MOD-0117 ve kodla karşılaştırmak.
2. `KEEP / CHANGE / NEW / MAP / BACKLOG / REJECT / OWNER DECISION` matrisi oluşturmak.
3. Cross-module sahiplik haritasını kesinleştirmek.
4. MOD-0117 module pack'i tek ve toplu amendment ile güncellemek.
5. Gerekli diğer module/capability pack değişikliklerini hazırlamak.
6. Compatibility ve migration planını yazmak.
7. Ortak altyapıyı koruyarak altı nesne için uygulama dilimleri çıkarmak.
8. Her nesnede backend → frontend → otomatik test → Control Tower browser kabulü → kullanıcı ekran kabulü → son bağımsız uygunluk denetimi yapmak.
9. Küçük ve anlamsız PR'lar yerine yönetilebilir teslimat grupları hazırlamak.
10. Tüm PPM tamamlandığında uçtan uca regresyon, tenant, permission, manifest, navigation, audit, Workflow ve WorkCenter denetimi yapmak.

## Sonuç

Bu yöntemle Governance Pack körlemesine kabul edilmez, ancak güçlü gereksinim içeriği de kaybedilmez. Önceki geliştirmelerin güvenli altyapısı korunur; veri modeli, lifecycle, ilişkiler ve UI kapsamı ise yönetici kararlarına göre kontrollü biçimde uzlaştırılır.
