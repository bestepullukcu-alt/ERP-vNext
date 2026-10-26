# WP-MVP1-ESIGN-HELP — kanıt (2026-10-14, agent result, pending CT)

> Dal `fix/mvp1-esign-help`, taban `cd3680d7c`, worktree `C:\Users\Ali\erp-vnext-wt-esign-help`. Yalnız yerel; push / PR yok.
> Belge yeri: `docs-organization` beş sorusundan 2. soru (belirli bir tarihteki işi kaydediyor, değişmeyecek) → `records/audits/2026-10/`.
> SEC-ESIGN1 madde 6, CT kararı B.

## 1. Desen — yeni bileşen yok

Platformda zaten kullanılan satır içi bilgi notu deseni: `bx-info-circle` + kısa metin. E-imza modülünün kendi sayfalarında aynı işaretlemeyle duruyor:

- `frontend/Diten.Web/Views/ESignature/MySignature.cshtml:28–30` (taban) — `<div class="alert alert-info d-flex gap-2 align-items-start" role="note"><i class="bx bx-info-circle fs-5"></i><span>@Localizer["ESign.Mine.Statement"]</span></div>`;
- `Views/ESignature/Requests.cshtml:89–91` (`ESign.Requests.Info`) ve `Views/ESignature/Settings/SampleApprovals.cshtml:67–69` (`ESign.Approvals.Note`), aynı işaretleme;
- modül dışında aynı desen: `Views/CRM/ConsentPreferences/_EvaluatePanel.cshtml:8` (`alert alert-info` + `bx-info-circle`).

Yedi sayfaya bu işaretleme `id="esignPageHelp"` ile eklendi. JS ya da CSS yok; `ui-popover.js` bağlanmadı.

| sayfa | yer (sonra) | anahtar | sayfa yetkisi |
|---|---|---|---|
| İmzam | `Views/ESignature/MySignature.cshtml:28` | `ESign.Help.Mine` | `esign.my-samples.manage` |
| İmza İsteklerim | `Views/ESignature/Requests.cshtml:90` | `ESign.Help.Requests` | `esign.requests.read` |
| İmza Doğrulama | `Views/ESignature/Verification.cshtml:26` | `ESign.Help.Verify` | `esign.signatures.read` |
| Denetim Dışa Aktarımı | `Views/ESignature/AuditExport.cshtml:102` | `ESign.Help.Export` | `esign.signatures.export` |
| İmza Politikası | `Views/ESignature/Settings/Policy.cshtml:28` | `ESign.Help.Policy` | `esign.policy.read` |
| İmza Anlamları | `Views/ESignature/Settings/Meanings.cshtml:79` | `ESign.Help.Meanings` | `esign.meanings.read` |
| İmza Örneği Onayları | `Views/ESignature/Settings/SampleApprovals.cshtml:68` | `ESign.Help.Approvals` | `esign.samples.approve` |

## 2. Dil

- E-imza bir Platform modülü; zorunlu diller en ve tr.
- Ölçüm: `ESignatureWebTests.Seven_languages_carry_exactly_the_ESignature_keys_with_real_text` yedi `ESignature.*.resx` dosyasını tek anahtar setine sabitliyor ve İngilizce kopyayı reddediyor. Anahtarlar yalnız en/tr'ye eklenseydi bu mevcut koruma kırmızı olurdu.
- Bu yüzden fr, es, zh, ar ve ru'ya da gerçek çeviri eklendi.
- Anahtar kataloğu `Views/ESignature/ESignature.cs`'ye 7 satır eklendi; son satıra virgül konuldu.

## 3. Türkçe metinler (aynen, resx'ten)

**İmzam** (`ESign.Help.Mine`)

> İmzam, elektronik imzaya ekleyebileceğiniz imza örneğinizi sakladığınız ve verdiğiniz imzaları gördüğünüz sayfadır; esign.my-samples.manage yetkisi gerekir. Örnek kaydederken şifreniz yeniden sorulur. Şirket politikası onay istiyorsa yeni örnek, esign.samples.approve yetkisi olan başka bir kişi onaylayana kadar Beklemede kalır; kendi örneğinizi hiçbir zaman onaylayamazsınız (dört göz). Aynı anda tek bir etkin örneğiniz olur; bir imzada kullanılmış örnek geri çekilemez, yalnız devre dışı bırakılır.

**İmza İsteklerim** (`ESign.Help.Requests`)

> İmza İsteklerim, bir modülün sizden imzalamanızı beklediği işleri listeler; esign.requests.read yetkisi gerekir. Liste son tarihe göre sıralanır: en yakın son tarih üstte, aynı tarihte daha yeni istek önce gelir. İmzalarken istekte belirtilen imza anlamı kullanılır ve şifreniz sorulur; reddetmek en az on karakterlik bir gerekçe ister ve talep eden bilgilendirilir.

**İmza Doğrulama** (`ESign.Help.Verify`)

> İmza Doğrulama, bir kaydın üzerindeki imzaları ve bugünkü durumlarını gösterir: geçerli, kayıt değişti, bozulmuş ya da geçersiz kılındı; esign.signatures.read yetkisi gerekir. Doğrulama (esign.signatures.verify) imzalanan içeriği kaydın bugünkü hâliyle karşılaştırır; imza kaydının kendisi hiç değişmez. Bir imzayı geçersiz kılmak esign.signatures.invalidate yetkisi, şifreniz ve en az on karakterlik bir gerekçe ister ve ayrı bir kayıt olarak saklanır. Şirkete bağlı kayıtlar yalnız veri kapsamınızdaki şirketler için gösterilir; kendi imzalarınızı her zaman görürsünüz.

**Denetim Dışa Aktarımı** (`ESign.Help.Export`)

> Denetim Dışa Aktarımı, imza kayıtlarını filtreleyip denetim ve teftişler için PDF ya da CSV olarak indirmenizi sağlar; esign.signatures.export yetkisi gerekir. Her dışa aktarım bir numara alır; kimin, hangi filtreyle, kaç kayıt indirdiği ve dosyanın SHA-256 özeti değiştirilemez şekilde kaydedilir. Tarihler UTC olarak basılır ve şirkete bağlı kayıtlar veri kapsamınızla sınırlıdır.

**İmza Politikası** (`ESign.Help.Policy`)

> İmza Politikası, hangi işlemin elektronik imza istediğini, varsayılan imza anlamını ve görsel imza kuralını belirler; okumak için esign.policy.read, değiştirmek için esign.policy.manage yetkisi gerekir. Şifrenin her imzada mı yoksa geçerlilik süresi içinde bir kez mi sorulacağı ve yeni bir imza örneğinin kullanılmadan önce onay gerektirip gerektirmediği de buradan ayarlanır. Bir şirket, tenant politikasından farklı satırlara sahip olabilir ve bunları geri alabilir; her değişiklik gerekçe ister ve aşağıdaki geçmişe yazılır.

**İmza Anlamları** (`ESign.Help.Meanings`)

> İmza Anlamları, imzanın neyi beyan ettiğini tanımlar, örneğin Onaylıyorum ya da İnceledim; imza penceresi bu listeden seçtirir. Okumak için esign.meanings.read, yönetmek için esign.meanings.manage yetkisi gerekir. Bir anlamın kodu kalıcıdır ve hiç değişmez; görünen adı dillere göre girilir ve imza anında imzalayanın dilindeki metin imza kaydına yazılır. Artık kullanılmayan bir anlam silinmez, pasifleştirilir ve yeni imzalarda seçilemez.

**İmza Örneği Onayları** (`ESign.Help.Approvals`)

> İmza Örneği Onayları, şirket politikası onay istediğinde yeni imza örneklerinin incelendiği sayfadır; onaylamak için esign.samples.approve, reddetmek için esign.samples.reject yetkisi gerekir. Dört göz kuralı gereği kendi örneğiniz için karar düğmesi çizilmez. Ret gerekçe ister ve her karar aşağıdaki karar günlüğüne değiştirilemez şekilde yazılır. Onaylanan örnek, sahibinin tek etkin örneği olur.

Her metindeki kural kod ya da mevcut testle doğrulandı:
- **Şifre yeniden sorulur:** örnek kaydı `sample` amaçlı bilet ister.
- **Kendi örneğini onaylayamaz:** `A_sample_owner_gets_no_decision_button_on_their_own_sample`, `With_approval_required_…_its_owner_cannot_decide_it`.
- **Tek etkin örnek:** `A_new_active_sample_makes_the_previous_one_inactive`.
- **Kullanılmış örnek geri çekilemez:** `A_used_sample_is_never_withdrawn_only_deactivated…`.
- **Ret, geçersiz kılma ve geri alma için en az 10 karakter gerekçe:** `Reject_invalidate_and_revert_are_the_red_reasoned_confirm_of_at_least_ten_characters`.
- **İstek sırası:** `ESignatureRequestListOrderMongoTests` (SEC-ESIGN1).
- **Görünürlük:** VIS1 / FUB1 ve ESIGN 1.0.1 notu.
- **Dışa aktarım günlüğü:** `ESign.Export.Log.*` alanları ve ESIGN §4.25.
- **Anlam kodu kalıcı:** ESIGN §4.21, "the code is the path and never changes".
- **Ret gerekçesi ve karar günlüğü:** `Rejection_needs_a_reason_and_lands_in_the_decision_log`.

## 4. Testler

| | önce | sonra |
|---|---|---|
| Web (`Diten.Web.Tests`) | **2199 / 2199** | **2220 / 2220** (+21) |
| build | 0 hata, 24 uyarı | 0 hata, 24 uyarı |

İlk derlemede kendi test dosyamda bir analizör uyarısı vardı: `xUnit2009`, `Assert.True(StartsWith)`. `Assert.StartsWith` ile düzeltildi; yeni uyarı yok.

`ESignaturePageHelpWebTests` — 21 test:
- `The_page_help_text_is_real_in_en_and_tr` ×7: anahtar katalogda var; en ve tr metni boş değil, en az 120 karakter, en az iki cümle, yer tutucu sözcük yok, en ≠ tr.
- `The_rendered_page_draws_the_help_note_with_its_text` ×7: gerçek Razor render'ında `#esignPageHelp` notu `bx-info-circle` ile çiziliyor ve metni resx değeri (renderer kültürü en).
- `Without_the_page_key_the_help_note_is_not_drawn` ×7: yetkisiz kullanıcıya yalnız UAS-001 açıklaması çiziliyor, not çizilmiyor.

Taban kodunda 14 test kırmızıydı: 7 metin + 7 render. Yetkisiz durum testleri tabanda da yeşil.

## 5. Sabotaj

`ESignature.tr.resx`'ten `ESign.Help.Mine` satırı silindi:
- **2 kırmızı / 31.** Kırmızıya dönenler:
  - `ESignaturePageHelpWebTests.The_page_help_text_is_real_in_en_and_tr` (MySignature);
  - mevcut `ESignatureWebTests.Seven_languages_carry_exactly_the_ESignature_keys_with_real_text`.
- Özgün baytlar geri yazıldı (`git checkout` kullanılmadı) → **31 / 31 yeşil**, `git status --porcelain` boş.

## 6. Kapsam

`git diff --stat cd3680d7c..HEAD`:
- `frontend/Diten.Web/Views/ESignature/**`: 7 view ve `ESignature.cs`;
- `frontend/Diten.Web/Resources/Views/ESignature/*.resx` (7);
- `frontend/Diten.Web.Tests/ESignature/ESignaturePageHelpWebTests.cs` (yeni);
- bu belge ve MOD-0022 §19 notu.

ESignatureController, servis kodu, ortak layout ve resx dosyalarına dokunulmadı.

Sahibin filosuna istek gönderilmedi; filo durdurulmadı.
