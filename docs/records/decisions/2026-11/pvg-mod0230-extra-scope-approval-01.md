---
module: MOD-0230
work_packages: WP-0230-DL12,WP-0230-DL12-PREP-WIN,WP-0230-DL12-REGISTRY-01
status: decided
decision_date: 2026-11-20
authorization_scope: one-windows-acl-test-method-and-five-fixed-registry-trigger-cells
operational_runtime_authorized: false
publication_authorized: false
---

# MOD-0230 — iki dar ek kapsam için doğrudan insan onayı

Kullanıcı CT sohbetindeki (`01a1f7d8-bf54-7703-ae91-a8ba29312b0d`) iki maddelik ek kapsam sorusuna kendi mesajında **“onaylıyorum”** yanıtını verdi. Soru öğesi `call_868ab904429e48deadc8df1219dd6ebd` / 0. Bu onay bir belge veya işçi mesajından çıkarılmadı; doğrudan insan yanıtıdır. Kaynak CT commit'i `ce70f5ddf5de1c12d9908521e8f63acfd635d1b5`.

## 1. PREP-WIN — tek test metodu istisnası

Onaylanan öneri `docs/roadmap/plans/pvg-mod0230-windows-acl-scope-proposal-2026-11-20.md`, kanonik LF SHA-256 `89e373028ecf7acdbe9d12f0de09fa2d06d4839f617bca24fb4d651a491eb758`. Bağımsız son görüş `docs/records/reviews/MOD-0230/2026-11-20-windows-acl-scope-review-03.md`: UYGUN. Resmî inceleme ZIP'i kaynak commit `06dd930b0a1413f407120ed9f20b4dbac7d34cea`, SHA-256 `66a8efa5e75e6354276f30afefaf8e72b039aa28cd538330d6c692178d3b86a0`.

Mevcut iki izinli WIN test dosyasına ek olarak `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs` dosyasında yalnız `Configuration_scan_fails_closed_on_inaccessible_directory` metot gövdesi Windows ACL uyumuna açılır. Önerideki gerçek UnauthorizedAccessException önkoşulu/scanner kontrolü, DACL geri yükleme ve cleanup, altı ayrı derlenen RED/GREEN kanıtı, tam Windows API süiti ve son PR'ın test edilen kaynakla bağlı gerçek Unix CI kapanış şartları aynen benimsenir.

Eski kirli işçi ağacı ve donmuş iki WIP korunur. Checkpoint veya kırmızı testle commit istisnası yoktur. Paket yazarı mevcut MOD-0230 pack/domain'i bu kesin dar değişikliğe uyumlar. Yeni sürüm aynı insan onayı kapsamında ready-for-dev/build-test olarak kayda geçirilebilir; bu izin yeni iş kuralı, başka yol veya kabul gevşetmesi eklemez. Değişen son baytlar yeniden resmî bağımsız inceleme/HIGH kaydına bağlanır. Temiz replacement worker ağacı ve yenilenmiş normal prompt/dağıtım kapısı 0'dan sonra donmuş iki WIP patch'i LF içerik/hash eşitliğiyle taşınır; ardından tek yeni metot uygulanır. Önceki onaylı paket hash'i `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`, domain hash'i `914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c`; yeni hash'ler yerine kullanılamaz.

## 2. Ortak regresyon kaydı — XMC-001 istisnası

`WP-0230-DL12-REGISTRY-01` yalnız yönetişim/kayıt düzeltmesidir. Tek yazma yolu `tests/known-failures.tsv`. Kaynak ledger Git blob'u `6038f88ef70db0bedfbde183527f8022b9ca0c64`. Yalnız mevcut satır 4–8'deki beş `fixed` kaydın `rerun_trigger` hücresinden `,*.xml` öğesi kaldırılır. Başka hücre, kayıt, durum, fingerprint, guard veya XML taraması değişmez. Ana dalda da mevcut olan kayıt sapmasının kanıtı `docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/REGISTRY-BLOCKER.md`; bağımsız teknik görüş `REGISTRY-PROPOSAL-REVIEW.md` içindedir. XMC insan onayı bu doğrudan yanıtla karşılandı.

Ayrı işçi normal yönetişim prompt/inceleme/dağıtım kapısından sonra yalnız bu beş hücreyi düzeltir. `known_failures.py validate` ve korunmuş bağımsız normal Web TRX'ine gerçek `check --suite Diten.Web.Tests` 0 olmalı. Bellekte tek eski XML öğesi geri konduğunda üretim validator'ının mevcut tracked dosyalarla reddettiği ölçülür; guard kaynağı değişmez. Tam kapsam/diff bağımsız doğrulanmadan ve entegrasyon kapısı geçmeden Web işi VERIFIED/INTEGRATED sayılmaz. Bu kayıt dosyasını başka amaçla değiştirme izni verilmez.

## Yetki ve kapanış

CT yalnız paketleme, kayıt ve koordinasyon yapar; uygulama ayrı işçilere devredilir. Her işçi kendi dalında yalnız doğrulanmış izinli dosyaları yerel commit eder, push/PR yapmaz. Önceki kullanıcı talebiyle doğrulanmış işçi commit'leri CT dalına merge commit ile alınır; tek son PR hedefi korunur. Yeni onay push, PR açma, main merge veya operasyonel kullanım izni değildir.

Kullanıcının önceki Beste beyanı korunur; bu kayıt doğrulanmış GitHub hesap kimliği iddiası değildir. Yazar gerçek CLI hesabı ElikPenco; HIGH insan onaylayan ile yazarın farklı olması ve deferred bağımsız insan main merge kapısı korunur. Eski incelemeler kendi sürümlerine bağlı kalır. İşçi ağaçları main erişilebilirliği ve koruma kontrollerinden önce silinmez.
