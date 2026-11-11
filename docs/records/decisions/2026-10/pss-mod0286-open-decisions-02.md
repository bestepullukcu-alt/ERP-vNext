---
record_id: PSS-MOD0286-OPEN-DECISIONS-02
status: öneri — sahip kararı bekliyor (hiçbir karar verilmedi)
prepared_at_local: 2026-10-23
prepared_by: PSS-UI Control Tower (öneri taslağı)
block: MOD-0286 P1 açık kararları D-3, D-4, D-5, D-6, D-7, D-8a, D-8b, D-8c (D-1 ve D-2 `pss-mod0286-open-decisions-01.md` ile kapandı)
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md rev 06 sha 7f6868d9 (sha256 7f6868d91a03d1390676c5c2711d5c7a17abbc5b0e4beccb3a09c9fd049ef09a, commit f6575de30)
measured_at: dal chore/ct/pss-ui-ct-01, HEAD 44238eab7, 2026-10-23
---

# MOD-0286 — açık kararlar 02 (öneri taslağı)

> Bu kayıt **öneridir**. Hiçbir kararı vermez, sahibin yerine karar yazmaz. Her bölüm soruyu, ölçülmüş bugünkü durumu,
> seçenekleri, SAP / Oracle karşılaştırmasını, CT önerisini ve sahibin vereceği tek kelimelik sözü verir. Geçerli cevap,
> sahip sözünü verdikten sonra açılacak ayrı bir karar kaydıdır (kayıt 01 biçimi); bu dosya o sözden sonra değiştirilmez.

**Onay bağlamı (her karar için geçerli).** Paketin onay satırı `execution/approvals/approvals.tsv:7` bu dalda var (commit
`688348ec1`, hedef sha `7f6868d9`, inceleme 06 UYGUN), `main`'de yok (`git grep -c 0286 main -- execution/approvals/approvals.tsv`
→ çıktı yok, exit 1). Paket gövdesini değiştiren her karar paketi rev 07 yapar: sha değişir, inceleme 06 ve onay satırı
bayatlar; **yeni bağımsız inceleme (07) ve yeni onay satırı** gerekir. Aşağıdaki önerilerin hiçbiri paketi değiştirmez;
kural dosyasını (`.antigravity/**`, sahip onayı), kaydı ya da DL-1 şartnamesini değiştirir.

**Satır kayması kuralı.** Paket kural dosyalarını satır numarasıyla atıflar (`frontend-js-standard.md` :30…:981,
`premium-modal-standard.md` :1, :11, :16, :27-52, `frontend-standards.md` :79…:213, `module-pack-standard.md` :88, :292, :608,
`views-organization.md` :39, :47, :53, `dynamic-localization-standard.md` :2, :5, :28, :30, :78, :181). Bir kural düzeltmesi
satır ekler ya da silerse bu atıflar kayar ve paket değişmeden yanlış olur. Önerilen bütün düzeltmeler **yerinde, satır sayısı
korunarak** yapılır; yeni satır gerekiyorsa atıflanan son satırın altına eklenir.

**Paket başlığı notu.** Paketin `:24` satırı hâlâ "Draft, revision 05" der; rev 06 yalnız Implementation Notes `:506`'da yazar.
Rev 06 "dilim tablosu dışında değişiklik yok" diye kaydedildiği için bu bir yazım farkıdır; düzeltmek paketi değiştirir (rev 07),
bu kayıt düzeltmeyi önermez, yalnız not eder.

## Özet

| Karar | Sahip | CT önerisi | Paketi değiştirir mi | Ertelemenin riski |
|---|---|---|---|---|
| D-3 BL-655 MOD-0046 eşlemesi | Enterprise Architect / sahip (XMC-001) | ertele; D-4 tetiklenince yeniden sor | hayır | 🟢 yalnız raporlarda iki sahiplik |
| D-4 P2 tetikleyicisi | sahip | ilk tüketici `approved` ya da sahip kararı; tüketiciler Blueprint adıyla anılır | hayır (adlandırma kayıtta) | 🟢 çalışma zamanı etkisi yok |
| D-5 MOD-0013 etiketi | sahip (`.antigravity/**`, AGENTS.md) | genişletilmiş yeniden etiket (6 satır + 2 ek isabet) | hayır | 🟡 MOD-0013 paketi açılırsa yanlış atıf |
| D-6 MOD-0297 kimliği | Enterprise Architect (XMC-001) | ayrışma olarak kaydet, MOD-0297'yi yalnız Blueprint adıyla an | hayır | 🟡 tüketici bağı adsız kalır |
| D-7 mobil tasarım kiti | sahip | ayrı; P2 revizyonunda yeniden sor | hayır | 🟡 iki görsel dil uzun vadede ayrışır |
| D-8a premium modal inline stil | kural sahibi (sahip, `.antigravity/**`) | FG-003; yalnız kural metni (sınıflar zaten var) | hayır | 🟡 kopyalar sürer (4 dosya) |
| D-8b createCrudTable tercih satırı | kural sahibi (sahip); CT BL-440 sahibi olarak taraf | `createList` + yasak pratik satırı | hayır | 🟡 yeni sayfa eski yolu seçer |
| D-8c layout opsiyonel satırı | kural sahibi (sahip) | açık layout; `:208` düzeltilir | hayır | 🟡 platform dışına genellenirse 🔴 |

---

### D-3 — BL-655 MOD-0046 eşlemesi

**Soru.** MOD-0046 "Tenant Core UI"nin eski anlamı depodaki gibi `MOD-0009-FU03`'e mi bağlı kalır, yoksa Blueprint'in
"Platform Administration workspace + MOD-0286" eşlemesine mi taşınır?

**Karar sahibi.** Enterprise Architect / sahip (paket `:52` "Who"). XMC-001: `MOD-0009-FU03` ve kimlik kaydı bu modülün işi
değildir; bu blok karar veremez, yalnız MOD-0286 açısından etkiyi yazar.

**Bugünkü durum (ölçüldü).**
- `execution/registries/module-id-registry.md:145` MOD-0046 = Deprecated Alias → `MOD-0009-FU03`, sonunda "DIVERGENCE … the Master
  retires the legacy meaning to the Platform Administration workspace + MOD-0286 page governance; this repo's MOD-0009-FU follow-up
  mapping is kept until an Enterprise Architect / owner decision".
- `module-id-registry.md:67` `MOD-0009-FU03` Tenant Core UI, `in-progress`, "Active pack exists".
- `docs/roadmap/backlog/product-backlog.md:10841` BL-655; `:10845` "DURUM: ERTELENDİ … KARAR: sahip, 2026-10-19"; `:10853-10855` Master 8.8
  ADR-8.8-001 açık, A seçeneği öneriliyor (Legacy_ID_Migration_Map son hedef, FU'lar geçici takma ad, önce üç FU'nun kapsam ölçümü);
  `:10856` "Gelecek regresyon riski: 🟢".
- Paket atıfları (`:145`, `:67`, `:10841`) tutuyor.

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — ertele (BL-655 ERTELENDİ kalır) | P1 iki eşlemede de yürür; hiçbir dosya değişmez | 🟢 | Kimlik aracı iki eşlemeyi de kabul ediyor (`:10856`). Ertelemenin bozabileceği: P2 konsolu "Platform Administration workspace" ile örtüşürse aynı sayfa iki sahipte görünür; P2 revizyonuna kadar zararsız |
| B — Master A seçeneği | Önce üç FU'nun kapsam ölçümü (salt okunur), sonra kayıt satırları ve MOD-0009-FU03 paketi yeniden bağlanır | 🟡 | Yürüyen bir FU'nun paketi, bağlantıları ve kabul kayıtları değişir (`:10850-10851`); başka modülün işi |
| C — depo eşlemesi kalıcı | Blueprint sahibine geri bildirim; kayıt satırındaki DIVERGENCE notu kalıcı açıklamaya döner | 🟡 | Master ADR'nin önerisine ters; Blueprint güncellenmezse ayrışma her sürümde yeniden raporlanır |

**SAP ve Oracle.** Büyük ölçüde uygulanmaz (bu bir kimlik eşleme sorusu, tasarım sorusu değil). Genel bilgi, doğrulanmadı:
SAP Fiori apps reference library'de uygulama kimliği sabit kalır, emekli uygulama bir "successor" uygulamaya işaret eder; yani
eski kimlik yeniden kullanılmaz, eşleme ayrı tutulur. Bu, A/B'nin "önce eşleme, sonra birleştirme" sırasıyla uyumludur.

**CT önerisi.** A (ertele). Paketle aynı ("Not decided by this pack. P1 proceeds under either mapping."). Yeniden sorma tetikleyicisi:
D-4 P2'yi açtığında, çünkü ancak o zaman MOD-0286'nın bir sayfası "Platform Administration workspace" ile çakışabilir.

**Uygulama yolu.** A: dosya değişmez; karar kaydı yeterli. B ya da C seçilirse iş Enterprise Architect'indir: `module-id-registry.md`
(paylaşılan dosya, ayrı commit), BL-655 satırı, MOD-0009-FU03 paketi — bu bloğun yazma kapsamı dışında. Paket değişmez; yeni inceleme
gerekmez. `.antigravity/**` değişmez.

**Sahibin vereceği söz.** `d3-ertele` · `d3-master-a` · `d3-depo`

---

### D-4 — P2 tetikleyicisi

**Soru.** P2 (DL-3: yönetişim konsolu, kayıt sayfaları, API) ne zaman açılır?

**Karar sahibi.** Sahip (paket `:53`).

**Bugünkü durum (ölçüldü).**
- Paket `:53` önerisi: ilk gerçek tüketici (MOD-0295 ya da MOD-0297) `approved` olunca ya da sahibin açık kararıyla.
- Paket `:333` tüketiciler: MOD-0295 Operational Agent Workspace (W-3, NOT STARTED, kayıt satırı yok) ve MOD-0297 HR Capability Block Shell
  (W-2, NOT STARTED, D-6 çakışması). `grep -c MOD-0295 module-id-registry.md` = 0.
- `verify_module_id.py . --check-id MOD-0295 --name "Operational Agent Workspace"` → `OK … proven against Blueprint/registry`;
  `--check-id MOD-0297 --name "HR Capability Block Shell"` → `OK`. İki tüketicinin de paketi yok (`execution/domains/*/module-packs/`
  altında `MOD-0295` / `MOD-0297` dosyası yok).
- Paket `:541` FU-7 (P2 revizyonu D-4'ten sonra), `:555` DL-3 "deferred, not assignable to a WP".
- **Paket içi gerilim:** D-4 MOD-0297'yi tetikleyici sayar, D-6 (`:55`) "do not cite MOD-0297 as a consumer until resolved" der.
- Blueprint `Dependencies_Normalized` (HARD bağımlılık, NOT STARTED) **yeniden ölçülmedi**: ortamda `openpyxl` yok; paketin ölçümü aktarılır.

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — ilk tüketici `approved` ya da sahip kararı (paket) | P2 bugün açılmaz; tüketici paketi onaylanınca FU-7 başlar | 🟢 | Çalışma zamanı etkisi yok. Ertelemenin bozabileceği: tüketici paketi yazılırken sayfa kalıbı kaydı olmadığı için kalıp yine markdown + DL-2 listesiyle taşınır; DL-2 bunu ölçer |
| B — sabit tarih ya da dalga (ör. W-2 başı) | Konsol tüketiciden önce kurulur | 🟡 | Tüketicisiz konsol "tema kararı sayfa sayfa sızar" sorununu yeni biçimde tekrarlar (paket `:53` gerekçesi); bakım maliyeti erken başlar |
| C — yalnız sahibin açık kararı | Otomatik tetikleyici yok | 🟢 | Güvenli ama tüketici onaylandığında P2'yi kimsenin hatırlamama riski var (🟡 unutulma) |

**SAP ve Oracle.** Genel bilgi, ayrıntıları doğrulanmadı. SAP'de sayfa kalıpları (floorplan: List Report, Object Page, Worklist)
Fiori design guidelines'ta belge olarak, SAP Fiori elements'te kod şablonu olarak teslim edilir; ayrı bir "kalıp yönetişim
konsolu" müşteriye sunulan birincil araç değildir. Oracle Redwood de kalıpları tasarım belgeleri ve Oracle JET / Visual Builder
şablonları olarak verir. İkisinde de önce belge + şablon, sonra (varsa) yönetim yüzeyi gelir; bu A'yı destekler.

**CT önerisi.** A, bir ifade düzeltmesiyle: tetikleyici tüketiciler kimlik + Blueprint adıyla anılır ("MOD-0295 Operational Agent
Workspace", "MOD-0297 HR Capability Block Shell"); böylece D-6 çözülmeden de tetikleyici belirsiz olmaz (`verify_module_id.py`
bu adla `OK` veriyor). Bu ifade pakete değil karar kaydına yazılır; paketten ayrılma yalnız ifadededir.

**Uygulama yolu.** Karar kaydı (kayıt 01 biçimi). Paket değişmez; yeni inceleme gerekmez. FU-7 başlarken P2 revizyonu paketi
zaten rev 07+ yapar ve o revizyon kendi incelemesini ve onay satırını ister.

**Sahibin vereceği söz.** `d4-tuketici` · `d4-tarih` · `d4-sahip`

---

### D-5 — MOD-0013 etiketi

**Soru.** Kural dosyalarındaki "MOD-0013" etiketi MOD-0286'ya çevrilir mi?

**Karar sahibi.** Sahip (paket `:54`; `.antigravity/**` korunan yol, `AGENTS.md:99`; AGENTS.md proje sözleşmesidir).

**Bugünkü durum (ölçüldü).**
- Paketin saydığı satırlar tutuyor: `.antigravity/rules/premium-modal-standard.md:1`, `:11`; `.antigravity/rules/dynamic-localization-standard.md:2`,
  `:5`, `:181`; `AGENTS.md:139`.
- **Paketin saymadığı iki isabet:** `.antigravity/ARCHITECTURE.md:234` ("frontend-standards.md … (MOD-0013/22/23/24)") ve
  `.antigravity/workflows/debug.md:59` ("Standards Violation: [e.g., MOD-0013, WORKFLOW-001]"). `grep -rn MOD-0013 .antigravity AGENTS.md`
  toplam 8 isabet, 5 dosya.
- Kayıt: `module-id-registry.md:70` MOD-0013 = `retired-from-module-namespace`, Blueprint MOD-0013 = Platform Standards Registry, "the repo
  UI-standard concept aligns to Blueprint MOD-0286"; `:164` MOD-0286 "Owns the repo confirm / toast UI standard formerly labelled MOD-0013".
- Okuyucu: `.antigravity/scripts` ve `scripts` altında 0 isabet; testlerde 5 dosyada yalnız yorum, tırnak içinde 0. Uygulama kodunda 21 dosya;
  yorum dışı olanlar konsol önekleri (`Views/Shared/_GlobalNotification.cshtml:142, :234, :261, :264, :266`, `_GlobalConfirmation.cshtml:475`
  "[MOD-0013 showToast]" / "[MOD-0013 showConfirm]") — kullanıcıya görünmez, hiçbir kod bu dizgeyi okumaz.
- `AGENTS.md:139` Çekirdek bloğunun (`:21-39`) dışında; `sync_adapters.py` özeti yalnız Çekirdek bloğundan alır (`sync_adapters.py:1040-1050`),
  bu yüzden `.claude/rules/agents-core.md` özeti değişmez. Diğer bağdaştırıcılar ölçülmedi.

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — paketteki 6 satır MOD-0286 olur | Kural ve AGENTS.md etiketleri düzelir | 🟢 | Yalnız etiket; ancak `ARCHITECTURE.md:234` ve `debug.md:59` eski etiketle kalır, düzeltme yarım görünür |
| B — 6 satır + 2 ek isabet | `.antigravity` ve AGENTS.md içinde MOD-0013 kalmaz | 🟢 | Aynı iş, tam kapsam; uygulama kodundaki 21 dosya ve test yorumları kapsam dışı (başka modüller, XMC-001) |
| C — etiketi sil, modül kimliği yazma | Kural başlıkları kimliksiz "standart" olur; eşleme yalnız kayıtta (`:70`, `:164`) | 🟡 | `dynamic-localization-standard.md` bir dil kuralıdır; onu MOD-0286'ya etiketlemek dil kuralını UI tasarım modülüne bağlar. Silmek bunu önler ama okuyucu kuralın sahibini kayıttan bulmak zorunda kalır |
| D — ertele | Hiçbir şey değişmez | 🟡 | Ertelemenin bozabileceği: Blueprint MOD-0013 Platform Standards Registry için paket açıldığında ajanlar premium modal kuralını yanlış modüle atıflar; DCP-002 kimlik denetimi etiket metnini okumaz, yakalamaz |

**SAP ve Oracle.** Genel bilgi, doğrulanmadı: SAP Fiori design guidelines ve Oracle Redwood kılavuzları ürün modülü numarası taşımaz;
tasarım standardı bir ürün modülü gibi numaralanmaz, sürümüyle anılır. Bu C'ye yakın bir model; ancak bu depoda kurallar MOD
kimliğiyle izleniyor (kayıt `:70`), bu yüzden doğrudan aktarılmaz.

**CT önerisi.** B. Paketten ayrılma: kapsam iki ek isabetle genişler (paket "three rule locations" der, FU-2 `:536`; ölçüm beş dosya
gösteriyor). `dynamic-localization-standard.md` için C'nin gerekçesi geçerli bir itirazdır; sahip dil kuralını ayırmak isterse
B'yi o dosya hariç uygulayıp o dosyada C'yi seçebilir — bu ikinci bir söz gerektirir.

**Uygulama yolu.** Tek ayrı commit, sahip onayıyla: `.antigravity/rules/premium-modal-standard.md`, `.antigravity/rules/dynamic-localization-standard.md`,
`.antigravity/ARCHITECTURE.md`, `.antigravity/workflows/debug.md`, `AGENTS.md`; her değişiklik yerinde, satır sayısı korunur. Ardından
`python3 .antigravity/scripts/sync_adapters.py` (AGENTS.md değiştiği için; Çekirdek dışı olduğu için çıktının değişmemesi beklenir,
ölçülmedi). Şartname §9 satır 4'ün durumu ayrı bir DL-1 commit'inde güncellenir. Paket değişmez; yeni inceleme gerekmez.
Konsol önekleri (`_GlobalNotification.cshtml`, `_GlobalConfirmation.cshtml`) `frontend/` altındadır, bu karar onlara dokunmaz.

**Sahibin vereceği söz.** `d5-etiket` · `d5-etiket-genis` · `d5-sil` · `d5-ertele`

---

### D-6 — MOD-0297 kimliği

**Soru.** Blueprint MOD-0297 = "HR Capability Block Shell" ile depo kayıt satırı MOD-0297 = "Tenant Subscription Management" arasındaki
ayrışma nasıl ele alınır, ve MOD-0295'in kayıt satırı yokken MOD-0286 tüketicilerini nasıl anar?

**Karar sahibi.** Enterprise Architect (paket `:55`). XMC-001: kimlik kaydı ve `docs/reference/modules/platform/README.md` bu modülün
değildir; bu blok karar veremez.

**Bugünkü durum (ölçüldü).**
- `module-id-registry.md:189` MOD-0297 "Tenant Subscription Management", Deprecated Alias → MOD-0361; aynı satırın sonu "canonical MOD-0297
  stays HR Capability Block Shell" der ve "RUNTIME COMPATIBILITY LITERAL: Hangfire `Diten.Platform.MOD-0297.*` job IDs remain unchanged in code".
  Yani ayrışma kayıtta **yazılı**; çakışma satırın ad sütunu ile kendi notu arasındadır. Paket `:55` bunu "collision" diye özetler; ölçüm
  daha dar bir durum gösteriyor.
- `verify_module_id.py . --check-id MOD-0297 --name "HR Capability Block Shell"` → `OK`; `--name "Tenant Subscription Management"` →
  `BLOCKED … canonical-name mismatch … Gate failed closed`. Kimlik aracı Blueprint adını kanonik sayıyor.
- `docs/reference/modules/platform/README.md:14` "MOD-0297 Tenant Subscription Lifecycle" (eski anlam).
- MOD-0295: kayıtta satır yok (`grep -c` = 0); `verify_module_id.py --check-id MOD-0295 --name "Operational Agent Workspace"` → `OK`.
- `module-id-registry.md:183` MOD-0361 eski MOD-0297 / MOD-0298 anlamlarını taşıyor.

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — ayrışma olarak kaydet (paket) | MOD-0286 MOD-0297'yi yalnız Blueprint adıyla anar; dosya değişmez | 🟢 | Kayıt ayrışmayı zaten not ediyor; kimlik aracı doğru adı kabul ediyor. Ertelemenin bozabileceği: README `:14` okuyan biri MOD-0297'yi abonelik modülü sanar |
| B — Enterprise Architect FU-5 (`:539`) işini şimdi yapar | MOD-0295 satırı eklenir; MOD-0297 satırı Blueprint anlamına, eski anlam takma ad sütununa alınır; README `:14` düzelir | 🟡 | Paylaşılan kayıt dosyası; Hangfire `MOD-0297` iş kimlikleri kodda kalmalıdır (çalışma zamanı uyumluluğu), yeniden adlandırma yapılmaz |
| C — HR alanı MOD-0297 paketini açana kadar ertele | A gibi, tetikleyicisi HR | 🟡 | Tüketici bağı o güne kadar adsız kalır; D-4 ifadesi (Blueprint adıyla anma) bunu telafi eder |

**SAP ve Oracle.** Uygulanmaz (depo içi kimlik kaydı sorusu). Genel ilke, doğrulanmadı: iki üretici de emekli bir nesne kimliğini yeni
anlamla yeniden kullanmaktan kaçınır; burada Blueprint kimliği yeniden kullanmış, depo eski anlamı takma ad olarak tutuyor.

**CT önerisi.** A. Paketle aynı; ek olarak D-4'teki "kimlik + Blueprint adı" ifadesi bu kararın tüketici tarafını kapatır. B'nin zamanını
Enterprise Architect seçer (FU-5).

**Uygulama yolu.** A: karar kaydı; dosya değişmez. B: Enterprise Architect'in ayrı commit'i (`module-id-registry.md`, `docs/reference/modules/platform/README.md`);
bu bloğun yazma kapsamı dışında. Paket değişmez; yeni inceleme gerekmez.

**Sahibin vereceği söz.** `d6-ayrisma` · `d6-ea-duzelt` · `d6-ertele`

---

### D-7 — Mobil (Android) tasarım kiti

**Soru.** Android tasarım kiti (`:core:design`) MOD-0286'nın tüketicisi mi, ayrı bir tasarım sistemi mi?

**Karar sahibi.** Sahip (paket `:56`).

**Bugünkü durum (ölçüldü).**
- `0456e913e` (2026-09-11) "feat(mobile): M0.3 :core:database … + M0.5 :core:design"; `4dbd105f9` (2026-09-11) "feat(mobile): M1 rollout Wave 1 —
  4 HR feature modules". `git branch -a --contains 0456e913e` → yalnız `remotes/origin/hr-future`.
- Bu ağaçta `mobile/` klasörü yok.
- Şartname Ek A `A-9`: belge yalnız Razor / Bootstrap / `backbone-custom.css` yüzeyini ölçer.

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — ayrı; P2'de yeniden sor (paket) | P1 şartnamesi yalnız web yüzeyini kapsar | 🟢 | P1'in ölçemediği yüzeyi iddia etmez. Ertelemenin bozabileceği: renk, ikon ve terim iki kitte ayrı evrilir; P2'de birleştirme maliyeti büyür (🟡 uzun vade) |
| B — şimdi tüketici | P1 şartnamesi Android'i de bağlar | 🔴 | Razor / Bootstrap kuralları Android'e uygulanmaz; DL-2 Android'i ölçemez; şartname ölçemediği bir yüzey için kanıtsız iddia taşır (paket `:56` gerekçesi) |
| C — ortak belirteç katmanı, ayrı bileşen kitleri | Renk / tipografi / aralık belirteçleri tek kaynak; bileşen kuralları platform başına | 🟡 | Doğru uzun vadeli hedef olabilir ama belirteç adlandırma kararı (şartname A-7, `--diten-*` yalnız 8 satır) henüz yok; P1'e sığmaz |

**SAP ve Oracle.** Genel bilgi, ayrıntılar doğrulanmadı. SAP Fiori ortak tasarım dilini ve ilkeleri paylaşır ama platform başına ayrı
kılavuz ve kit verir (Fiori for Web / UI5, Fiori for iOS, Fiori for Android SDK'ları). Oracle Redwood web (JET, Visual Builder) ve
mobil yüzeylerde ortak görsel dili hedefler; mobil kitin nasıl paketlendiği doğrulanmadı. İkisi de C'ye benzer: ortak dil + ayrı kit.

**CT önerisi.** A. Paketle aynı. P2 revizyonunda sorulacak soru C olarak yazılmalı ("ortak belirteç, ayrı kit"), çünkü SAP modeli bu
yöne işaret ediyor ve A-7 belirteç kararıyla birlikte verilebilir.

**Uygulama yolu.** Karar kaydı; dosya değişmez. Paket değişmez; yeni inceleme gerekmez. C ileride seçilirse P2 revizyonu (paket rev 07+)
ve `hr-future` dalının sahibiyle ayrı iş gerekir (XMC-001).

**Sahibin vereceği söz.** `d7-ayri` · `d7-tuketici` · `d7-ortak-belirtec`

---

### D-8a — Premium modal ikon HTML'inde inline stil (FG-003)

**Soru.** `premium-modal-standard.md` §2'deki inline `style=` ikon şablonu FG-003'e (satır içi stil yok) göre düzeltilir mi?

**Karar sahibi.** Kural sahibi (sahip; `.antigravity/**`) (paket `:57`).

**Bugünkü durum (ölçüldü).**
- `.antigravity/rules/premium-modal-standard.md` 93 satır; `grep -c 'style='` = **8**: `:27`, `:28`, `:35`, `:36`, `:43`, `:44`, `:51`, `:52`.
  Tek satırlar (`:28` …) `<i>` üzerinde `font-size: 2.5rem; color: #…`; çift satırlar (`:27` …) `swal-icon-circle` `<div>` üzerinde
  yerleşim + sabit renkli `background` / `border`. Paket "four prescribed lines" der (UIC-13 `:159`); ölçüm dört **şablon**, sekiz **satır**.
- FG-003'ün tanım satırı `.antigravity/rules` altında **bulunamadı**; kod yalnız uygulamalarda geçer: `frontend-js-standard.md:145` (JS ile
  `element.style.zIndex` yasağı), `frontend-standards.md:194` (KPI kart stili `backbone-custom.css`'te), `views-organization.md:91`,
  `frontend-datatable-template.md:147`. Genel inline CSS yasağı `frontend-standards.md:66` (UI-001: "Inline CSS yerine `backbone-custom.css`
  içindeki paylaşımlı sınıflar"); sabit renk yasağı `frontend-standards.md:58`. Paketin `:145` ve `:194` atıfları satır olarak tutuyor ama
  genel kural `:66`'dadır.
- **Canlı kod zaten sınıflı:** `Views/Shared/_GlobalConfirmation.cshtml` `grep -c 'style='` = 0; `:156-167` `swal-icon-circle bg-label-*`
  sınıflarını kurar; `wwwroot/assets/css/backbone-custom.css:6277-6291` `.swal-icon-circle` ve `.swal-icon-circle i { font-size: 2.5rem; }`
  ("Colour comes from the per-type `text-*` utility"); `:213` koyu tema kuralı. Paketin "markup moves to classes in backbone-custom.css"
  önerisindeki CSS işi yapılmış; bayat olan yalnız kuraldaki şablon.
- Şablon kopyaları: `font-size: 2.5rem` geçen dosyalar `wwwroot/assets/js/Account/login.js`, `forgot-password.js`, `reset-password.js`,
  `Views/CRM/Accounts/_Form.cshtml` (ve `tests/global-confirm-styles.test.js`). Bunların şablondan kopya olup olmadığı satır satır
  **ölçülmedi**; başka modüllerin dosyalarıdır (XMC-001).

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — FG-003 uygulanır (paket) | §2 şablonu `_GlobalConfirmation.cshtml`'in sınıflı biçimine çevrilir, satır sayısı korunur | 🟢 | CSS zaten var; yalnız kural metni değişir. UIC-13'ün "D-8a'ya kadar" hoşgörüsü kendiliğinden düşer |
| B — şablon FG-003 istisnası olarak yazılır | İnline stil kuralda meşrulaşır | 🔴 | Sabit renkler koyu temayı (`backbone-custom.css:213`) ve `frontend-standards.md:58` sabit renk yasağını atlar; yeni sayfalar kopyalar |
| C — ertele | Kural bayat kalır | 🟡 | Ertelemenin bozabileceği: ajanlar "GOLDEN RULE" başlıklı şablonu kopyalamaya devam eder (bugün en az 4 dosyada aynı değer); UIC-13 hoşgörü listesi kayıtta kalır |

**SAP ve Oracle.** Genel bilgi, ayrıntılar doğrulanmadı. SAP UI5'te mesaj kutusu ikonunu ve önem rengini kontrol (ör. MessageBox) ve
tema parametreleri verir; uygulamanın ikon HTML'i yazması ve satır içi stil kullanması önerilmez. Oracle JET / Redwood'da önem
düzeyi ikonu ve rengi bileşen ile tema CSS değişkenlerinden gelir. İkisinde de stil bileşen + temanın, sayfanın değil; A ile aynı yön.

**CT önerisi.** A. Paketten ayrılma: (1) `backbone-custom.css` işi gerekmez, sınıflar `:6277-6291`'de var — iş yalnız kural metnidir;
(2) atıf `frontend-standards.md:66` (genel yasak) ile tamamlanmalı; (3) FG-003'ün tanım satırının olmaması ayrı bir bulgudur, bu kararla
birlikte değil, ayrı sahip sözüyle ele alınmalı. Kopya dosyalar (Account, CRM) bulgu olarak sahiplerine sorulur, dokunulmaz.

**Uygulama yolu.** Ayrı commit, sahip onayıyla: `.antigravity/rules/premium-modal-standard.md` `:27-52` yerinde (8 satır → 8 satır).
Şartname §9 satır 1 durumu ayrı DL-1 commit'inde "çözüldü (sahip kararı, kayıt yolu)" olur. Paket değişmez (UIC-13 metni "until D-8a"
dediği için kararla birlikte geçerliliğini yitirir, metin değişmeden doğru kalır); yeni inceleme gerekmez. DL-2 ölçüm kaydı
(`auto-01`) değişmez: kayıtlar değişmez, sonraki koşu yeni dizine yazılır.

**Sahibin vereceği söz.** `d8a-fg003` · `d8a-istisna` · `d8a-ertele`

---

### D-8b — `createCrudTable` "tercih edilen yol" satırı

**Soru.** `frontend-js-standard.md:53` "Tercih edilen yol: `createCrudTable`" satırı BL-440 `createList` kararına göre düzeltilir mi?

**Karar sahibi.** Kural sahibi (sahip; `.antigravity/**`) (paket `:58` "owner / CT as BL-440 owner"). CT BL-440 sahibi olarak taraftır;
kural metni korunan yolda olduğu için söz sahibindir.

**Bugünkü durum (ölçüldü).**
- `.antigravity/rules/frontend-js-standard.md:53` "Tercih edilen yol: `window.DitenDataTable.createCrudTable({ tableEl, bulk: bulkOptions, actions: { onRowAction }, config })`" — tutuyor.
- **Atıf tutmuyor:** paket ve şartname §9 satır 2 çatışmanın karşı tarafını "aynı dosyanın yasak pratikler tablosu (`:981`)" diye verir.
  `:981-991` tablosunda `createCrudTable` ya da `createList` satırı **yok** (satırlar: `$(...).DataTable`, elle `layout` düğmeleri,
  `row.remove`, `toastr`, `ApiBaseUrl`, çerez / token, `$.ajax`). `createList` `frontend-js-standard.md` içinde **0** kez geçer.
- Gerçek karşı taraf: `frontend-datatable-template.md:87`, `:100`, `:114` (`createList({ dataMode … })`, BL-440 paket 3) ve BL-440:
  `product-backlog.md:6199` başlık, `:6203` "KARAR VERİLDİ, YÜRÜYOR … KARAR: 2026-09-23", `:6245` KARAR satırı, `:6264` paket 2 notu
  "`DitenDataTable.createList` (dataMode fail-closed …)", `:6268` "`createCrudTable` 84 eski çağıran için `dataMode`'suz kalıyor (bilinçli;
  zorunluluk `createList`'te)". Paketin `:6199-6203` aralığı başlık ve durum satırını gösterir, `createList` adı `:6264`'tedir.
  Yani çatışma aynı dosyada değil, **aynı katmanda iki dosya** arasındadır.
- Doğrulayıcı: `verify_datatable_page.py:522` `LIST_FACTORY_CALL`, `:535-536` `createList` çözümü (paket atfı tutuyor); `:593-595`
  "Shared layer: createList builds through createCrudTable" — `createCrudTable` fabrikanın iç katmanıdır, kaldırılacak bir işlev değildir.
- Canlı: `grep -rl 'createList(' --include='*.js' frontend/Diten.Web/wwwroot/assets/js` = 37; `createCrudTable(` = 81 (paketle aynı).

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — `:53` yerinde düzeltilir (paket) | `:53` `createList`'i tercih edilen, `createCrudTable`'ı eski sayfaların yolu diye yazar | 🟢 | Yeni sayfa doğru yolu okur; tek satır, satır kayması yok |
| B — A + yasak pratik satırı | A'ya ek olarak `:981` tablosunun sonuna "yeni sayfada doğrudan `createCrudTable` çağrısı → `createList`" satırı | 🟢 | UIC-4'ün "no new `createCrudTable`" maddesi bugün hiçbir kural satırına dayanmıyor; bu satır onu dayandırır ve paketin `:981` atfını doğru kılar. Tablo sonuna ekleme paketin atıfladığı hiçbir satırı kaydırmaz (paketin bu dosyadaki en büyük atfı `:981`) |
| C — ertele | `:53` bayat kalır | 🟡 | Ertelemenin bozabileceği: kuralı okuyan ajan yeni listeyi `createCrudTable` ile yazar (81 dosya örnek oluşturuyor); `dataMode` zorunluluğu (`:6268`) atlanır, UIC-4 her yeni sayfada kırmızı çıkar |

**SAP ve Oracle.** Genel bilgi, doğrulanmadı. SAP Fiori elements'te liste sayfası şablon + açıklama (annotation) ile kurulur; eski
"freestyle" yol desteklenir ama yeni uygulamada şablon önerilir — yani "tercih edilen yol" ile "eski yol" belgede ayrı yazılır.
Oracle JET'te eski bileşenler (`oj-*`) yerine yeni Core Pack bileşenleri (`oj-c-*`) önerilir ve eskiler belgede işaretlenir. İkisi de B'ye benzer.

**CT önerisi.** B. Paketten ayrılma: paket yalnız `:53`'ü düzeltir; ölçüm, paketin dayandığı `:981` satırının bugün var olmadığını
gösteriyor, bu yüzden yasak pratik satırı eklenmeden UIC-4'ün kural dayanağı eksik kalır. Metin `createCrudTable`'ı "kaldırılmış" değil
"fabrikanın iç katmanı; sayfa doğrudan çağırmaz" diye yazmalıdır (`verify_datatable_page.py:593-595`).

**Uygulama yolu.** Ayrı commit, sahip onayıyla: `.antigravity/rules/frontend-js-standard.md` `:53` yerinde, tablo sonuna (`:991` altına)
bir satır. Şartname §9 satır 2 ayrı DL-1 commit'inde: karşı taraf `frontend-datatable-template.md:87` + BL-440 `:6264-6268` diye düzeltilir
ve durum güncellenir. Paket değişmez — `:58` ve UIC-4 `:150` atıfları B'den sonra doğru hâle gelir; paket metni düzeltilmek istenirse bu
rev 07, yeni inceleme ve yeni onay satırı demektir, CT bunu önermez.

**Sahibin vereceği söz.** `d8b-createlist` · `d8b-createlist-yasak` · `d8b-ertele`

---

### D-8c — "Layout opsiyoneldir" satırı

**Soru.** `frontend-standards.md:208` "manuel `Layout =` opsiyoneldir" satırı, açık layout isteyen iki kurala göre düzeltilir mi?

**Karar sahibi.** Kural sahibi (sahip; `.antigravity/**`) (paket `:59`).

**Bugünkü durum (ölçüldü).**
- `.antigravity/rules/frontend-standards.md:208` "`Views/Platform/_ViewStart.cshtml` zaten admin layout'u atadığı için view'da manuel `Layout = ` opsiyoneldir." — tutuyor.
- `.antigravity/rules/module-pack-standard.md:88` "Razor sayfasinda layout **ACIKCA** yazilir"; `:292` layout zorunluluğu test edilebilir madde;
  `:608` `shell: platform-admin` → tüm .cshtml'de açık `Layout` — tutuyor.
- `.antigravity/rules/views-organization.md:39` "Yeni oluşturulan her Razor sayfasında shell tipine göre layout açıkça seçilir" (`:40-41` örnekler) — tutuyor.
- Canlı: `frontend/Diten.Web/Views/Platform/_ViewStart.cshtml` 3 satır, `Layout = "_LayoutPlatformAdmin"`. `Views/Platform` altında 19 `Index.cshtml`;
  2'si açık `Layout = "_LayoutPlatformAdmin"` yazmıyor: `Views/Platform/ReferenceData/Index.cshtml`, `Views/Platform/Workflow/Index.cshtml`
  (başka modüller, XMC-001). Kök `Views/_ViewStart.cshtml` `_Layout`'a (donuk, eski) gider (şartname Ek C §3).

**Seçenekler.**

| Seçenek | Ne olur | Risk | Neden |
|---|---|---|---|
| A — açık layout; `:208` düzeltilir (paket) | `:208` "view'da `Layout =` açıkça yazılır; `_ViewStart` yalnız güvenlik ağıdır" olur | 🟢 | İki kural bire karşı; UIC-1 olduğu gibi kalır. İki platform sayfası sahiplerine bulgu olarak gider; çalışma zamanı aynı (`_ViewStart` zaten atıyor) |
| B — `:208` kabul edilir | `module-pack-standard.md:88, :292, :608` ve `views-organization.md:39` platform için gevşetilir, UIC-1 değişir | 🟡 | İki kural dosyası ve paketin UIC-1'i değişir → paket rev 07, yeni inceleme, yeni onay satırı |
| C — ertele | Çelişki kalır | 🟡 | Ertelemenin bozabileceği: ajan `:208`'i okuyup layout'u atlar, UIC-1 kırmızı çıkar. "Opsiyonel" alışkanlığı tenant sayfasına genellenirse sayfa kök `_ViewStart` üzerinden donuk `_Layout`'a düşer — o durumda 🔴 |

**SAP ve Oracle.** Büyük ölçüde uygulanmaz. Genel bilgi, doğrulanmadı: Fiori launchpad'de kabuğu (shell) platform verir, uygulama kabuk
seçmez; Oracle Redwood uygulamalarında da sayfa şablonu kabuğu belirler. Buradaki soru Razor'a özgü bir "açık mı, örtük mü" sorusudur.

**CT önerisi.** A. Paketle aynı. Gerekçeye eklenen ölçüm: örtük layout'un tek güvenli olduğu yer `Views/Platform/`; dışında kök `_ViewStart`
donuk `_Layout`'u verir, bu yüzden genel kural "açık" olmalıdır.

**Uygulama yolu.** Ayrı commit, sahip onayıyla: `.antigravity/rules/frontend-standards.md:208` yerinde (paket `:213`'ü de atıfladığı için satır
sayısı korunur). Şartname §9 satır 3 durumu ayrı DL-1 commit'inde. İki platform sayfası için bulgu, sahiplerine sorularak (XMC-001). Paket
değişmez; yeni inceleme gerekmez. B seçilirse paket değişir (UIC-1) → rev 07 + inceleme 07 + yeni onay satırı.

**Sahibin vereceği söz.** `d8c-acik` · `d8c-opsiyonel` · `d8c-ertele`

---

## Karar sırası önerisi

1. **D-8b** önce: kural bugün yeni sayfaları yanlış yola yönlendiriyor (81 örnek dosya); düzeltme tek satır + bir tablo satırı.
2. **D-8a**: kural şablonu bayat, canlı kod zaten sınıflı; kopyalar sürüyor. D-5 ile aynı dosyaya (`premium-modal-standard.md`) dokunur;
   iki ayrı commit olarak arka arkaya yapılabilir, ama her biri kendi sahip sözüyle.
3. **D-8c**: çalışma zamanı etkisi yok (platform `_ViewStart` koruyor), ama yönlendirme çelişkisi var.
4. **D-5**: yalnız etiket; D-8a ile aynı oturumda yapılabilir.
5. **D-4, D-7**: yalnız kayıt; P2 revizyonundan (FU-7) önce herhangi bir zamanda.
6. **D-3, D-6**: Enterprise Architect'in; bu blok yalnız öneriyi iletir, sıraya koymaz.

**PR'ı bekleyenler ve beklemeyenler.** Hiçbir kararın **verilmesi** paket onay PR'ının (satır `688348ec1`) `main`'e girmesini beklemez. Uygulamada:
- D-8a, D-8b, D-8c, D-5 kural commit'leri PR'ı beklemez; ama bu dala yazılırlarsa aynı PR'a girerler ve onaylayan (bestepullukcu-alt) onları
  da inceler. Ayrı dal mı aynı PR mı, sahibin kararıdır (bu kayıt seçmez).
- Şartname §9 durum güncellemeleri ilgili kural commit'inden **sonra** gelir; DL-1 dilimidir, onay satırının `main`'e girmesini bekler
  (kayıt 01 "Pakete etkisi": WP'ler kuyruk ve onay `main`'e girmeden dağıtılmaz).
- D-3, D-4, D-6, D-7 dosya değiştirmez; yalnız karar kaydı ister, hiçbir şeyi beklemez.
- Paketi değiştiren tek yol D-8c-B'dir (ve paket metni düzeltmeleri); o yol PR'daki onay satırını bayatlatır.

## Sınırlar

- Bu dosya öneri taslağıdır; karar vermez, paket, kural, kayıt ya da şartname değiştirmez. Commit edilmedi.
- Ölçüm yalnız bu dalda (HEAD `44238eab7`) yapıldı; `main` ile yalnız onay satırı karşılaştırıldı.
- Blueprint 8.8 çalışma kitabı (`Dependencies_Normalized`, `Dev_Readiness`) yeniden okunmadı (`openpyxl` yok); Blueprint iddiaları paketten
  aktarıldı, kimlikler `verify_module_id.py` ile doğrulandı.
- SAP ve Oracle karşılaştırmaları genel bilgidir, web araması yapılmadı; "doğrulanmadı" yazan her cümle kaynakla teyit edilmemiştir.
- Başka modüllerin dosyaları (Account ve CRM şablon kopyaları, iki platform sayfası, kimlik kaydı, platform README, `hr-future` dalı)
  yalnız okundu ve bulgu olarak anıldı (XMC-001); şablon kopyalarının kaynağı satır satır ölçülmedi.
- `sync_adapters.py` koşulmadı; AGENTS.md:139'un Çekirdek dışında olduğu ve özetin yalnız Çekirdek'ten alındığı kaynak okunarak bulundu.
