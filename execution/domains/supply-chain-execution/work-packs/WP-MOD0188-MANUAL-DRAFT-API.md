# WORK PACKAGE — WP-MOD0188-MANUAL-DRAFT-API

**Control Tower, 2026-10-07.**

- **Kaynak:** MOD-0188 Module Pack, AC-D10/D12/D13/D14/D27; kullanıcının 29 kriterin tamamı ve manuel kabul kararı.
- **Kapsam:** Yalnız MOD-0188 PlanningService Draft API/Application, owned Demand v2 DRAFT sözleşmesi ve testleri.
- **Çalışma yeri:** Mevcut `ERP-vNext-git`, `feature/sce/mod-0188-demand-planning`. Yeni worktree veya branch yok. Ali 2026-10-07 tarihinde yalnız bu dalda kapsamı denetlenmiş yerel commit'e izin verdi; push/PR ayrıca onay gerektirir.
- **Durum:** Gönderime hazır; CT K13 ve kullanıcı E4 kabulü bekler.

## Kanit (CT)

- `ManualDraftStoreContracts.cs:6-27` kalıcı Create, EditWeek, Read ve TransitionReview sınırlarını taşır; canlı API veya izin vermez.
- `ManualDraftMongoStore.cs:13` mevcut Mongo store'dur. Önceki gerçek Mongo testleri Draft, audit ve inceleme çekirdeğini ölçtü; API davranışını ölçmedi.
- `PlanningCyclesController.cs:9` ve `DemandHistoryImportBatchesController.cs:10` mevcut v2 API örnekleridir. Draft controller yoktur.
- `DemandPlanningServiceCollectionExtensions.cs:20-27` Draft create/update ve plan review izin anahtarlarını kaydeder; `:83` satırında `IManualDraftStore` DI kaydı vardır. Platform LegalEntity yetki kaynağı hâlâ canlı bağlı değildir.
- `contracts/drafts/demand-v2.openapi.yaml:36-179` revision okuma/yayın taslağını taşır; Draft yazma ve inceleme endpoint'leri bulunmaz. Bu dosya DRAFT'tır, FROZEN değildir.

## NE

1. Mevcut domain/store çekirdeğini kullanarak manuel Draft oluşturma, kapsamlı okuma, hafta düzenleme, incelemeye gönderme, bağımsız onay ve gerekçeli ret için CQRS handler + ince controller dilimini ekle. Her endpoint'i owned Demand v2 taslağında aday olarak belgeleyip DRAFT bırak.
2. Tenant ve aktörü doğrulanmış sunucu bağlamından al. Seçilen LegalEntity yalnız ipucudur; yetkili atama ve Demand izinleri her işlemde yeniden doğrulanır. Kaynak yoksa veya nedenleri ayıramıyorsa erişimi açma; güvenli kapalı hata ve açık engel raporu ver.
3. Mevcut 52 hafta tamlık, tek aday, görev ayrılığı, idempotency, beklenen sürüm ve zorunlu audit semantiğini gevşetme. Sunucu zamanı kullan; istemci audit aktörü/zamanı ve `IsEligible` kabul etme.

## KORU / YAPMA

- Frontend/Gateway, başka servisler, merkezî contracts/registry, Demand v1 FROZEN ve `.antigravity` değişmez.
- Canlı cross-service çağrı ekleme; yalnız frozen contract/mock ile izole geliştir. Mock canlı kabul kanıtı değildir.
- Yayın/invalidation veya MOD-0189 davranışı bu WP'de yoktur. AC'leri bitmiş işaretleme.
- **DUR:** FROZEN contract kırılması, veri kaybı riski veya merkezî/paylaşılan dosya düzenlemesi zorunlu olursa; kalan bağımsız işi bitirip kanıtla raporla.

## Acceptance

- **E2:** API ve handler testlerinde izin, Tenant/LegalEntity/kayıt kapsam kaybı, bağımsız inceleme, 52 hafta kapısı, tekrar istek, eski sürüm/yarış ve audit hatasında kısmi etki olmaması ölçülür. Gerçek Mongo testleri yalnız izinli geçici loopback replica set üzerinde; tam MOD-0188 test takımı ve API build raporlanır. Atlanan testler ayrıca belirtilir. Kritik koruma geçici bozulunca ilgili test kırmızıya döner, sonra kaynak geri alınır.
- **K13:** Ajanın sonucu CT tarafından sabit kaynak durumunda diff ve testlerle bağımsız denetlenmeden ACCEPTED sayılmaz.
- **E4:** Canlı Platform/MDM yetkisi ve kullanıcı arayüzü bağlandıktan sonra Ali'nin manuel kontrolü ayrı kapıdır. Bu WP tek başına AC-D10/D12/D13/D14/D27'yi kapatmaz.

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner

@[.antigravity/agents/orchestrator.md]

WP: WP-MOD0188-MANUAL-DRAFT-API. Repo: `C:\Users\epenc\Documents\Codex\2026-09-29\https-github-com-bestepullukcu-alt-erp\work\ERP-vNext-git`; branch: `feature/sce/mod-0188-demand-planning`. Başka checkout'a geçme. İlk adımda yolu, branch'i ve mevcut kirli Git durumunu doğrula; kullanıcı değişikliklerine dokunma.

Bu WP belgesinin tamamını; AGENTS.md, domain config, MOD-0188 Module Pack, `.antigravity/workflows/add-module.md`, ilgili security/data/API/test kurallarını ve `.antigravity/agents/backend-architect.md`, `security-agent.md`, `data-agent.md`, `integration-agent.md`, `testing-agent.md` rollerini oku. Gerçek alt ajan çalıştırılmadıysa çalıştırıldı deme.

NE / KORU / YAPMA / DUR / Acceptance bölümlerini uygula. Mevcut Mongo Draft çekirdeğini API'ye bağla; owned Demand v2 taslağını DRAFT olarak eşle. Yetkili Platform/MDM kaynağı yokken canlı kullanıma açılmış gibi davranma. Testleri gerçek geçici Mongo ile ve tam takım olarak koş; atlananları bildir. Kritik kural için kırmızı-yeşil duyarlılık kanıtı ver. Dosya/satır, komut, sonuç ve açık kapıları Türkçe raporla. CT'nin bağımsız K13 denetimini bekle.

Yeni worktree/branch yok. Yerel commit öncesi mevcut staged/unstaged dosya listesini ve commit'e girecek diff'i tek tek doğrula; yalnız MOD-0188 kapsamını commit et. Önceden staged olan merkezî/kullanıcı değişikliklerini ve başka modül dosyalarını commit'e katma, onların staging durumunu bozma. Güvenli kapsam ayrımı yapılamıyorsa commit yapmadan raporla. Push/PR yok. Başka modüle geçme.
