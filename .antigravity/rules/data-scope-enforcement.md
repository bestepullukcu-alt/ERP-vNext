---
description: "SEC-002 — Veri kapsamı zorunluluğu: kiracı çapında satır döndüren her modül, kapsamı IDataScopeResolver'dan sorar ve sorgusunu daraltır"
---

# Veri Kapsamı Zorunluluğu

Yetki "ne yapabilir", **veri kapsamı** "hangi satırları görür" sorusunu cevaplar. İkisi ayrı mekanizmadır; bu
standart ikincisinin ne zaman ve nasıl uygulanacağını sabitler.

> **Otorite:** `.antigravity/` katmanı (AGENTS.md §1). Kimlik doğrulama ve varsayılan-ret tabanı SEC-001'dedir,
> kiracı izolasyonu `multi-tenancy.md`'dedir. Kapsamın kendisi MOD-0018-FU15'te hesaplanır; bu dosya onu **kimin
> sormak zorunda olduğunu** söyler.

---

## 1. Neden var

Kapsam bugün hesaplanıyor ama neredeyse hiç sorulmuyor. Ölçüldü (2026-09-21, `origin/main`):
`IDataScopeResolver` üretim kodunda yalnız üç yüzeyde tüketiliyor — iş raporu (`WorkReportQueryHandler` +
`WorkReportScopeSource`), görev atama uygunluğu (`TaskAssignmentScopeResolver`) ve "neden erişebiliyorum" ekranı
(`SelfAccessExplainService`).

Sonuç: bir modülün okuma yetkisini alan kişi, bir birime atanmış olsa bile o modülün **kiracıdaki bütün
satırlarını** görür. Yetkiyi veren yöneticinin beklediği bu değildir: "CRM'i okuyabilsin" demek "bütün müşterileri
görsün" demek olmamalıdır. Kiracı izolasyonundan farkı da budur — `TenantId` süzgeci başka şirketin verisini
kapatır, kapsam aynı şirket içindeki başkasının verisini kapatır.

SAP ve Oracle bu ayrımı üründe kurar: SAP'de yetki nesnesi alan değeri taşır (şirket kodu, satış organizasyonu),
Oracle'da işlev güvenliğinin yanında ayrı bir veri güvenliği politikası vardır. Bizdeki karşılığı
`IDataScopeResolver`'dır.

---

## 2. Kapsam nedir, nereden gelir

`IDataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, featureCode, ct)` çağrısı kişinin MOD-0288
organizasyon verisinden türeyen kapsam listesini verir. `EntitlementDataScopeKind` on üç tür tanımlar;
`OrgDataScopeResolver` v1 bunlardan **dördünü** üretir:

| Tür | Anlamı |
|---|---|
| `OrgUnit` | Kişinin birimi ve altındaki bütün alt birimler — çözücü alt ağacı **önceden düzleştirir** |
| `Position` | Kişinin pozisyonu |
| `ManagerChain` | Raporlama zinciri **yukarı** (üstündeki pozisyonların kimlikleri) |
| `LegalEntity` | Birimin bağlı olduğu tüzel kişilik (MOD-0220 referansı) |

`ManagerChain`'in yukarı bakması kasıtlıdır: veri kapsamı "üstlerim üzerinden hangi satırları görebilirim" diye
sorar. Atama kapsamı (`TaskAssignmentScopeResolver`) ters yöne, aşağı yürür; ikisi karıştırılmaz.

Dört davranış ezberlenir:

- **Kapalı başlar.** Geçerli, tarihi geçmemiş bir pozisyon ataması yoksa kapsam boştur → sıfır satır.
- **Karar vermez.** Kapsam hiçbir zaman "izin var/yok" demez; yalnız satır kümesini tarif eder.
- **Yetki türetmez.** Pozisyondan rol/izin çıkarılmaz.
- **Dışlama taşıyabilir.** `EntitlementDataScope.IsInclude` yanlış olabilir (§3.8).

---

## 3. Kural

**Kiracı çapında satır döndüren her sorgu, kapsamı sorar ve sorgusunu daraltır.** Uygulaması:

1. Kapsam **sunucuda** uygulanır; listeyi çekip tarayıcıda süzmek kapsam değildir.
2. Kapsam **sorgunun içinde** uygulanır; sayfalama ve toplamlar daraltılmış kümeden hesaplanır. Önce çekip sonra
   atmak sayfa sayısını ve toplamı yanlış gösterir.
3. Kapsam **boşsa sonuç boştur**. "Kapsam yok → hepsini göster" yazan kod kuralın tam tersidir. Aynı şey hata için
   de geçerlidir: kiracı yok, kullanıcı yok ya da çözücü hata verdi → boş kapsam, süzgeçsiz sorgu değil.
4. Kapsam tercihi (ör. "yalnız benimkiler") **yalnız daraltabilir**. Genişleten bir tercih 403 ile reddedilmez,
   sessizce yok sayılır ve kişi zaten hakkı olanı görür. Referans: `WorkReportScopeSource.ResolveAsync`.
5. Kiracının **tamamını** görmek ayrı bir izindir; kapsamın bir türü değildir. Kalıp:
   `<modül>.<özellik>.read-tenant-wide` (örnek: `platform.tasks.work-report.read-tenant-wide`). Bu izin
   ölçülmeden tenant-wide küme kurulamaz; boolean parametreyle değil ayrı bir üretici ile kurulur
   (`WorkReportScope.TenantWideScope()`).
6. Yetki ile kapsam **ayrı yerlerde** durur: yetki uç noktada, kapsam sorguda. Biri diğerinin yerine geçmez.
7. Kapsam kararı **tek yerde** çözülür ve paylaşılır; aynı modülde ikinci bir kopya yazılmaz. Rapor ile liste aynı
   kapsamı okumak zorundadır; geride kalan ikinci kopya ne çöker ne log basar, yalnız başkasının verisini çizer.
8. **İkinci bir kapsam motoru yazılmaz.** Modül kapsamı hesaplamaz, çözücünün verdiğini kendi alanlarına çevirir.
   Bu çeviride iki yön vardır ve ikisi de daraltma yönünde biter:
   - Modülün karşılığı olmayan bir tür (ör. görevlerde `LegalEntity`) **çevrilmez**; birim→tüzel kişilik gibi bir
     birleştirme uydurulmaz. Karşılığı olmayan tür daraltır, sızdırmaz.
   - `IsInclude = false` taşıyan bir kapsam **düşürülmez**. Modül dışlamayı doğru uygulayamıyorsa sonucu boşa
     çevirir ve "desteklenmiyor" diye kaydeder; yok sayılan dışlama, birinin bilerek kapattığı satırı gösterir.
9. Kapsam dışında bırakılan bir kayda kimlikle doğrudan erişim de **404/boş** döner; liste süzülüp detay açık
   kalmaz.

### Ne zaman uygulanmaz

- Kayıt kişinin kendi kaydıysa ve sorgu zaten kişiye göre süzüyorsa (ör. "benim görevlerim").
- Kiracı içinde herkese açık olduğu **modül paketinde yazılı** olan referans/sözlük verisi.
- Platform yöneticisi yüzeyleri (kiracı üstü). Platform aktörü tek bir kiracının org ağacında çözülmez; o sınır
  kiracının kendisidir.
- **Yönetim yüzeyleri: sınırı yetkinin kendisi olan ekranlar.** Kullanıcı ve rol yönetimi bunlardır. Kapsanacak
  bir alan yoktur: kayıt bir kişinin HESABIDIR, organizasyondaki yeri değil — birim bilgisi pozisyon atamasında,
  başka bir serviste durur. `auth.users.manage` yetkisini kime verdiğin zaten "kim kullanıcıları yönetebilir"
  sorusunun cevabıdır.

  ⚠ BU İSTİSNA İŞ VERİSİNE UZANMAZ. Müşteri, sipariş, görev, toplantı bir yönetim yüzeyi değildir; "yetki zaten
  sınır" cümlesi oralarda kuralı kaldırmaz, yalnız kuralın uygulanmadığını gizler. İstisna, yönettiği şey
  hesabın kendisi olan ekranla sınırlıdır.

  ⚠ VE DEVREDİLMİŞ YÖNETİM BU İSTİSNANIN ALTINDA SAKLANAMAZ. "Türkiye tüzel kişiliğinin kullanıcılarını yalnız
  Cem yönetsin" gerçek ve yaygın bir ihtiyaçtır (Oracle'da data role, SAP'de kullanıcı grubu). O gün geldiğinde
  cevap "istisnamız var" değil, kendi iş paketidir: hesabı organizasyona bağlamak servisler arası bir iştir ve
  öyle planlanır.

Hangi istisnanın geçerli olduğu modül paketinde bir cümleyle yazılır; yazılmamışsa kural uygulanır. Ölçüldü
(2026-09-23): Kullanıcılar listesi (`GetAllUsersQueryHandler` → `GetAllByTenantAsync`) yalnız `TenantId` ile
süzüyor ve bu dördüncü duruma girer — paketine o cümle yazılana kadar kural onu da bağlar.

---

## 4. Yeni iş için kapı

Bir modül paketi `ready-for-dev` olamaz ve bir iş paketi CT tarafından kabul edilemez, eğer:

- modül kiracı çapında satır döndürüyor, ve
- pakette kapsam cümlesi yok, ya da kod kapsamı sormuyor, ya da kapsamı kanıtlayan testi yok.

**Kapsam cümlesi** şu üçünü söyler: hangi alan kapsamı taşır (`OrganizationUnitId`, `PoolPositionId`, …), hangi
kapsam türleri çevrilir, tenant-wide izni var mı ve adı ne.

**Kanıt testi:** iki kişi, iki ayrı birim, aynı yetki → birinin listesinde diğerinin kaydı çıkmaz; pozisyon
ataması olmayan üçüncü kişi hiçbir kayıt görmez; tenant-wide izni olan görür. Sabotaj: kapsam daraltması
kaldırıldığında test kırmızı olur (AGENTS.md test kuralı — sabotaj kanıtı olmayan guard, guard değildir).

---

## 5. Referans uygulama

Kopyalanacak örnek üç dosyadır; yenisini tasarlamadan önce bunlar okunur:

- `Features/Tasks/Services/WorkReportScope.cs` — kapsamı modülün alanlarına çeviren tip; `Empty`,
  `MatchesNothing`, `TenantWideScope()`, dışlama ve karşılıksız tür davranışı burada.
- `Features/Tasks/Services/WorkReportScopeSource.cs` — tek çözüm noktası; tercih yalnız daraltır, üç yoldan
  kapalı başlar.
- `Features/Tasks/Handlers/QueryHandlers/WorkReportQueryHandler.cs` — daraltılmış kümenin sorguya girişi.

Kapsamı bugün tüketen diğer yüzeyler: `Features/Tasks/Services/TaskAssignmentScopeResolver.cs` (atama uygunluğu,
ters yön) ve `API/Authorization/Explain/SelfAccessExplainService.cs`.

---

## 6. Geçiş

Bu kural **yeni modüller** ve **hâlihazırda üzerinde çalışılan modüller** için bugünden geçerlidir. Daha önce
yazılmış modüllerin bağlanması tek tek backlog maddesidir; kural geçmişe dönük toplu bir düzeltme emri değildir.

**Başka bir serviste çalışıyorsan** (MDM, HCM, …): `IDataScopeResolver` bugün yalnız Platform'da kayıtlı
(`Diten.Platform.Application/DependencyInjection.cs` → `OrgDataScopeResolver`). Ortak kütüphanedeki
`NoOpDataScopeResolver` **boş liste** döndürür, yani kayıtlıysa hiçbir satır açmaz — "kapsam yok, hepsi serbest"
diye okunacak bir varsayılan yoktur. Kendi servisinde kapsam uygulayacaksan çözücüyü oraya da kaydedersin;
`NoOp`'u üretimde kaydetmek kapsamı kapatmak demektir, açmak değil.

Servis hesapları için ayrı bir veri mekanizması kurulmaz: servis hesabına da pozisyon/birim verilir ve aynı kapsam
uygulanır. Servis hesabının farkı kimlik ve davranış tarafındadır (ekrandan giriş, anahtar, süre, görünürlük),
veri tarafında değil.
