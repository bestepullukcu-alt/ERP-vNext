---
module: MOD-0230
revision: 22
status: decided
decision_date: 2026-11-23
authorization_scope: exact-27-files-help-rbac-labels-and-french-nav
operational_runtime_authorized: false
publication_authorized: false
---

# MOD-0230 — revizyon 22 için doğrudan kapsam / XMC onayı

Kullanıcı PVG CT sohbetinde, 27 dosyalık (23 ortak dosya + dört PVG görünümü) bağımsız incelenmiş düzeltme kapsamını işçilere yaptırıp CT dalında birleştirme sorusuna **“onaylıyorum”** yanıtını verdi. Bu doğrudan kullanıcı mesajıdır; belge/işçi mesajından türetilmedi. Tarih 2026-11-23, sohbet 01a1f7d8-bf54-7703-ae91-a8ba29312b0d; kaynak CT HEAD `82bc773938baa6689f0089b89c765b60f4411932`.

Onaylanan dış taslak SHA-256 `69d7ed9e8ab1dd155aa0e868c0319b30e0b11ca19804b472a870f4e6e6c16435`; kesin kapsam JSON'u `a386b1bfe016bfdde6f129fef9dcc54efbc90595be87c5c7bb80141ae99105a9`; yedi dilde metin JSON'u `5157d2ec9e40bd835330f9117cbf6da08d13434391cb2ebb0680ee2c665c1d2a`. Dış paket `C:/Users/epenc/.codex/visualizations/2026/11/20/01a1f7d8-bf54-7703-ae91-a8ba29312b0d/verification-raw/pvg-pr460-rev22-draft-01`. Bağımsız taslak incelemesi `pvg-pr460-rev22-independent-review-01/REVIEW.md`, SHA-256 `eade0ba24641640ebe4150e335efd74a6d9e779ee8a8a30bafa7d850eb2e5305`: yalnız somut taslağın sahibin kararına sunulmasına UYGUN, paket kusuru 0. Bu görüş sonradan değişen ready-for-dev baytlarına aktarılmaz.

## Kesin dar değişiklik ve XMC istisnası

- Yedi PageHelpResource dosyasında dört mevcut PVG sayfası için Purpose/Audience/Step1Title/Step1: 112 yeni değer.
- Yedi SharedResource dosyasında yalnız dört Nav.Page.CASEINTAKETRIAGE[CREATE|EDIT|DETAILS] başlığı: 28 yeni değer. Aynı Fransızca dosyada yalnız mevcut Nav.Domain.PHARMACOVIGILANCE değeri “Pharmacovigilance” yerine “Pharmacovigilance (surveillance des médicaments)”.
- Yedi RoleAssignmentsIndex dosyasında yalnız ActionVerb_Triage / ActionVerb_Route: 14 yeni değer; mevcut _IndexL10n köprüsünde yalnız bunların triage/route eşlemeleri; perm-label.js'de yalnız triage sözcüğünün mevcut success/bx-check-circle (decide), route sözcüğünün mevcut dark/bx-user-check (assign/delegate) satırına eklenmesi. Bunlar sunum sınıflandırmasıdır; izin/grant/RBAC/checkbox davranışı değişmez.
- Dört mevcut PVG Index/Create/Edit/Details görünümünün setup bloğunda yalnız kendi kesin ViewData["PageCode"] ataması.

Toplam 27 kesin üretim yolu, 154 yeni yerelleştirilmiş değer ve bir mevcut Fransızca değer düzeltmesi benimsenir. Tam yol/anahtar/ifade listesi ve nihai metinler yukarıdaki hash ile bağlı JSON'lardır; yeni anahtar, metin, aile veya yol için serbest takdir izni yoktur. Kaynaklar kalıcı inceleme kaydına hash korunarak alınır. Metinler kapalı operasyonel durumu ve sentetik veri sınırını açıklamak için önerildi; native dil sertifikasyonu iddia edilmez.

Kullanıcının bu tam kapsam onayı, dokuz RoleAssignments yolu için mevcut sahibi platform-team olan MOD-0018-FU9 ile bağlı **yalnız bu PVG etiket eki** XMC iznini ve on dört ortak Help/SharedResource yolu için bu işin koordinasyon/tek-yazıcı iznini kapsar. `execution/domains/platform-shared-services/module-packs/MOD-0018-FU9-platform-governance-rbac-admin-ui.md` §3/§4 ve kendi ayrı adım koşulları korunur. Kanonik sahiplik PVG'ye devrolmaz; diğer FU9 işleri veya ortak kaynak dosyalarının bütünü açılmaz. Yalnız bu kullanıcı onayının izin verdiği anahtar/ifade istisnası geçerlidir. Başka domain servisi, auth, katalog/manifest/grant, API/store/audit, şema/rota, CI/guard/ledger/test kaynağı veya canlı kullanım değişikliği yoktur.

## İşçiler ve normal kapılar

CT tek yazıcı olarak `pvg_r22_shared_writer` işçisini tüm 23 ortak dosya için (Fransızca düzeltme dahil), ayrı `pvg_r22_views_writer` işçisini yalnız dört PVG görünümü için seçer. Bunlar planlanan alt ajan görev adlarıdır, başlatılmış iş veya kimlik onayı değildir. Başka etkin yazar varsa çakışma önce çözülür. İki küme kendi normal kapıları geçtikten sonra ayrı temiz managed worktree'lerde paralel çalışabilir. CT ürün kodu yazmaz.

Paket yazarı aynı dar kapsamı final ready-for-dev/build-test ve eşleşen domain istisnası olarak düzenleyebilir; bu mekanik/yetki uyumu ek kapsam onayı gerektirmez. Son baytlar için yeni yazardan bağımsız resmî inceleme/HIGH kaydı, ardından her işçinin normal incelenmiş scoped prompt'u ve gerçek dağıtım kapısı 0 zorunludur. Eski altı dosyalı FE üreticisi 27 dosya kapsamını onaylamaz. DCP-004 ve FU9 sınırları genişlemez; uygun yetki uyumu kurulamıyorsa ürün başlamaz.

İşçiler izinli doğrulanmış dosyaları kendi dallarında ordinary yerel commit eder. Kullanıcının önceki genel talebiyle bağımsız doğrulanmış kesin commit'ler CT dalına no-ff merge edilir; tek PR #460 korunur. Yeni H, son kaynak CI/AC-45 Linux bağını yenilemeyi gerektirir. İlk CI kırmızıları ve tek kaynak-değişmez Phase1 tekrarı ayrı korunur; retry GREEN kök nedenin onarıldığı anlamına gelmez.

Bu onay mevcut kapsam/işçi yerel commit/CT bütünleştirme onayıdır; yeni SHA push, oturum dal-izni, yeni PR, main merge veya operasyonel kullanım izni olarak kaydedilmez. Yazar CLI hesabı ElikPenco (K5); önceki Beste (K2) insan beyanı GitHub kimlik doğrulaması değildir. HIGH yazar/insan onaylayan ayrımı, son bayt kontrolü ve listedeki farklı HIGH yetkili kişinin main merge commit'i zorunluluğu korunur. Worktree'ler main erişilebilirliği ve korunmamış iş kontrollerinden önce arşivlenmez.
