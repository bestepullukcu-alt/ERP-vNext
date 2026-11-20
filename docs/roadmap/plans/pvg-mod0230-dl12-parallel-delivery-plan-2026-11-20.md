# MOD-0230 DL-12 — paralel teslim planı

2026-11-20. CT koordinasyon planıdır; module pack, insan onayı veya normal dağıtım kapısının yerine geçmez. Kesin yetki: rev21 pack SHA256 `ea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4`, domain `2411fa9500c7696b0751767be23661bc3db6c87468821ac43f7e9d56ba62ef8b`; doğrudan rev20 sahip onayı ve extra-scope-approval-01 insan kararı. Yeni MOD/FU veya kapsam açılmaz.

## Başlama kapısı

Ana veri koruma geliştirmesi ancak PREP-WIN ve PREP-WEB bağımsız E2 doğrulaması ve birleşik entegrasyon kontrolleri tamamlanınca başlar. Registry düzeltmesi CT'de entegredir. Web teslimi15791333 CT merge78804713 içindedir; taze birleşik test sonucu beklenir. Windows v2.0 promptu bağımsız UYGUN/S-54 kontrolü0 aldı; normal dağıtım kontrolü sürüyor. Bu plan yazıldığı anda ana DL12 BLOCKED, hiçbir ana feature işçisi başlatılmadı.

CT ürün kodu yazmaz. En çok iki alt çalışma paralel yürür. İlk iki iş arka uç/API ve yedi dilde tek sayfa anahtarıdır; bunların doğrulanmış commit'leri CT'ye alındıktan sonra ekran/MVC işi başlar. Ekranın gerçek connected GREEN kanıtı yeni API sözleşmesini gerektirdiği için bu bağımlılık korunur. Her işçinin kesin ExpectedHEAD'i kendi temiz ağacında dağıtımdan önce ölçülür; bu plan gelecek HEAD uydurmaz.

## Arka uç ve API

Yalnız şu yedi yol; kök `services/Diten.PvgService/`:

- `src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs`: yalnız UpdateDraftAsync ve gerekli update-local yardımcılar.
- Aynı dizinde `PvgApplicationReasonCodes.cs`: yalnız IntakeDraftNotEditable.
- `src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs`: yalnız item-response record ve From eşlemesi; request/list/route/HTTP mapping değişmez.
- `tests/Diten.PvgService.RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs`: yalnız Cross_tenant_mutations_do_not_leak_existence_through_evidence_link_denial testinin Update çağrı beklentileri.
- `tests/Diten.PvgService.Api.Tests/PvgCaseIntakeEditKeepsStoredDataTests.cs`: yeni kabul testleri ve dosya içi test yardımcıları/decorator/policy tracing.
- Aynı test projesinde `Persistence/PvgMongoStartRefusalTests.cs`: Unknown_store_value_refuses_start içinde yalnız `[InlineData(" Mongo")]`.
- Aynı test projesinde `PvgCaseIntakeTriageBindingTests.cs`: üç geçerli triage sonucu için yalnız `Assert.Equal(0, harness.Store.ReadCalls)`.

Update sırası onaylı sözleşmeye bağlanır: tenant/context/permission, request-shape, field policy, tek persisted snapshot, not-found/state/marker, reporter-link, evidence, client/snapshot version equality ve aynı snapshot sürümüyle ReplaceAsync. Field policy reddi editable/closed/missing/cross-tenant için aynı güvenli ret ve0read/0write verir. Reporter/evidence reddi sürüm eşitliğinden önce kalır. Ortak GetDraftAsync sürümü atar; diğer komutları etkileyerek değiştirilmez. Triage/Route/Get/List davranışı korunur.

Gerçek host/store kanıtı InMemory ve Mongo üzerinde üretilir. Mevcut host callback ve test assembly içindeki PvgMongoApiHost kullanılabilir; private BindingTests/ApiContractTests harness'ları ortak erişilebilir varsayılmaz. Mongo'nun internal constructor'ını açmak yerine özgün DI descriptor/factory korunup test dosyasındaki decorator ile wrap edilir; recursive GetRequiredService kullanılmaz. Yarış, update snapshot'ı yakalandıktan sonra gerçek HTTP triage'ın tamamlanmasını bounded bariyerle bekletir; fabricated conflict veya sleep kanıt değildir.

AC40 state/üç marker/policy/sürüm/race ve AC41 on beş üye/enum-adı/detail ile başarılı minimal liste; AC42(c) gerçek create/read/update/reread eşitliği; AC43/44 tek satır sınırları ölçülür. Liste pozitif200 ve beklenen kimliği içerir; item tam id/status/version sınırında ve tüm raw yanıtta hassas sentinel/değer yoktur. Refused/boş/409 liste pozitif kanıt sayılmaz. Mevcut evidence-denial, missing-version ve Triage/Route testleri değiştirilmeden yeşil kalmalıdır.

Yeni test dosyası toplamı artırır; WIN'in395'e sabit kabul üreticisi bu feature'a uygulanmaz. CT yeni kabul üreticisi ve değişiklik öncesi kırmızı tabanı normal prompt'tan önce dondurur. Yeni vaka adları/sayıları, iki gerçek store, test assembly/source/TRX bağı, tam API ve RegPvBase0fail/0skip, gerçek known-failures0 ve ayrı compiling scratch RED/GREEN kanıtları açıkça raporlanır. Dış sabotajlarda status/marker/policy sırası/snapshot/race/DTO/alan taşıması ile AC43 host TrimStart ve AC44 erken store read ayrı ölçülür; işçi üretim host'unu veya başka test dosyasını düzenlemez.

## Yedi dil

Yalnız `frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.{en,tr,fr,es,zh,ar,ru}.resx` sayfa kaynaklarında DraftNotEditable anahtarı. Yedi gerçek dosya CT tarafından rg ile bulundu; yeni dizin açılmaz. Yeni anahtarın mevcut olup olmadığı dağıtımdan önce ayrıca ölçülür. Yedi değer anlamlı çevrilir, anahtar/parity ve XML denetlenir. SharedResource skill'i yalnız ortak kaynakları kapsar; sayfa kaynaklarını denetlediği iddia edilmez. JS/bridge/ortak kaynak değişikliği bu işte yoktur.

## Ekran ve MVC

Arka uç ve çeviri commit'leri bağımsız doğrulanıp CT'ye entegre olduktan sonra başlar. Yalnız module pack'in altı yolu: form.js, mevcut MVC controller update input/ToPayload kısmı, mevcut WebTests, sayfa JS testi, _Form.cshtml içinde yalnız Edit SourceReference/PatientSubjectCode required/indicator düzeltmesi ve _IndexL10n.cshtml tek anahtar köprüsü. Create, auth/header/error/route, yeni dependency/csproj/CI ve diğer ekranlar değişmez.

Gerçek Razor→üretim JS→ordered FormData→gerçek MVC binding/ToPayload→gerçek API/store→reread zinciri kabul yoludur. Null/empty/whitespace, deliberate clear/edit, array null/empty/comma/CRLF/order/duplicates, tarih tick hassasiyeti ve bilinmeyen select kodları ölçülür. Untouched değerler normalize edilmez. SourceReference embedded CRLF fixture'ı gerçek API create/read'den gelmelidir. Complete-detail eksik/bozuk/tutarsız üyelerde Save kapalı ve zorlanmış submit'te0update gerekir. Dört token taşıyıcısı assertion'ı ve gerçek invalid-token400/0update korumaları kalır; token repair/canned200 kabul edilmez.

## Birleştirme ve kapanış

İşçiler yalnız doğrulanmış kendi commit'lerini bildirip durur; push/PR yok. CT bağımsız E2 ve birleşik kontrollerden sonra her teslimi no-ff merge ile kendi dalına alır. Uzakta tek CT dalı ve tek sonuç PR'ı hedeflenir; son push/PR için kesin dal/SHA sahibine sunulur. Main'e doğrudan commit/push ve force yok. İkinci insan main merge kapısı saklıdır.

Final kaynak H için gerçek service-tests Linux pvg-api ve Phase1 kanıtı gereklidir. Sentetik M ölçülürse H∈parents(M) ve tree(M)==tree(H), aynı job/run-attempt checkout/TRX bağı kanıtlanır; aksi halde mevcut dispatch ile gerçek C==H ölçülür. H değişimi önceki sonucu geçersiz kılar. Bu gelecek CI sonucu PREP/feature geliştirmesinin döngüsel önkoşulu yapılmaz. E2 teknik entegrasyondur; E4/live ve operasyon izni değildir. İşçi ağaçları main erişilebilirliği ve korunmamış iş bulunmadığı ölçülmeden silinmez.

Kaynak değerlendirmesi: pvg_revision17_proposal salt-okunur BE brief'i c6ee38135 üzerinde; istenen gpt-6.2-astra/high, gerçek model/effort doğrulanamaz. CT mevcut Update sırasını ayrıca okuyup dar kapsam ve bağımlılık ayrımını kabul etti. Bu plan runtime başarısı iddiası taşımaz.
