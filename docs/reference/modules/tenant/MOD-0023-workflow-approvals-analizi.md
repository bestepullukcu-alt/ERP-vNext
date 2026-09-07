# Workflow Approvals (MOD-0023) — Uçtan Uca Analiz

> **Modül:** MOD-0023 — Workflow Config / Approval Templates
> **Servis:** `Diten.Platform` (platform seviyesinde paylaşılan servis, tenant-scoped)
> **UI:** `Diten.Web` → `/Platform/Workflow` (proxy-profile)
> **Tarih:** 2026-06-24
> **Kapsam:** Sayfanın amacı, ekranlar/aksiyonlar, alanlar, modüller arası entegrasyon

---

## 1. Bu sayfa ne amaçla yapılmış

**Workflow Approvals (MOD-0023)**, Diten ERP vNext'in **merkezi, tenant-scoped onay/iş-akışı motorudur.** `Diten.Platform` içinde **platform seviyesinde paylaşılan bir servis** olarak yaşar ama tüm verisi tenant'a izole.

Tek cümleyle: **"Bir iş kaydı (sipariş, fatura, izin vb.) durum değiştirmeden önce onaydan geçmeli mi, geçtiyse serbest mi?" sorusunu yöneten motor.**

### Mimari konum
- UI `Diten.Web` içinde, **proxy-profile** ile çalışır: tarayıcı → `/Platform/Workflow/api/**` (MVC proxy, HttpOnly token'ı server-side okur) → Gateway (5000) → Platform (`/api/v1/workflow/**`).
- Tarayıcı asla token/tenant üretmez; `TenantId` her zaman JWT/claim'den server-side çözülür.
- MVP duruşu: **sadece onay-odaklı**; tam BPMN motoru, low-code, SignalR "source-of-truth" yok.

### Sahip olduğu 7 aggregate (hepsi `TenantScopedEntity`)

| Nesne | Rolü |
|---|---|
| **WorkflowTemplate** | Mantıksal onay tanımı (TemplateCode, status, aktif yayın pointer'ı) |
| **WorkflowTemplateVersion** | Yayınlanmış **değişmez** sürüm (adımlar, SLA bağları, VersionNumber) |
| **WorkflowInstance** | Bir `ObjectRef` üzerinde çalışan akış; başladığı versiyona pinlenir |
| **ApprovalTask** | İnsan aksiyon birimi (approve / reject / delegate / request-info / cancel) |
| **RuntimeAssignmentSnapshot** | Bir görev için "şu an kim aksiyon alabilir" anlık çözümü |
| **WorkflowTransitionLog** | Her geçişin **append-only** kaydı (aktör, from→to, reasonCode, correlationId) |
| **SlaEscalationRule** | SLA penceresi + eskalasyon politikası |

> Önemli: **hiçbir nesne iş kaydının durumunu tutmaz** — kayda sadece opak `ObjectRef = modül + objectType + objectId` ile referans verir.

### Temel kavram zinciri

```
Template (tarif)
   └─ Publish ─▶ Version (değişmez)
        └─ Start ─▶ Instance (canlı çalışma)
             └─ ApprovalTask (sıradaki onaycıya düşer)
                  ├─ approve / reject / delegate / request-info / cancel
                  ├─ TransitionLog (her geçiş kaydı)
                  └─ gecikirse ─▶ Escalation / Timeout
```

---

## 2. Index sayfası — Definitions DataTable + 2 araç butonu

Ana ekran `Diten.Web/Views/Platform/Workflow/Index.cshtml`. Bir **Definitions** DataTable'ı (golden-compact) + iki global buton: **Run Escalations** ve **Transition Gate**.

### Definitions tablosu kolonları
`Template Code` · `Name` · `Status` · `Created At` · `Actions`

Status lifecycle: `Draft → Reviewed → Published → Superseded → Archived`

### Satır aksiyonları (9 adet)

| Aksiyon | Açılış | Ne yapar | Lifecycle kapısı |
|---|---|---|---|
| **Detail** | Ayrı sayfa `/Definitions/{id}` | Şablon meta + sekmeler (Versions/Instances/SLA) | Her zaman |
| **Publish** | Offcanvas | Tanım JSON'unu yeni değişmez versiyon olarak yayınlar | Draft/Reviewed/Published |
| **Start Instance** | Modal | Şablonu gerçek bir kayıt için başlatır | **Sadece Published** (aksi halde disabled) |
| **Versions** | Ayrı sayfa `/Versions` | Yayınlanmış versiyon listesi | Ever-published |
| **Designer** | Ayrı sayfa `/Designer` | Form tabanlı çoklu-adım tasarımcı | Her zaman |
| **Visual Designer** | Ayrı sayfa `/VisualDesigner` | BPMN diyagram tasarımcısı | Her zaman |
| **Instances** | Ayrı sayfa `/Instances` | Bu şablonun çalışan örnekleri | Ever-published |
| **Tasks** | Modal | Görevler + onay aksiyonları | Ever-published |
| **SLA Rules** | Modal | SLA kuralları listesi + ekleme | Ever-published |

> Designer / Versions / Instances aksiyonları modaldan **ayrı sayfaya** taşındı (golden-compact DataTable + sayfa standartları). Tasks ve SLA Rules hâlâ modal.
> Lifecycle kapısı: backend gerçek kuralı uygular (Start, aktif yayınlı versiyon ister; Publish superseded/archived'da reddedilir), UI da aksiyonları buna göre disable eder.

---

## 3. Aksiyon ekranlarının içindeki alanlar

### Publish (offcanvas)
`Definition JSON*`, `Schema Version*`, `Expression Version*`, `Expected Template Version` (optimistic concurrency), `Expected Row Version`, `Publish Reason`.

→ Yeni `WorkflowTemplateVersion` (IsImmutable=true) üretir, şablonu `Published` yapar, **step'lerdeki `sla` bloklarından SLA kurallarını otomatik üretir.**

### Start Instance (modal)
`Template Code`, **`Object Type*`**, **`Object Id*`**, `Object Ref`, **`Candidate Principal Ids*`** (Users/Positions toggle + Select2), `Reason Code`, `Due At`, `Idempotency Key`, `Comment Required`, `Evidence Required`.

→ Aktif yayın yoksa 409. İlk adımın candidate'larını **gerçek kullanıcılara çözer** (pozisyon → `position_assignments`), `WorkflowInstance` + ilk `ApprovalTask` oluşturur. `Idempotency Key` çift başlatmayı engeller.

### Designer (sayfa) — form tabanlı çoklu adım
- **Üst:** `Stage Code/Name`, `Schema/Expression Version`, `Object Type`, `Description`.
- **Her adım kartı** (Add Step ile çoğaltılır, yukarı/aşağı taşı, sil): `Step Code`, `Step Name*`, `Step Type`, **`Candidate Principal Ids*`** (Users/Positions toggle), `Comment/Evidence Required`, SLA `Due/Escalate/Timeout`, `Escalation Principal Ids`.

→ Tek stage içinde N **sıralı** adım = ardışık onay zinciri (örn. Software Developer → CEO). Aynı JSON şeklini üretir, "Use in Publish" ile yayınlar.

### Visual Designer (sayfa) — BPMN
Sol palet + canvas (bpmn-js) + sağ **properties panel**: `Element Id`, `Step Code`, `Step Name`, `Step Type`, `Candidate Principal Ids` (toggle), `Comment/Evidence Required`, SLA `Due/Escalate/Timeout`, `Escalation Principal Ids`. Diyagramdaki task'lar sequence-flow sırasına göre step'lere map edilir.

### Versions (sayfa)
Kolonlar: `Version #` · `Status` · `Schema` · `Expression` · `Immutable` · `Published At` · `Published By` · `Created At`. Satır aksiyonu: **View Definition JSON** (modal).

### Instances (sayfa)
Kolonlar: `Object Ref` · `Status` · `Object Type` · `Stage/Step` · `Started/Due/Completed At`. Satır aksiyonu: **Detail** (modal: Id, ObjectType/Id/Ref, Status, Stage/Step, tarihler, LastTransitionAt, CorrelationId).

### Tasks (modal) — insan aksiyonları
Liste: `Task Id` · `Instance Id` · `Status` · `Stage/Step` · `Due` · `Actioned By` · `Completed`. Satır başına 5 aksiyon (terminal görevlerde disabled). Her aksiyon modali ortak `Actor Id*`, `Reason Code*`, `Idempotency Key*` ister; ek alanlar:

| Aksiyon | Ek alanlar |
|---|---|
| **Approve** | Evidence Ref, Comment |
| **Reject** | Evidence Ref, Comment |
| **Delegate** | Delegate Principal Id*, Comment |
| **Request Info** | Target Principal Id, Evidence Ref, Comment |
| **Cancel** | Comment |

### SLA Rules (modal)
Liste + create form: `Template Id*`, `Stage Code*`, `Step Code*`, `Due In Minutes*`, `Escalate After Minutes*` (≥Due), `Timeout After Minutes` (≥Escalate), `Escalation Principal Ids*`.

### Run Escalations (modal — global)
`Now UTC` (test için "şu an" override), `Max Items` (batch), `Idempotency Key`.

→ Geciken görevleri tarar, eşiklere göre **Escalate** (escalation principal'lara yeniden atar) veya **Timeout** uygular. Sonuç: `Evaluated / Escalated / Timed Out / Skipped` + satır tablosu. Normalde bir recurring-job ile çalışır; bu buton manuel/test tetikleyici. Idempotent (çift eskalasyon olmaz).

### Transition Gate Test Panel (modal — global)
`Object Type*`, `Object Id*`, `Object Ref*`, `Requested Transition*`, `Requested Target State*`, `Actor Id*`, `Reason Code`.

→ **Salt-okunur** değerlendirme: o kayıt için bu geçiş **Allowed / Blocked / NotApplicable** mı? + Gate Status, Blocking Reason/Message, ilgili Instance/Task Id. Hiçbir şey değiştirmez (simülasyon/teşhis).

---

## 4. Bu sayfaya başka modülden veri nasıl gelmeli

İki yönlü:

### A) Çalışma zamanı (runtime) — bir iş kaydını onaya sokmak
Kaynak modül (örn. Satınalma) onaya ihtiyaç duyduğunda `POST /api/v1/workflow/instances` ile **Start** çağırır ve şu üçlüyü verir:
- `objectType` (örn. `PurchaseOrder`), `objectId` (kaydın id'si), `objectRef` (opak referans),
- `templateId` / `templateCode` (hangi onay tarifi),
- `candidatePrincipalIds` (`user:{id}` / `position:{id}` formatında).

### B) Referans verisi (lookup) — cross-service
- **Kullanıcılar:** `Diten.AuthService` (`/api/users`).
- **Pozisyonlar + atamalar:** `Diten.Platform` org verisi (`positions`, `position_assignments`). Pozisyon → çalışma anında atanmış kullanıcıya çözülür. Atama yoksa "candidate required" hatası (geçici seed / Position Assignments ekranı ile çözülür).
- **SLA verisi:** ayrı beslenmez; **publish anında** step'in `sla` bloğundan otomatik üretilir.

> Kural: `TenantId` asla client payload'undan alınmaz; her zaman JWT/claim'den server-side çözülür. Cross-tenant okuma → 404/boş (metadata sızıntısı yok).

---

## 5. Bu modül diğer modülleri nasıl etkiliyor

### Ana mekanizma: Lifecycle Transition Gate (senkron, pull-based)
- Kaynak iş modülü, bir kaydın **durum geçişini commit etmeden ÖNCE** `POST /api/v1/workflow/transitions/evaluate` ile gate'e sorar.
- **Aktif/bloklu** bir workflow varsa → geçiş **engellenir** (kaydın durumu değişemez). Workflow **tamamlanınca** → engel kalkar, geçiş serbest.
- Gate, kaydı `{TenantId, ObjectRef}` index'i ile bulur. **Read-only**, yan etkisiz.

> **Kritik sınır:** İş kaydının **gerçek durumunu kaynak modül tutar** — MOD-0023 sadece **geçiş kapı bekçisidir**; state'i forklamaz/sahiplenmez.

### Sahiplenmediği (tükettiği, yeniden yazmadığı) şeyler

| Konu | Sahibi |
|---|---|
| RBAC / yetki kataloğu / erişim kararları | **MOD-0018** + `Diten.AuthService` |
| Audit trail | **MOD-0021** + audit pipeline |
| Operasyonel görev yürütme (system actions/checklist) | **MOD-0024 (Tasks)** |
| İş kaydının lifecycle **state**'i | **Kaynak iş modülü** |
| BPMN / low-code / görsel builder | MVP'de kapsam dışı |

### Event yayını
Workflow handler'larında **outbox/domain event emisyonu YOK** (kod taramasıyla teyit). Entegrasyon **asenkron event'le değil, senkron gate API çağrısıyla** olur. (İleride SignalR eklenirse "projection-only", asla source-of-truth değil.)

### Yetkiler (`[HasPermission]`)
`definitions.view/manage/publish`, `instances.start/view`, `tasks.approve/reject/delegate/request-info/cancel`, `escalations.manage/run`, `transitions.evaluate`.

> Permission **sabitlerini** MOD-0023 tanımlar ama **seed/grant MOD-0018/Auth'a aittir** (ayrı task).

---

## 6. API uçları (özet)

| Method | Yol | Yetki |
|---|---|---|
| POST | `definitions` | `definitions.manage` |
| GET | `definitions` / `definitions/{id}` | `definitions.view` |
| POST | `definitions/{id}/publish` | `definitions.publish` |
| GET | `definitions/{id}/versions` / `.../{versionId}` | `definitions.view` |
| POST | `instances` | `instances.start` |
| GET | `instances` / `instances/{id}` | `instances.view` |
| GET | `tasks` | `instances.view` |
| POST | `tasks/{taskId}/approve\|reject\|delegate\|request-info\|cancel` | ilgili `tasks.*` |
| POST | `transitions/evaluate` | `transitions.evaluate` |
| POST/GET | `sla-rules` | `escalations.manage` |
| POST | `escalations/run` | `escalations.run` |
| GET | `lookups/positions` | `definitions.view` |

> Hepsi `api/v1/workflow/**` altında; UI bunlara Gateway (5000) / same-origin proxy üzerinden erişir, Platform portuna doğrudan asla.

---

## Özet zihinsel model

**Tasarla** (Designer / Visual Designer) → **Yayınla** (Publish → değişmez Version) → **Çalıştır** (Start → Instance, ilk Task onaycıya düşer) → **Onayla/Reddet/Devret** (Tasks) → gecikirse **Escalate/Timeout** (Run Escalations) → tüm bunlar boyunca diğer modüller geçişlerini **Transition Gate**'e sorarak bloklu kalır → workflow bitince serbest.

> Durum kaynak modülün; onay akışı MOD-0023'ün; yetki MOD-0018'in; audit MOD-0021'in.
