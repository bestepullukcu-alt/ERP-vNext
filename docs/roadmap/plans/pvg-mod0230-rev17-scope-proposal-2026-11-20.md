# MOD-0230 revizyon 17 — kapsam değişikliği önerisi

Durum: öneri; paket değişikliği, geliştirme veya dağıtım yetkisi değildir. Hazırlayan `module-pack-author` rolündeki ayrı Codex ajanı `/root/pvg_revision17_proposal`; ana oturum modeli devralındı, Claude çalıştırılmadı. CT kaynakları karşılaştırdı ve bu öneriyi kaydetti. Revizyon 16'nın onaylı metni değiştirilmez.

Mevcut MOD-0230 Case Intake & Triage, `pharmacovigilance`, `Diten.PvgService`, tenant shell, Compact referansı ve 25 form alanı korunur. Yeni modül, kimlik, sahiplik veya plan yeteneği üretilmez. Risk HIGH kalır.

## Üç veri koruma düzeltmesi

1. Güncelleme, kayıt ve sürümü aynı okumadan alır. İstekteki ExpectedVersion okunan sürümle eşleşmelidir; atomik değiştirme de aynı sürümü kullanır. Araya triyaj girdiğinde güncelleme çakışır; triyaj verisi ve yeni sürümü korunur. Mevcut depo sözleşmesi ve sürüm çakışması nedeni korunur.
2. Dokunulmamış metinler ve kanıt referansı dizisi, kenar boşlukları, virgül/satır sonu, sıra ve tekrarlar dahil kayıpsız taşınır. Kullanıcının isteğe bağlı alanı bilerek boşaltması yine alanı temizler. Sunucuda her boş alanı eski değerle dolduran kısmi güncelleme eklenmez. Create davranışı sessizce değiştirilmez.
3. Dokunulmamış ReceivedAtUtc ve AwarenessAtUtc, desteklenen saniye altı UTC tick hassasiyetiyle korunur. Saniye hassasiyetinde gösterim saklı değeri yuvarlamaz. Kullanıcı zamanı değiştirdiğinde mevcut UTC anlamı ve sunucu doğrulaması uygulanır.

## Önerilen en dar dosya sınırı

Mevcut DL-12 dosyalarına şu dar izinler eklenmesi önerilir:

- `PvgIntakeDraftApplicationService.cs`: yalnız UpdateDraftAsync akışındaki sürümlü okuma, istemci–okuma sürümü eşitliği, düzenlenebilirlik denetimi ve aynı sürümle değiştirme. Diğer komutların kuralları ve depo arayüzü değişmez.
- `form.js`: prefill yanında yalnız düzenleme gönderiminin dokunulmamış özgün değerleri kayıpsız taşıması. Liste, Details, triyaj ve yönlendirme betikleri değişmez.
- `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs`: yalnız düzenleme girdisi, güncelleme payload dönüşümü ve gerekli dar yardımcılar. İzin denetimi, kimlik başlıkları, route ve genel hata eşlemesi korunur.
- `frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs`: gerçek MVC model binder ve vekil dönüşümünü geçen koruma senaryoları. Mevcut iddialar zayıflatılmaz.

Domain-config, frontend koruma istisnasında controller ve C# test yolunu bugün içermez. Öneri seçilirse yalnız bu modüle ait yukarıdaki yollar için açık domain hizalaması da hazırlanır; paket domain sınırını kendiliğinden aşmaz.

MVC ile PVG test hostlarını aynı zincirde bağlamak için `frontend/Diten.Web.Tests/Diten.Web.Tests.csproj` değişikliği gerekirse, gereklilik önce ölçülür ve ayrıca açık kapsama alınır. Ölçülmeden bu ortak proje dosyası izinli değildir.

## Kabul ölçütü ekleri

Mevcut AC-40–44 ve yedi dil beklentisi korunarak aşağıdaki davranışlar ölçülür:

- `Update_requires_the_version_read_with_the_editability_snapshot`: okunan sürüm n iken n+1 gönderen istek hiçbir yazma yapamaz; n ile normal güncelleme pozitif kontroldür.
- `Concurrent_triage_between_update_read_and_replace_is_not_overwritten`: triyaj okuma sonrasında deterministik olarak tamamlanır; güncelleme çakışır ve triyaj alanları/sürümü korunur.
- `Mvc_update_preserves_untouched_stored_text_and_reference_sequence`: kenar boşluklu metin, virgüllü ve satır sonlu referans, tekrarlar ve sıra gerçek MVC dönüşümünden geçer; API'ye giden değerler özgündür.
- `Edit_round_trip_preserves_subsecond_received_and_awareness_instants`: iki farklı yedi basamaklı saniye altı UTC değeri form→gerçek MVC dönüşümü→PVG test hostu→depo zincirinden aynı tick değerleriyle döner.
- `Edit_round_trip_clears_only_the_optional_field_the_user_emptied`: yalnız isteğe bağlı bir alan temizlenir; diğer metin, referans ve zamanlar korunur.

Kopuk tarayıcı/API testleri, elle kurulmuş başarı cevaplarıyla gerçek zincirin yerine geçmez. Zincir ölçülemiyorsa madde açık kalır. Kritik koruma testlerinin her biri kendi derlenen sabotajıyla kırmızıya dönmelidir.

## Windows test hazırlığı

`services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs` dosyasının argüman kurulumunda `--nounixsocket` yalnız Unix ortamlarında eklenir. Bu, Windows MongoDB 7.0.43'te ölçülmüş başlangıç engelini giderme önerisidir. Rastgele loopback portu, 27017 yasağı, eksik ikilide hata ve replica-set koşulları korunur. Dosya mevcut DL-12 kapsamında değildir; ayrı hazırlık işi veya yeni revizyonda açık izin gerekir.

## Sahip politika kararı

F-4 orta düzey politika konusudur; kanıtlanmış dördüncü engel değildir. CT önerisi: kayda özgü düzenlenemezlik cevabı, mevcut alan politikası izin verdikten sonra gösterilsin; alan politikası reddettiğinde güvenli ret cevabı korunsun. Bu seçim revizyon 16 kural 2'nin sırasını değiştirir ve sahibin açık seçimi olmadan uygulanmaz.

## Sınır ve geri dönüş

Şema, veri göçü ve depo arayüzü değişmez. Gateway, başka modüller, 404/hata modeli, Audit DL-5, izin veren uyarlayıcılar ve canlı ekran kapsam dışıdır. MOD-0231/0232/0234'e dokunulmaz. Geri dönüş geliştirme commit'leri üzerinden planlanır; kaynak paket, onay ve inceleme kayıtları silinmez.

Sahip bu öneriyi seçerse yeni paket taslağı hazırlanır, bağımsız incelenir ve yeni özete bağlı onay alınır. E2 entegrasyon, E4 canlı kabulün yerine geçmez; KK-8 ve G-23 korunur.
