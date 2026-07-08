# Portfolio Delivery (PPM)

**Kısaltma:** `PPM`
**Kısa kod (branch):** `ppm`
**Parent module:** `MOD-0117 — Project & Portfolio Management (PPM)` (Blueprint-kanonik; preflight ile doğrulanmış)
**Module ID policy:** parent `MOD-0117`; alt yetenekler yalnızca `MOD-0117-FUxx` çocukları olarak, `verify_module_id.py --parent MOD-0117` preflight'ından geçerek açılır. FU numarası uydurulamaz.
**Ana servis:** `services/Diten.PpmService/` — ⚠️ **henüz scaffold edilmedi**; yalnızca C1 "PPM Work Records Core" module pack'i `approved`/`ready-for-dev` olduktan ve açık kullanıcı onayı verildikten sonra oluşturulur.

## İş Tanımı

Portfolio Delivery domain'i; iş/proje kayıtları (Work Records), bunlara bağlı görev instance'ları (PPM Tasks), workstream hiyerarşisi, proje takvim planlaması (Calendar Scheduling), proje efor kaydı (Project Effort Log) ve toplantı/durum raporlarını (Meeting / Status Reports) sahiplenir. Eski `DitenPPM` / `PharmacovigilanceWeb` PPM yeteneğinin ERP-vNext'e governance-uyumlu, aşamalı yeniden inşasıdır — eski UI/backend dosyaları **kopyalanmaz**; yalnızca iş kuralı referansı olarak kullanılır.

Orkestrasyon sözleşmesi: [DCP-003 PPM Work Management](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md).

## Kapsam (Yüksek Seviye)

- Work Records (proje/iş kaydı yaşam döngüsü, tenant-scoped kod üretimi)
- PPM Tasks (görev instance'ları: subtask, dependency, checklist, complete+rapor)
- Workstream & hiyerarşi
- Calendar Scheduling (schedule slot, yerleşmemiş iş kuyruğu)
- Project Effort Log (geçici ASSUMPTION rejimi — aşağıya bak)
- Meeting / Status Reports (aggregate + action→task dönüşümü)
- PPM'e özel referans veriler (kategori/tip/öncelik enum'ları — geçici, MOD-0048'e devredilecek)

## Kapsam Dışı

- Onay/SLA/eskalasyon motoru — `→ MOD-0023 Workflow Designer` (PPM yalnızca tüketicidir; MOD-0023 hazır olana dek **yalın status lifecycle**)
- Görev/checklist **şablonları** — `→ MOD-0024 Task & Checklist Engine`
- Bildirim gönderimi (e-posta/SMS) — `→ MOD-0027 Notification Service`
- Kalıcı doküman/binary sahipliği — `→ MOD-0028 / MOD-0262` (PPM evidence/document **link** deseni kullanır)
- Kurumsal lookup SSOT — `→ MOD-0048 Reference Data Management`
- Time Entry / devamsızlık / izin SoR'u — `→ MOD-0280 Time, Attendance & Leave Management` (EA-TBD; PPM tarafında yalnızca "Project Effort Log")
- Organizasyon dizini — `→ MOD-0288 Organization, Person & Position Directory`
- Google Calendar / Meet, SignalR gerçek-zamanlı hub, AI Action Extraction, TimerPopup, Excel import — **ilk faz dışı** (DCP-003 §6)

## "Workflow" Adlandırma Yasağı (zorunlu)

Bu domain'de `Workflow`, `WorkflowTask`, `WorkflowCategory` ve türevleri **hiçbir yeni** route, permission key, UI metni, menü adı, C# class/namespace, JS dosyası, Mongo koleksiyon adı, module pack adı veya branch slug'ında kullanılamaz. Sebep: `MOD-0023 Workflow Designer` ile kavram/menü çakışması. Onaylı sözlük: **Work Record, Project Record, Work Item, PPM Task, Project Effort Log, Meeting / Status Report, Workstream, Schedule Slot**. Tek istisna: eski sistemden veri migration'ında kaynak koleksiyon adlarının salt-okunması.

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md) — sınırlar, repo scope, runtime kararları
- [module-packs/](module-packs/) — her aktif modülün sözleşme dosyası (henüz boş; ilk pack: C1 Work Records Core)
- [DCP-003-ppm-work-management.md](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md) — capability-level sözleşme (`draft`)

## Otorite Hiyerarşisi (Yeni Modül Yazarken)

1. **Module Pack** — [module-packs/{ID}.md](module-packs/)
2. **Domain Config** — [domain-config.md](domain-config.md)
3. **AGENTS.md** — repo kontratı
4. **`.antigravity/rules/`** — engineering NASIL
5. **`execution/portfolio/master-development-plan.md`** — modül envanteri, wave planı

## Yeni Modül Eklerken

Tam akış için: [docs/agent-usage-guide.md](../../../docs/agent-usage-guide.md). Kısa hâli:

1. DCP-003'ün ilgili candidate'inin sırasının geldiğini ve bloklayıcılarının kapandığını doğrula
2. FU kimliği için preflight çalıştır: `py -3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0117-FUxx --name "..." --parent MOD-0117`
3. `/prepare-module-pack` çağır (modül adı + alan sayısı + iş kuralları); "Workflow" yasağına uy
4. Üretilen pack'i incele → `status: approved`
5. Branch aç: `feature/ppm/{id-lower}-{slug}` (örn. `feature/ppm/mod-0117-work-records-core`)
6. `@orchestrator {pack-yolu}` çağır → test → `status: done` + PR
