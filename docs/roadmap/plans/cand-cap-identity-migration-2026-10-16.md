# CAND-CAP → kanonik modül kimliği göçü — CT kontrolü ve düzeltilecekler (2026-10-16)

> **Durum:** not + yapılacaklar. Göç işi **başlamadı**. Kaynak: dışarıdan gelen bir öneri (CAND-CAP-0001…0011'in Blueprint 8.1 / 8.2 hedefleri). CT, öneriyi repodaki kaynaklarla karşılaştırdı:
> - `docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx`
> - `execution/registries/module-id-registry.md`
> - `execution/portfolio/delivery-capability-packs/DCP-006-crm-commercial-delivery-capability-pack.md`
> - modül paketleri ve kod
>
> **Kural:** Tek seferde bul-değiştir YOK. Her kimlik göçü kendi iş paketiyle yapılır. Önce şunların hepsi ölçülür: rota, izin anahtarı, namespace, Mongo koleksiyonu, API / olay, test, belge, modül paketi, ağ geçidi eşlemesi, eski takma ad.

## 1. Önerilen hedefler ve CT sonucu

| Aday | Yetenek | Önerilen işlem | Hedef | CT sonucu |
|---|---|---|---|---|
| CAND-CAP-0001 | Tenant User / Identity Foundation | yeniden adlandır | MOD-0360 | ⚠ 8.2 gerekli (8.1'de yok) |
| CAND-CAP-0002 | SaaS Subscription, Plan & Entitlement | yeniden adlandır | MOD-0361 | ⚠ 8.2 gerekli |
| CAND-CAP-0003 | Platform Administration & Operations | ayrı modül değil; konsol | MOD-0009 çalışma alanı | ◐ kısmen (§3.3) |
| CAND-CAP-0004 | Tenant Impersonation / Support Tooling | kod yok, bekle | ileride MOD-0360 özelliği | ✅ makul |
| CAND-CAP-0005 | SaaS Billing & Invoicing | kod yok, bekle | ileride MOD-0364 | ⚠ 8.2 gerekli |
| CAND-CAP-0006 | Work Aggregation / Task Center (WorkCenter) | adayı emekli et | MOD-0024 çalışma alanı | ✅ 8.1'de 0024 = Task & Checklist Engine · ⚠ §3.2 |
| CAND-CAP-0007 | Enterprise Strategy & Performance Mgmt | yeniden adlandır | MOD-0352 | ✅ 8.1'de var; kayıtta zaten "deprecated alias" · ⚠ §3.2 |
| CAND-CAP-0008 | Decomposition & Work Structuring Engine | yeniden adlandır | MOD-0354 | ✅ 8.1'de var · ❗ §3.1 |
| CAND-CAP-0009 | Business Process Management | yeniden adlandır | MOD-0355 | ✅ 8.1'de var; kayıtta "deprecated alias" |
| CAND-CAP-0010 | Working Calendar & Public Holidays | yeniden adlandır | MOD-0362 | ⚠ 8.2 gerekli · ❗ §3.1 |
| CAND-CAP-0011 | Structured Content & Messaging (Content Studio / SCMM) | yeniden adlandır | MOD-0363 | ⚠ 8.2 gerekli · kodda 51 dosya (büyük göç) |

**Doğrulananlar:**
- 8.1 adları: MOD-0009 Tenant / Environment Management, 0018 RBAC / ABAC Authorization, 0021 Audit Trail Service, 0024 Task & Checklist Engine, 0352 Enterprise Strategy Management, 0354 Decomposition & Work Structuring Engine, 0355 Business Process Architecture & Modeling.
- Kayıttaki aday adları öneriyle birebir aynı.
- **Eski numara çakışması gerçek.** Kayıt, eski numara olarak 0001 → MOD-0047, 0002 → MOD-0297, 0002-FU05 → MOD-0298, 0005 → MOD-0299 gösteriyor. 8.1'de MOD-0047 = Business Continuity & Crisis Management; 0297 / 0298 / 0299 = HR Capability Block Shell / Employee Profile Workspace / Position & Organization Assignment. Bu numaralar çalışan kodda takma ad olarak KALMAMALI.

## 2. Doğrulanamayan — 8.2 gerekli
8.1'deki en büyük numara **MOD-0359**. MOD-0360…0364 öneriye göre **Master 8.2**'de; 8.2 repoda yok.
→ **Yapılacak:** 8.2 dosyası `docs/reference/blueprint/` altına konur (kanonik kaynak). CT 0360–0364'ü ondan doğrular.

## 3. Önerinin görmediği çakışmalar — düzeltilecek

### 3.1 CAND-CAP-0008 iki anlamda kullanılıyor ❗
- Kayıtta 0008 = **Decomposition** (→ MOD-0354); Çalışma Takvimi = **0010** (→ MOD-0362).
- Ama Çalışma Takvimi modül paketleri ve CRM kodu Çalışma Takvimi'ni **"CAND-CAP-0008"** diye anıyor:
  - `execution/domains/platform-shared-services/module-packs/CAND-CAP-0008-FU02-working-calendar-public-holiday-auto-fetch.md` (+ FU01 / FU03 referansları);
  - `MOD-0155-FU06-cycle-capacity.md`, `MOD-0165-FU06-cycle-period.md`, `MOD-0165-FU07-…`;
  - CRM: `Features/CycleCapacity/Contract/CycleCapacityFeatureFlags.cs`, `Read/IWorkingDayChecker.cs`, `Read/IWorkingDayCounter.cs`, `Domain/Entities/CycleCapacity.cs` (+ toplam 7 kod dosyası, 18 belge).
- **Risk:** "0008 → MOD-0354" göçü yapılırsa Çalışma Takvimi referansları yanlış modüle (Decomposition) bağlanır.
- **Düzeltme:** 0008'in Çalışma Takvimi anlamındaki her kullanımı ayrı listelenip **MOD-0362**'ye (8.2 doğrulanınca) taşınır. Decomposition göçü yalnız `management-governance` tarafındaki gerçek 0008 kullanımlarına uygulanır.
- Hafıza / WP notlarımızdaki "CAND-CAP-0008 Working Calendar" ifadeleri de düzeltilir.

### 3.2 CAND-CAP-0006 / 0007 CRM'de başka anlamda ❗
- DCP-006 (satır ~149-152) iki CRM yeteneğine kayda yazılmadan "sıradaki boş numara" diye bu numaraları vermiş:
  - **CAND-CAP-0006** = Commercial Activity, Interaction and Commitment;
  - **CAND-CAP-0007** = Commercial Model and Proposition Routing.
- Kayıtta 0006 = WorkCenter, 0007 = Enterprise Strategy. Yani çakışma var; DCP-006'nın kendisi de "unresolved identity debt — EA must reserve" diyor.
- **Düzeltme:** DCP-006'daki iki yetenek yeni kimlik alır (8.2'de modül ya da yeni aday numarası); DCP-006 güncellenir.
- **Bağ:** "Commercial Activity, Interaction and Commitment" = BD analizindeki **"Commercial Activity & Correspondence Register"** boşluğu (2026-10-16 BD notu; e-posta, toplantı, taahhüt kaydı). İkisi tek yetenek olarak tanımlanmalı. Partner / BD planı: artifact Fe9v4weD.

### 3.3 CAND-CAP-0003 → MOD-0009: kısmen ◐
- Yönetim konsolunun tek ekranda toplanması doğru. Ama 8.1'de MOD-0009 "Environment & Release Management" grubunda.
- CAND-CAP-0003'ün alt işleri (FU01 profil ve ayarlar, FU02 şifre ve MFA, platform yöneticisi CRUD) kimlik işi.
- **Öneri:** konsol / çalışma alanı → MOD-0009; platform yöneticisi hesap yaşam döngüsü, şifre ve MFA → MOD-0360 (+ MOD-0018 yetki).

## 4. Sıra (öneri, CT notlarıyla)
1. **Ön koşul:** 8.2'yi repoya koy; 0360–0364'ü doğrula.
2. **Envanter:** §3.1 ve §3.2 çakışma listeleri (kod + belge + hafıza).
3. Göç iş paketleri:
   - **Önerinin kendi sırası:** 0007 → 0352 · 0008 (yalnız Decomposition) → 0354 · 0009 → 0355 · 0001 → 0360 · 0002 → 0361 · 0010 (+ 0008'in Çalışma Takvimi kullanımları) → 0362 · 0011 → 0363 · 0006 → 0024 çalışma alanı · 0003 → 0009 konsol + 0360 hesap yaşam döngüsü.
   - Ayrıca **DCP-006 yeni kimlikleri** ve eski numaraların (0047 / 0297–0299) çalışma zamanından temizlenmesi.
4. **Sahiplik:** kayıt ve EA kararı Kapanış / mimari hattı. CRM tarafındaki düzeltmeler (CycleCapacity referansları, DCP-006) bizim; göç paketi geldiğinde paketlenir.

## 5. İkinci öneri — eski proje anlamlı MOD numaraları (0040 / 0043 / 0044 / 0046 / 0047 / 0297 / 0298 / 0299)

**Öneri:** Master anlamları korunsun. Eski proje anlamlarından yalnız bağımsız kayıt sahibi (SoR) olanlar yeni kimlik alsın (MOD-0360 / 0361 / 0364); diğerleri özellik, çalışma alanı ya da mimari yönetişim olsun. Master'a bir "Legacy Module Identity Map" eklensin.

**8.1 doğrulaması (hepsi ✅):**
- MOD-0040 = Canonical ID & Correlation Standard · 0043 = SLO/SLA Monitoring · 0044 = Backup & Restore · 0046 = Performance & Capacity Management · 0047 = BCM.
- 0297 / 0298 / 0299 = HR Capability Block Shell / Employee Profile Workspace / Position & Organization Assignment.
- Atıf yapılan modüller: 0001 SoR & Ownership Registry · 0002 Interface Registry · 0013 Platform Standards Registry · 0014 Module Boundary Registry · 0169 Billing & Invoicing · 0286 UI Design System & Page Pattern Governance · 0288 Organization, Person & Position Directory.

**Repo bunu zaten büyük ölçüde yapmış** (`module-id-registry.md`, DCP-002):

| Eski | Repo kararı (bugün) | Öneri | Fark |
|---|---|---|---|
| MOD-0040 Tenant Organization Foundation | deprecated alias → **MOD-0288** (+ FU01 → 0288-FU01) | 0009 + 0288'e böl | küçük; repo 0288'e vermiş, tenant tarafı zaten 0009'da |
| MOD-0043 Tenant Architecture Foundation | deprecated alias → **MOD-0009-FU01** (done) | emekli; 0013 / 0014 | **farklı** — repo 0009'un alt işi yapmış |
| MOD-0044 Tenant Manager Backend | deprecated alias → **MOD-0009-FU02** | emekli; 0009 / 0360 / 0361'e dağıt | **farklı** |
| MOD-0046 Tenant Core UI | deprecated alias → **MOD-0009-FU03**; 0046-QG → MOD-0033-FU01 | çalışma alanı; 0009 + 0286 | **farklı** |
| MOD-0047 Tenant User Foundation | deprecated alias → CAND-CAP-0001 | MOD-0360 | aynı yön |
| MOD-0297 / 0298 | → CAND-CAP-0002 / 0002-FU05 | MOD-0361 (birleşik) | aynı yön |
| MOD-0299 SaaS Billing | → CAND-CAP-0005 | MOD-0364 | aynı yön; 0169 (müşteri O2C faturası) ayrımı doğru |

**CT değerlendirmesi:**
- "Master anlamını değiştirme" ilkesi **kesin doğru**. Değiştirmek, 0040 vb.'ye bağlı her paket, sözleşme ve plan için zincirleme göç demek.
- 0043 / 0044 / 0046 için **yeni bir karar verme**: repo bunları zaten MOD-0009'un FU01 / 02 / 03'ü yapmış ve aktif paketleri var. Önerinin "emekli et, 0013 / 0014'e dağıt" yaklaşımı mevcut kararla çelişir ve bir yeniden kimliklendirme turu daha doğurur. Mevcut karar korunmalı. Yalnız 0009-FU02 içindeki kullanıcı ve abonelik parçaları 0360 / 0361 kurulunca oraya bağlanır.
- **"Legacy Module Identity Map" zaten var:** `module-id-registry.md`'deki "Deprecated Alias" satırları tam olarak bu. Master'a ikinci bir tablo eklenirse iki harita zamanla ayrışır. Öneri: Master'da yalnız kayda bir atıf olsun, harita tek yerde (registry) kalsın.
- **Kod yükü küçük:** eski MOD numaraları kodda 0–2 dosyada, belgelerde 9–39 dosyada geçiyor. Gerçek kod göçü yalnız CAND-CAP-0008 (Çalışma Takvimi anlamında, 7 dosya) ve CAND-CAP-0011 (Content Studio, 51 dosya).

## 6. Blueprint ile karışıklığı kalıcı önleme — kurallar ve sıra

**Kurallar:**
1. **Anlamın tek kaynağı Blueprint** (8.1 → 8.2). Mevcut bir MOD numarasının anlamı hiçbir zaman değiştirilmez; numara yeniden kullanılmaz.
2. **Eşlemenin tek kaynağı `module-id-registry.md`.** Eski / aday / takma ad → kanonik hedef ve durum (Deprecated Alias · Migrating · Retired) yalnız burada tutulur. Master ve DCP'ler buna atıf yapar, kopyalamaz.
3. **Yeni MOD numarası yalnız Blueprint'ten gelir.** Geçici ihtiyaç için CAND-CAP numarası kayda **önce yazılır, sonra kullanılır**. DCP-006'daki gibi "sıradaki boş numara" varsayımı yasak.
4. **Çalışan kodda (rota, izin anahtarı, modül kodu, koleksiyon, olay) yalnız kanonik kimlik** kullanılır. Eski takma ad belgede tarihî not olarak kalabilir, kodda kalamaz.
5. **Koruma testi:** mimari testte, kodda "Deprecated Alias" durumundaki bir kimlik ya da kayıtta olmayan bir CAND-CAP / MOD numarası geçerse kırmızı. Sabotaj kanıtıyla.

**Sıra:**
1. 8.2'yi `docs/reference/blueprint/` altına koy; 0360–0364'ü doğrula.
2. Kayıt güncellemesi (tek iş paketi, mimari / Kapanış hattı):
   - 0360–0364 satırlarını aç;
   - CAND-CAP-0001 / 0002 / 0005 / 0010 / 0011 → hedefleri "Migrating";
   - 0006 → MOD-0024 çalışma alanı; 0003 → MOD-0009 konsol + 0360;
   - DCP-006'nın iki CRM yeteneğine yeni kimlik (BD etkileşim kaydıyla birlikte).
3. Envanter: CAND-CAP-0008'in Çalışma Takvimi kullanımları (7 kod + 18 belge) ve eski numaraların kod içi kullanımları (0040 → 2, 0297 → 1, 0299 → 1 dosya vb.).
4. Göç iş paketleri (kimlik başına; önce kod, sonra belge). Büyükler: 0011 → 0363 (Content Studio) ve 0010 + 0008-takvim → 0362.
5. Koruma testi (kural 5) en sonda; böylece geriye dönüş olmaz.

## 7. Karar önerisi — main'de olup 8.1'de olmayan yetenekler (CT, 2026-10-16)
Kaynak envanter: artifact "Blueprint Modülleri Main Durumu" (https://claude.ai/artifact/1KPVHJSp7wpwGGXPrvCQfu; main a814bad0f, 2026-10-10).

8.1'de örtüşme taraması: takvim / tatil, abonelik / yetkilendirme, kiracı kullanıcı kimliği, Content Studio / MLR / mesaj, yazışma / etkileşim için 8.1'de modül YOK. Yalnız iki eşleşme var:
- MOD-0261 Identity Provider: harici sağlayıcı, kiracı kullanıcı kaydı değil.
- MOD-0024 Task & Checklist Engine: WorkCenter'ın doğal evi.

**Ölçüt:** Kendi kayıt ailesi (SoR) olan → yeni kanonik modül. Başka modüllerin üzerinde bir ekran ya da konsol olan → mevcut modüle kat. Kodu olmayan → aday kalsın, geliştirme başlarken hedef numarayla açılsın.

| Aday | Main'de | Karar | Hedef | Gerekçe |
|---|---|---|---|---|
| CAND-CAP-0001 Tenant User / Identity | BE+FE %90 | **Yeni modül** | MOD-0360 (8.2) | Kiracı kullanıcı, üyelik, harici kimlik bağı = kendi SoR'u. 0261 harici sağlayıcı, 0018 yetki |
| CAND-CAP-0002 SaaS Subscription / Plan / Entitlement | BE+FE %100 | **Yeni modül** | MOD-0361 (8.2) | Plan → abonelik → yetkilendirme tek yaşam döngüsü; 0002-FU05 (eski 0298) içinde |
| CAND-CAP-0003 Platform Administration & Operations | BE+FE %85 | **Kat** | Konsol → MOD-0009; yönetici hesap / şifre / MFA → MOD-0360 (+0018) | Kendi SoR'u yok; diğer modüllerin üzerinde çalışma alanı |
| CAND-CAP-0004 Tenant Impersonation | kod yok | **Aday kalsın** | ileride MOD-0360 özelliği (+0018 / 0021) | Kod yok; güvenlik modeli EA kapılı |
| CAND-CAP-0005 SaaS Billing & Invoicing | kod yok | **Aday kalsın**; numara 8.2'de ayrılsın | MOD-0364 (8.2) | Kendi SoR'u olacak; MOD-0169 müşteri O2C faturasıyla karıştırılmamalı |
| CAND-CAP-0006 Task Center / WorkCenter | kod var (0024 altında 268 dosya) | **Kat** | MOD-0024 çalışma alanı | "Work Center" ekran adı kalır, modül kimliği 0024 |
| CAND-CAP-0007 Enterprise Strategy | zaten deprecated alias | **Kapat** (alias'ı emekli et) | MOD-0352 | 8.1'de var |
| CAND-CAP-0008 Decomposition | zaten deprecated alias | **Kapat**; ⚠ Çalışma Takvimi anlamındaki 0008 kullanımları ayrı (§3.1) | MOD-0354 | Envanterdeki 6 kod dosyası büyük olasılıkla CRM CycleCapacity'nin takvim referansları |
| CAND-CAP-0009 Business Process Mgmt | zaten deprecated alias | **Kapat** | MOD-0355 | 8.1'de var |
| CAND-CAP-0010 Working Calendar & Public Holidays | kod var (sevk edildi) | **Yeni modül** | MOD-0362 (8.2) | Takvim, tatil, kural istisnası = kendi SoR'u; CRM ve İK tüketiyor |
| CAND-CAP-0011 Content Studio (SCMM) | 52 kod dosyası | **Yeni modül** | MOD-0363 (8.2) | Yapılandırılmış içerik + mesaj + onay; 8.1'de MLR / promosyon içeriği modülü yok. MOD-0162 Knowledge Base ile sınır yazılmalı |
| DCP-006 "CAND-CAP-0006" Commercial Activity, Interaction & Commitment | kod yok | **Yeni kimlik gerekli** (8.2 / EA) | — | 8.1'de yok; BD analizindeki yazışma / etkileşim kaydıyla tek yetenek |
| DCP-006 "CAND-CAP-0007" Commercial Model & Proposition Routing | kod yok | **Aday kalsın**, doğru numarayla yeniden kaydedilsin | — | Kod yok; mevcut numara WorkCenter / Strateji ile çakışıyor |

**Özet:**
- 5 yeni modül: 0360, 0361, 0362, 0363 + 0364 ayrılmış.
- 2 kat: 0003 → 0009 / 0360; 0006 → 0024.
- 3 kapat: 0007 / 0008 / 0009 (zaten 8.1'de var).
- 2 aday: 0004, 0005 (koda başlarken 0360 özelliği / 0364).
- CRM'in iki aday numarası yeniden kimliklendirilir.

## 8. Kod etkisi — ölçüm (CT, 2026-10-16)
**Kod taşınmaz.** Hiçbir rota, izin anahtarı, namespace ya da Mongo koleksiyon adı CAND-CAP / eski MOD numarası taşımıyor. Değişecek olan etiketler.

| Kimlik | Kod satırı | Yorum | Dize içinde | Dize içindekiler ne |
|---|---|---|---|---|
| CAND-CAP-0011 | 75 | 71 | 4 | **CRM denetim kaydı `sourceModule: "CAND-CAP-0011"`** (`HttpCrmAuditPublisher.cs:126`) + 2 log metni + 1 BRD seed notu |
| CAND-CAP-0008 | 10 | 6 | 3 | CRM CycleCapacity (Çalışma Takvimi anlamında, §3.1) |
| MOD-0297 / 0299 | 5 | 0 | 5 | **Platform Hangfire tekrarlı iş kimlikleri** `Diten.Platform.MOD-0297.TrialExpiryScanJob`, `…SubscriptionRenewalJob` (`PlatformRecurringJobRegistrar.cs:30-31`) |
| CAND-CAP-0001 / 0002 / 0003 / 0006 | 1–6 | 0 | 1–6 | Yalnız `docs/roadmap/plans/mod-0183-root-uptake-recovery-01/baseline.json`'daki **dosya yolu karmaları** (modül paketi dosya adları) |
| CAND-CAP-0010, MOD-0040 | 2 | 2 | 0 | yalnız yorum |

**Çalışma anında dikkat isteyen iki yer:**
1. **Denetim kaydı (`sourceModule`):** Yeni olaylar MOD-0363 yazar. Eski denetim kayıtları DEĞİŞTİRİLMEZ (denetim değişmez kayıttır). Raporlama ve sorgular eski ile yeni değeri birlikte tanımalı; eşleme kayıttan (registry) okunur.
2. **Hangfire iş kimlikleri:** Kimlik değişirse yeni iş kaydedilir, eski kimlik Hangfire'da kalır ve iş **iki kez** çalışabilir. Göçte eski tekrarlı iş kaydı silinir (RemoveIfExists) ve bu bir testle korunur. Kayıttaki "RUNTIME COMPATIBILITY LITERAL" notu bunu anlatıyor.

**Belge tarafı (asıl yük):**
- `execution/.../module-packs/CAND-CAP-*.md` dosya adları ve içleri, belgeler arası bağlantılar.
- `baseline.json` karmaları dosya adı değişince yeniden üretilir.
- Hafıza ve WP notlarımızdaki eski adlar.

## 9. Proje ve Master ayrımı — kullanıcıya gelen öneri (2026-10-16) ve CT değerlendirmesi
**Öneri:** İkisi birden güncellensin, farklı amaçla.
- **Proje:** aktif CAND-CAP / eski kimlikler kanonik MOD'a taşınır (rota, izin, namespace, modül paketi, belge, test, gerekiyorsa DB / sözleşme).
- **Master:** yeni gerçek modüller eklenir. Katılanlar ayrı modül değil, ilgili modülün altında çalışma alanı / özellik olarak gösterilir; sınır, sayfa ve SoR notları güncellenir.

| Grup | Proje | Master |
|---|---|---|
| Yeni modül: 0001, 0002, 0010, 0011 (+ planlı 0005) | yeniden adlandır / taşı | yeni modül satırı ekle |
| Katılan: 0003, 0006, 0004 | kimliği emekli et; özellik / çalışma alanı olarak taşı | yeni modül YOK; ilgili modülün sınır / sayfa / SoR notu güncellenir |
| Zaten kanonik: 0007, 0008, 0009 | proje kimliğini MOD'a taşı | zaten var; gerekirse ad / sınır uzlaştır |

**CT değerlendirmesi:** ✅ Doğru, §6 kurallarıyla uyumlu. Uyum koşulları:
- **Rol ayrımı:** Master = anlam ve sınır (hangi modül var, neyi kapsar, hangi çalışma alanı hangi modülün altında). Kimlik kaydı (`module-id-registry.md`) = eski → yeni eşleme ve durum (Deprecated Alias / Migrating / Retired). Eşleme tablosu Master'a kopyalanmaz.
- **"Namespace / route / permission" güncellemesi bugün GEREKMİYOR:** §8 ölçümüne göre bunların hiçbiri CAND-CAP taşımıyor. Önerinin genel listesi doğru, ama ölçülen iş daha küçük.
- **"Gerekirse DB / sözleşme" maddesi bizde somut iki yer:** denetim `sourceModule` (eski kayıtlar değişmez) ve Hangfire iş kimlikleri (eski kayıt silinir).
- **Master'a eklenirken eksik kalmaması gerekenler:**
  - 0010'un sınırı: CRM ve İK tüketici, Platform sahibi.
  - 0011 ile MOD-0162 Knowledge Base sınırı.
  - 0005 / 0364 ile MOD-0169 müşteri faturası ayrımı.
  - DCP-006'nın "Commercial Activity, Interaction & Commitment" yeteneği için yeni modül (BD yazışma / etkileşim kaydıyla tek yetenek).
- **Önkoşul değişmedi:** 8.2 repoya konmadan 0360–0364 doğrulanamaz.

## 10. Plandaki yeri
Kullanıcı (2026-10-16): **planın sonunda düzeltilecek.** Yol haritası `ROADMAP-visit-workspace.md` §8 son satırı. Tetikleyiciler: 8.2'nin repoya girmesi + EA / Kapanış hattı ile sahiplik netleşmesi. Bizim (CRM) payımız:
- CycleCapacity'nin 0008 → 0362 etiketleri;
- CRM denetim `sourceModule` 0011 → 0363 (eski kayıt eşlemesiyle);
- DCP-006 yeniden kimliklendirme;
- hafıza ve WP notlarının güncellenmesi.

## 11. Üçüncü mesaj (2026-10-16) — eski 8 MOD numarası için özet öneri ve CT değerlendirmesi
**Öneri:**
- Blueprint anlamları değişmez; projede eski takma adlar emekli edilir.
- Eski anlamlar şöyle taşınır:
  - 0040 → 0009 + 0288'e katılır;
  - 0043 → 0013 + 0014 altında yönetişim konusu;
  - 0044 → ayrı modül değil, uygulama bileşeni;
  - 0046 → ayrı modül değil, platform yönetim çalışma alanı;
  - 0047 → 0360;
  - 0297 / 0298 → 0361;
  - 0299 → 0364.
- Sonra route / permission / namespace / docs / test referansları taşınır.
- Kural: eski Blueprint ID'leri yeniden kullanılmaz.

**CT değerlendirmesi:**
- ✅ **Kural ve 0047 / 0297 / 0298 / 0299 hedefleri doğru**; §1 ve §7 ile aynı. Kayıtta bunlar bugün CAND-CAP-0001 / 0002 / 0002-FU05 / 0005 üzerinden bağlı; göçte doğrudan 0360 / 0361 / 0364'e bağlanır.
- ◐ **0040:** Kayıt bugün 0040'ı (ve 0040-FU01'i) **MOD-0288**'e (+ 0288-FU01) taşımış, durum "done". Önerinin "0009 + 0288" ifadesi uyumlu: kiracı yapılandırması zaten 0009'da. Yeni iş yok, yalnız 0288 satırının notu "0009 ile sınır" diye netleştirilebilir.
- ⚠ **0043 / 0044 / 0046: mevcut kararla ÇELİŞİYOR.** Kayıt (DCP-002) bunları zaten **MOD-0009-FU01 / FU02 / FU03** yapmış:
  - FU01 Tenant Architecture Foundation — done;
  - FU02 Tenant Manager Backend — in-progress;
  - FU03 Tenant Core UI — in-progress;
  - her birinin aktif modül paketi var.

  Öneri bunları "0013 / 0014 yönetişim konusu", "uygulama bileşeni" ve "çalışma alanı" olarak dağıtıyor. Bu, çalışan bir kararı bozar, aktif paketleri sahipsiz bırakır ve gereksiz bir göç turu açar.
  **CT önerisi:** MOD-0009-FU01..03 kalsın. FU02 (backend) içindeki kullanıcı ve abonelik parçaları 0360 / 0361 kurulunca oraya bağlanır. FU03 (UI) zaten "platform admin çalışma alanı" anlamında, yani önerinin amacına uygun.
- ℹ **"route / permission / namespace" taşıması:** §8 ölçümüne göre bunların hiçbiri eski numara taşımıyor. Gerçek iş belgeler + 2 çalışma anı değeri (CRM denetim `sourceModule`, Platform Hangfire iş kimlikleri).

**Karar bekleyen tek nokta (kullanıcı / EA):** 0043 / 0044 / 0046 için kayıttaki MOD-0009-FU01..03 kararı mı korunacak (CT önerisi), yoksa öneri mi uygulanacak?

## 12. Dördüncü mesaj (2026-10-16) — iş bölümü: proje mi, Master mı
**Öneri:** Teknik göç projede yapılır. Master'da kanonik mimari ve eskiden kanoniğe eşleme (özet / not) güncellenir.

| İş | Proje | Master |
|---|---|---|
| Eski takma adları emekli etme | Evet | Hayır |
| Rota / izin / namespace / test / belge kimlik göçü | Evet | Hayır |
| Eski anlamı yeni kanonik modüle bağlama | Evet | Evet |
| Kanonik Blueprint anlamını koruma | — | Evet |
| Eski kimlik → kanonik hedef eşlemesi | tercihen belge / kayıt | Evet, özet / not |
| Yeni gerçek modül ekleme | geliştirme sonra | Evet |

**CT değerlendirmesi:** ✅ §9 ile aynı.
- **Tek koşul:** Eşleme iki yerde tutulursa yetkili kaynak **kimlik kaydı** (`module-id-registry.md`) olmalı. Master'daki özet ona atıf yapan bir not olarak kalmalı; ikisi ayrışırsa kayıt kazanır. Bu, iki haritanın zamanla birbirinden kopmasını önler.
- **İş yükü:** "Rota / izin / namespace" bugün boş çıkıyor (§8); projede gerçek iş belgeler + 2 çalışma anı değeri.
- **Açık karar** hâlâ §11'deki: 0043 / 0044 / 0046.
