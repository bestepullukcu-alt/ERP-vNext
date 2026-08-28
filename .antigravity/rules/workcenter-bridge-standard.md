# Görev Merkezi Köprüsü — Yasaklar (WC-D1)

> **BU DOSYA BİR TARİF DEĞİLDİR. YALNIZCA YASAK LİSTESİDİR.**
>
> Köprüden bugüne kadar **gerçek bir modül geçmedi.** Tek tüketicisi, deseni kanıtlamak için
> yazılmış ve `Temporary = true` ile işaretlenmiş bir referans satırıdır
> (`RemoteWorkItemProviderOptions.Temporary`). "Nasıl bağlanır" tarifini bugün yazarsak
> ölçtüğümüzü değil, **varsaydığımızı** yazmış oluruz — ve o varsayım bir hafta sonra kimseyi
> uyarmadan yalan olur.
>
> Aynı disiplinin örneği: `connect-module-to-workcenter.md`'nin ilk uyarısı — "bu dosyada
> bilerek rakam ve durum tespiti yoktur".
>
> **Tarif, köprüden geçen İLK GERÇEK MODÜLDEN SONRA yazılacaktır.** O modül yazılırken ölçülen
> her şey buraya değil, o tarife girer. Buraya yalnız yasak girer.

---

## 0. Neden yalnız yasak?

Bir yasak, bir tarifin aksine **eskimez**: neyin yapılmayacağını söyler, bugünkü durumu değil.
Ve aşağıdaki her yasak **var olan bir teste** işaret eder, çünkü bir cümle test edilemez.

⚠ **Testin adını buradan kopyalama.** Adlar `HttpWorkItemBridgeTests.cs` ve komşularından
alınmıştır; bir ad değişirse bu dosya değil, **testin kendisi** doğrudur. Şüphede kal ve testi aç.

**Test dosyaları:** `services/Diten.Platform/tests/Diten.Platform.Application.Tests/WorkAggregation/`

---

## 1. Yasaklar

### Y1 — Modül başına köprü sınıfı YAZILMAZ
İki sınıf vardır: `HttpWorkItemProvider` (okuma) ve `HttpWorkItemActionDispatcher` (yazma).
Yeni bir modül **yeni bir satırdır, yeni bir sınıf değil** — `WorkAggregation:RemoteProviders`
altında bir yapılandırma satırı.

| Koruyan test | Dosya |
|---|---|
| `Two_configuration_rows_bind_two_providers_and_two_dispatchers_from_one_class_each` | `HttpWorkItemBridgeTests` |
| `The_network_seam_has_exactly_one_implementation_and_it_names_no_module` | `HttpWorkItemBridgeTests` |
| `Only_the_expected_work_item_seams_are_implemented_in_the_platform_assemblies` | `WorkItemBridgeSingletonGuardTests` |

⚠ **GEREKÇE — bu bir üslup tercihi değil, GERİ ALINMIŞ bir tavsiyedir.**
"İlk ihtiyaç duyan deseni kurar" tavsiyesi **2026-08-26'da geri alındı.** Sebep: N ekip N köprü
sınıfı yazarsa N zaman aşımı, N hata sözlüğü, N kimlik taşıma yolu olur. Biri yavaşladığında
pano yavaşlar ve **hangisi olduğunu kimse söyleyemez** — çünkü teşhis edilecek tek bir yer
yoktur. Tek sınıf, tek teşhis noktasıdır.

### Y2 — Adres OPERATÖR YAPILANDIRMASINDAN gelir; manifest'ten ASLA
Adres `RemoteWorkItemProviderOptions.BaseUrl`'dedir; elle yazılır, `MdmService:BaseUrl` ve
depodaki diğer servisler arası adreslerle aynı şekilde. Eksik ya da bozuk adres **servisi
başlatmaz** — sessizce "kalıcı olarak erişilemeyen kaynak" olmaz.

| Koruyan test | Dosya |
|---|---|
| `A_row_with_no_address_stops_the_service_rather_than_becoming_a_permanently_dead_source` | `HttpWorkItemBridgeTests` |
| `No_manifest_type_declares_a_field_whose_name_could_carry_a_network_address` | `ModuleManifestCarriesNoAddressTests` |
| `The_manifest_property_set_is_pinned_so_a_field_the_name_check_would_miss_still_fails` | `ModuleManifestCarriesNoAddressTests` |

⚠ Gerekçe: manifest **çağrılan tarafın** gönderdiği veridir. İçine adres koymak, çağrılan tarafın
Platform'a "çağıranın JWT'sini şuraya yolla" demesi demektir — callee'nin yazdığı bir yönlendirme.
Depoda bunun örneği yok ve bu tur bir örnek yaratmıyor.

⚠ **AÇIK KAPANDI (2026-08-28) — ama nasıl kapandığını bilmen gerekiyor.**
Bu yasak 2026-08-28 sabahı yalnız bu dosyadaki bir cümleydi: `ModuleManifestDocument` içinde
hiçbir adres alanı yoktu, ama biri eklese **hiçbir şey kırılmazdı.** Artık iki test var ve
**ikisi farklı şeyi yakalıyor** — çünkü ölçtük:

- **Ad tabanlı kontrol** (`address·url·uri·host·endpoint·origin·callback·webhook·server·port·
  path·link·location·target·destination`) okunabilir olanıdır ve hatayı **adıyla** söyler.
  Ama **yalnız haberdar olduğu adı yakalar.** Ölçüldü: manifest'e `BaseUrl` eklendiğinde
  kırmızıya döndü; `Reachable` eklendiğinde **kaçırdı.** Alan bilerek eklendiğinde seçilecek
  ad tam olarak ikinci türdendir.
- Bu yüzden asıl tutan ikincisidir: manifest tiplerinin **özellik kümesi sabitlenmiştir.**
  Manifest'e eklenen **her** alan, adı ne olursa olsun kırar. `Reachable` bunu geçemedi.

⚠ Bunun bedeli: **meşru bir alan eklendiğinde de kırılır.** Bu kasıtlıdır. Manifest istemci
verisidir; oraya eklenen her alana bir insanın bakıp "bu bir adres değil" demesi bir satır
sürer, atlanması bir kimlik belirtecine mal olur. Alanı listeye ekle ve devam et.

### Y3 — İzinler satırın `Actions` haritasındadır; okuma ve yazma AYNI haritayı kullanır
Bir satır izinlerini **bir kez** beyan eder. Sağlayıcı bu anahtarları
`RequiredActionPermissions` olarak yayınlar, dağıtıcı aynı anahtarı aksiyonun
`RequiredPermission`'ı olarak adlandırır. İki liste elle eşit tutulmaz — **inşa gereği** eşittir.

| Koruyan test | Dosya |
|---|---|
| `A_row_declares_its_permissions_once_for_both_halves` | `HttpWorkItemBridgeTests` |
| `No_dispatcher_names_a_permission_its_provider_does_not_declare` | `WorkItemActionDispatchTests` |

⚠ Gerekçe: beyan edilmeyen ama sorgulanan bir anahtar, izni **gerçekten olan** bir kullanıcıya
sessizce "reddedildi" der. Varsayımsal değil — bir kez yaşandı (onboarding notu §3).

### Y4 — Bir modül BAŞKA modülün sağlayıcı kodunu iddia edemez
Bir öğe, satırın kodundan başka bir `source.providerCode` taşıyorsa **düşürülür**. Aynı kodun
iki satırda olması **servisi başlatmaz**.

| Koruyan test | Dosya |
|---|---|
| `An_item_claiming_another_modules_provider_code_is_dropped` | `HttpWorkItemBridgeTests` |
| `The_same_provider_code_twice_stops_the_service` | `HttpWorkItemBridgeTests` |

⚠ Gerekçe: yinelenen kod iki sağlayıcıyı tek ad altında bağlar — pano her iki kümeyi gösterir ve
her yazma, container'ın önce saydığı dağıtıcıya gider. Yani **hangi modülün yazdığı kura ile
belirlenir.**

### Y5 — İzin kararı SUNUCUNUNDUR; modül ne derse desin
Modül bir aksiyonu "etkin" diye yansıtsa bile, çağıranın izni yoksa **devre dışı** çizilir ve
sebebi yazılır. Satırın yapılandırmadığı bir aksiyon ne okuyucuya sunulur ne de iletilir.

| Koruyan test | Dosya |
|---|---|
| `An_action_the_caller_lacks_the_permission_for_is_disabled_whatever_the_module_said` | `HttpWorkItemBridgeTests` |
| `An_action_the_row_does_not_configure_is_never_forwarded` | `HttpWorkItemBridgeTests` |
| `An_action_the_row_does_not_configure_is_not_offered_to_the_reader` | `HttpWorkItemBridgeTests` |
| `A_caller_without_the_permission_is_refused_before_anything_is_dispatched` | `WorkItemActionDispatchTests` |

⚠ Gerekçe: modülün gönderdiği "etkin" bayrağı **istemci verisidir.** Ona uyulursa yetkilendirme
kararını çağrılan taraf verir.

### Y6 — Modül cevap vermezse yazma REDDEDİLİR
Yazma yolunda sessizlik "belki oldu" değildir. Cevap vermeyen ya da bütçesini aşan bir modüle
yapılan yazma **REFUSED** döner — okuma yolunun aksine (orada kaynak "erişilemedi" diye
raporlanır ve pano yine çizilir).

| Koruyan test | Dosya |
|---|---|
| `A_write_to_a_module_that_does_not_answer_is_REFUSED` | `HttpWorkItemBridgeTests` |
| `A_write_that_exceeds_its_budget_is_REFUSED_on_the_same_terms` | `HttpWorkItemBridgeTests` |
| `A_remote_modules_refusal_code_survives_the_bridge` | `HttpWorkItemBridgeTests` |

⚠ Gerekçe: okuma yarım çizilebilir, yazma çizilemez. Kullanıcı düğmeye bastı ve ne olduğunu
öğrenmek zorunda.

### Y7 — Sözleşme sürümü uymazsa RAPORLANIR, yansıtılmaz
Satırın `ContractVersion`'ı ile modülün cevabındaki sürüm ayrışırsa, öğeler **tahmin edilerek
çizilmez**; kaynak "erişilemedi" olarak raporlanır.

| Koruyan test | Dosya |
|---|---|
| `A_module_answering_a_different_contract_version_is_reported_rather_than_projected` | `HttpWorkItemBridgeTests` |
| `Reports_a_provider_with_an_unsupported_contract_version_instead_of_dropping_it_silently` | `GetMyWorkItemsHandlerTests` |

⚠ Gerekçe: sessizce atlanan bir sağlayıcı, panoda **boş bir sekme** olarak görünür ve "kimsenin
işi yok" diye okunur — "sistem konuşamıyor" diye değil.

---

## 🔗 İlgili
- `execution/portfolio/delivery-capability-packs/DCP-004-provider-onboarding-note.md` — sözleşme
- `.antigravity/workflows/connect-module-to-workcenter.md` — bağlamadan önce **ölçülecekler**
- `docs/product-backlog.md` — **günün** açık maddeleri (bu dosya tarihsizdir)
