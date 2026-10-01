# ADR-004 — Zaman girişi (MOD-0280-FU01) Diten.Platform'da barınır

| | |
|---|---|
| **Durum** | Kabul edildi |
| **Tarih** | 2026-09-29 |
| **Karar veren** | Sahip (2026-09-17: zaman girişinin SoR'u MOD-0280 — DEC-002 teyidi) · CONTROL TOWER (mimari, sahibin yetkisiyle, 2026-09-29) |
| **İlgili** | Blueprint 8.1 `MOD-0280 Time, Attendance & Leave Management` (W-3, yerleşim "Domain App (HCM Time & Leave)") · `execution/registries/module-id-registry.md` MOD-0280 / MOD-0280-FU01 satırları · DEC-002 (`docs/roadmap/backlog/product-backlog.md`) · ADR-003 (aynı desen, MOD-0357) · `docs/reference/modules/tenant/workcenter/workcenter-reporting-and-time-decisions.md` §6 |
| **Etkilenen** | `execution/domains/human-capital-management/domain-config.md` · `module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md` · `Diten.Platform` · Görev Merkezi sayaç ve efor kartı · MOD-0023 (reddetmede gerekçe seçeneği) |

---

## Soru

Zaman girişinin sahibi MOD-0280 (İK) — bu 2026-09-17'de teyit edildi. İK alanının `domain-config.md:58` kararı
arka ucu `Diten.HcmService` olarak koyuyor. İlk dilim (sayaç, haftalık çizelge, yönetici onayı, görevin harcanan
süresi) **nerede** koşacak?

## Karar

1. **`MOD-0280-FU01` Diten.Platform içinde, kendi modülü olarak koşar.** Kendi koleksiyonları (`time_entry_*`),
   kendi izin ailesi (`time-entry.*` — `platform.*` değil, HumanCapitalService'in `hcm.time-attendance-leave.*`'i
   değil), kendi modül kodu (`time-entry`) ve kendi self-registration manifesti.
2. **Her kayıt `TenantId + UserId` ile anahtarlanır.** Çalışan (Employee) kimliği taşınmaz.
3. **Görevlere, toplantılara ve org şemasına yalnız port üzerinden dokunur.** Görev koleksiyonlarıyla join yok;
   `Features/TimeEntry` içinde `Adapters/` dışında başka modülün deposu anılmaz (kaynak taraması muhafızı).
   Başka modüllerin bu modülü çağırdığı iki nokta (`ITaskTransitionObserver`, `ITaskSpentTimeSource`)
   `Application/Contracts` altında arayüzdür.
4. HumanCapitalService'in "Time, Attendance & Leave" hazırlık kayıtları modülüne dokunulmaz.

## Neden

- **v1'in bütün bağımlılıkları Platform'da:** görev yaşam döngüsü ve geçiş kaydı, toplantı/tutanak katılımı,
  çalışma takvimi ve `IWorkingHoursProvider`, pozisyon / bağlı olunan pozisyon, MOD-0023 onayı, denetim izi,
  outbox, Hangfire. Hepsi aynı süreçte; ağ köprüsü gerekmiyor.
- **HcmService bugün bu işi taşıyamaz (ölçüldü 2026-09-29):** outbox/eventing yok (0 dosya); `Employee` kaydında
  `PersonId` var, `UserId` yok (`Employee.cs:7`) — sayacı başlatan kullanıcı çalışana bağlanamıyor; MOD-0023
  karar alma ucu 409 döndüren bir taslak (`EmployeeDraftsController.cs:92-98`).
- **Emsal:** ADR-003 (MOD-0357 Toplantılar) aynı gerekçeyle Platform'da barındı ve taşınabilirlik sınırlarını
  paketin sözleşmesi yaptı; bu karar aynı sınırları uygular.
- SAP CATS İK'nın da projenin de içinde değildir, zamanı bir kez kaydedip alıcılara dağıtır; Oracle Time and Labor ve
  Workday Time Tracking de çalışan kimliğinin yanında ayrı ürünlerdir. Belirleyici olan kaydın **çalışan kimliğine ve
  iş nesnelerine** yakın durmasıdır — bugün ikisi de Platform'da.

## Sonuçlar

- `human-capital-management/domain-config.md` MOD-0280-FU01'i kapsam içi modüller arasına bu sapmayla birlikte alır.
- **Taşıma yolu bir veri taşımasıdır, yeniden yazım değildir:** taşıma birimi `time_entry_*` koleksiyonları +
  `time-entry.*` anahtarları + `Features/TimeEntry/**` + iki sözleşme arayüzü. İzin anahtarları servis adı
  taşımadığı için taşımada yeniden adlandırılmaz (PKS-001 §2 değişmez anahtar kuralı). Gateway'de tek önek
  (`/api/v1/time-entry`) yeni servise yönlendirilir; iki arayüz outbox olayı ve HTTP okumasına dönüşür.
- Blueprint yerleşiminden ("Domain App (HCM Time & Leave)") ve HcmService arka uç kararından sapma paketin §26'sında
  kayıtlıdır.
- Paket `ready-for-dev`'e bu ADR ile birlikte geçer; kayıt defteri durumu paketle birlikte güncellenir.

## Yeniden ele alma tetikleyicisi

Aşağıdakilerden biri gerçekleşince bu karar yeniden açılır ve taşıma planlanır:

- MOD-0251 çalışan ana verisi canlıda ve çalışan kaydı kullanıcıya (`UserId`) bağlı; **ya da**
- MOD-0280'in izin/devamsızlık veya puantaj (attendance) dilimleri başlıyor — o dilimler bordro ve İK ana verisiyle
  aynı serviste durmalıdır.
