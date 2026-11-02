# ADR-005 — İzin ve devamsızlık (MOD-0280-FU02) şimdilik Diten.Platform'da barınır

| | |
|---|---|
| **Durum** | Kabul edildi |
| **Tarih** | 2026-10-27 |
| **Karar veren** | Sahip (`platform`, ardından ADR-004 tetikleyicisi kendisine gösterildikten sonra `adr-commit`, 2026-10-27, CT · İzin ve devamsızlık sohbeti; kule–modül dağılımı `dagilim-evet`, 2026-10-27) · CONTROL TOWER (gerekçe ve ölçüm) |
| **İlgili** | ADR-004 (MOD-0280-FU01 Platform'da; bu kararın emsali ve yeniden ele alma tetikleyicisi) · Blueprint 8.8 `MOD-0280 Time, Attendance & Leave Management` (yerleşim "Domain App (HCM Time & Leave)") · `execution/registries/module-id-registry.md` MOD-0280-FU02 satırı · `docs/records/audits/2026-10/hcm-leave-discovery-01/RECORD.md` |
| **Etkilenen** | `execution/domains/human-capital-management/domain-config.md` · gelecekteki `module-packs/MOD-0280-FU02-*.md` · `Diten.Platform` (platform-shared-services alanının servisi) · MOD-0023 onay akışı · Görev Merkezi |

---

## Soru

İzin ve devamsızlığın sahibi MOD-0280 (İK). İK alanının `domain-config.md:61` kararı arka ucu `Diten.HcmService` olarak
koyuyor. ADR-004'ün yeniden ele alma tetikleyicisi de tam bu durumu söylüyor: "MOD-0280'in izin/devamsızlık …
dilimleri başlıyor — o dilimler bordro ve İK ana verisiyle aynı serviste durmalıdır." İlk dilim (izin türleri, izin
hakları, izin talebi, bakiye, onay) **nerede** koşacak?

## Karar

1. **ADR-004'ün tetikleyicisi gerçekleşti ve yeniden değerlendirildi.** Hedef aynı kalır: izin, bordro ve İK ana
   verisiyle aynı serviste durmalıdır. Ama bugün o servis bu işi taşıyamıyor (aşağıda, ölçüldü 2026-10-27). Bu
   yüzden **`MOD-0280-FU02` şimdilik Diten.Platform içinde, kendi modülü olarak koşar**, MOD-0280-FU01'in yanında.
2. Kendi koleksiyonları, kendi izin ailesi (`platform.*` değil, HumanCapitalService'in `hcm.time-attendance-leave.*`'i
   değil), kendi modül kodu ve kendi self-registration manifesti olur. Adlar paket taslağında (HCM-LEAVE-06) belirlenir.
3. **Kayıtlar `TenantId` ile sınırlanır; kişi anahtarı CT · Çalışan kaydı'nın yazacağı sözleşmeye göre belirlenir**
   (kullanıcı ↔ kişi ↔ çalışan bağı). Sözleşme gelene kadar paket kişi anahtarını kesinleştirmez.
4. **Görevlere, organizasyona, çalışma takvimine ve onay akışına yalnız port üzerinden dokunur.** Başka modülün deposu
   doğrudan okunmaz; FU01'in desenini (ADR-004 madde 3) uygular.
5. HumanCapitalService'in "Time, Attendance & Leave" hazırlık kayıtları modülüne ve `time-attendance-leave` rotasına
   dokunulmaz. Çalışan kaydına (MOD-0251) ve `Diten.HcmService`'e bu kule yazmaz.

## Neden

- **İlk dilimin bağımlılıkları Platform'da ve FU01 bunları canlıda kullanıyor:** MOD-0023 onay akışı
  (`TimesheetApprovalService.cs`), yönetici bulma (`ApproverResolver.cs`: pozisyon → bağlı olunan pozisyon → yönetici;
  yoksa havuz), pozisyon ataması (`PositionAssignment.cs`, `UserId`), çalışma takvimi ve tatiller (MOD-0362), Görev
  Merkezi, bildirim, denetim. Hepsi aynı süreçte; ağ köprüsü gerekmiyor.
- **HcmService bugün bu işi taşıyamaz (ölçüldü 2026-10-27, `3a607547e`):** outbox / olay yayını yok
  (`services/Diten.HcmService/src` içinde `Outbox`, `IEventPublisher`, `IPublishEndpoint` için 0 eşleşme); `Employee`
  kaydında `UserId` yok; taslağı onaya gönderme 409 `mod0251_lifecycle_activation_not_enabled` döndürüyor
  (`SubmitEmployeeDraftHandler.cs:9,12`). ADR-004'ün gerekçeleri değişmedi.
- **Kule–modül dağılımı (`dagilim-evet`):** izin kulesi `Diten.HcmService`'e ve çalışan kaydına yazmaz; o kod
  CT · Çalışan kaydı'nın.
- **Emsal:** ADR-004 (MOD-0280-FU01) ve ADR-003 (MOD-0357) aynı gerekçeyle Platform'da barındı.

## Sonuçlar

- **Başka bir alanın servisinde çalışmak bir sapmadır.** `Diten.Platform` platform-shared-services alanının servisidir
  (AGENTS.md Çekirdek 6: başka domain'in servisine dokunma). FU01 bu sapmayı ADR-004 ile kaydetti; FU02 de bu kayıtla
  aynı sapmayı alır. Platform'da kod yazan her iş paketi XMC-001 onayını ayrıca ister.
- `human-capital-management/domain-config.md` MOD-0280-FU02'yi kapsam içi modüllere bu sapmayla birlikte alır;
  "leave, absence … stay out of scope" satırı yalnız bu dilim için kalkar. Bu güncelleme paket taslağıyla birlikte yapılır.
- **Taşıma yolu bir veri taşımasıdır, yeniden yazım değildir:** taşıma birimi FU02'nin koleksiyonları + izin anahtarları
  + kendi `Features/**` klasörü + port arayüzleri. İzin anahtarları servis adı taşımaz (PKS-001 §2). Gateway'de tek önek
  yeni servise yönlendirilir. FU01 ile FU02 aynı anda taşınır.
- Blueprint yerleşiminden ("Domain App (HCM Time & Leave)") ve HcmService arka uç kararından sapma paketin sapma
  bölümünde kayıtlı olur.

## Ek karar 1 — MOD-0003 ve MOD-0037 yokken ilk dilim (2026-10-27)

> Sahip kararı `gecici-sozlesme` (2026-10-27, CT · İzin ve devamsızlık sohbeti). Kaynak: bağımsız tasarım denetimi 01,
> bulgu F-02 (`docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md`).

**Soru.** Blueprint `Dependencies_Normalized` satır 522 ve 524, MOD-0280 için MOD-0003 Data Contract Registry ve
MOD-0037 Integration Monitoring & Reconciliation'ı HARD bağımlılık sayıyor. MOD-0280-FU01 ilk dilimde bunları "gerekmez"
saydı (paket C-13: servisler arası sözleşme yok, dış entegrasyon yok, UserId anahtarı). FU02 ise CT · Çalışan kaydı'nın
HCM-CORE-BUNDLE sözleşmesiyle `Diten.HcmService`'ten veri okuyacak, yani FU01'in gerekçesi taşınamaz. İkisi de bugün
yapılmamış (kimlik kaydında MOD-0003 `planned / missing`, MOD-0037 `review / planned`).

**Karar (geçici, ilk dilim için).**

1. **MOD-0003'ün yerine — sözleşme depoda ve testli.** FU02'nin tükettiği her sözleşme (HCM-CORE-BUNDLE okuma uçları ve
   olayları) sürümlü bir sözleşme dosyası olarak depoda durur; sözleşmenin sahibi CT · Çalışan kaydı'dır. Hem üretici
   (HcmService) hem tüketici (FU02) tarafında sözleşme testi bulunur. FU02 tükettiği sözleşme sürümünü kendi paketinde
   ve yapılandırmasında açıkça yazar; bilinmeyen sürüm reddedilir.
2. **MOD-0037'nin yerine — izleme ve uzlaştırma izin tarafında.** Çalışan verisini eşitleyen iş her koşuda sonucunu
   (okunan, değişen, eşleşmeyen kayıt sayısı ve hata) denetim izi ile kaydeder; eşleşmeyen kayıtlar için uzlaştırma
   raporu üretir. Sorumlu izin kulesidir (MOD-0280-FU02).
3. **Çalışan servisine ulaşılamazsa (kapalı hata).** Yeni izin talebi açılamaz ve ekranda sade bir mesaj gösterilir.
   Var olan izinler, bakiyeler ve onay görevleri okunmaya devam eder. Bekleyen onay kararları uygulanır, ama yeni bakiye
   hesabı gerekiyorsa servis gelene kadar beklenir. Önbellekteki eski çalışan verisiyle yeni talep açılmaz.

**Sonuçlar.**

- Bu bir sapmadır ve paketin sapma bölümünde kayıtlı olur. Platform alanının MOD-0003 ve MOD-0037 sahiplerine bilgi
  verilir; bu karar o modülleri yapmaz ve onlara yazmaz.
- **Geri dönüş tetikleyicisi:** MOD-0003 ya da MOD-0037 `ready-for-dev` olunca FU02 o modüle bağlanır. Depodaki
  sözleşme dosyası kayda taşınır, izin tarafındaki izleme MOD-0037'ye devredilir.
- Gerçek çalışan verisiyle kabul, MOD-0251'in aktifleştirme dilimine bağlı kalır. Geliştirme ve test sahte çalışanla
  yapılabilir; kabul kanıtı sahte veriyle verilmez.

## Yeniden ele alma tetikleyicisi

Aşağıdakilerden biri gerçekleşince bu karar yeniden açılır ve FU01 + FU02 birlikte taşınır:

- MOD-0251 çalışan ana verisi canlıda, çalışan kullanıcıya bağlı ve `Diten.HcmService` olay yayınlıyor; **ya da**
- bordro (MOD-0281 Payroll Governance & Payroll Controls) dilimi başlıyor ve izin verisini tüketmesi gerekiyor.
