# MG-MEET-S28 — Dört prompt planı (PLANNED adımı için taslak)

> Taslaktır; kod, kuyruk, commit ya da dağıtım izni değildir. Kuyruk `mg`, satır `MG-MEET-S28`. Modül: MOD-0357 Management Review & Cadence (Toplantılar). Dilim S28, teslim DL-28, kanıt E5. Paket: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` (değişmez).
> Tek dal, tek birleştirme isteği. Dört prompt ara adımdır; ara durum ana dala gitmez. Taban: eb561752267ae5f7f5f573a43e451bdd0c9073bc. P2, P3 ve P4'ün başlangıç commit'i önceki prompt'un sonudur, bugün bilinmez.

Bütün prompt'larda ortak sınır: `Features/Tasks/**` değişmez; yeni izin anahtarı yok; S28'de yeni denetim olayı yok; paket, `ocelot.json`, `_Layout.cshtml`, Archive ve `.antigravity/**` değişmez; `main`'e commit ya da push yok, yalnız açık yolla stage, yıkıcı git yok; port, kimlik ya da tanımlayıcı uydurulmaz; başka modülde düzeltme gerekirse durulur ve sorulur. Test koşuları `scripts/known_failures.py` ile ayrıştırılır. Yerel gerçek Mongo `C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe`; docker yok.

## P1 — Ortak kontrol ve yazma işleyicileri

Kapsam: `MeetingWriteAccess` ve sistem kaynaklı işaretleyiciyi kurar, 15 yazma işleyicisini ve `FollowUpOfMeetingId` kontrolünü korur, `RespondToInvitationHandler` sırasını değiştirir, taramayı işaretleyici ve zincirsiz yedekle bağlar, 14 dosyada 53 test kurulumunu günceller, Roslyn sınıf koruması ve işaretleyici korumasını yazar.

Bağımlılık: yok (paket §24.7). Ön koşul: kuyruk satırı `READY`, kayıt satırı `origin/main`'de, HIGH inceleme kaydı.

İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingSystemOrigin.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/AgendaItemCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MinutesCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingTaskBridgeCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**

Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/Archive/**, frontend/Diten.Web/**, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv

## P2 — Oluşturma yolu

Kapsam: Oluşturanın göreceği kuralı (400 `MEETING_CREATOR_CANNOT_VIEW`), tekrar dalı (200 ya da sabit gövdeli 409 `MEETING_CREATE_CONFLICT`), `RequireCreatorCanView` ve `PreAcceptedUserId` komut parametreleri ile tarama, yansıma ve HTTP gövde korumaları, üç neden kodu ve 7 dilde köprü metinleri.

Bağımlılık: P1'in son commit'i (`MeetingWriteAccess`, `MeetingSystemOrigin`, `FollowUpOfMeetingId` çağrısı).

İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, frontend/Diten.Web/wwwroot/assets/js/Meetings/api.js, frontend/Diten.Web/Views/Meetings/_IndexL10n.cshtml, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.en.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.tr.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.fr.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.es.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.zh.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.ar.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.ru.resx, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**

Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Resources/SharedResource*.resx, frontend/Diten.Web/wwwroot/assets/js/shared/**, frontend/Diten.Web/wwwroot/assets/css/**, frontend/Diten.Web/Program.cs, frontend/Diten.Web/tests/meetings-reason-code-bridge.test.js, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv

## P3 — Takip toplantısı

Kapsam: Bağlantı hedefi kontrolü, tamamlama ve yaşam döngüsü 409'ları, Q25 zamanlayan satırı, önce taşıma ve en son bağlantı, satır düzeyi atlama, ortam başına mükerrer sayımından sonra kısmi tekil dizin `ux_meeting_agenda_items_tenant_meeting_carried_link`, gerçek Mongo ile yarıda kesilme ve eşzamanlı tamamlama satırları.

Bağımlılık: P2'nin son commit'i. Ön koşul: staging ve canlı için mükerrer sayım sonucu CT'den, sıfır.

İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IMeetingRepositories.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/MeetingRepositories.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**

Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/Archive/**, frontend/Diten.Web/**, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv

## P4 — Rapor, Görev Merkezi ve kanıt

Kapsam: Rapor ve dışa aktarım maskesi, 7 dilde `MeetingReportExportPlaceholders`, `MeetingRelatedRecordResolver`'ın düşürmesi, HTTP ve gerçek Mongo ile tam yazma matrisi, bayt eşitliği ve seri satırları, paketin 34 sabotaj maddesi (35 koşu), E5 kanıt paketi.

Bağımlılık: P3'ün son commit'i. Sonraki adımlar CT'nindir: kabul koşusu, S28-9 kaydı, PR.

İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/QueryHandlers/MeetingReportQueryHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingReportModels.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportSerializer.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportPlaceholders.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/RecordLinks/MeetingRelatedRecordResolver.cs, frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.js, frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.l10n.js, frontend/Diten.Web/Views/Meetings/Report/_IndexL10n.cshtml, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.en.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.tr.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.fr.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.es.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.zh.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.ar.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.ru.resx, frontend/Diten.Web/tests/meeting-report-*.test.js, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.API/Controllers/MeetingsController.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs

Son sekiz yol yalnız geçici sabotaj içindir; iş sonunda farkları boştur (P4 DOĞRULA 8).

Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Resources/SharedResource*.resx, frontend/Diten.Web/wwwroot/assets/js/shared/**, frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/**, frontend/Diten.Web/wwwroot/assets/css/**, frontend/Diten.Web/Program.cs, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv

## CT kararları ve okumaları (paket değişmez)

**Yazdığım iş bölümü pakete uymuyordu; paket kazanır.** Koruma P1'de yazıldığı için onun kapsadığı sınıflar P1'de geçmek zorundadır:

- `CreateMeetingHandler` içindeki `FollowUpOfMeetingId` kontrolü P1'dedir (paket S28-2 kapsamı, 5 istisnada yok); HTTP satırı P2'de kalır.
- `ScheduleFollowUpMeetingHandler` kaynak kontrolü P1'dedir (S28-1'in 15 işleyicisinden biri); P3 bağlantı hedefini ekler.
- `RequireCreatorCanView` hesabı P2'dedir: P2 oluşturan kuralını yalnız açarsa kaynağın katılımcısı 400 alır.
- `PreAcceptedUserId` taraması kademelidir: P2 "varsa yalnız `MeetingFollowUpCommandHandlers.cs`", P3 "tam bir dosya" (paket satır 4497–4498).

**Paket içi ve paket-kod farkları.** Paket onaylıdır, düzenlenmez; bir sonraki revizyona not:

- §25 satır 4795 `RequireCreatorCanView: false` yazar, S28-10 (satır 4330–4332) taraf olmayan için `true` der: S28-10 izlenir.
- §5 satır 319 `MongoDbIndexConfigurations.cs`, §24.31 satır 3995 `PlatformSchemaManifest.Meetings.cs` der: §24.31 izlenir.
- Satır 4408 `data` "yalnız kimlik" der, `MeetingModels.cs:469` `CarriedAgendaItemCount` da taşır: işçi en küçük çözümü seçer, JSON testi ölçer, P3'te durma koşulu vardır.
- Satır 4406 ile 4417 sırayı vermez: döngü kontrolü önce çalışır, `MEETING_SELF_FOLLOW_UP` testleri korunur.
- §24.31 dosya listesi `DependencyInjection.cs`, `IMeetingRepositories.cs`, `MeetingRepositories.cs` ve (yalnız sabotaj) `MeetingsController.cs`'i anmaz; §5 satır 313, 318, 321 izin verir.
- Paketin ad vermediği tanımlayıcılar (`MeetingSystemOrigin`, `TryCreateAsync` ve neden kodu sabitleri) CT okumasıdır, prompt'larda sabittir.

**S28-8 satırlarının dağılımı CT kararıdır:** oluşturma P2, takip P3, seri, rapor ve durum satırları P4.

## Prompt'lara girmeyenler

- S28-9 CT'nin kayıt adımıdır.
- Staging ve canlıda mükerrer sayım: işçinin erişimi yok, sonuç CT'den gelir, yoksa P3 dizinden önce durur. Kimin koşacağı sahip kararıdır.
- `ScheduleReviewMeetingForTaskHandler` tekrar yolu (paket satır 4094–4101): S28-8'de satırı yok; test edilmez, P4 "Known gaps"te yazar.
- "İstek sürerken silinme" (satır 4622): P4 önceden silinmiş toplantıyı sınar, yarış değil.
- Sonraki dilim notları (S15, S19, S23, S25, S27) kendi dilimlerindedir.

## Ölçülmeyen

- `dotnet` derleme ve test koşmadı; yerel `mongod` denenmedi.
- Prompt'lardaki bazı komut değerleri koşulmadı: `--suite` adları, `verify_datatable_page.py` seçenekleri, sayım sorgusunun BSON alanları (P3 işçiye önce doğrulatır).
- Taslakları yazan alt ajan kural dosyalarının bir kısmını yalnız adlandırdı, açmadı.
- CT, P1'i ve bu planı baştan sona okudu; P2, P3, P4'ü yalnız bulgu satırlarında okudu. Dört taslak iki ayrı alt ajana ön inceleme için verildi (2026-10-31, kayıt dışı): 1 ENGEL (P2 create replay kurulumu, kapandı), 12 ORTA, 8 DÜŞÜK; hepsi işlendi, kod koşmadı, bu kurala göre yeni tur yok. Bağımsız HIGH incelemesi her prompt'un son `Expected HEAD`'inde yapılır.
- Ana daldaki para birimi testi kayıtlı değildir; P4 yalnız bu bir arızayı tolere eder ve raporlar. Çözümü bu kulenin işi değildir.
