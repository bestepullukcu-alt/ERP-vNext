# WP-VW-W6 — Eczane ön siparişi (MOD-0168) + fiyat listeleri ve MF / iskonto kuralları (MOD-0156)

> **Durum:** 1. adım W6-PACK (modül paketleri) — 2026-10-19 (CT). Kod istemleri paketler `ready-for-dev` olunca bu belgeye eklenir.
> **Dayanaklar:**
> - [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md): K-W9 ve K-W10 (a)–(h).
> - Brief ve tasarım: [BRIEF-W6](mockups/visit-workspace/BRIEF-W6-pharmacy-pre-order.md), mockup [W6 Eczane On Siparis (standalone).html](mockups/visit-workspace/W6%20Eczane%20On%20Siparis%20(standalone).html), tasarım farkı [DESIGN-GAP-W6](mockups/visit-workspace/DESIGN-GAP-W6-2026-10-19.md) (§5 D1–D16 + §7 karar).

## Kararlar (özet; ayrıntı DESIGN-GAP-W6 §5 / §7)
| # | Karar |
|---|---|
| K-O1 | Sipariş = **MOD-0168 Order Capture**; fiyat listesi + MF / iskonto kuralları + değerlendirme API'si + ara kur tablosu = **MOD-0156 Price Lists & Discount Guardrails** (ilk dilim). İkisi Blueprint 8.7 ve kayıt tablosu gereği commercial-suite domain'inde → `Diten.CrmService` içinde, CRM modüllerinden ayrı sınırla (kendi klasör, koleksiyon ve izin ad alanı). |
| K-O2 | **İzin ad alanı `commercial.*`** (kullanıcı 2026-10-19). Örnekler: `commercial.pre-orders.*`, `commercial.price-lists.*`, `commercial.pricing-rules.*`, `commercial.fx-rates.*`. PKS-001 §4'e açık satır olarak eklenir; `crm.*` ile karışmaz. |
| K-O3 | Ön sipariş bir **transfer siparişidir**: depoya iletilir, faturayı depo keser; bizim satış siparişimiz değildir (`fulfilment = depot`). Stok hareketi yoktur. Ürün kimliği product-master v1.1 SKU'dur (`skuId` + çözülen seviye; `Retired` girmez). |
| K-O4 | Ülke kapsamı MDM tüzel kişilikleridir; satan şirket temsilcinin koltuğundan gelir (W3L). Fiyatlı mod: yürürlükte liste varsa. Adetli mod: liste yoksa. Tutar liste para biriminde tutulur; yerel karşılık kur anlık görüntüsüyle gösterilir. |
| K-O5 | D1–D16 (kullanıcı kabulü). Özellikle: varsayılan her zaman yönetici onayı (kiracı ayarı); "Değişiklik iste" → imza yeniden alınır; bağlantı 48 sa, **oturum açtırır** (oturumsuz onay yok); iptal gerekçeli ve depoya / eczaneye bildirimli; reddedilen sipariş kopyalanabilir; MF oranla denetlenir; en özel kural kazanır; depo cevabını satış operasyonu işaretler; fiyat gönderimde sabitlenir; liste yayımlama ve kural değişikliği **dört göz**; kur tablosunu satış operasyonu girer; numara taslakta verilir (`MG-ORD-…`, RCS-001); onay Görev Merkezi + e-posta bağlantısı, onaycıyı Platform'un yönetici çözümleyicisi bulur (PLT-NOTIFY). |

---

## §36.1 Agent Prompt — W6-PACK (paste-ready · yalnız belge: iki modül paketi)

```text
@[.antigravity/agents/module-pack-author.md]
WP: WP-VW-W6 · W6-PACK — MOD-0168 Order Capture ve MOD-0156 Price Lists & Discount Guardrails modül paketleri (ilk kapsam: eczane ön siparişi + fiyat listesi / MF-iskonto kuralları ilk dilimi), kararlar KİLİTLİ
Repository: C:\tmp\vw-w6-pack (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-pack · tek commit, push YOK. Kullanıcının C:\Users\user\Desktop\ERP-vNext checkout'una DOKUNMA. Yalnız belge; kod YOK.

ÖNCE OKU (sırayla): execution/domains/commercial-suite/work-packs/WP-VW-W6-pharmacy-pre-order.md (K-O1…K-O5) · mockups/visit-workspace/DESIGN-GAP-W6-2026-10-19.md (tamamı: ekranlar §1, kurallar §2, farklar §3, standartlar §4, D1–D16 §5, karar §7, iş bölümü taslağı §6) · mockups/visit-workspace/BRIEF-W6-pharmacy-pre-order.md · ROADMAP-visit-workspace.md K-W9 / K-W10 · docs/analysis/contracts/product-master-bundle.openapi.yaml (SKU seviyeleri, getSku, lifecycleStatus) · execution/domains/commercial-suite/domain-config.md + crm-rbac-integration-plan.md (§2 commercial.* ad alanı) + crm-sor-boundary.md · emsal paket biçimi: module-packs/MOD-0155-FU02-visit-report.md (künye alanları, §0–§21 bölümleri, D-soru kilitleme deseni) · .antigravity/rules/permission-key-standard.md §4 · WP-VW-W5-notifications.md (K-N1…K-N9: e-posta altyapısı, yönetici çözümleyicisi) · WP-VW-W3L-layered-evidence.md K-L3 (koltuk → tüzel kişilik).
ORTAM: python3 yok → `py -3`.
NE:
(1) Kimlik ön kontrolü (fail-closed): `py -3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0168 --name "Order Capture"` ve `… --check-id MOD-0156 --name "Price Lists & Discount Guardrails"`; çıktıyı rapora koy. Biri sıfır dışı dönerse DUR.
(2) execution/domains/commercial-suite/module-packs/MOD-0168-order-capture.md: MOD-0155-FU02 künye ve bölüm düzeniyle (domain commercial-suite, service Diten.CrmService, shell tenant, golden_reference DEV-0001 liste yüzeyleri için). İlk kapsam = eczane ön siparişi (DESIGN-GAP §1 A/B/C/E, §2 kurallar, §6 W6-BE-a + W6-BE-c). Kayıt sahipliği: PreOrder (+ satırlar, olay kaydı, onay, iletim, iptal, depo cevabı), kiracı ön sipariş ayarları, onay bağlantı belirteci. Tüketilenler: MOD-0156 değerlendirme API'si + fiyat çözümü, MOD-0149 kurum (eczane / depo = Account id), MDM ürün ana verisi (SKU) + tüzel kişilik, MOD-0288 / Platform koltuk + yönetici çözümleyicisi, Platform Notifications (W5 iç uç), Workflow / Görev Merkezi (W3-CFG deseni), MOD-0262 ikili depo (imza, PDF), RCS-001 kayıt kodu. Durum makinesi §12'de (DESIGN-GAP §2 + §3 önerileri: Onaylandı ayrı durum, iletim başarısız + yeniden deneme). Kapsam dışı: bizim satış siparişimiz / faturalama (Program T3), EDI, ATP / stok. MOD-0168 kaydındaki "SoR boundary with Finance / Order-Mgmt = EA-TBD" §2'de açık nokta olarak kalır.
(3) execution/domains/commercial-suite/module-packs/MOD-0156-price-lists-discount-guardrails.md: aynı düzen. İlk dilim: fiyat listesi (tüzel kişilik × ülke × para birimi; tür Resmî / Şirket listesi; kademeler depo / eczacı alış / halka satış; KDV; Taslak / Planlandı / Yürürlükte / Geçmiş; içe aktarma hazırlık alanı + fark + yayımlama, dört göz), MF / iskonto kuralları (tüzel kişilik × SKU × isteğe bağlı segment / dönem; yumuşak / sert; en özel kazanır; MF oranı ve birleşik formül §2), değerlendirme API'si (gerekçe kodu + parametre döner, metin istemcide 7 dil), kural deneme, değişiklik geçmişi, ara kur tablosu (tüzel kişilik, kaynak, tarih; MOD-0277 ileride). Pricing approvals = liste / kural yayımlama onayı; sipariş onayı MOD-0168'de.
(4) Her iki pakette: D-soruları KİLİTLİ olarak yaz (kaynak: DESIGN-GAP §5 D1–D16 + §7, K-O1…K-O5; yeni D-sorusu icat etme — gerçek bir boşluk bulursan "AÇIK" olarak listele ve DUR değil rapor et); izin anahtarları `commercial.<kaynak>.<eylem>` (PKS-001 3 segment, lowercase-dotted) — okuma / yazma / onay / yayımlama ayrımıyla; gateway rotaları ve rota kapsama testi şartı (§15; W3L / W5 dersi: yeni uç = ocelot yöntemi + kapsama satırı); 7 dil; UI standartları (DESIGN-GAP §4: Golden Compact, _TabCard, --bs-primary, .diten-field, h5 başlık, RTL); test beklentileri (sabotaj kanıtlı); ön sipariş için mobil not yeri (W8).
(5) Künye: status: draft · runtime_code_allowed: false · flip_approved_by: boş — kullanıcı paketleri okuyup onaylayınca CT `ready-for-dev`'e çevirir.
(6) .antigravity/rules/permission-key-standard.md §4 tablosuna tek satır: `commercial.*` → commercial-suite (CPQ / O2C / Service / BizDev; MOD-0156…0163, 0168…0172, 0282…0284), kullanıcı kararı 2026-10-19 (WP-VW-W6 K-O2). Kullanıcı bu tek satırlık korunan yol değişikliğini onayladı; .antigravity altında BAŞKA değişiklik YOK. `python3 .antigravity/scripts/sync_adapters.py` gerekiyorsa (çekirdek blok değişmiyor) koşma, raporla.
(7) Kayıt tablosu: MOD-0168 ve MOD-0156 satırlarına paket yolu (aktif paket sütunu / notu, mevcut desenle); `commercial.*` ad alanı notundaki "reservation pending" → "approved by owner 2026-10-19 (WP-VW-W6 K-O2)".
KORU/YAPMA: kod, test kodu, gateway YOK; başka modül paketlerine dokunma (yalnız gerekiyorsa bağımlılık satırında atıf); yeni MOD / CAND-CAP icadı YOK.
DOĞRULA: `py -3 .antigravity/scripts/verify_module_id.py . --check-all` → 0 hard; `py -3 scripts/check_pack_nav_keys.py` → 0 yeni (paketlerde menü anahtarı tanımlanıyorsa); mimari TRX + `py -3 scripts/known_failures.py check` → taban sınıfları (DocsPath özeti değişirse yaz); iki paketin künyesi `ct_queue` / `prompt_gate` okuyabileceği biçimde (id = dosya adındaki MOD, tek künye id).
Commit: "docs(crm): WP-VW-W6 W6-PACK — MOD-0168 Order Capture (pharmacy pre-order first scope) and MOD-0156 Price Lists & Discount Guardrails (price lists, MF / discount rules, evaluation API, interim FX) module packs, decisions locked; commercial.* namespace recorded in PKS-001 §4" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>.
TESLİM (Türkçe, kısa, §22): Biten · Açık · Sizden istenen; her paket için bölüm listesi + kilitli D-soruları + varsa AÇIK noktalar; izin anahtarları tablosu; doğrulama çıktıları. K13.
```

## §37 CT kabul — W6-PACK (2026-10-20) + kullanıcı kararları
Ajan commit'i `c7d3251df` → test dalı. İki paket (MOD-0168 878 satır, MOD-0156 816 satır), PKS-001 §4 `commercial.*` satırı, kayıt tablosu paket yolları.
- **CT kontrolü:** `check-all` 0 hard; `check_pack_nav_keys` 0 yeni; mimari = taban sınıfları. Paket bölümleri MOD-0155-FU02 düzeninde (§0–§22); D1–D16 + K-O1…K-O5 kilitli; §21 denetlenen olay tabloları her yazma komutunu içeriyor.
- **Kullanıcı kararları (2026-10-20) → paketlerde §20.3:** onaycı okuma ucu (PLT-W6) ✔ · numara CRM içi `ORD` sayacı + RCS-001 §3 `ORD` satırı ✔ (bu commit, kullanıcı onaylı korunan yol) · PDF eki iç uca (PLT-W6) ✔ · ilk sürüm segmentsiz ✔ · **denetim: W6 komutları denetimli yazılır → önkoşul F8-a (AUD-001 K4 ortak iletici, CRM yol `b`)** · tek yürürlükte liste ✔ · öncelik segment+dönem > segment > dönem > genel ✔ · GTIN eşleştirme v1.1 ile ✔ · **EK-1: fiyat listesi elle de eklenebilir.**
- **Durum:** paketler `draft` — `ready-for-dev` çevirisi kullanıcının ayrı onayıyla (kontrol listesinin son maddesi).
- **Sıra:** PLT-W6 ∥ F8-a → W6-BE-a → W6-BE-b ∥ W6-BE-c → W6-WEB-a ∥ W6-WEB-b.

**2026-10-20 — paketler `ready-for-dev` (kullanıcı onayı):** MOD-0168 ve MOD-0156 `status: ready-for-dev`, `runtime_code_allowed: true`. F8-a kapsamı (kullanıcı): ortak iletici + CRM benimsemesi + yakın dönemde denetimsiz gelen CRM komutları (W3E QR, W3L kapsam yaz / kaldır, W5 bildirim ayarı) da işaretlenir; eski ~47 komut Faz 8 (F8-b) olarak en sonda kalır.

---

## §36.1 Agent Prompt — PLT-W6 (paste-ready · Platform + Gateway; F8-a ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · PLT-W6 — Platform: kullanıcının yöneticileri okuma ucu (W6 onaycı adı / Görev Merkezi adayları) + iç olay ucuna PDF eki (depo / eczane e-postası)
Repository: C:\tmp\vw-w6-plt (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-plt · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Platform + Gateway sahibi kullanıcı (onay 2026-10-20, bu istemin kapsamı); kapsam dışı gerekirse DUR ve sor (XMC-001).

ÖNCE OKU: execution/domains/commercial-suite/module-packs/MOD-0168-order-capture.md §20.3 (AÇIK-2, AÇIK-4) + §7 bağımlılıklar · WP-VW-W3L-layered-evidence.md K-L8 + §37 PLT-ORG-SEAT ve canlı E4 notu (yeni Platform kiracı yolu İKİ listeye girer: Gateway `TenantResolutionMiddleware.IsTenantScopedOrgPath` VE `services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs` IsTenantScopedOrgPath; ocelot rotası + kapsama testi) · WP-VW-W5-notifications.md K-N1, K-N2 + PLT-NOTIFY sonucu (ILineManagerResolver, iç olay ucu `POST api/internal/notifications/events/dispatch`, sözleşme eki) — PLT-NOTIFY test dalında değilse DUR ve sor · Platform `OrganizationSeatsController` / `OrganizationSeatReader` (K-L8 deseni) · `NotificationDispatchAttachment` (mevcut ek desteği ve sınırları).
NE:
(1) GET api/platform/organization/seats/{userId}/line-managers — yetki K-L8 ile aynı (kendisi anahtarsız; başkası `platform.organization-seats.read`; okumadan önce 403). Yanıt `Response` zarfında: { userId, resolution: LineManager | ManagerChain | None, positionId?, positionCode?, positionName?, managers: [{ userId, displayName, positionId, positionCode }] } — e-posta YOK. ILineManagerResolver'ı kullanır (kural kopyalanmaz). `[LoginOnly]` gerekçesi + ExpectedLoginOnly listesi (PLT-ORG-SEAT emsali). Gateway: rota (GET) + kapsama testi; kiracı yolu iki listede zaten `/api/platform/organization/seats` öneki — alt yolun kapsandığını gerçek middleware'li HTTP testiyle göster.
(2) İç olay ucuna isteğe bağlı `attachments[]`: { fileName, contentType, contentBase64? | documentRef? (MOD-0262) } — mevcut NotificationDispatchAttachment sınırları (boyut, tür) uygulanır; aşımda 400 ve gönderim yok; ek yeniden denemede korunur; yanıtta ek içeriği dönmez.
KORU/YAPMA: mevcut uçların davranışı aynı; timesheet onaycı davranışı aynı; localhost:27017 YOK; gerçek e-posta YOK.
DOĞRULA: yeni testler (kendisi / anahtarla / anahtarsız 403 + sıfır okuma / başka kiracı yok / boş koltuk → zincir / yönetici yok → None / gerçek middleware ile tenant_user 200, platform admin reddi; ek: sınır içi gönderilir, aşım 400, yeniden denemede ek aynı); 3 sabotaj (.bak + touch, geri al + md5): yetki denetimi kaldır → kırmızı; Platform.Common listesinden seats öneki çıkar → kırmızı; ek boyut sınırı kaldır → kırmızı. Platform Application tam takım (DITEN_TEST_MONGOD + -o services/Diten.Platform içinde; taban aynı baş) + LoginOnly bekçisi (taban: ESignature satırları) + Gateway tam takım + mimari (known_failures.py).
Commit: "feat(platform): WP-VW-W6 PLT-W6 — line managers of a user (seat endpoint sibling, same authority rule, no e-mail) and attachments on the internal event dispatch" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — F8-a (paste-ready · Building.Blocks + CRM + Platform; PLT-W6 ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · F8-a (= ROADMAP Faz 8 AUD-CRM-1 ilk parça) — AUD-001 K4 ortak denetim iletici + işaret `Diten.Building.Blocks` içinde; CRM benimser (yol b: merkezi günlüğe iletim); yakın dönemin denetimsiz CRM komutları işaretlenir; W6 komutları için hazır zemin
Repository: C:\tmp\vw-f8a (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-f8a · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.Building.Blocks/** (yalnız ekleme), services/Diten.CrmService/**, Platform'da yalnız CRM denetim kategori / işlem tanımları (gerekiyorsa), tests/architecture/audit-ledger/Diten.CrmService.md. Diğer dokuz servisin benimsemesi YOK (yalnız ileride kullanabilecekleri sözleşme). Onay: kullanıcı 2026-10-20 (denetim kayıtları yapılsın; F8-a kapsamı). .antigravity/** DEĞİŞMEZ.

ÖNCE OKU: .antigravity/rules/audit-trail-standard.md (tamamı; özellikle §1, §4.2–4.3, §5 yol a/b/c ve kanıt kuralları, K4 kararı — ortak iletici, servis başına kopya YASAK, K8 — anahtar boşken sessiz değil, §8 ölçen testler) · tests/architecture/audit-ledger/README.md + Diten.CrmService.md + CT-DECISIONS.md · tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandard/** (işaret / pipeline tanıma; işaret ve davranış Building.Blocks'ta tanımlı olabilir, davranışın kaydı servisin kendisinde aranır) · MDM emsali (yol b): services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Audit/IAuditableCommand.cs, IAuditDetailsProvider.cs, IPlatformAuditForwarder.cs, Behaviors/AuditForwardingBehavior.cs, Api/Audit/PlatformAuditForwarder.cs · Platform `/api/internal/audit/append` (InternalAuditController) + kategori / işlem doğrulaması · execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md (ready-for-dev) + MOD-0021-FU01 (review — güvenilir kaynak belirteci; bu işte kullanılmaz, raporda ilişkisini yaz) · CRM: Infrastructure/Audit/HttpCrmAuditPublisher.cs, Logging*AuditPublisher, IVisitEvidenceAuditPublisher.
NE:
(1) Building.Blocks: `IAuditableCommand` işareti + `IAuditDetailsProvider` (isteğe bağlı ayrıntı) + `AuditForwardingBehavior<TRequest,TResponse>` (MediatR pipeline; başarı / ret / hata ayrı — §4: reddedilen komut da kaydedilir) + `IPlatformAuditForwarder` sözleşmesi + HTTP uygulaması (`POST /api/internal/audit/append`, X-Internal-Api-Key, kiracı, aktör, kaynak modül, nesne türü / kimlik / sürüm, korelasyon; sır / gereksiz kişisel veri YOK — §3). K8: anahtar / adres boşsa başlangıçta uyarı + çalışma anında atlanan her kayıt uyarı ve sayaç (sessiz değil). MDM'nin kendi kopyasına DOKUNMA (ileride ortak olana geçer — raporda not).
(2) CRM benimsemesi: davranışın kaydı CRM DI'da (pipeline), iletici yapılandırması (Platform taban adresi + AuthService:InternalApiKey; geliştirme ortamında açık), kaynak modül komut başına (komutun modülüne göre: MOD-0155 / MOD-0162 / MOD-0363 …). Mevcut I*AuditPublisher yayıncıları DEĞİŞMEZ (F8-b).
(3) İşaretlenecek komutlar (kullanıcı kapsamı): taban dalda `py -3 scripts/known_failures.py check` çıktısının AuditTrail "YENİ-AYRINTI" listesindeki CRM komutları — W3E QR toplu komutları, W3L `UpsertVisitEvidencePolicyScopeCommand` / `ArchiveVisitEvidencePolicyScopeCommand`, W5 `UpdateVisitNotificationSettingsCommand` (listeyi ölçerek kesinleştir). Her biri IAuditableCommand + anlamlı ayrıntı.
(4) Defter: Diten.CrmService.md'de yeni iz satırı (yol `b`, tür `işaret`, belirteç `IAuditableCommand`). Bir izin kabulü CT kararıdır: CT-DECISIONS.md'ye eklenecek satırı TASLAK olarak raporda ver, dosyaya YAZMA; sabitlenmiş sayılar / AcceptedTrails testi gerekiyorsa değişikliği raporda göster, CT uygular.
(5) Platform: CRM'in gönderdiği kategori / işlem değerleri Platform'da tanımlı değilse (standart §5 notu) tanımla — yalnız CRM satırları.
KORU/YAPMA: hiçbir komutun iş davranışı değişmez (denetim hatası komutu düşürür mü — §4.3 kategorisine göre; iş kayıtları için "en iyi çaba, sessiz değil"); diğer servislere benimseme YOK; localhost:27017 YOK.
DOĞRULA: Building.Blocks birim testleri (başarılı / reddedilen / hata kaydı, ayrıntı, sır alanı yok, anahtar boş → uyarı + sayaç, HTTP gövdesi); CRM: işaretli bir komut gerçek pipeline'da tek ekleme çağrısı yapar (sahte HTTP işleyici), işaretsiz komut yapmaz; mimari: AuditTrail testinde CRM'in "yeni denetimsiz" listesi taban kayıtlı parmak izine döner (known_failures.py: YENİ-AYRINTI → BİLİNEN olmalı) — olmuyorsa nedenini yaz; 3 sabotaj: pipeline kaydı CRM DI'dan çıkar → kırmızı; bir komuttan işaret kaldır → mimari kırmızı; anahtar boşken uyarı satırı sil → kırmızı. CRM Application tam takım (-o repo içi; taban aynı baş) + Building.Blocks testleri + MDM build (etkilenmediğini göster) + mimari.
Commit: "feat(audit): WP-VW-W6 F8-a — AUD-001 K4 shared audit forwarder and marker in Building.Blocks (not silent when unconfigured), adopted by CRM (path b), recent CRM write commands marked" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22): CT-DECISIONS taslağı + sabit sayı değişiklikleri ayrı başlıkta. K13.
```

## §37 CT kabul — F8-a E2 ACCEPTED (2026-10-20 · canlı denetim kaydı kanıtı E4'te)
Ajan commit'i `1db2d3811` → test dalına cherry-pick (W5-FIX'in üstüne, çakışmasız).
- **Yapılan:** `Diten.BuildingBlocks.Auditing` — `IAuditableCommand` (kategori / işlem / nesne türü / kaynak modül zorunlu), `IAuditDetailsProvider`, `AuditForwardingBehavior` (başarı / ret / hata ayrı; 401 / 403 → Denied; hata cümlesi değil kodu; kayıt düşse de komut sonucu değişmez — iş kaydı "en iyi çaba, sessiz değil"), HTTP iletici `POST /api/internal/audit/append` (X-Internal-Api-Key), K8 uyarı + sayaç. Sır adlı anahtarlar `[REDACTED]`. CRM: davranış `AddApplication` en dışta, iletici `PlatformService:BaseUrl` + `AuthService:InternalApiKey`, kiracı sunucu tarafı. İşaretli 8 komut (W3E QR ×5 — payload kayda girmez; W3L kapsam ×2; W5 bildirim ayarı — adres değil alan adları). Platform `AuditCategory.Crm = 18`. Defter: `crm-merkezi-iletim | b | işaret | IAuditableCommand`. MDM'nin kendi kopyası değişmedi (ortak bloğa geçiş ayrı iş).
- **CT uyguladı:** `AuditTrailStandardTests.AcceptedTrails` +1 (`Diten.CrmService`, `crm-merkezi-iletim`, `b`, `IAuditableCommand`); `CT-DECISIONS.md` satırı (2026-10-20); `PinnedCounts` değişmez. CI satırları (`service-tests.yml` `bb-auditing` matrisi + `run_phase1_gates.sh` derleme) **onaylandı** — CiServiceTestsGuard gereği; CODEOWNERS kapsamında olduğu için main PR'ında sahip onayı ister.
- **CT kontrolü:** CT sabotajı (ajanın yapmadığı): sır maskelemesi kaldırıldı → 4 test **kırmızı** (`A_secret_named_key_never_carries_its_value`, `Every_secret_shaped_key_is_redacted…` ×3); md5 geri. Building.Blocks 33/0; CRM 2940/1 (yalnız bilinen VisitPlanningFix2 -o); MDM build 0 hata; mimari 127/17 — `known_failures.py`: **BİLİNEN=2** (AuditTrail `EveryWriteCommand` artık main'in kayıtlı parmak iziyle aynı), YENİ-AYRINTI 6→5 (yalnız DocsPath dal özeti), YENİ=10 ortam; `AcceptedTrails` yeşil.
- **Açık:** canlıda `audit_events` kanıtı (fleet açıkken W3L kapsam kaydı → Platform denetim günlüğü) E4'te; `[HasPermission]` ile denetleyicide reddedilen istekler MediatR'a ulaşmadığı için kayda düşmez (standart §8 notu); MOD-0021-FU01 güvenilir kaynak belirteci geldiğinde iletici arayüzü arkasında değişir. F8-b (eski ~47 CRM komutu + I*AuditPublisher yayıncıları) en sonda.

## §37 CT kabul — PLT-W6 E2 ACCEPTED (2026-10-20)
Ajan commit'i `84f6d0d51` → test dalına cherry-pick (W5-FIX / F8-a / W5-WEB'in üstüne, çakışmasız). Platform + Gateway.
- **Yapılan:** (1) `GET api/platform/organization/seats/{userId}/line-managers` — yanıt `{ userId, resolution, positionId?, positionCode?, positionName?, managers: [{ userId, displayName, positionId, positionCode }] }`, e-posta yok; zincir PLT-NOTIFY `ILineManagerResolver`'dan, adlar `IUserDisplayNameResolver` (toplu, Auth kapalıysa adsız döner); yetki koltuk ucuyla ortak `OrganizationSeatAuthority` (kendisi anahtarsız, başkası `platform.organization-seats.read`, okumadan önce 403); `[LoginOnly]` + bekçi listesi; ocelot GET rotası + kapsama; iki kiracı listesinde `seats` öneki alt yolu kapsıyor — gerçek ara katmanlı HTTP testi (tenant_user 200, platform admin 403). (2) İç olay ucuna `attachments[]` — `{ fileName, contentType, contentBase64 | documentRef (MOD-0262, gövdedeki kiracıda) }`; en çok 5 dosya, toplam ≤ 256 KB (kalıcı ek sınırı → yeniden denemede aynı ek); yol karakterli ad / iki ya da sıfır kaynak / bozuk base64 / izin dışı tür → 400, gönderim yok; tür izin listesi `application/pdf`, `image/png`, `image/jpeg` (yeni kural — kabul).
- **CT kontrolü:** CT sabotajı (ajanın yapmadığı): dosya adındaki yol karakteri denetimi kaldırıldı → `A_malformed_attachment_is_400_and_nothing_is_sent(shape: "path")` **kırmızı**; md5 geri. Birleşik ağaçta hedefli Platform (iç uç + yöneticiler + yeniden deneme + sözleşme) 52/0; Gateway 280/0; mimari 127/17 (BİLİNEN=2, YENİ-AYRINTI=5 — F8-a sonrası taban). Ajan: Platform tam takım 8774/24 = taban 8750/24 (aynı 24 — bu koşu düzeninde kaynak yolu / Mongo kilit testleri ortam kırmızısı), 3 sabotaj kırmızı (biri test zayıflığını yakaladı → test mutlak 256 KB ile yeniden yazıldı).
- **Açık:** `documentRef` aynı kiracıdaki her MOD-0262 içeriğini ekleyebilir (iç anahtarla korunuyor; kapsam sınırı istenirse ayrı karar). W5-FIX2 aynı serviste değişiklik yapacak — onu bu başın üstüne alırken birleştirme CT'de.
- **W6 kod önkoşulları TAMAM:** F8-a ✔ · PLT-W6 ✔ · paketler ready-for-dev ✔ → W6-BE-a.

**Sıra değişikliği (CT, 2026-10-20):** MOD-0156 paketi §1.1 / §3 süreç içi arayüzleri (`IPriceResolver`, `IPricingEvaluator`, `IFxRateReader`) MOD-0168'in tükettiği yüzey olarak tanımlıyor → önce **W6-BE-b (MOD-0156 backend)**, sonra **W6-BE-a (MOD-0168 sipariş)**, sonra W6-BE-c (belge + e-postalar), sonra W6-WEB-a ∥ W6-WEB-b.

## §36.1 Agent Prompt — W6-BE-b (paste-ready · CRM: MOD-0156 Price Lists & Discount Guardrails backend, ilk dilim)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-BE-b — MOD-0156 Price Lists & Discount Guardrails backend (ilk dilim): fiyat listesi (elle + içe aktarma) + dört göz yayımlama, MF / iskonto kuralları + değerlendirme, ara kur tablosu, değişiklik geçmişi, süreç içi arayüzler (MOD-0168 tüketir), API + Gateway
Repository: C:\tmp\vw-w6-beb (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-beb · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/**, gateway (rota + kapsama testi), Platform `CrmManifestProvider` (yalnız izin anahtarları), BRD seti gerekiyorsa yalnız katalog önerisi (yükleme YOK). Web YOK (W6-WEB-b).

MODÜL PAKETİ (yetki kaynağı, ready-for-dev): execution/domains/commercial-suite/module-packs/MOD-0156-price-lists-discount-guardrails.md — TAMAMINI oku; özellikle §0.2 yer, §1.1, §3 nesneler + süreç içi arayüzler, §4.1–§4.7 normatif kurallar (tek yürürlükte liste, kademeler, KDV, içe aktarma hazırlık alanı + GTIN / SKU eşleştirme, kural önceliği, MF oranı + birleşik formül, dört göz, FxRate, değerlendirme API'si, değişiklik günlüğü), §5 repo kapsamı, §10 backend düzeni, §12 durum makineleri, §13 doğrulama + hata yolları, §14 yetki (`commercial.*`), §15 Gateway / API, §16 kabul ölçütleri (backend olanlar), §17 test beklentileri, **§20.3 KARARLAR (paketin önceki açık maddelerini geçersiz kılar) + EK-1 elle fiyat listesi**, §21 denetlenen olaylar.
EK KURALLAR (CT, 2026-10-20):
- Denetim: **her yazma komutu `IAuditableCommand`** (F8-a ortak iletici, `Diten.BuildingBlocks.Auditing`; kategori Crm, kaynak modül MOD-0156); §21'deki `aday` yollar yerine bu işaret. Mimari AuditTrail testinde yeni denetimsiz komut OLMAMALI (known_failures.py: BİLİNEN kalır, YENİ-AYRINTI gelmez).
- İlk sürüm **segmentsiz** (§20.3 AÇIK-3): segment alanı veri modelinde durabilir, değerlendirme yalnız dönem / genel; öncelik segment+dönem > segment > dönem > genel.
- Ürün kimliği product-master v1.1 SKU (`docs/analysis/contracts/product-master-bundle.openapi.yaml`): `skuId` + çözülen seviye; `Retired` reddedilir; MDM çağrısı mevcut CRM MDM istemci deseniyle (fail-closed, kullanıcı token'ı).
- Gateway: yeni `api/commercial/...` (ya da paketin §15'te seçtiği) uçlar için ocelot yöntemleri + rota kapsama testi (W3L / W5 dersi: yöntem eksikse kırmızı).
- Saat: enjekte TimeProvider (gerçek saat yok).
NE: paketin W6-BE-b kapsamını (backend + süreç içi arayüzler + API + izinler + gateway) uygula. Süreç içi arayüz imzaları paketteki §4.6 ile birebir; MOD-0168 bunları tüketecek (W6-BE-a) — imzayı değiştirme gereği çıkarsa DUR ve sor.
KORU/YAPMA: CRM'in mevcut modülleri değişmez (yalnız DI kaydı eklenir); Web YOK; gerçek MDM'ye bağlanma (sahte istemci); localhost:27017 YOK (geçici mongod deseni); veri yükleme YOK.
DOĞRULA: paketin §16 / §17 backend ölçütleri ve sabotajları; özellikle: tek yürürlükte liste (yeni yürürlük öncekini bitirir), dört göz (hazırlayan onaylayamaz), içe aktarma: eşleşmeyen / Retired satır yayımlanmaz, elle eklenen satır içe aktarılanla aynı doğrulama (EK-1), değerlendirme: MF 10+2 → %16,7, birleşik 1−(1−i)(1−m) 0,1 yuvarlama, yumuşak / sert, adetli mod (liste yok → fiyat yok, MF + adet kuralları), kur anlık görüntüsü; kiracı izolasyonu (başka kiracı 404); yetki (her anahtar); denetim (her yazma komutu tek kayıt — sahte iletici). En az 4 sabotaj (.bak + touch, md5): tek yürürlükte liste kuralı, dört göz, birleşik formül, kiracı süzgeci. CRM Application tam takım (-o repo içi; taban aynı baş — bilinen VisitPlanningFix2 -o, kararsız PiiMasking), Gateway tam takım, Platform Crm manifest testleri, mimari (known_failures.py — AuditTrail BİLİNEN kalmalı).
Commit: "feat(crm): WP-VW-W6 W6-BE-b — MOD-0156 price lists (manual + import, four-eyes publish, one active list), MF / discount guardrail rules and evaluation, interim FX rates, change log, in-process contracts for MOD-0168, API + gateway; every write command audited" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W6-BE-b E2 ACCEPTED (2026-10-21)
Ajan commit'i `26c9239b9` → test dalına cherry-pick (`2df98aad2` üstüne, çakışmasız). CRM (MOD-0156) + Gateway + Platform manifest; 55 dosya.
- **Yapılan:** fiyat listesi (elle + xlsx/csv içe aktarma; elle kalem içe aktarılanla aynı denetim — EK-1), dört göz yayımlama (hazırlayan karar veremez 403, ilk karar kazanır, iade gerekçeli), (LE, ülke) başına tek yürürlükte liste (yeni liste başladığı gün eskisini bitirir; aşama okumada tarihten; aynı gün ikinci liste 409), yayımlanmış liste düzenlenemez → yeni sürüm + fark; içe aktarma (ürün kodu eşleşmesi; eşleşmeyen / Retired / tekrar / hatalı fiyat ayrı sayılır; yalnız eşleşen satır taslağa, tek dönüşüm; MDM kapalı 503 yazmasız); MF / iskonto kuralları (soft ≤ hard, segment reddi, Retired reddi, dört göz, revizyon / emeklilik, aynı özgüllükte çakışma reddi, yazmasız deneme); değerlendirme yalnız `Evaluation/` (MF 10+2 = %16,7; 1−(1−i)(1−m) 0,1 yuvarlama; soft / hard; adetli mod; kur anlık görüntüsü + kural seti parmak izi); ara kur tablosu (asOf'tan önceki en son); değişiklik günlüğü; §4.6 imzaları birebir; her yazma komutu `IAuditableCommand` (Crm, MOD-0156); 6 kök × 2 ocelot rotası + kapsama; Platform manifest 16 `commercial.*` anahtarı. Workflow sonuç tüketicisine fiyat rotası eklemeli (W3-BE-c deseni; diğer rotalar değişmedi).
- **CT kontrolü:** birleşik ağaçta CRM tam takım (`-o` repo içi, gerçek mongod 27391) 3031 / 2 / 5 atlanan — kırmızılar `-o` yan etkisi: bilinen VisitPlanningFix2 + **yeni S4 yapı testi** (kaynak klasörünü çıktı klasöründen yukarı yürüyerek arıyor; `-o` repo kökünde olunca bulamıyor). Gateway 310/0; Platform CrmManifest 15/0; mimari 123/21 — fazla 4 kırmızı (MongoTestDatabaseGuard ×2, TenantContradictionSite ×2) repo içindeki `.claude/worktrees/*` klasörlerinin taranmasından (ortam); fiyat komutlarından hiçbiri denetimsiz listede değil, AuditTrail BİLİNEN. `[HasPermission]`'sız iki okuma ucu (`pricing-history`, `pricing-contract`) paketin §15 "herhangi biri" kuralı — DoctorInsights deseni, kabul.
- **CT sabotajları (ajanın yapmadıkları):** (1) yayımlanmış liste düzenleme yasağı kaldırıldı → `L7` **kırmızı**; (2) elle kalemde Retired denetimi kaldırıldı → `L2(AL5 … pricing_sku_retired)` **kırmızı**; (3) Mongo `FxRateRepository.GetLatestAsync` sıralaması Descending → Ascending → **YEŞİL kaldı** (test verisinde asOf öncesinde tek kur var) → **TEST BOŞLUĞU**: canlıda aynı çiftin iki kuru olunca en eskisi döner ve sipariş yanlış kurla sabitlenir. Hepsi md5 geri.
- **Ajanın açık noktaları — CT kararı:** GTIN eşleşmesi product-master v1.2'ye kadar kapalı (kabul; BL-584 W-04 takip) · "bugün" = UTC (kabul; kiracı saat dilimi gelince takip) · karar ucunda anında uygulama + okumada uzlaştırma (kabul) · değerlendirme MDM kapalıyken Retired denetimini atlar (kabul; yazmada var) · dosya düzeni toplu (kabul, bölme yok) · rol ataması + `COM-PRICING-PUBLISH` şablonu → W6-CFG · BRD yalnız öneri.
- **Takipler → W6-BE-a ön işi:** FU-W6-FX-LATEST-TEST (Mongo testine asOf öncesinde iki kur + en yeninin döndüğü iddia; CT sabotajı 3 kırmızı olmalı) · FU-W6-S4-PATH (S4 kaynak aramasını `-o` konumundan bağımsız yap: repo kökünden `services/Diten.CrmService/src/...`).

## §36.1 Agent Prompt — W6-BE-a (paste-ready · CRM: MOD-0168 Order Capture — eczane ön siparişi backend · W6-BE-b test dalına alındıktan SONRA)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-BE-a — MOD-0168 Order Capture backend (ilk kapsam: eczane ön siparişi): PreOrder + satırlar + olay kaydı, durum makinesi, MOD-0156 fiyat / kural değerlendirmesi, imza, onay (Workflow / Görev Merkezi + oturum açtıran tek kullanımlık bağlantı), ORD numarası, kiracı ayarları, liste / önizleme API'si, görünürlük, API + Gateway
Repository: C:\tmp\vw-w6-bea (worktree, test/crm-content-visit-e2e güncel başı — W6-BE-b içinde) · Branch: wp/vw-w6-bea · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/**, gateway (rota + kapsama testi), Platform `CrmManifestProvider` (yalnız izin anahtarları). Web YOK (W6-WEB-a); PDF + e-postalar YOK (W6-BE-c).

ÖN KOŞUL: W6-BE-b (MOD-0156 süreç içi arayüzleri `IPriceResolver`, `IPricingEvaluator`, `IFxRateReader`) test dalında olmalı — değilse DUR ve sor.
MODÜL PAKETİ (yetki kaynağı, ready-for-dev): execution/domains/commercial-suite/module-packs/MOD-0168-order-capture.md — TAMAMINI oku; özellikle §0.2 yer, §1.1, §2 sahiplik / yasaklar, §3 nesneler, §4.1 PreOrder, §4.2 hesap ve gösterim (hesap MOD-0156'da), §4.3 ayarlar, §4.4 onay bağlantısı, §4.5 onay (Workflow + yönetici çözümleyicisi), §4.8 görünürlük, §5 repo, §10 backend düzeni, §12 durum makinesi, §13 doğrulama + hata yolları, §14 yetki (`commercial.*`), §15 Gateway / API, §16 kabul ölçütleri (backend olanlar), §17 testler, **§20.3 KARARLAR (AÇIK-2 yöneticiler ucu, AÇIK-3 ORD sayacı, AÇIK-5 segmentsiz, AÇIK-6 denetim) — paketin önceki açık maddelerini geçersiz kılar**, §21 denetlenen olaylar. MOD-0156 paketi §4.6 (değerlendirme sözleşmesi). WP-VW-W6 §37 PLT-W6 (yöneticiler ucu yanıtı).
EK KURALLAR (CT, 2026-10-20):
- Denetim: her yazma komutu `IAuditableCommand` (F8-a; kategori Crm, kaynak modül MOD-0168); AuditTrail testinde yeni denetimsiz komut YOK.
- Onaycı adı / kimliği: Platform `GET api/platform/organization/seats/{userId}/line-managers` (PLT-W6) — Gateway üzerinden kullanıcı token'ıyla; W3L koltuk istemcisi deseni (3 sn + 1 deneme, istisna fırlatmaz; Platform kapalı → onay "yönetici çözülemedi" durumunda bekler ve raporlanır, sipariş gönderimi reddedilmez ama Onay bekliyor'da kalır — paketin §13 hata yollarıyla çelişirse paket kazanır).
- Satan şirket / ülke / para birimi: temsilcinin Platform koltuğu → tüzel kişilik (W3L K-L3 okuyucusu) + MDM LegalEntity (CountryCode, BaseCurrencyCode, kısa kod — ORD öneki; kısa kod alanı yoksa DUR ve raporla).
- ORD numarası: kiracı × şirket öneki × `ORD`, en az 6 hane, sıfırlanmaz, taslakta verilir; eşzamanlı güvenli (atomik sayaç); RCS-001 §3 / §5.8.
- Fiyat gönderimde sabitlenir (D12); fiyatlı / adetli mod MOD-0156'dan; kur anlık görüntüsü siparişe yazılır.
- Onay bağlantısı: belirteç yalnız SHA-256 özetiyle saklanır, tek kullanım, süre kiracı ayarı (varsayılan 48 sa), **oturum açtırır** (oturumsuz onay YOK — D4); süresi dolmuş / kullanılmış bağlantı ayrı durum.
- Ürün kimliği product-master v1.1 SKU (`skuId` + seviye; `Retired` reddedilir).
- Gateway: yeni uçlar için ocelot yöntemleri + rota kapsama testi.
- Saat: enjekte TimeProvider.
ÖN İŞ (CT K13 W6-BE-b, ayrı commit'te değil bu commit'te): (a) FU-W6-FX-LATEST-TEST — `PricingStructureAndMongoTests` M1'e asOf öncesinde iki kur + en yeninin döndüğü iddia (sıralamayı Ascending yapan sabotaj kırmızı olmalı); (b) FU-W6-S4-PATH — S4 kaynak aramasını `-o` çıktı konumundan bağımsız yap (repo kökünden `services/Diten.CrmService/src/...` de aransın).
NE: paketin W6-BE-a kapsamını uygula (§6 iş bölümü: sipariş + onay + ayarlar + liste / önizleme; belge ve e-posta olaylarının yayın noktalarını W6-BE-c için soyut bırak — `IPreOrderNotifier` gibi bir arayüz + no-op / günlük uygulaması, W6-BE-c doldurur).
KORU/YAPMA: CRM'in mevcut modülleri ve MOD-0156 değişmez (yalnız arayüz tüketimi + DI); Web YOK; gerçek Platform / MDM / Workflow'a bağlanma (sahte istemciler); localhost:27017 YOK (geçici mongod deseni).
DOĞRULA: paketin §16 / §17 backend ölçütleri ve sabotajları; özellikle: durum geçişleri (taslak → onay bekliyor → onaylandı / reddedildi / değişiklik istendi → depoya iletildi; iptal gerekçeli; onay bekleyen düzenlenemez), sert sınır aşımı gönderimi engeller, yumuşak aşım onaya gider, kiracı ayarı "sınır içi kendiliğinden" açıkken kural aşımı yoksa doğrudan onaylanır, değişiklik isteğinde imza temizlenir, onay bağlantısı tek kullanım + süre + oturum, ORD sayacı eşzamanlı tekil (gerçek mongod testi), görünürlük (temsilci / yönetici ekibi / operasyon / başka kiracı 404), denetim (her yazma tek kayıt). En az 5 sabotaj (.bak + touch, md5): sert sınır engeli, onay bekleyen düzenleme yasağı, bağlantı tek kullanım, ORD atomikliği, kiracı süzgeci. CRM Application tam takım (-o repo içi; taban aynı baş), Gateway tam takım, Platform Crm manifest, mimari (known_failures.py — AuditTrail BİLİNEN kalmalı).
Commit: "feat(crm): WP-VW-W6 W6-BE-a — MOD-0168 pharmacy pre-order: order + lines + event log, state machine, MOD-0156 evaluation at submit, signature, approval (workflow task + one-time signed-in link), ORD numbering, tenant settings, list / preview API, visibility; every write audited" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W6-BE-a E2 ACCEPTED (2026-10-21)
Ajan commit'i `c9542f453` (taban `dc30bef06`) → test dalına cherry-pick (W5-FIX2 §37 `d6b01d627` üstüne, çakışmasız). CRM (MOD-0168) + Gateway + Platform manifest; 40 dosya.
- **Ön işler:** FU-W6-FX-LATEST-TEST (asOf öncesinde üç kur, en yeni döner — W6-BE-b'deki CT sabotajı artık kırmızı) ✔ · FU-W6-S4-PATH (S4 `-o`'dan bağımsız; CT tam koşusunda yeşil) ✔.
- **DUR + kullanıcı kararları (ajan oturumunda, 2026-10-21):** MDM LegalEntity'de kısa kod alanı yok → (1) ORD öneki kiracı ayarında (LE → 2–5 harf; numara verdikten sonra değişmez / silinmez, 409); (2) ortak `ClaimWorkflowStartRequest`'e isteğe bağlı `CandidatePrincipalIds` (diğer çağıranlar boş — testli); (3) imza MOD-0262 `CrmVisitEvidence` kapsamında (sahip öğe = sipariş); (4) ekip görünürlüğü W5 bölge deseni + çözülen onaycı görür.
- **Yapılan:** PreOrder kökü + yalnız eklenen olay kaydı; geçişler yalnız `PreOrderStateMachine`'de (yapısal test); taslakta satan şirket / ülke / para birimi koltuk → LE → MDM; ORD `MG-ORD-000001` atomik `$inc`, taslakta, yeniden kullanılmaz; gönderimde sunucu kuralları (satırsız / deposuz / imzasız / depo e-postasız / sert aşım → 422 + `L1:kod`), fiyat + kur + değerlendirme gönderimde sabitlenir (D12), onay kuralı `always` | `auto-within-limits` (D1); onaycı PLT-W6 line-managers → Workflow adayı; bağlantı tek kullanım + SHA-256 + süre + oturum; ilk karar kazanır (409), temsilci kendi siparişine karar veremez (403); değişiklik iste → taslak + imza temizlenir; Görev Merkezi kararı olay tüketicisi + okumada uzlaştırma; iptal / kopya / depo cevabı / ayarlar; 19 uç (liste, ayrıntı, bağlam, önizleme, Excel); her yazma komutu `IAuditableCommand` (MOD-0168; belirteç / imza / e-posta denetime girmez); 4 ocelot rotası + kapsama; manifest 11 anahtar.
- **CT kontrolü:** birleşik ağaçta CRM tam takım (`-o` repo içi, gerçek mongod 27391) 3161 / 2 / 5 atlanan — kırmızılar bilinen `PiiMasking` (kararsız) + `VisitPlanningFix2` (`-o`); Gateway 331/0; Platform CrmManifest 16/0; mimari 123/21 (W6-BE-b K13'teki ortam seti; sipariş / fiyat komutlarından hiçbiri denetimsiz listede değil; AuditTrail BİLİNEN). **CT sabotajları (ajanın yapmadıkları):** (1) bağlantı süresi denetimi kaldırıldı → `K3_an_expired_link…` **kırmızı**; (2) temsilci görünürlüğü herkese açıldı → `V1`, `F9`, `F14` **kırmızı**; (3) ORD önek kilidi kaldırıldı → `S2_a_prefix_that_numbered_an_order_is_locked…` **kırmızı**. Hepsi md5 geri.
- **Ajanın açık noktaları — CT kararı:** bağlantı kararı `DecidePreOrderCommand` içinde (tek denetim kaydı, `channel=email-link`) kabul · bağlantı `SubmissionNo`'ya bağlı kabul · iki kök yazımında işlem yerine telafi (önce bağlantı, sipariş yazılamazsa bağlantı silinir) kabul · `read-all` yalnız kapsam genişletir kabul · Platform kapalıyken "unresolved" + sonraki açılışta yeniden çözüm kabul · GTIN v1.2'ye kadar kapalı (W6-BE-b'den).
- **Takipler:** FU-ACCOUNT-PHARMACY-IDS (MOD-0149 Account'ta GLN, vergi no, eczane kodu yok — veri sahibi / CRM Account) · FU-PM-DOSAGE-FORM (product-master v1.1'de dozaj formu yok — MDM) · FU-MDM-LE-SHORTCODE (MDM LegalEntity kısa kod alanı; olunca ORD öneki oradan — XMC-001, MDM sahibine) · W6-CFG: `COM-PREORDER-APPROVAL` (ilk adımda kendi adayı OLMAMALI) + `COM-PRICING-PUBLISH` şablonları, rol ataması, kiracı önek kaydı, yetki betiği.

## §36.1 Agent Prompt — W6-BE-c (paste-ready · CRM + Platform şablonları: depo belgesi PDF + e-posta olayları + iletim)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-BE-c — MOD-0168 depo belgesi (depo nüshası + eczane kopyası, PDF + önizleme), yedi e-posta olayı (Platform iç uç), onaylanan siparişin depoya sistem adımıyla iletimi + başarısız iletimde yeniden deneme
Repository: C:\tmp\vw-w6-bec (worktree, test/crm-content-visit-e2e güncel başı — W6-BE-a içinde) · Branch: wp/vw-w6-bec · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/**, Platform yalnız `CrmManifestProvider` (olaylar + varsa izin) + `NotificationTemplateSeed` (şablonlar) — Platform sahibi kullanıcı (bu istemin kapsamı), gateway (yeni uç varsa rota + kapsama). Web YOK (W6-WEB-a).

ÖNCE OKU: MOD-0168 paketi (ready-for-dev) — §1.1 (5. adım iletim), §4.6 e-posta olayları, §4.7 depo belgesi, §12 durum makinesi (iletim başarısız + yeniden deneme), §13.2 hata yolları, §16 AC-FLOW-8 + AC-DOC-1 + AC-L10N-1, §17 testler, §20.3 KARARLAR (AÇIK-4 ekler), §21 denetlenen olaylar · WP-VW-W6: §37 PLT-W6 (iç uç `attachments[]`: ≤ 5 dosya, toplam ≤ 256 KB, pdf / png / jpeg, `documentRef` MOD-0262), §37 W6-BE-a (`IPreOrderNotifier` no-op'u + iletici yayın noktaları; açık noktalar, takipler) · WP-VW-W5: "Sözleşme eki" + §37 W5-FIX2 (`idempotencyKey`, `queued` yanıtı, 3 sn + 1 deneme, istisna fırlatmaz) · Örnekler: CRM `Infrastructure/VisitNotifications/PlatformNotificationClient.cs` (istemci deseni), `Infrastructure/ContentComposition/Rendering/PdfSharpKnowledgePathRevisionRenderer.cs` (PDFsharp-MigraDoc 6.1.1, CRM'de zaten var), Platform `CrmManifestProvider` NotificationEvents (W5 `VisitReportEvent`) + `NotificationTemplateSeed`.
EK KURALLAR (CT, 2026-10-21):
- Olay kodları TİRESİZ (Platform katalog kuralı, W5 dersi): pakette `commercial.pre-order.approval-requested` gibi yazılanlar → `commercial.preorder.approvalrequested`, `.autoapproved`, `.changesrequested`, `.rejected`, `.transmitteddepot`, `.pharmacycopy`, `.cancelled`. W6-BE-a'da sabit varsa eşitle; Platform kataloğu + CRM sabitleri + test aynı listeyi kullansın.
- Şablonlar 7 dilde (en, tr, fr, es, zh, ar, ru); içerik 06 mockup'ının `sip_*` şablonları + DESIGN-GAP-W6 §3 ekleri. Değişkenler gösterim metni. Boş bölüm konuda yazılmaz (W5-FIX2 F-4 kuralı). Konu = olay + sipariş no + eczane; kişi adı konuya girmez.
- İstemci: W5 deseni — her çağrıda `idempotencyKey` = (sipariş, olay, iletim denemesi) → tek gönderim; 3 sn + 1 deneme; istisna fırlatmaz; e-posta başarısızlığı onay / gönderim / iptali ASLA bozmaz (K-N8); yalnız depo iletim e-postası iletim durumunu belirler.
- PDF: tek şablon iki nüsha (depo nüshası, eczane kopyası) + önizleme aynı şablondan (iki kopya şablon yasak); depo nüshasında etkin indirim % YOK; kendiliğinden onayda "Kendiliğinden onaylandı (kiracı ayarı)"; "Bu bir ön sipariştir; faturayı depo düzenler"; eksik veri alanları (GLN, vergi no, eczane kodu, dozaj formu — §37 W6-BE-a takipleri) boşsa satır / etiket hiç basılmaz ("—" yok); belge dili = kiracı varsayılan dili; metinler 7 dil resx; Arapça sağdan sola düzgün. PDF iletimde üretilir, MOD-0262'ye yazılır (W6-BE-a imza deseniyle aynı kapsam ya da paketin seçtiği; uygun kapsam yoksa DUR ve sor), `Transmission.DepotDocumentRef` / `PharmacyCopyRef` bağlanır; e-postaya `documentRef` ile eklenir (256 KB üstü → iletim başarısız + neden; sessiz kırpma yok).
- İletim: onaylanan sipariş sistem adımıyla iletilir; depo e-postası `queued` yanıtı → iletildi; Platform ulaşılamaz / belirsiz → iletim başarısız + yeniden deneme (enjekte TimeProvider; artan bekleme, üst sınır, yetkiyle elle "yeniden dene" komutu); aynı deneme iki kez e-posta açmaz (anahtar). İletilmiş siparişin iptalinde depo + eczane iptal e-postası (AC-FLOW-8), iletilmemişte yok.
- Denetim: yeni her yazma komutu `IAuditableCommand` (MOD-0168); denetime e-posta adresi ve PDF içeriği girmez. Saat: enjekte TimeProvider. Gerçek Platform / SMTP / MOD-0262'ye bağlanma (sahte istemciler); localhost:27017 YOK (geçici mongod).
NE: paketin W6-BE-c kapsamı (§4.6 + §4.7 + iletim / yeniden deneme + belge önizleme / indirme ucu) — W6-BE-a'nın no-op notifier'ını gerçek uygulamayla değiştir.
KORU/YAPMA: W6-BE-a / W6-BE-b davranışı değişmez (yalnız yayın noktaları dolar); W5 bildirim yolları değişmez; Web YOK.
DOĞRULA: yedi olay doğru alıcı / doğru anda (onay iste, kendiliğinden onay, değişiklik iste, red, depoya iletim, eczane kopyası, iptal); aynı olay + deneme tek gönderim (anahtar); e-posta hatası iş akışını bozmuyor; iletim başarısız → yeniden deneme → başarı; elle yeniden dene yetkili; PDF iki nüsha (depo nüshasında indirim % yok — metin çıkarımıyla), önizleme = PDF şablonu, MOD-0262 referansı bağlı; 256 KB sınırı; 7 dil şablon + resx eksiksiz; Arapça. En az 5 sabotaj (.bak + touch, md5): depo nüshasına indirim % geri, tekillik anahtarı gönderme, e-posta hatasında onayı geri al, iptal e-postasını iletilmemişte de üret, yeniden deneme zamanlayıcısını kaldır. Tam takımlar: CRM Application (-o repo içi; taban aynı baş), Platform (DITEN_TEST_MONGOD + -o services/Diten.Platform içinde; ya da en az Notifications + CrmManifest + NotificationTemplateSeed testleri — tam takım atlanırsa nedenini yaz), Gateway (uç eklendiyse), mimari (known_failures.py — AuditTrail BİLİNEN kalmalı).
Commit: "feat(crm,platform): WP-VW-W6 W6-BE-c — pre-order depot document (depot copy + pharmacy copy, PDF + preview from one template), seven e-mail events through the internal dispatch with idempotency keys, system transmission to the depot with retry; every write audited" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## W6-BE-c DUR (2026-10-21) + kararlar → bölünme: PLT-W6-DOC ∥ W6-BE-c1 → W6-BE-c2
**Ajanın ölçtüğü (kod yazmadan durdu, worktree `C:\tmp\vw-w6-bec` boş):** MOD-0262'ye yazmanın tek yolu `CrmVisitEvidence` kapsamı + oturum açmış kullanıcı token'ı (`visit-evidence.write`; imzayı onaycı okumak için ayrıca `read-all`). Platform'da kullanıcısız (iç anahtarlı) MOD-0262 yazma / okuma ucu yok. İletim ise çoğunlukla kullanıcısız koşar (Görev Merkezi kararı olay tüketicisiyle gelir; otomatik yeniden deneme zamanlayıcıdır) → PDF üretilip yazılamaz, imza okunamaz. Paketteki "uygun kapsam yoksa DUR ve sor" tetiklendi; kullanıcı: önce Platform iç ucu.

**CT kararları (2026-10-21):**
- **Yeni kapsam `CommercialPreOrderDocument`** (ziyaret kanıtı değil, belge): PDF, ≤ 256 KB, sahip öğe = sipariş kimliği. İmza **taşınmaz** (W6-BE-a'daki `CrmVisitEvidence` kaydı kalır); iç okuma ucu CRM'e yalnız kendi iki kapsamını (`CrmVisitEvidence`, `CommercialPreOrderDocument`) açar.
- **Bölünme:** PLT-W6-DOC (Platform iç uçları) ∥ W6-BE-c1 (PDF'e bağlı olmayanlar) → W6-BE-c2 (PDF + MOD-0262 + depo e-postasına ek + iletimin açılması).

**Kullanıcı kararı (2026-10-21) — dış alıcı dili (B):** kiracı varsayılan dili `en` kalır (97c5 kaydı `DefaultLanguage: en`, `Settings.Language: en` — kullanıcı salt-okunur sorguyla doğruladı). **Kendi dili olmayan dış alıcıların (PV / MSL posta kutusu, depo, eczane) e-postası ve depo belgesi satıcı şirketin ülkesinin dilinde:** temsilcinin satıcı tüzel kişiliği → MDM `CountryCode` → desteklenen 7 dilden biri (TR → tr, FR → fr, ES → es, RU → ru, Arapça konuşan ülkeler → ar, CN → zh, diğerleri → kiracı dili; ör. RO → en). İç kullanıcılar (temsilci, yönetici) kendi dil tercihini alır (değişmez). CRM bunu Platform iç ucunun alıcı başına `locale` alanıyla gönderir (Platform değişmez). Tek ortak CRM çözümleyicisi (W5 + W6 aynı kural).

## §36.1 Agent Prompt — PLT-W6-DOC (paste-ready · Platform: MOD-0262 iç anahtarlı yazma + okuma, yeni kapsam; W6-BE-c1 ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · PLT-W6-DOC — Platform MOD-0262 belge deposuna kullanıcısız (iç anahtarlı) yazma + okuma ucu ve yeni `CommercialPreOrderDocument` kapsamı (CRM sistem iletimi PDF yazar, imzayı okur)
Repository: C:\tmp\vw-plt-w6doc (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-plt-w6doc · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Platform sahibi kullanıcı (bu istemin kapsamı). Kapsam: services/Diten.Platform/**, services/Diten.Platform.Common/** (yalnız gerekiyorsa yol listeleri). CRM YOK, Gateway rotası YOK (iç uç).
ÖNCE OKU: WP-VW-W6 "W6-BE-c DUR (2026-10-21) + kararlar" bölümü, §37 PLT-W6 (iç olay ucu deseni: `X-Internal-Api-Key`, kiracı gövdeden, `[LoginOnly]` / iç yol bekçi listeleri, `documentRef`), Platform: `Contracts/DocumentRepository/IContentStorageGateway.cs` (`ContentStorageScope` — yalnız EKLEME, değer adla yansır), `ContentStorageOptions.cs` (kapsam başına dosya kuralı), `API/Controllers/DocumentRepositoryController.cs` + `DocumentRepositoryService` (bugünkü kullanıcı yolu), iç olay ucu denetleyicisi (PLT-NOTIFY) + `TenantResolutionMiddleware` (`/api/internal` atlaması — Platform.Common).
NE:
(1) `ContentStorageScope.CommercialPreOrderDocument` (yeni değer, sonda); dosya kuralı: yalnız `application/pdf`, ≤ 256 KB (yapılandırılabilir), sahip öğe zorunlu (sipariş kimliği).
(2) `POST api/internal/document-repository/objects` — gövde { tenantId, sourceModule, scope, ownerItemId, fileName, contentType, contentBase64 }; yalnız izin verilen (sourceModule, scope) çiftleri: CRM → `CommercialPreOrderDocument` (yazma). Yanıt { documentRef, sizeBytes, sha256 }. Kiracı gövdeden; kiracı yoksa / pasifse 404.
(3) `GET api/internal/document-repository/objects/{documentRef}?tenantId=&sourceModule=` — içerik (tür + bayt). CRM'e okuma yalnız `CrmVisitEvidence` + `CommercialPreOrderDocument`; başka kapsam / başka kiracı → 404 (varlık sızdırma yok).
(4) Her iki uç `X-Internal-Api-Key` (mevcut iç anahtar deseni); kullanıcı token'ı kabul edilmez; `[LoginOnly]` / iç yol bekçi listeleri güncel (mimari bekçi testleri yeşil kalmalı). Yazma Platform'un mevcut belge deposu denetim kaydını üretir (kaynak modül CRM, aktör = sistem). Saat: enjekte TimeProvider.
KORU/YAPMA: bugünkü kullanıcı yolu (DocumentRepositoryController, `CrmVisitEvidence` kuralları) DEĞİŞMEZ (kanıtla); başka kapsam iç uçtan yazılamaz; gerçek S3 / depolama yok (sahte geçit); localhost:27017 YOK.
DOĞRULA: iç anahtarsız / yanlış anahtar 401; token'lı kullanıcı çağrısı reddedilir; izinli olmayan kapsam yazma 403/404; başka kiracının referansı okuma 404; PDF olmayan / 256 KB üstü 400; okuma = yazılan bayt (sha256); denetim kaydı. Sabotajlar (.bak + touch, md5, en az 4): kapsam izin listesi, kiracı süzgeci okumada, boyut sınırı, anahtar denetimi. Tam takım: Platform Application (DITEN_TEST_MONGOD + -o services/Diten.Platform içinde; taban aynı baş), mimari (known_failures.py).
Commit: "feat(platform): WP-VW-W6 PLT-W6-DOC — internal key-only document repository write + read for the CRM system path, new CommercialPreOrderDocument scope (PDF ≤ 256 KB); the user path is unchanged" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W6-BE-c1 (paste-ready · CRM + Platform şablonları + küçük Web: e-posta olayları, dış alıcı dili, iletim çatısı; PDF YOK; PLT-W6-DOC ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-BE-c1 — yedi tiresiz e-posta olayı + 7 dil şablon + Platform istemcisi (tekillik anahtarı), dış alıcı dili (satıcı şirketin ülkesi — W5 + W6 ortak), iletim çatısı + artan beklemeli yeniden deneme + iptal e-postası; PDF ve MOD-0262 YOK (W6-BE-c2)
Repository: C:\tmp\vw-w6-bec (MEVCUT boş worktree, dal wp/vw-w6-bec — önce test/crm-content-visit-e2e güncel başına ileri sar) · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/**, Platform yalnız `CrmManifestProvider` (olaylar) + `NotificationTemplateSeed` (şablonlar) — Platform sahibi kullanıcı (bu istemin kapsamı), Web yalnız FU-W5-PREVIEW-SUBJECT (aşağıda).
ÖNCE OKU: WP-VW-W6 → "W6-BE-c DUR (2026-10-21) + kararlar" bölümü (dış alıcı dili kararı B) + "§36.1 Agent Prompt — W6-BE-c" (EK KURALLAR'ın tamamı geçerli; bu istem onun PDF'siz yarısı) + §37 W6-BE-a · WP-VW-W5: "Karar (kullanıcı 2026-10-21) — konu satırı" + §37 W5-FIX2 · MOD-0168 paketi §4.6, §12, §13.2, §16 AC-FLOW-8, §17, §21.
NE:
(1) W6-BE-c istemindeki yedi tiresiz olay (`commercial.preorder.*`) — Platform kataloğu + 7 dil şablon + CRM sabitleri + gerçek `IPreOrderNotifier` (W5 istemci deseni, `idempotencyKey` = sipariş + olay + deneme, 3 sn + 1, istisna fırlatmaz, e-posta hatası akışı bozmaz). Depo e-postası (`transmitteddepot`) ve eczane kopyası şablonları ek bekleyecek biçimde (ek W6-BE-c2'de bağlanır).
(2) Dış alıcı dili (kullanıcı kararı B): tek CRM çözümleyicisi — satıcı tüzel kişilik → MDM `CountryCode` → desteklenen dil (TR tr, FR fr, ES es, RU ru, Arapça ülkeler ar, CN zh; diğerleri kiracı dili). Kendi dili olmayan e-posta alıcısına iç uçta alıcı başına `locale` olarak gider. W6 (depo, eczane) VE W5 (PV / MSL posta kutusu; temsilcinin koltuğundaki tüzel kişilik) aynı çözümleyiciyi kullanır. İç kullanıcılar (temsilci, yönetici) değişmez. MDM kapalı → kiracı dili (istisna yok).
(3) İletim çatısı: onaylanan siparişin sistem iletimi (`IPreOrderTransmitter` gerçek uygulama) + iletim başarısız durumu + artan beklemeli yeniden deneme (enjekte TimeProvider, üst sınır) + yetkili elle "yeniden dene" komutu; belge üretimi bir port (`IPreOrderDocumentProducer`) arkasında. **Üretim DI'da belge üreticisi W6-BE-c2'ye kadar YOK → iletici kapalı kalır (sipariş bugünkü gibi `approved`'da bekler, sahte "iletildi" yok)**; testlerde sahte üretici ile uçtan uca.
(4) İletilmiş siparişin iptalinde depo + eczane iptal e-postası; iletilmemişte yok (AC-FLOW-8).
(5) FU-W5-PREVIEW-SUBJECT (Web, küçük): W5 bildirim önizlemesinin konu satırı Platform şablonunun konusuyla aynı (olay · hekim adı · aciliyet — kullanıcı kararı); ürün konudan çıkar; boş bölüm yazılmaz. 7 dil resx gerekiyorsa eksiksiz.
Denetim: yeni her yazma komutu `IAuditableCommand` (MOD-0168). Gerçek Platform / MDM yok (sahte); localhost:27017 YOK.
DOĞRULA: yedi olay doğru alıcı / doğru anda; aynı olay + deneme tek gönderim; e-posta hatası akışı bozmuyor; dil: TR şirketi → tr, RO şirketi → kiracı dili (en), MDM kapalı → kiracı dili, iç kullanıcı kendi dili, W5 PV / MSL aynı kural; iletim başarısız → artan bekleme → başarı (sahte üretici); üretici yokken iletici kapalı (sipariş `approved`); iptal e-postası yalnız iletilmişte; önizleme konusu = şablon konusu. Sabotajlar (en az 5): tekillik anahtarı gönderme, e-posta hatasında onayı geri al, dil çözümleyicisini kiracı diline sabitle, iptal e-postasını iletilmemişte de üret, yeniden deneme zamanlayıcısını kaldır. Tam takımlar: CRM (-o repo içi; taban aynı baş), Platform en az Notifications + CrmManifest + NotificationTemplateSeed (tam takım atlanırsa nedeni), Web (VisitNotifications + resx), mimari (known_failures.py).
Commit: "feat(crm,platform,web): WP-VW-W6 W6-BE-c1 — seven pre-order e-mail events with idempotency keys and 7-language templates, external recipients get the selling company's country language (W5 + W6), system transmission frame with retry (document producer arrives in W6-BE-c2), cancellation mail only after transmission; W5 preview subject matches the real one" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

**W6-BE-c2 (PLT-W6-DOC + W6-BE-c1 test dalına alınınca CT yazar):** tek şablondan iki nüsha PDF + önizleme (depo nüshasında indirim % yok), PLT-W6-DOC iç ucuyla MOD-0262'ye yazma + imzayı okuma, depo / eczane e-postasına `documentRef` eki, üretim DI'da üretici bağlanır → iletim açılır.

## §37 CT kabul — PLT-W6-DOC E2 ACCEPTED (2026-10-21)
Ajan commit'i `4ecdb91d3` (taban `cacb33778` = test dalının o anki başı) → test dalına cherry-pick (çakışmasız). Yalnız `services/Diten.Platform/**`, 8 dosya.
- **Yapılan:** `ContentStorageScope.CommercialPreOrderDocument = 6` (sonda; yalnız `.pdf` / `application/pdf`, ≤ 256 KB ayarlanabilir, tür sabit; depolama anahtarı bölümü `commercial-pre-order-documents`; kural kapıda VE depolama geçidinde). `POST api/internal/document-repository/objects` (201 `{ documentRef, sizeBytes, sha256 }`; kapsam yalnız adla; dosya adında yol karakteri yasak; içerik `%PDF-` ile başlar) + `GET api/internal/document-repository/objects/{documentRef}?tenantId=&sourceModule=`. İzin listesi: `CRM` / `MOD-0168` yazma yalnız `CommercialPreOrderDocument`; okuma `CrmVisitEvidence` + `CommercialPreOrderDocument`; izin dışı yazma 403, izin dışı / başka kiracı / başka kapsam okuma 404. `X-Internal-Api-Key` önce (401); `Authorization` başlıklı çağrı 401; LoginOnly iç liste. Yazma denetimli (`StoreInternalRepositoryObjectCommand`, DocumentManagement / Create, aktör System). Kullanıcı yolu değişmedi (testli).
- **CT kontrolü:** birleşik ağaç = ajanın tabanı + bu commit (tam takım ajanınki geçerli: Platform 8852/24 ⊂ taban 8822/25 — fazla taban kırmızısı kararsız `TimesheetNotification T3_04`; mimari 127/17 iki tarafta aynı). CT hedefli koşu (DocumentRepository + CrmVisitEvidence + LoginOnly + InternalNotification, gerçek mongod): 163/2 — 2 kırmızı bilinen ESignature LoginOnly satırları (`InternalESignActionsController.Register`, `ESignatureSamplesController.Image`, `ESignatureSettingsController.ListMeanings`; yeni denetleyici değil). **CT sabotajları:** (1) `%PDF-` içerik denetimi kaldırıldı → `A_file_that_is_not_one_pdf…(not-pdf-bytes)` **kırmızı**; (2) dosya adındaki `/`, `\`, `:` denetimi kaldırıldı → **YEŞİL** (testte yalnız `../depot.pdf` var, o da `..` kuralına takılıyor) → **TEST BOŞLUĞU** → FU-PLT-DOC-FILENAME-TEST (W6-BE-c2 ön işi). md5 geri.
- **Kabul edilen sapmalar:** JSON içinde base64 (MOD-0262 AD-4 istisnası; 256 KB ile sınırlı, kodda gerekçeli) · okumada ikinci kiracı karşılaştırması tek başına testsiz (depo zaten süzüyor; ana koruma sabitleme, sabotajı kırmızı) · her yazma yeni sahip sürümü (W6-BE-c2 aynı deneme için PDF'i yeniden üretmemeli — aşağıda) · şirket kimliği boş (kullanıcısız yol).

## §37 CT kabul — W6-BE-c1 E2 ACCEPTED (2026-10-21)
Ajan commit'i `4377b539d` (taban `3ab593051` = test dalının o anki başı) → test dalına cherry-pick (çakışmasız) → `c47640c4f`. CRM + Platform (manifest + şablon tohumu) + Web; 32 dosya.
- **Yapılan:** yedi tiresiz olay `commercial.preorder.{approvalrequested, autoapproved, changesrequested, rejected, transmitteddepot, pharmacycopy, cancelled}` (Platform manifest + CRM sabitleri + iki taraf aynı listeyi sabitleyen testler); 49 şablon (`NotificationTemplateSeed.PreOrders.cs`; konu = olay · sipariş no · eczane, kişi adı yok, boş bölüm yazılmaz; depo / eczane e-postasında CRM bağlantısı yok, indirim % için şablonda yer yok); `PlatformPreOrderNotifier` (W5 istemcisi, anahtar `preorder:{id}:{olay}:{tur}[:parça]`, e-posta hatası akışı bozmaz); dış alıcı dili `ExternalRecipientLanguages.ForCountry` (TR / FR / ES / RU / CN / Arap Birliği; diğerleri kiracı dili) — W6 sipariş satıcı ülkesinden, W5 PV / MSL temsilci koltuğu → LE → MDM ülkesinden, gönderimde satıra yazılır; iletim çatısı `PreOrderTransmitter` (belge → depo e-postası sonucu belirler → eczane kopyası en iyi çaba), belge üretici portu üretimde YOK → iletim kapalı; yeniden deneme 5 dk × 2ⁿ, üst sınır 6 sa, kiracı `TransmissionRetryMax` bütçesi, bütçe bitince yalnız elle; zamanlayıcı `PreOrderTransmissionHostedService` (kiracılar arası listeler, her siparişi kendi kiracı kapsamında denetlenen `RunPreOrderTransmissionRetryCommand` ile); iptalde depo + eczane yalnız iletilmişse, ayrı e-postalar; FU-W5-PREVIEW-SUBJECT (önizleme konusu = Platform şablonu, 7 dil; tohum kaynağını okuyan Web testi).
- **CT kontrolü:** birleşik ağaç = ajanın tabanı + bu commit → ajanın tam takımları geçerli (CRM 3194/1 = taban 3162/1 `VisitPlanningFix2`; Web 2747/2 = taban `ShipmentDetailsDateNode` ×2; mimari 127/17 aynı; Platform alt kümesi 1003/0). CT hedefli koşu (Commercial + VwW5 + VisitNotification) 266/0. **CT sabotajları:** (1) kiracı yeniden deneme bütçesi yok sayıldı → `R2_the_tenant_budget_ends_the_automatic_retries…` **kırmızı**; (2) eczane kopyası hatası iletim denemesini başarısız saydı → **YEŞİL** → **TEST BOŞLUĞU**: böyle bir gerilemede yeni deneme yeni anahtarla açılır ve **depoya aynı sipariş e-postası ikinci kez gider**. md5 geri.
- **Açık noktalar — CT kararı:** `sourceModule = "CRM"` (Platform üretici listesi MOD-0155'i tanıyor; manifest sahibi adı) kabul — MOD-0168 görünmesi istenirse ayrı Platform satırı (takip değil, ihtiyaç olursa) · e-posta bağlantı yolları `/Commercial/PreOrders/Details/{id}` ve `/Commercial/PreOrders/ApprovalLink?token=…` → **W6-WEB-a bu yolları birebir kullanır ya da CRM sabitini aynı commit'te günceller** · zamanlayıcı yalnız `transmission_failed`'ı alıyor → c2 açılınca `approved`'da bekleyenler kendiliğinden iletilmez → **W6-BE-c2 ön işi** · silinen `LoggingPreOrderNotifier` / `NotWiredPreOrderTransmitter` kabul.
- **W6-BE-c2 ÖN İŞ'ine eklenenler (aşağıdaki istemde):** FU-W6-PHARMACY-COPY-BEST-EFFORT-TEST · FU-W6-APPROVED-BACKLOG.

## §36.1 Agent Prompt — W6-BE-c2 (paste-ready · CRM: depo belgesi PDF + MOD-0262 + e-posta eki + iletimin açılması · PLT-W6-DOC VE W6-BE-c1 test dalındayken)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-BE-c2 — MOD-0168 depo belgesi (depo nüshası + eczane kopyası, tek şablon PDF + önizleme), PLT-W6-DOC iç ucuyla MOD-0262'ye yazma + imzayı okuma, depo / eczane e-postasına `documentRef` eki, üretim DI'da belge üreticisi → iletim açılır
Repository: C:\tmp\vw-w6-bec2 (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-bec2 · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/**, Platform yalnız ön iş testi (aşağıda), gateway (yeni uç varsa rota + kapsama). Web YOK.
ÖN KOŞUL: test dalında §37 PLT-W6-DOC VE §37 W6-BE-c1 olmalı — değilse DUR ve sor.
ÖNCE OKU: WP-VW-W6 → "§36.1 Agent Prompt — W6-BE-c" (EK KURALLAR'ın PDF maddeleri), "W6-BE-c DUR + kararlar" (dış alıcı dili B), §37 PLT-W6-DOC (uç sözleşmesi, `sourceModule = MOD-0168`, okuma izin listesi), §37 W6-BE-c1 (belge üreticisi portu, iletim çatısı) · MOD-0168 paketi §4.7, §16 AC-DOC-1 + AC-L10N-1, §17 · Örnek: `Infrastructure/ContentComposition/Rendering/PdfSharpKnowledgePathRevisionRenderer.cs` (PDFsharp-MigraDoc 6.1.1).
ÖN İŞ (bu commit'te): (b) FU-W6-PHARMACY-COPY-BEST-EFFORT-TEST — eczane kopyası e-postası başarısızken iletim "iletildi" kalır, yeni deneme açılmaz, depoya ikinci e-posta GİTMEZ (CT sabotajında eczane hatasını başarısızlık saymak yeşil kalmıştı; artık kırmızı olmalı). (c) FU-W6-APPROVED-BACKLOG — iletim açıldığında `approved`'da bekleyen ve hiç iletim denemesi olmayan siparişler zamanlayıcıyla kendiliğinden iletilir (W6-BE-c1 zamanlayıcısı yalnız `transmission_failed` alıyordu); testli. (a) FU-PLT-DOC-FILENAME-TEST — Platform `InternalDocumentRepositoryTests` kötü dosya adı vakalarına `dir/depot.pdf`, `dir\depot.pdf`, `c:depot.pdf` ekle (CT sabotajında `/ \ :` denetimi kaldırılınca test yeşil kalmıştı; artık kırmızı olmalı).
NE:
(1) Tek şablon, iki nüsha (depo nüshası, eczane kopyası) + önizleme aynı şablondan (iki kopya şablon yasak): §4.7 içerik; depo nüshasında etkin indirim % YOK; kendiliğinden onayda "Kendiliğinden onaylandı (kiracı ayarı)"; "Bu bir ön sipariştir; faturayı depo düzenler"; eksik alanlar (GLN, vergi no, eczane kodu, dozaj formu) basılmaz; belge dili = dış alıcı dili kuralı (satıcı şirketin ülkesi — W6-BE-c1 çözümleyicisi); metinler 7 dil resx; Arapça sağdan sola; tek PDF ≤ 256 KB (imza görseli küçültülür; aşarsa iletim başarısız + neden, sessiz kırpma yok).
(2) Eczacı imzası W6-BE-a'nın `CrmVisitEvidence` kaydından PLT-W6-DOC okuma ucuyla (kullanıcısız) okunur.
(3) PDF iletimde üretilir, PLT-W6-DOC yazma ucuyla MOD-0262'ye yazılır; `Transmission.DepotDocumentRef` / `PharmacyCopyRef` bağlanır. Aynı iletim denemesinin yeniden denemesinde PDF YENİDEN ÜRETİLMEZ (bağlı ref kullanılır; her yazma Platform'da yeni sürüm açar).
(4) Depo e-postası (`transmitteddepot`) ve eczane kopyası e-postasına `attachments[].documentRef` (PLT-W6); W6-BE-c1 tekillik anahtarı ve dil kuralı aynen.
(5) Üretim DI'da gerçek belge üreticisi → iletim AÇILIR (approved → iletildi / iletim başarısız + yeniden deneme).
(6) Önizleme / indirme ucu (paketin §15'i; yoksa `GET api/commercial/pre-orders/{id}/document?copy=depot|pharmacy`) — görünürlük kuralları W6-BE-a ile aynı; Gateway rotası + kapsama.
Denetim: yeni yazma komutu varsa `IAuditableCommand`; PDF içeriği / e-posta adresi denetime girmez. Gerçek Platform yok (sahte istemci); localhost:27017 YOK.
DOĞRULA: iki nüsha metin çıkarımı (depo nüshasında indirim % yok, eczane kopyasında var; kendiliğinden onay metni; eksik alan etiketi yok); önizleme = PDF şablonu (aynı model); TR şirketi → Türkçe, RO şirketi → kiracı dili; Arapça; 256 KB aşımı → iletim başarısız + neden; Platform yazma hatası → iletim başarısız + yeniden deneme, ikinci denemede PDF yeniden üretilmez; e-postada documentRef; görünürlük (başka temsilci / kiracı 404). Sabotajlar (en az 5): depo nüshasına indirim % geri, yeniden denemede PDF'i yeniden üret, 256 KB denetimini kaldır, belge dilini kiracı diline sabitle, önizleme için ayrı şablon. Tam takımlar: CRM (-o repo içi; taban aynı baş), Platform en az DocumentRepository + Notifications, Gateway (uç eklendiyse), mimari (known_failures.py).
Commit: "feat(crm): WP-VW-W6 W6-BE-c2 — pre-order depot document (depot copy + pharmacy copy, one template for PDF and preview) stored through the internal document repository, attached to the depot and pharmacy mails; transmission switched on" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W6-BE-c2 E2 ACCEPTED (2026-10-21 · Arapça / Çince PDF açık karar)
Ajan commit'i `23deb753f` (taban `b89d563e6` = test dalının o anki başı) → test dalına cherry-pick (çakışmasız) → `674451b8b`. CRM + Platform testi; 25 dosya.
- **Ön işler:** (a) FU-PLT-DOC-FILENAME-TEST ✔ (`dir/…`, `dir\…`, `c:…`; `/ \ :` sabotajı 3 kırmızı) · (b) FU-W6-PHARMACY-COPY-BEST-EFFORT-TEST ✔ (CT sabotajı artık kırmızı) · (c) FU-W6-APPROVED-BACKLOG ✔ (zamanlayıcı `approved` + denemesiz siparişleri 2 dk bekleme payıyla alır).
- **Yapılan:** tek model `PreOrderDocumentModel` + tek kural yeri `PreOrderDocumentRules.Build` + tek işleyici `PreOrderDocumentRenderer` (MigraDoc) — önizleme ve PDF aynı yol; depo nüshasında hiçbir indirim % yok (yalnız modelde); kendiliğinden onay / onaylayan adı + zamanı; "faturayı depo düzenler" iki nüshada; olmayan alan hiç basılmaz; 37 anahtar × 7 dil resx; belge dili satıcı ülkesinden; ≤ 256 KB yoksa `document_too_large` (kırpma yok); imza PLT-W6-DOC okuma ucuyla, okunamazsa `signature_unavailable` + yeniden deneme; `PlatformPreOrderDocumentStore` (iç anahtar, `MOD-0168`, 10 sn + 1); belge ref'leri başarısız denemede de bağlanır → yeniden denemede PDF yeniden üretilmez; depo / eczane e-postasına `attachments[].documentRef`; üretim DI'da üretici → **iletim AÇIK**; `GET api/commercial/pre-orders/{id}/document?copy=depot|pharmacy` (read + W6-BE-a görünürlüğü; rota zaten vardı).
- **CT kontrolü:** birleşik ağaç = ajanın tabanı + bu commit → ajanın tam takımları geçerli (CRM 3213/1 = taban `VisitPlanningFix2`; mimari 127/17 aynı; Platform DocumentRepository + Notifications 1068/0). CT hedefli Commercial 264/0. **CT sabotajları:** (1) imza okunamazken iletimi sürdür (imzasız belge) → `A2_a_signature_that_cannot_be_read_fails_the_attempt…` **kırmızı**; (2) belge ucunda görünürlük yerine düz okuma → `P2_the_preview_follows_the_orders_visibility…` **kırmızı**. md5 geri.
- **Açık — kullanıcı kararı gerekiyor (K-W6-PDF-SCRIPT):** PDFsharp 6.1 Arapça harf birleştirme / sağdan sola dizgi yapmıyor (Arapça PDF okunaksız); Arial'de Çince glif yok (Çince PDF'de karakter basılmıyor). E-postalar (HTML) etkilenmez. GMG'nin AE şirketi (mockup TR / AE) Arapça belge alacağı için gerçek durum. CT önerisi: düzeltme gelene kadar **belge dili ar / zh ise PDF İngilizce basılsın** (e-posta kendi dilinde kalır), kalıcı çözüm FU-W6-PDF-RTL-CJK (font çözümleyici + şekillendirme; F-SCMM-16B-FONT ile birleştirilebilir).
- **Takipler:** FU-W6-PDF-RTL-CJK · FU-W6-SIGNATURE-RESIZE (CRM'de görüntü kütüphanesi yok; büyük imza → `document_too_large`) · FU-PLT-TENANT-LANG-INTERNAL (kiracı dilini kullanıcısız okuyan Platform iç ucu yok; belge düşüş dili `Commercial:PreOrders:Document:FallbackLanguage` = `en`). Kabul: önizleme canlı çizilir (MOD-0262 kopyası okunmaz; model aynı), `copy` yoksa depo nüshası.

## W6-WEB sırası (CT, 2026-10-21)
DESIGN-GAP §6 iki Web işini paralel öngörüyordu; ikisi aynı dosyalara dokunur (Platform `CrmManifestProvider` menü kayıtları, ortak kural iletisi kaynağı `Rule.{code}` — MOD-0156 §11 "tek kaynak tercih edilir") → **sıralı: W6-WEB-a → W6-WEB-b.** Ekran E (belge) `PreOrders` klasöründe olduğu için W6-WEB-a'ya alındı. Kural iletilerinin tek kaynağını W6-WEB-a kurar, W6-WEB-b kullanır. Mockup: `W6 Eczane On Siparis (standalone).html` / `decoded-w6/W6-on-siparis.dc.html` (A–E). Mockup'ta olmayan ekranlar (Ön sipariş ayarları, Kurlar sekmesi) Golden standart form / liste ile yapılır.

## §36.1 Agent Prompt — W6-WEB-a (paste-ready · Web + Platform menü: ön sipariş ekranları A, B, C, C-bağlantı, Details, E, Ayarlar)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W6 · W6-WEB-a — MOD-0168 ön sipariş ekranları: A oluştur / düzenle (tablet + telefon öncelikli), B liste, Details, C onay (Görev Merkezi), C-bağlantı, E depo belgesi önizlemesi, Ön sipariş ayarları; ortak kural iletisi kaynağı
Repository: C:\tmp\vw-w6-weba (worktree, test/crm-content-visit-e2e güncel başı — W6-BE-a/b/c1/c2 içinde) · Branch: wp/vw-w6-weba · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: frontend/Diten.Web/**, frontend/Diten.Web.Tests/**, Platform yalnız `CrmManifestProvider` (menü + sayaç). CRM backend'e DOKUNMA (eksik uç varsa DUR ve sor).
ÖNCE OKU: MOD-0168 paketi §9 (yerleşim + ekran tablosu + UI standartları + menü anahtarları), §11 (dosya sözleşmesi, UXH-001 kodları), §14 (yetki), §15 (API), §16 AC-UI / AC-L10N, §4.2 gösterim kuralları, §4.8 görünürlük · DESIGN-GAP-W6 §1 (A–E), §2 kurallar, §3, §4 (standart kazanır), §7 kararlar · Mockup `mockups/visit-workspace/decoded-w6/W6-on-siparis.dc.html` (ve standalone) · WP-VW-W6 §37 W6-BE-a / W6-BE-c1 (e-posta bağlantı yolları) / W6-BE-c2 (belge ucu) · MOD-0156 §11 (kural iletisi tek kaynak) + CRM `PricingReasonCodes` + `PreOrderReasonCodes`.
EK KURALLAR (CT, 2026-10-21):
- **E-posta bağlantı yolları sabit:** `/Commercial/PreOrders/Details/{id}` ve `/Commercial/PreOrders/ApprovalLink?token=…` birebir bu yollarla (W6-BE-c1 CRM sabiti). Görev Merkezi derin bağlantısı `Approve` sayfasına.
- **Kural iletileri tek kaynak:** `Rule.{code}` anahtarları (MOD-0156 + MOD-0168 gerekçe kodlarının tamamı, parametre yer tutucularıyla) ortak bir Commercial kaynağında 7 dilde + window.L10n köprüsü; W6-WEB-b aynı kaynağı kullanacak. Kod listesi CRM sabitlerinden; bir kod eksikse test kırmızı (sabitleri okuyan test).
- Ekran A: duyarlı sayfa (cihaz çerçevesi / kendi üst çubuğu YOK), "Sistemden 🔒" şeridi, satırda kural rozeti + ileti + fiyat kaynağı + MF satırı, son 3 sipariş, "Onay gerekir mi?" (onaycı + boş koltuk yedeği), imza (dokunmatik), Gönder kapalıyken açıklama (sert aşım / imza yok / satır yok), gönderimden sonra zaman çizelgesi, "İptal et ve yeni sipariş aç" / "Kopyala ve yeni sipariş aç". Fiyat yalnız sunucudan (istemcide hesap YOK — yalnız gösterim); adetli modda fiyat / iskonto alanı yok.
- Ekran C: "Neden onayımda?", "Siz … Reports to pozisyonundasınız", girilen ↔ sınır, 6 aylık grafik + tablo, Reddet / Değişiklik iste gerekçe zorunlu; kendi breadcrumb'ı yok. C-bağlantı: oturum açtıktan sonra; geçerli / kullanılmış / süresi dolmuş / geçersiz hâlleri; "Bu bağlantı bir kez kullanılabilir · süresi {n} saat".
- Ekran E (`Document.cshtml`, siparişe ait): depo nüshası ↔ eczane kopyası düğme grubu, W6-BE-c2 belge ucundan PDF görüntüleme / indirme, iletim izi.
- Liste B: Golden Compact sunucu modu; para birimleri arası toplam / sıralama YOK; "Onayımı bekleyenler" süzgeci; hızlı görünümde satış operasyonuna depo cevabı.
- Standartlar: Golden başlık (h5 + p), `.diten-field`, `--bs-primary` (sabit renk yok), `_TabCard` gerekiyorsa, RTL mantıksal özellikler, sayı / tarih / para kültürle, menü manifestten (`Nav.Page.PREORDERS`, `Nav.Page.PREORDERSETTINGS` + onayımı bekleyen sayacı), UXH-001 yardım 7 dil (`COM-PREORDER-LIST/FORM/APPROVE/SETTINGS`), Enter / chord, yetkisiz iskelet yok (UAS-001). `_CreateEditOffcanvas` YASAK.
KORU/YAPMA: mevcut sayfalar aynı; mock / harness kullanıcının oturum sekmesine enjekte edilmez; localhost:27017 YOK.
DOĞRULA: xUnit + RunNode (vitest çalışmıyor): sayfa / proxy / yetki testleri (her sayfa yetkisizde iskeletsiz), bağlantı yolları sabitle eşit, kural iletisi anahtarları CRM kodlarının tamamını kapsar (7 dil), adetli modda fiyat alanı yok, depo cevabı yalnız satış operasyonuna, E düğme grubu iki nüsha, resx 7 dil eksiksiz, yardım 7 dil. `verify_datatable_page.py --area Commercial --module PreOrders --format gaps` (sapmaları numaralı liste). Sabotajlar (en az 3): yetki denetimi kaldır, bağlantı yolunu değiştir, bir kural iletisi anahtarını sil. Diten.Web.Tests tam takım (taban aynı baş; bilinen `ShipmentDetailsDateNode` ×2), Platform CrmManifest. Görsel kanıt E4'te (mockup yan yana).
Commit: "feat(web): WP-VW-W6 W6-WEB-a — pharmacy pre-order screens (create / edit, list, details, approval, approval link, depot document, settings) and the shared rule message source" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W6-WEB-b (paste-ready · Web + Platform menü: fiyat ve kurallar ekranı D · W6-WEB-a test dalına alındıktan SONRA)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W6 · W6-WEB-b — MOD-0156 ekranı D: Fiyat listeleri (liste, ayrıntı + fark, elle oluştur / düzenle, 3 adımlı içe aktarma, yayıma gönder / karar) · MF-iskonto kuralları · Kural deneme · Değişiklik geçmişi · Kurlar
Repository: C:\tmp\vw-w6-webb (worktree, test/crm-content-visit-e2e güncel başı — W6-WEB-a içinde) · Branch: wp/vw-w6-webb · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: frontend/Diten.Web/**, frontend/Diten.Web.Tests/**, Platform yalnız `CrmManifestProvider` (menü). CRM backend'e DOKUNMA.
ÖN KOŞUL: §37 W6-WEB-a test dalında olmalı (ortak kural iletisi kaynağı) — değilse DUR ve sor.
ÖNCE OKU: MOD-0156 paketi §9 (ekran D: beş sekme `_TabCard`, sütunlar, ayrıntı, içe aktarma, kural formu, deneme, kurlar), §11, §12 (durumlar: Yürürlükte / Taslak / Planlandı / Geçmiş), §14, §15, §16 AC-UI, §20.3 + EK-1 (elle liste) · DESIGN-GAP-W6 §1 D, §2, §4 · Mockup `decoded-w6/W6-on-siparis.dc.html` (D) · WP-VW-W6 §37 W6-BE-b (uçlar, dört göz, aşama okumada, GTIN kapalı) + §37 W6-WEB-a (ortak kural iletisi kaynağı).
EK KURALLAR (CT, 2026-10-21):
- Kural iletileri W6-WEB-a'nın ortak kaynağından (yeni kopya YOK).
- Dört göz UI'da görünür: hazırlayan kendi listesine / kuralına karar düğmesi görmez (sunucu 403 zaten); iade gerekçe zorunlu.
- İçe aktarma: dosya → önizleme (fark, eşleşmeyen, Retired, tekrar, hatalı fiyat ayrı sayılar) → yayıma gönder; yalnız eşleşen satırlar; GTIN eşleşmesi kapalı olduğu bilgi metniyle belirtilir (v1.2'ye kadar).
- Kural formu: MF "10+2" girişi ve yanında etkin % (gösterim; hesap sunucudan — `trial` ucu ya da değerlendirme yanıtı, istemcide formül YOK); segment alanı "yakında" (ilk sürüm segmentsiz).
- Kurlar sekmesi mockup'ta yok → Golden liste + form (tüzel kişilik, çift, tarih, kur, kaynak; düzeltme geçmişe yazılır).
- Standartlar: Golden başlık, `.diten-field`, `--bs-primary`, `_TabCard` (beş sekme, her sekme kendi sayfası), RTL, kültürle sayı / tarih / para, menü manifestten (`Nav.Page.COMMERCIALPRICING`), UXH-001 yardım 7 dil (`COM-PRICELIST-LIST/FORM/IMPORT`, `COM-PRICINGRULE-LIST/FORM`, `COM-PRICING-TRIAL`, `COM-PRICING-HISTORY`, `COM-FXRATE-LIST`), Enter / chord, yetkisiz iskelet yok. `_CreateEditOffcanvas` YASAK.
KORU/YAPMA: W6-WEB-a sayfaları ve ortak kaynak değişmez (yalnız kullanım); mock / harness kullanıcının sekmesine yok; localhost:27017 YOK.
DOĞRULA: xUnit + RunNode: her sekme yetkisizde iskeletsiz; dört göz düğme görünürlüğü; içe aktarma önizleme sayıları; istemcide fiyat / indirim formülü yok (kaynak taraması); resx + yardım 7 dil. `verify_datatable_page.py --area Commercial --module {PriceLists|PricingRules|FxRates|PricingHistory} --format gaps`. Sabotajlar (en az 3): sekme yetki denetimi, hazırlayana karar düğmesi göster, ortak kaynak yerine yerel kopya. Diten.Web.Tests tam takım (taban aynı baş), Platform CrmManifest. Görsel kanıt E4'te.
Commit: "feat(web): WP-VW-W6 W6-WEB-b — price lists, discount guardrail rules, rule trial, change history and FX rates screens" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W6-WEB-a E2 ACCEPTED (2026-10-21 · görsel kanıt E4'te)
Ajan commit'i `d662f5efa` (taban `bfea98f71`) → test dalına cherry-pick (WP-CRM-MOBILE-REQ belge commit'i üstüne, çakışmasız) → `177873a21`. Web + Platform `CrmManifestProvider`; 65 dosya; CRM backend'e dokunulmadı.
- **Yapılan:** `/Commercial/PreOrders/…` — B liste (Golden Compact sunucu modu; süzgeçler + "Onayımı bekleyenler (n)"; para birimleri arası toplam / sıralama yok; depo cevabı yalnız satış operasyonu), A oluştur / düzenle (duyarlı; "Sistemden 🔒"; kural rozeti; son 3 sipariş; "Onay gerekir mi?"; dokunmatik imza; Gönder kapalıyken neden; zaman çizelgesi; İptal / Kopyala ve yeni aç; fiyat yalnız sunucudan, adetli modda fiyat / iskonto yok), Details, C onay (neden onayımda, Reports to, girilen ↔ sınır, 6 aylık grafik + tablo para birimi başına, gerekçe zorunlu), C-bağlantı (oturum sonrası; geçerli / kullanılmış / süresi dolmuş / geçersizleşmiş / bulunamadı), E belge (iki nüsha, PDF görüntüle / indir, iletim izi), Ayarlar (Golden form). Bağlantı yolları CRM sabitleriyle birebir (CRM kaynağını okuyan test). Ortak kural iletisi kaynağı `CommercialRules` resx + `commercial-rules.js` (53 anahtar × 7 dil; eksik kod → test kırmızı). Manifest: `PREORDERS`, `PREORDERSETTINGS` sayfaları; 11 anahtar sayfa eylemlerine taşındı. Sayfa resx 311 × 7, yardım 4 kod × 7.
- **CT kontrolü:** hedefli Web (PreOrder + Commercial) 25/0; Platform CrmManifest + ModuleManifest 63/0; ajan Web tam takım 2772/2 = taban 2747/2 (`ShipmentDetailsDateNode` ×2). **CT sabotajları:** (1) `decision` eylemi `approve` yerine `read` anahtarına bağlandı → `P4_every_write_and_read_checks_its_key…` **kırmızı**; (2) ayar kaydetme yetki denetimi kaldırıldı → `P4` **kırmızı**. md5 geri.
- **Açık noktalar — CT kararı:** menü sayacı rozeti için depoda manifest sayaç deseni yok → **FU-NAV-BADGE** (Building.Blocks manifest alanı + Platform + Web menü; XMC-001, sonraya) · yardım kodları menü kodlarıyla (`PREORDERS`, `PREORDERSETTINGS`, `COMPREORDERFORM`, `COMPREORDERAPPROVE`) — yardım bekçisi gereği, kabul · `PricingErrors` kodları kaynağa W6-WEB-b'de eklenecek · A ekranında imza görseli yerine "imzalayan · zaman" (CRM imza okuma ucu yok; görsel PDF'te) → **FU-W6-SIGNATURE-PREVIEW** · `verify_datatable_page` sapmaları (Compact `_Form` ↔ `Details` bölüm farkı bilinçli; varsayılan profilde ortak `personalization-client.js` ve `window.API` — proxy sayfa) kabul.
- **Fleet:** `.resx` değişikliği → tam yeniden başlatma.

## Kullanıcı kararı (2026-10-21) — K-W6-ROLES
Satış operasyonu izinleri ve fiyat / kural yayımlama (dört göz) **Department Manager ya da Admin**'de. Ayrı "satış operasyonu" rolü açılmaz. Dört göz kişi bazında (sunucu hazırlayanın kararını 403 ile reddeder), aynı rolde iki farklı kişi yeterli. Canlıda temsilci rolü `CrmUser` (WP-CRM-MOBILE-REQ).

## §36.1 Agent Prompt — W6-CFG (paste-ready · yapılandırma: iş akışı şablonları, kiracı önek, rol – izin betikleri; betikler deneme modunda)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-CFG — iki Workflow şablonu (COM-PREORDER-APPROVAL, COM-PRICING-PUBLISH), kiracı ORD önek kaydı, rol – izin atama betikleri (yerel 97c5 + canlı be823006); hepsi varsayılan deneme modunda, uygulamayı kullanıcı yapar
Repository: C:\tmp\vw-w6-cfg (worktree, test/crm-content-visit-e2e güncel başı — W6-WEB-a içinde) · Branch: wp/vw-w6-cfg · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: scripts/** (betikler), execution/** (WP notu), gerekiyorsa şablon tanım dosyası. Ürün kodu YOK. Canlıya / yerel Mongo'ya YAZMA YOK (betikler `--apply` olmadan yalnız rapor üretir; çalıştırmayı kullanıcı yapar).
ÖNCE OKU: WP-VW-W6 → "Kullanıcı kararı — K-W6-ROLES", §37 W6-BE-a (W6-CFG maddeleri: COM-PREORDER-APPROVAL ilk adımda KENDİ ADAYI OLMAMALI — yoksa Platform çalışma anı adaylarını kullanmaz; kiracı önek kaydı), §37 W6-BE-b (COM-PRICING-PUBLISH), §37 W6-WEB-a · WP-CRM-MOBILE-REQ (CRM-ROLE-1) · Örnek betikler: `scripts/rbac/grant_w5_visit_notifications_97c5.py`, W3-CFG / W4-CFG betikleri ve şablon betikleri (WP-VW-W3 / W4 §37), `scripts/golive/` canlı desenleri · Canlı roller: Admin, Viewer, TenantAdmin, General User, CrmUser, Team Leader, Department Manager, HR Manager · Platform `CrmManifestProvider` (bütün `commercial.*` anahtarlarının tam listesi buradan okunur — elle liste YOK).
NE:
(1) Rol – izin matrisi (betik ve WP notunda aynı tablo):
   - CrmUser (temsilci): commercial.pre-orders.read, .create, .submit, .cancel + crm.account.read, crm.contact.read, crm.account-contact.read, crm.visit-plan.read (CRM-ROLE-1) + mdm.product-master.read (ürün seçici; anahtar adı MDM manifestinden doğrulanır).
   - Team Leader + Department Manager (onaycı yönetici): commercial.pre-orders.read, .approve, .cancel.
   - Department Manager + Admin (satış operasyonu + yayımlama): commercial.pre-orders.read-all, .record-depot-response, .retry-transmission; commercial.pre-order-settings.*; commercial.price-lists.* (yayımlama dahil); commercial.pricing-rules.* (yayımlama dahil); commercial.fx-rates.*.
   - Admin: bütün commercial.* anahtarları.
   Manifestte olup matriste olmayan anahtar → betik raporunda "atanmadı" listesi (sessiz atlama yok).
(2) Betikler (deneme modu varsayılan, `--apply` ile yazar, işaretçi `manual-grant-w6-cfg`, tekrar çalıştırılabilir — var olanı yeniden eklemez): yerel 97c5 (mevcut rollere) ve canlı be823006 (yukarıdaki rollere; canlı bağlantı / kimlik bilgisi betikte YOK — kullanıcının mevcut canlı yöntemi `scripts/golive/` desenine göre, ayrı dosya).
(3) Workflow şablonları: COM-PREORDER-APPROVAL (tek adım, şablonda aday YOK → çalışma anı adayları = temsilcinin hattındaki yöneticiler; sonuç CRM'e mevcut olay yoluyla) ve COM-PRICING-PUBLISH (dört göz onay adımı; aday pozisyonları yerelde POS-SALES-OPS, canlıda `<ülke>-SALES-OPS` — W3 go-live'da oluşturulanlar); şablon oluşturma betiği deneme modunda, mevcut W3-CFG şablon betiği deseninde.
(4) Kiracı ORD öneki: 97c5 GMG tüzel kişiliği (1754b820…) için önek (ör. `MG`) — ön sipariş ayarları ucuyla yazan betik (deneme modu); canlı için LE listesi raporlanır, öneki kullanıcı seçer.
(5) WP notu: matris, betik adları, çalıştırma sırası (şablon → önek → izinler → yeniden giriş), geri alma yolu (işaretçiyle silme).
KORU/YAPMA: başka modülün rollerine / izinlerine dokunma; parola / token betikte yok; varsayılan rol şablonu (Auth `DefaultRolePermissionTemplate`) DEĞİŞMEZ (ayrı takip CRM-ROLE-TEMPLATES).
DOĞRULA: betiklerin deneme çıktısı (yerel 97c5 — salt okuma bağlantısı varsa; yoksa sahte veriyle birim testi), matris ↔ manifest eşleşmesi testi (manifestteki her commercial.* anahtarı matriste ya da "atanmadı" listesinde), tekrar çalıştırmada sıfır yeni ekleme, `--apply` olmadan yazma yok (sabotaj: deneme bayrağını yok say → test kırmızı).
Commit: "chore(crm): WP-VW-W6 W6-CFG — pre-order and pricing workflow templates, tenant ORD prefix, role permission grants (local 97c5 + live be823006) as dry-run scripts; rep CRM read keys for mobile" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.2 Uygulama raporu — W6-CFG (2026-10-21 · betikler yazıldı, hiçbiri `--apply` / `APPLY = true` ile çalıştırılmadı)
Dal `wp/vw-w6-cfg` (taban `807e80d96`). Ürün kodu değişmedi; canlıya ve yerel Mongo'ya yazma yok. Auth `DefaultRolePermissionTemplate` değişmedi (takip CRM-ROLE-TEMPLATES).

### Rol – izin matrisi (K-W6-ROLES; tek kaynak `scripts/rbac/w6_cfg_role_matrix.json`, canlı betik aynı kopyayı taşır — test farkı yakalar)
| Rol | Anahtarlar |
|---|---|
| `CrmUser` (temsilci) | `commercial.pre-orders.read`, `.create`, `.submit`, `.cancel` · `crm.account.read`, `crm.contact.read`, `crm.account-contact.read`, `crm.visit-plan.read` (CRM-ROLE-1) · `mdm.product-master.read` (ürün seçici — aşağıda açık nokta) |
| `Team Leader` (onaycı yönetici) | `commercial.pre-orders.read`, `.approve`, `.cancel` |
| `Department Manager` (onaycı + satış operasyonu + dört göz) | Team Leader'ınkiler + `commercial.pre-orders.read-all`, `.record-depot-response`, `.retry-transmission` · `commercial.pre-order-settings.*` · `commercial.price-lists.*` · `commercial.pricing-rules.*` · `commercial.fx-rates.*` (yayımlama dahil) |
| `Admin` | `commercial.*` |
| **İş akışı (MOD-0023)** | CrmUser `platform.workflow.instances.start`, `.instances.view` · Team Leader `.instances.view`, `.tasks.approve`, `.tasks.reject` · Department Manager + Admin dördü birden (kullanıcı kararı 2026-10-21, ikinci commit) |

**İş akışı anahtarları neden:** CRM onay turunu çağıranın token'ıyla başlatır ve karara bağlar (`GatewayClaimWorkflowClient`) → gönderim `instances.start`, onaycının görevi `instances.view`, karar `tasks.approve / reject`; bunlar olmadan Gönder 503, karar 503 / 403. İlk commit'te yalnız raporlanıyordu; kullanıcı kararıyla (2026-10-21) matrise taşındı.

`commercial.*` anahtarları Platform `CrmManifestProvider`'dan okunur (yerel betik kaynak dosyayı, canlı betik manifestten kayıtlı kataloğu); `.*` satırı o önekin bütün manifest anahtarlarıdır. 2026-10-21'de 27 anahtar: hepsi en az bir rolde ("atanmadı" boş), yalnız Admin'de olan: `commercial.pre-orders.export`. Matriste manifestte olmayan anahtar (yazım hatası) ya da hiçbir şeye uymayan `.*` satırı → betik durur.

### Betikler
| Dosya | Hedef | Ne yapar |
|---|---|---|
| `scripts/golive/w6-cfg-workflow-templates.js` | yerel (`TARGET='local'`) / canlı (`'live'`) · `/Platform/Workflow` konsolu | `COM-PREORDER-APPROVAL`: tek adım, **aday yok** (`candidatePrincipalIds: []`) → Platform çalışma anı adaylarını (CRM'in verdiği hattaki yöneticiler) kullanır (`WorkflowInstanceStartCoordinator`: ilk adımın adayı boşsa isteğinkiler); SLA yok; `rejectCommentRequired`. `COM-PRICING-PUBLISH`: dört göz adımı, aday yerelde `POS-SALES-OPS`, canlıda 8 ülkenin `<ÜLKE>-SALES-OPS`'u; SLA 4320 / 2880 (W3-CFG). Yayındaysa dokunmaz; pozisyon eksikse DUR |
| `scripts/golive/w6-cfg-preorder-prefix.js` | yerel / canlı · `/Commercial/PreOrderSettings` konsolu | Yerel: GMG (`1754b820…`) → `MG`. Canlı: tüzel kişilikleri + mevcut önekleri listeler, önek listesi boş (kullanıcı seçer, `WANTED.live`'a yazar). Mevcut listeyi okuyup yalnız eksik satırı ekler, okuduğu sürümle yazar; var olan öneki değiştirmez (farklı önek / başka şirkette aynı önek → DUR); diğer ayarları göndermez |
| `scripts/rbac/grant_w6_cfg_rbac_97c5.py` | yerel 97c5 · Mongo `diten_auth_v3` | W5 deseni (subtype-4, tarih yerel kayıttan, işaretçi `manual-grant-w6-cfg`, idempotent, rol / izin oluşturmaz). Yerelde olmayan rol raporlanır ve atlanır; `--role-map "CrmUser=…"` mevcut role eşler; `--strict-roles` eksik rolde durur. Katalogda olmayan anahtar → yazmadan durur (kod 3). |
| `scripts/golive/w6-cfg-grant-roles-live.js` | canlı be823006 · `/RoleAssignments` konsolu | `grant-roles-live.js` deseni (ekranın `/RoleAssignments/assign` ucu); yalnız eksikleri ekler; katalogda yok / rol yok → DUR satırı; matriste olmayan `commercial.*` → ATANMADI satırı |

Parola / belirteç hiçbir betikte yok (konsol betikleri oturum çerezi + sayfanın başlıkları; Python yerel Mongo).

### Çalıştırma sırası (kullanıcı)
1. Fleet'i yeniden başlat (W6-BE-a / BE-b / WEB-a manifest anahtarları kataloğa girsin) → yerleşik tarayıcıda giriş.
2. **Şablonlar:** `/Platform/Workflow` → `w6-cfg-workflow-templates.js` (kuru) → plan doğruysa `APPLY = true`. Canlıda `TARGET = 'live'`.
3. **Önek:** `/Commercial/PreOrderSettings` → `w6-cfg-preorder-prefix.js` (kuru) → `APPLY = true`. Canlıda önce kuru çalıştır, LE seç, `WANTED.live`'a yaz.
4. **İzinler:** yerel `py -3 scripts/rbac/grant_w6_cfg_rbac_97c5.py` (kuru) → `--apply`; canlı `/RoleAssignments` → `w6-cfg-grant-roles-live.js` (kuru) → `APPLY = true`.
5. Rol sahipleri **yeniden giriş** yapar.

### Geri alma
- Yerel izinler: `db.rolePermissions.deleteMany({CreatedBy: "manual-grant-w6-cfg"})`.
- Canlı izinler: ekran ucu işaretçi yazmaz → raporun "EKLENDI" satırları `/RoleAssignments`'tan kaldırılır.
- Önek: numara vermemiş önek ayarlar ekranından silinir (numara vermişse CRM 409 — geri alınamaz, RCS-001 §5.4).
- Şablon: yayımlanmış şablon silinmez; yeni sürüm yayımlanır ya da şablon arşivlenir (`/Platform/Workflow`).

### Deneme çıktısı (yerel 97c5, salt okuma, 2026-10-21)
- 27 `commercial.*` anahtarı, atanmadı: yok, yalnız Admin: `commercial.pre-orders.export`.
- Yerel roller: Admin, DocumentMasterRegisterLinker, GQD, QADocumentation, Viewer → CrmUser / Team Leader / Department Manager **yok**, atlandı.
- **DUR (kod 3):** 27 `commercial.*` anahtarı yerel katalogda yok (fleet W6'dan sonra yeniden başlatılmamış). İşaretçili satır: 0.
- Konsol betikleri yerelde çalıştırılmadı (fleet / oturum yok); sahte pencereyle Node testleri koştu.

### Doğrulama
`scripts/tests/test_w6_cfg_scripts.py` 17 / 0 (unittest; Node yoksa konsol testleri atlanır). Matris ↔ manifest, JSON ↔ canlı JS eşliği, deneme modunda yazma yok, uygula → tekrar çalıştırmada sıfır ekleme, eksik rol / katalog, ön sipariş şablonunda aday yok, önek birleştirme / çakışma. Her rolün iş akışı anahtarları (temsilci karar anahtarı almaz). Sabotajlar (12, hepsi kırmızı, md5 geri): Python deneme bayrağı yok sayıldı · Python var olanı yeniden ekler · canlı izin deneme bayrağı · ön sipariş şablonuna aday · şablon deneme bayrağı · önek deneme bayrağı · JSON'a anahtar eklendi (JS'ye değil) · boş joker satır · (ilk commit, rapor-yalnız sürüm) Python / canlı JS iş akışı anahtarını verir · (ikinci commit) JSON'da temsilciden `instances.start` düştü · canlı JS'de Team Leader'dan `tasks.reject` düştü.

### Açık noktalar
- **`mdm.product-master.read` katalogda yok** (ölçüm: yerel `permissions` 0 satır; hiçbir manifestte ve Auth `DataSeeder`'da yok — yalnız `DefaultRolePermissionTemplate` ve ürün kimliği profilinde adı geçiyor). MDM `ProductMasterController` bu anahtarı istiyor → temsilcinin ürün seçicisi (`/Commercial/PreOrders/api/lookups/skus`) bu anahtar olmadan boş / 403. Yerel betik CrmUser eşlenince bu anahtar yüzünden durur; canlı betik "DUR: katalogda yok" satırı yazar. Karar: MDM sahibi anahtarı katalog / manifeste ekler (XMC-001) ya da seçici başka okuma anahtarına bağlanır.
- `COM-PRICING-PUBLISH` iki nesne türüne hizmet ediyor; şablonun `requestedObjectType` alanı `commercial.pricing` (Platform bu alanı başlatmada denetlemiyor — ölçüldü; örnek kendi nesne türünü taşır).
- ~~İş akışı anahtarları (karar gerekli)~~ → **KAPANDI (kullanıcı, 2026-10-21):** `platform.workflow.*` satırları matrise taşındı (JSON + canlı JS); betikler artık veriyor.
- Canlı LE öneki kullanıcının seçimi (MDM'de kısa kod alanı yok — FU-MDM-LE-SHORTCODE).

## §37 CT kabul — W6-CFG E2 ACCEPTED (2026-10-21 · betikler deneme modunda, uygulama kullanıcıda)
Ajan commit'leri `4b57bc3c2` + `9a2e18466` (taban `807e80d96`) → test dalına cherry-pick (CRM-ACC-EDIT §37 üstüne, çakışmasız) → `b56b7f15c` + `471279142`. Yalnız `scripts/**` + WP notu; ürün kodu yok.
- **Yapılan:** rol – izin matrisi `scripts/rbac/w6_cfg_role_matrix.json` (K-W6-ROLES; manifestteki her `commercial.*` anahtarı matriste ya da "atanmadı" listesinde — testli) + iş akışı anahtarları (temsilci `platform.workflow.instances.start/.view`; onaycılar `instances.view` + `tasks.approve/.reject`; temsilciye karar anahtarı YOK); yerel betik `scripts/rbac/grant_w6_cfg_rbac_97c5.py` (deneme varsayılan, `--apply` ile yazar, işaretçi `manual-grant-w6-cfg`, tekrar çalıştırmada sıfır ekleme); canlı tarayıcı konsolu betikleri `scripts/golive/w6-cfg-grant-roles-live.js`, `w6-cfg-workflow-templates.js` (COM-PREORDER-APPROVAL şablonda adaysız; COM-PRICING-PUBLISH `<ülke>-SALES-OPS` / yerel POS-SALES-OPS), `w6-cfg-preorder-prefix.js` — hepsi `APPLY = false` varsayılan, kimlik bilgisi yok (sayfanın kendi oturumu + anti-forgery). Testler `scripts/tests/test_w6_cfg_scripts.py` 17/17.
- **CT kontrolü:** 17/17 (`py -3 -m unittest`); betiklerde parola / token / bağlantı dizgesi kimliği yok; üç canlı betiğin son satırı `APPLY = false`. **CT sabotajı:** yerel betikte deneme bayrağı yok sayıldı → `test_dry_run_writes_nothing` **kırmızı**; md5 geri.
- **Açık:** `mdm.product-master.read` yerel katalogda yok — Auth'un ürün kimliği yetki profilinde tanımlı (`ProductIdentityLifecycleEntitlementGrantProfile`), MDM ürün kimliği yetkisi gelince eşitlenen anahtar → temsilcinin ürün seçicisi kapalı kalır → **FU-MDM-PRODUCT-MASTER-READ** (MDM / Auth sahibi; XMC-001) · canlı ORD öneki kullanıcı seçimi bekliyor · yerel uygulama sırası: fleet tam yeniden başlatma (katalog yeni anahtarları alsın) → şablonlar → önek → izinler (`--apply`) → yeniden giriş.

## §37 CT kabul — W6-WEB-b E2 ACCEPTED (2026-10-21 · görsel kanıt E4'te)
Ajan commit'i `c559d38c2` (taban `807e80d96`) → test dalına cherry-pick (çakışmasız) → `b2fb55bd9`. Web + Platform `CrmManifestProvider`; 89 dosya; CRM backend'e dokunulmadı.
- **Yapılan:** `/Commercial/Pricing` tek menü öğesi (`COMMERCIALPRICING`), beş sekme ortak `_TabCard` (her sekme kendi sayfası, yetkisizde çizilmez): Fiyat listeleri (Golden Compact sunucu modu; elle oluştur / düzenle — EK-1; ayrıntı + önceki sürüme fark; 3 adımlı içe aktarma — eşleşen / eşleşmeyen / Retired / tekrar / hatalı fiyat ayrı sayılar, yalnız eşleşen satır taslağa, GTIN kapalı notu), MF-iskonto kuralları (yumuşak · sert çiftleri; "10+2" yanında etkin % sunucunun deneme ucundan; segment "Yakında"; revize / emeklilik), Kural deneme (yazmasız), Değişiklik geçmişi (salt okuma), Kurlar (Golden liste + form). Dört göz: vekil kaydı hazırlayanı CRM'in kimlik sırasıyla (`sub` → NameIdentifier → e-posta → ad; oluşturan / son düzenleyen / açık turu gönderen) `mine` ile işaretler, hazırlayana karar düğmesi çizilmez; iade gerekçeli. Ortak kural kaynağı 53 → 79 anahtar (26 `PricingErrors` × 7 dil; kopya yok). Manifest: 15 anahtar sayfa eylemlerine, `fx-rates.import` API-only. `PricingIndex` resx 302 × 7, yardım 8 kod × 7.
- **CT kontrolü:** hedefli Web (Pricing + PreOrder) 50/0; Platform CrmManifest + ModuleManifest + Navigation 94/0; ajan Web tam takım 2797/2 = taban 2772/2 (`ShipmentDetailsDateNode` ×2). **CT sabotajları:** (1) kural kararı `publish` yerine `read` anahtarına bağlandı → `D4_every_call_checks_its_key…` **kırmızı**; (2) `mine` işaretinden "açık turu gönderen" çıkarıldı → `D6_the_proxy_marks_what_the_caller_prepared…` **kırmızı**. md5 geri.
- **Açık noktalar — CT kararı:** CRM'de tek kur okuma ucu yok → vekil kur listesinde en çok 10 sayfa tarıyor → **FU-W6-FX-GET** (küçük CRM ucu) · menü öğesi `price-lists.read` ile açılıyor (yalnız kur / kural yetkili kullanıcı menüyü görmez — K-W6-ROLES'te DM / Admin hepsini aldığı için kabul) · kur içe aktarma ekranı yok, `fx-rates.import` API-only kabul · `verify_datatable_page` sapmaları (tek vekil `/Commercial/Pricing/api` paket §15 gereği; dışa aktarma anahtarı olmayan üç listede dosya menüsü kapalı; ortak `personalization-client.js` / `window.API`) kabul.
- **W6 kod tamam** → kalan: kullanıcı uygulaması (fleet tam yeniden başlatma + W6-CFG betikleri) → W6 canlı E4 (yerel) + mockup görsel karşılaştırması → K-W6-PDF-SCRIPT kararı.

## §37 CT — W6 yerel E4 (2026-10-21, 1. tur · YARIM KALDI — engel)
**Hazırlık (kullanıcı onayıyla):** kullanıcı `grant_w6_cfg_rbac_97c5.py --apply` + yeniden giriş; CT (kullanıcının açık isteğiyle) iki Workflow şablonunu oluşturup yayımladı — `COM-PREORDER-APPROVAL` `8757e68a…` v1 Published (adaysız), `COM-PRICING-PUBLISH` `87aa17ae…` v1 Published (aday POS-SALES-OPS); ön sipariş ayarlarına GMG (`1754b820…`) → `MG` öneki (ayar sürümü 1).
**Geçenler:** menüde Ön Siparişler / Ön Sipariş Ayarları / Fiyat Listeleri ve Kurallar; yetkisizken "erişim yetkiniz yok" (iskelet yok — UAS-001), yetkiyle liste boş durum; A formu: "Sistemden 🔒" GMG · TR · TRY · Yalnız adet (liste yok → adetli mod, MF + adet), eczane seçici çalışıyor (CRM hesapları), "Onay gerekir mi?" → "Pozisyonunuzun üstünde onaycı bulunamadı" (Admin = CEO, Reports-to yok — beklenen), Gönder kapalı + üç neden; fiyat ekranı D: liste boş durum, "Yeni liste" formu (satan şirket listesi MDM'den).
**Bulgular:**
- **F-1 (ENGEL) — ürün seçici boş:** `GET /Commercial/PreOrders/api/lookups/skus` → `{available:false}`; MDM `api/product-master/stockable-items` `[HasPermission("mdm.product-master.read")]` ister, anahtar yerel katalogda yok (Auth ürün kimliği yetki profili `ProductIdentityLifecycleEntitlementGrantProfile`; MDM manifestinde yok). Sipariş satırı eklenemiyor → onay / iletim / PDF akışı test edilemiyor. → **FU-MDM-PRODUCT-MASTER-READ öne çekildi** (MDM / Auth sahibi; XMC-001). Canlıda da aynı engel olur.
- **F-2 (kontrol) — MDM tüzel kişiliklerinde `countryCode` boş:** önek betiğinin `legal-entities` listesinde 36 şirketin hepsi `countryCode: null`. Sipariş formunda GMG için TR görünüyor (başka bir alandan geliyor olabilir) — dış alıcı dili (karar B) ve fiyat listesi ülkesi aynı kaynaktan mı okuyor, ölçülecek.
- **F-3 (görsel) — fiyat ekranı sekmeleri dar genişlikte yalnız simge:** 613 px'te `_TabCard` hapları etiketsiz; masaüstü genişlikte ve mockup ile karşılaştırılacak.
- **Test ortamı eksiği:** yerelde tek kullanıcı (Admin, CEO koltuğu). Onay (yönetici) ve dört göz (ikinci yayımlayıcı) için en az iki ek kullanıcı + koltuk gerekiyor: temsilci (CrmUser, bir yöneticiye bağlı koltuk) ve ikinci Department Manager.

**W6 yerel E4 — ek (2026-10-21, aynı gün):** Fiyat ekranının ürün seçicisi de `product-master` ucunu kullanıyor → fiyat listesi kalemi ve kural da FU-MDM-PRODUCT-MASTER-READ'e bağlı. Ürünsüz test edilen: Kurlar — GMG · EUR→TRY · 21 Eki · 37,85 · "TCMB (E4 testi)" kaydedildi ve listede. F-2 kapandı (MDM ayrıntı ucunda ülke dolu; lookup DTO'su ülke taşımaz — tasarım gereği; 32/37 şirket dolu, 5 Diten demo şirketi boş). Sema'nın birincil koltuğu POS-MED-DIR "Bağlı olduğu: CEO" yapıldı (yerel test verisi; Admin = Sema'nın yöneticisi). Dış alıcı dili: BDT ülkeleri → Rusça (kullanıcı kararı, test `1b870823a`).
**Yeni bulgular (W6-FIX adayı):** G-1 Kurlar sekmesinin alt başlığı fiyat listesinin cümlesi ("Her liste bir satış şirketine…") · G-2 tarih seçici (flatpickr) Türkçe arayüzde İngilizce ay / gün adları — ortak mı sayfaya özel mi ölçülecek · G-3 kur listesinde "Son değişiklik" kişi adı yerine kullanıcı kimliği (W5 F-3'ün aynısı; ad çözücü) · F-3 (dar ekranda sekmeler yalnız simge) masaüstü genişlikte etiketli — duyarlı tasarım, kabul.


**W6 yerel E4, 2. tur — 2026-10-21/22 (CT, kullanıcıyla birlikte; kiracı 97c5): ana akış GEÇTİ**

**Ürün zinciri açıldı.** Ürün seçici FU-MDM-PRODUCT-MASTER-READ yüzünden boştu.
- KONSET GSKU onaylandı: Admin gönderdi, Sema Görev Merkezi'nden onayladı.
- Bunun için yerelde iki değişiklik gerekti:
  - (1) MDM'in iletilen kullanıcı token'ı sınırı 16 KB'tan 48 KB'a çıkarıldı (geçici, commit YOK). Admin'in token'ı ≈27 KB → FU-MDM-DELEGATED-TOKEN-16K.
  - (2) MDM'in referans verisi servis kimliği (`AuthWorkflowVerifiedMarketServiceIdentityProvider`, audience `TRUSTED_REFERENCE_DATA_CONSUMER`) bootstrap'ta yoktu. Yerel ayara ikinci Auth istemcisi `diten-mdm-reference` olarak eklendi.
- Ardından `local_enable_mdm_product_master_read_97c5.py --apply` → seçicide "KONSET · 28 C62" görünüyor.

**Fiyat listesi, dört göz (D13).** Sema "KONSET TR test listesi"ni hazırladı: depo 100 / eczacı 110 / halka 140 TRY, KDV %10. Sema yayıma gönderdi, Admin Görev Merkezi'nden onayladı → yayımlandı (v3).
- Yerel test verisi: Admin, POS-SALES-OPS'a ikincil olarak atandı. Tek aday gönderenin kendisi olunca Platform haklı olarak reddediyordu.

**Siparişler.**
- **MG-ORD-000001:** Sema → 1 ETAP ECZANESİ · TEST ECZA DEPOSU (Q-DEPOT-SOR sorusu açık) · 10 × KONSET · 1.210 TRY · imza → onaycı Admin (Reports-to CEO) → onay → `transmitted`, depo PDF'i + eczane kopyası.
- **MG-ORD-000002:** Sema → Admin "değişiklik iste" (gerekçe zorunlu; sipariş taslağa döndü, imza silindi, olay geçmişi korundu — D2) → Sema düzeltip yeniden imzalatarak gönderdi (`submissionNo` 2) → Admin onay → `transmitted`.

**E-posta.**
- Yerelde bütün Platform e-postaları `ProviderTlsFailed` ile düşüyordu. Neden Avast Mail Shield: SMTP TLS'ye araya girip kendi sertifikasını sunuyor, MailKit iptal denetiminde reddediyor. Ortam sorunu, kod değil. Avast kapatılınca:
  - "değişiklik istendi" e-postası gerçekten teslim edildi;
  - depo e-postası PDF ekiyle teslim edildi (dil tr, `MG-ORD-000002-depot.pdf`).
- İç alıcıların dili `en`: kiracı varsayılanı (B kararı).

**Mockup görsel karşılaştırması.** Yapıldı → DESIGN-GAP-W6 §8, V-1…V-19.

**Yerelde düzeltilenler (commit YOK, W6-FIX'te gerçek rota / akış testiyle kabul edilecek).**
- **G-4** — fiyat listesi düzenleme kaydı her zaman 400 dönüyordu: form `legalEntityId` gönderiyordu. `form.js` artık göndermiyor ve şirket alanı kilitli.
- **G-5** — Web eylem uçlarının rotası `{action}` idi. Bu MVC'nin ayrılmış rota değeri olduğu için şu uçların **hepsi 404** dönüyordu:
  - fiyat listesi gönder / karar / sürüm / at;
  - içe aktarma;
  - kurallar;
  - **ön sipariş eylemleri**.
  
  Rota `{verb}` yapıldı. Testler metodu doğrudan çağırdığı için rotayı hiç sınamıyordu.

**Yeni bulgular (W6-FIX).**
- **G-6:** fiyat listesinde yerel `window.confirm` kullanılıyor.
- **G-7:** onay ekranında "Onayla" onay sorusu sormuyor.
- **G-8:** gönderimden sonra sayfa düzenleme ekranında kalıyor.
- **G-9:** satan şirket kodu MDM'den `GMGGRANDMEDCALLACLARLTDST-238` olarak geliyor.
- **G-10:** onay ekranından verilen kararın kanalı `work-center` yazılıyor.
- **G-11:** CRM'deki "iletildi" yalnız "kuyruğa alındı" demek; e-posta sonradan düşerse CRM'e geri bildirim yok.
- **G-12 (CİDDİ, Platform; W5-FIX2 × BL-454): onay isteği e-postası HİÇ gitmiyor.**
  - İç olay ucu e-postayı yalnız kuyruğa yazıyor (QueueOnly).
  - Gizli değişken `approvalUrl` kayıtta maskeli tutuluyor.
  - Süpürme maskeli bağlantıyı gönderemiyor ve e-postayı `ACTION_LINK_NOT_RETRYABLE` ile kapatıyor.
  - Düzeltme: gizli değişken varsa queueOnly olmamalı (`QueueEmailNotificationHandler.cs:190`).
- **G-13 (CİDDİ): e-postadaki bağlantı oturum kapalıyken açılınca giriş sayfası kiracıyı bilmiyor.**
  - Ne bağlantıda ne yönlendirmede `tenantId` var.
  - Yerelde varsayılan kiracı …0001'e giriş yapılıyor → `tenant-unavailable` ile atılıyor.
  - Canlıda "kiracı eksik" hatası beklenir.
  - W5 e-posta bağlantıları da aynı risk altında.
- **Ayrı (MOD-0149):** kurum formunda İstanbul seçilince ilçe listesi Edirne'de kalıyor.

**Kalan.** Şu dallar test edilmedi:
- reddet;
- iptal (D5);
- depo yanıtı (D9);
- kurallı satır (sınır gösterimi);
- RU / EN dış e-posta;
- e-posta bağlantısıyla onay (G-12 + G-13 düzeltilince).


## W6-FIX planı (CT, 2026-10-22)

E4'ün 2. turu ve mockup karşılaştırmasında çıkan bulgular üç pakete bölündü.

**Sıra:**
- **W6-FIX-a ∥ W6-FIX-c** paralel çalışır.
- **W6-FIX-b**, FIX-c test dalına girdikten sonra başlar. Nedeni: ekran, toplamları CRM yanıtından okur; istemcide hesap yapılmaz.

**Kapsam dışı kalanlar:**
- **G-9:** satan şirket kodu MDM'den geliyor; MDM notuna gider.
- **G-11:** "iletildi" ile gerçek teslim arasındaki fark bir kullanıcı kararı; ayrı ele alınacak.
- **V-6:** eczane hızlı seçimi; FU-W6-PHARMACY-QUICKPICK.
- **G-10 bulgu değil, kapandı.** `Approve/{id}` Görev Merkezi'nin derin bağlantısıdır, bu yüzden kanalın `work-center` yazılması doğrudur. E-posta bağlantısının kanalı ayrıdır.

**Kullanıcının checkout'undaki yerel değişiklikler:**
- **G-4** (`form.js`) ve **G-5** (`{verb}`) commit edilmedi. FIX-a bunları kendi worktree'sinde baştan ve testli yazar. CT, kabul sırasında yerel kopyaları atıp FIX-a'nın commit'ini alır.
- MDM'deki 16 KB geçici değişikliği kullanıcı kararıyla şimdilik yerinde kalıyor.

## §36.1 Agent Prompt — W6-FIX-a (paste-ready · Platform + Web + CRM bağlantıları: onay e-postası, e-posta bağlantısında kiracı, eylem rotaları, fiyat listesi düzenleme)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-FIX-a — G-12 onay isteği e-postası hiç gitmiyor (Platform), G-13 e-posta bağlantısıyla girişte kiracı kayboluyor (Web + CRM bağlantıları, W5 dahil), G-5 Web eylem rotaları 404 ({action}), G-4 fiyat listesi düzenleme kaydı 400
Repository: C:\tmp\vw-w6-fixa (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-fixa · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA (orada aynı dosyalarda commit edilmemiş yerel düzeltmeler var; onları ALMA, kendin yaz). Kapsam: services/Diten.Platform/** (yalnız bildirim kuyruğu), frontend/Diten.Web/** (+ Tests), services/Diten.CrmService/** (yalnız e-posta bağlantı üreticileri + testleri).
ÖNCE OKU: WP-VW-W6 "W6 yerel E4, 2. tur" notu (G-4, G-5, G-12, G-13) · WP-VW-W5 K-N1 + §37 W5-FIX2 (F-1 queue-only) · Platform QueueEmailNotificationHandler (BL-454 gizli değişken maskeleme) + EmailDispatchJob (ActionLinkRedacted → ACTION_LINK_NOT_RETRYABLE) · Web Program.cs cookie auth + tenant-shell guard (tenant-unavailable) + AccountController.Login + assets/js/Account/login.js (tenantId sorgu parametresi) · CRM PreOrderClients.cs (IPreOrderLinks) + W5 VisitNotification bağlantı üreticisi (reportUrl).
İŞ:
(1) G-12 — QueueEmailNotificationHandler: gizli değişken varsa (secretNames boş değilse) kuyruğa-yalnız yazma YOK; gönderim istek içinde yapılır (W5-FIX2 öncesi yol). Gizlisiz e-postalar kuyrukta kalır (F-1 korunur). İkinci kusur: süpürme ACTION_LINK_NOT_RETRYABLE ile kapatırken İLK hatanın kodunu ezmesin — ilk kod korunur (ErrorCode / ErrorMessage'da ikisi birden okunur biçimde ya da ayrı alan; mevcut okuyucuları kırma).
(2) G-13 — e-posta bağlantıları kiracıyı taşır: CRM W6 OrderUrl + ApprovalLinkUrl ve W5 reportUrl'e `tenantId=<D-biçimli guid>` eklenir. Web: oturum kapalıyken korunan sayfaya gelen istek giriş sayfasına yönlendirilirken istekteki `tenantId` (geçerli guid ise) giriş adresine taşınır (cookie OnRedirectToLogin ya da eşdeğeri; returnUrl korunur). Giriş sonrası returnUrl'e dönülür; returnUrl yerel olmayan adresse reddedilir (açık yönlendirme yok). Geçersiz / eksik tenantId → bugünkü davranış.
(3) G-5 — Web'deki dört eylem rotası `{action}` → `{verb}` (MVC'nin ayrılmış rota değeri; yöntem adıyla eşleşip her gerçek eylemi 404 yapıyordu): CommercialPricingController (price-lists / imports / rules) + PreOrdersController (orders). Bütün Web projesinde başka `{action}` rota parametresi kalmadığını tara.
(4) G-4 — Commercial/PriceLists/form.js: düzenlemede PUT gövdesinde `legalEntityId` GÖNDERİLMEZ (CRM güncellemede her değeri reddediyor); düzenlemede satan şirket alanı kilitli.
KORU/YAPMA: queue-only'nin gizlisiz e-postalardaki davranışı ve tekillik anahtarı aynı; BL-454 maskeleme aynı (gizli değer kayda yazılmaz); D4 (bağlantı oturum açtırır) aynı; mock / harness kullanıcının sekmesine yok; localhost:27017 YOK (geçici mongod).
DOĞRULA: (a) Platform: iç olay ucundan gizli değişkenli e-posta → sağlayıcı bir kez çağrılır, durum Sent; gizlisiz → Queued + süpürme gönderir; ACTION_LINK kapanışında ilk kod okunur. (b) Web: GERÇEK yönlendirme testi (WebApplicationFactory / TestServer, metodu doğrudan çağırma DEĞİL): dört rotanın her eylemi 404 değil (yetkisizde 403 / iskeletsiz); oturumsuz `/Commercial/PreOrders/ApprovalLink?token=x&tenantId=<guid>` → giriş adresinde tenantId + returnUrl; geçersiz tenantId taşınmaz. (c) CRM: üç bağlantıda tenantId var. (d) form.js: düzenleme gövdesinde legalEntityId yok (RunNode). Sabotajlar (en az 4): gizli → kuyruk koşulunu kaldır; rotayı {action}'a geri al; tenantId taşımayı kaldır; form.js'e legalEntityId'yi geri koy — her biri kırmızı. Platform tam takım (DITEN_TEST_MONGOD, -o repo içi), Diten.Web.Tests tam takım, CRM Application tam takım; taban aynı test dalı başı (bilinen kırmızılar known_failures.py).
Commit: "fix(crm): WP-VW-W6 W6-FIX-a — approval e-mails with a secret link are sent in the request, e-mail links carry the tenant to sign-in, action routes no longer 404, price list edit no longer sends the legal entity" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W6-FIX-c (paste-ready · CRM: sipariş toplamları + depo belgesi PDF farkları · W6-FIX-a ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W6 · W6-FIX-c — MOD-0168 sipariş toplamlarına liste ara toplamı + iskonto tutarı; depo belgesi PDF'i mockup E'ye yaklaştırma (antet, sütunlar, toplamlar, başlık bilgileri, yerel saat, eczane ili, iç iletim bilgisinin çıkarılması)
Repository: C:\tmp\vw-w6-fixc (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w6-fixc · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/** (+ testleri). Web'e DOKUNMA (FIX-b okur).
ÖNCE OKU: DESIGN-GAP-W6 §8 (V-3, V-13…V-19) + §1 E + §3 "E belgesi içeriği" · mockup E (`decoded-w6/W6-on-siparis.dc.html`, belge bölümü) · WP-VW-W6 §37 W6-BE-c2 (PDF üretici, PDFsharp, K-W6-PDF-SCRIPT açık) + "W6 yerel E4, 2. tur" notu · MOD-0168 paketi §4.2 (gösterim) · MDM tüzel kişilik ayrıntı istemcisi (adres / vergi no alanları var mı — yoksa DUR, raporla, FU yaz).
İŞ:
(1) V-3 / V-15 — toplamlar: `listSubtotal` (Σ adet × liste birim fiyatı, KDV hariç) + `discountAmount` (listSubtotal − net) sipariş yanıtına ve PDF'e; mevcut net / vat / gross / effectiveDiscountPct aynı. Geçmiş siparişler okunurken eksikse hesaplanır (göç yok). Etkin indirim depo nüshasında GİZLİ kalır (DESIGN-GAP §3 kararı), eczane kopyasında / ekranda görünür.
(2) PDF depo nüshası + eczane kopyası:
  - V-13 antet: satan şirket adı + adres + vergi no (MDM tüzel kişilik ayrıntısından; alan yoksa yalnız ad + FU-MDM-LE-ADDRESS-TAX);
  - V-14 satır sütunları: Ürün · Barkod (GTIN; yoksa "—") · Adet · MF · İsk. % · Birim fiyat · KDV % · Satır toplamı (MF / İsk. boşsa "—", sütun hep var);
  - V-16 başlık bilgileri: teslim tarihi (boşsa satır yok), para birimi, depo e-postası;
  - V-17 saatler (imza, onay) satan şirketin ülke saat diliminde, dilim adıyla (UTC yok);
  - V-18 eczane adresi il ile;
  - V-19 "İletim: deneme N" depo nüshasından ÇIKAR (iç bilgi).
  Tablo sayfa kenarını aşmasın (dar sütun + kaydırmasız kırma).
KORU/YAPMA: belge ve e-posta dili kuralı (satıcı ülkesi, BDT → ru) aynı; MOD-0262 yazma / sürüm davranışı aynı; ar / zh K-W6-PDF-SCRIPT açık (değiştirme); localhost:27017 YOK.
DOĞRULA: birim + üretici testleri: toplamlar (MF + iskontolu, iskontosuz, çok satırlı), PDF metninde sütun başlıkları + "—" + antet + yerel saat + il; depo nüshasında "deneme" ve etkin % yok, eczane kopyasında etkin % var. Sabotajlar (en az 3): discountAmount'u sıfırla; saat dilimini UTC'ye geri al; deneme satırını geri koy — kırmızı. CRM Application + Infrastructure tam takım (-o repo içi; taban aynı baş). Görsel kanıt: üretilen PDF'in sayfa görüntüsü raporda.
Commit: "feat(crm): WP-VW-W6 W6-FIX-c — order totals carry the list subtotal and discount; depot document letterhead, line columns, totals, local times and no internal transmission note" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W6-FIX-b (paste-ready · Web: ekran farkları ve küçük bulgular · W6-FIX-c test dalına alındıktan SONRA)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W6 · W6-FIX-b — ön sipariş ve fiyat ekranlarında E4 bulguları (G-1, G-2, G-3, G-6, G-7, G-8) + mockup farkları (V-1…V-5, V-7…V-10, V-12) + FU-VISIT-END-SEPARATOR
Repository: C:\tmp\vw-w6-fixb (worktree, test/crm-content-visit-e2e güncel başı — FIX-c içinde, FIX-a HENÜZ YOK: paralel çalışıyor) · Branch: wp/vw-w6-fixb · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: frontend/Diten.Web/** (+ Tests). CRM / Platform'a DOKUNMA (gereken veri yoksa DUR ve sor). FIX-a ile çakışmamak için şunlara DOKUNMA: `assets/js/Commercial/PriceLists/form.js`, `CommercialPricingController` ve `PreOrdersController`'daki eylem rotaları (`.../{action}` → FIX-a `{verb}` yapıyor), `Program.cs`, `AccountController` + `assets/js/Account/login.js`. Bunlardan birine dokunman gerekirse DUR ve sor.
ÖN KOŞUL: §37 W6-FIX-c test dalında olmalı (listSubtotal / discountAmount) — değilse DUR.
ÖNCE OKU: DESIGN-GAP-W6 §8 (V tablosu) + §4 standartlar · WP-VW-W6 "W6 yerel E4" 1. tur (G-1…G-3) + 2. tur (G-6…G-8) · mockup A, B, C (`decoded-w6`) · W6-WEB-a / W6-WEB-b §37'leri.
İŞ:
- G-1 Kurlar alt başlığı kendi cümlesi;
- G-2 tarih seçici arayüz diliyle (ortak yardımcıda mı sayfada mı — ölç; ortaksa ortak düzelt, raporla);
- G-3 kur listesinde "Son değişiklik" kişi adı (mevcut ad çözücü);
- G-6 fiyat listesi gönder / at → standart onay penceresi (window.confirm YOK);
- G-7 onay ekranında "Onayla" standart onay penceresiyle;
- G-8 gönderimden sonra Detay'a geçiş.
- V-1 sipariş satırları md ve üstünde tek satırlık tablo (başlık satırı bir kez; ÜRÜN · ADET · MF · İSK. % · BİRİM · TOPLAM), dar ekranda kart;
- V-2 satır altında fiyat kaynağı: liste adı · geçerlilik · kademe · KDV %, ürün bilgisi (form / GTIN varsa);
- V-3 özet: Ara toplam (KDV hariç, liste) · İskonto · Net (KDV hariç) · KDV %{oran} · Etkin indirim (MF + iskonto) · Genel toplam — değerler CRM yanıtından;
- V-4 önceki siparişlerde "ürün × adet" özeti;
- V-5 kayıtlı taslakta başlıkta sipariş no + eczane + durum rozeti (Golden başlık içinde), gönder düğmesi bağlama göre ("Yönetici onayına gönder" / "Gönder");
- V-7 liste sütun önceliği (1280 px'te Temsilci, Satır, Etkin indirim, Kural görünür);
- V-8 "Yerel karşılık sütunu" var mı ölç, yoksa sütun görünürlüğüne ekle;
- V-9 onay ekranı 6 ay: etkin indirim eğilimi + tabloda Etkin sütunu;
- V-10 onay ekranında tarih saatle, eczane ille, Reports-to pozisyon ADI;
- V-12 sekme etiketi "MF / iskonto kuralları" (Türkçe büyük harf hatası; başka Title Case dönüşümü varsa tara).
- FU-VISIT-END-SEPARATOR: ziyaret bitti ekranında kurum boşken "· ·" yazılmaz.
- Metinler 7 dilde, UXH-001 yardım güncel.
KORU/YAPMA: Golden standartlar (§4) kazanır; mockup'ın cihaz çerçevesi / kendi üst çubuğu alınmaz; istemcide fiyat / indirim formülü YOK; mock / harness kullanıcının sekmesine yok.
DOĞRULA: xUnit + RunNode: window.confirm kaynak taraması 0; Onayla onay penceresi; gönderim sonrası Details; özet satırları CRM alanlarından; sekme etiketi; 1280 px sütun önceliği ayarı; resx 7 dil. Sabotajlar (en az 3): window.confirm'e geri dön, özet değerini istemcide hesapla (kaynak taraması yakalamalı), "Iskonto" etiketine geri dön. Diten.Web.Tests tam takım (taban aynı baş). Görsel kanıt: mockup ile yan yana ekran görüntüsü (A satırlar + özet, B liste, C 6 ay) — raporda.
Commit: "fix(web): WP-VW-W6 W6-FIX-b — pre-order and pricing screens: line table, totals breakdown, confirmations, header and list columns, approval trend, labels" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W6-FIX-c E2 ACCEPTED (2026-10-23)

- **Alındı:** `wp/vw-w6-fixc` 68a891ed6 → test dalına `3bc18e9b2` (cherry-pick, çakışma yok). 21 dosya, yalnız CRM.
- **İnceleme:**
  - Toplam hesabı tek yerde, `PricingMath.ListSubtotal` / `DiscountAmount`; MF kutuları sayılmaz.
  - Değerlendirici, önizleme ve yanıt aynı sayıları taşır. Eski sipariş okurken tamamlanır (`PreOrderTotalsReading`), kayda yazılmaz.
  - Antet MDM `registeredAddressJson` + `taxId` / `vatNumber`'dan; taslakta ve gönderimde anlık görüntü alınır.
  - Saat `CountryTimeZones` ile yazılır (başkent dilimi, tabloda olmayan ülke "UTC" adıyla). İl adı MOD-0048 `city`'den.
  - "İletim: deneme N" depo nüshasından çıktı. 7 dil resx tamam.
- **Testler (CT, aynı baş):** CRM Application.Tests 3261 başarılı / 1 başarısız / 5 atlanan. Başarısız olan bilinen `VisitPlanningFix2Tests.The_account_and_contact_repositories_search_through_the_shared_pattern`. Ajanın raporuyla birebir.
- **CT bozma denemesi** (ajanın denemediği kural): eski sipariş tamamlaması 0 döndürsün → `T4_an_order_saved_before_the_fix_is_completed_on_read_and_nothing_is_written` KIRMIZI. Dosya geri alındı (md5 aynı).
- **Kullanıcı kararı (2026-10-23):** depo nüshasında satır **İsk. %** ve iskonto tutarı GÖRÜNÜR; yalnız **etkin indirim %** (iç bilgi) depo nüshasında gizli, eczane kopyasında görünür. Gerekçe: MOD-0168 §4.7 + DESIGN-GAP §3, ayrıca faturayı depo keser. BE-c2'nin satır iskontosunu da gizleyen eski D1 testi buna göre değişti.
- **Takip:** FU-MDM-LE-TIMEZONE (MDM tüzel kişiliğinde saat dilimi alanı yok).
  - FU-MDM-LE-ADDRESS-TAX gerekmedi; alanlar MDM'de var. Yerel tüzel kişilikte adres / VKN boşsa antette yalnız ad çıkar (veri işi).
  - W6-FIX-b `totals.listSubtotal` / `totals.discountAmount` okuyabilir → **W6-FIX-b başlayabilir** (FIX-a'dan bağımsız).

## §37 CT kabul — W6-FIX-b E2 ACCEPTED (2026-10-23)

- **Alındı:** `wp/vw-w6-fixb` efc63de73 → test dalına `27eb0d460` (55 dosya, yalnız `frontend/Diten.Web/**`; FIX-a'nın dosyalarına dokunulmadı).
- **İnceleme:**
  - **G-2:** ortak `diten-datefield.js` takvim dilini tarayıcının Intl'inden `<html lang>` için kuruyor. Değer ve gösterim biçimi değişmedi. 25 sayfa etkileniyor; İngilizce flatpickr'ın kendisi.
  - **V-3:** özet yalnız CRM alanlarından oluşuyor; alanı olmayan satır gösterilmiyor, istemcide hesap yok.
  - **G-6 / G-7:** `showConfirm` kullanılıyor; ticari ekranlarda `window.confirm` 0.
  - **V-12:** sekmelerdeki büyük harf dönüşümü yalnız fiyat ekranında kapatıldı.
- **Testler (CT, FIX-a ile birlikte aynı baş):** Diten.Web.Tests 2847 / 3 kırmızı. Kırmızılar bilinen `ShipmentDetailsDateNode` ×2 ve `WorkCenterMenuWithoutPermissions`.
- **CT bozma denemesi:** ortak tarih seçicide `localizeCalendars()` çağrısı kapatıldı → `PricingWebTests.D14_the_calendar_speaks_the_page_language_from_the_shared_date_field` KIRMIZI. Dosya geri alındı (md5 aynı).
- **Canlı:**
  - V-7 kısmen görüldü: Temsilci sütunu Tarih ve Depo'dan önce görünüyor.
  - Liste ve sipariş formu tam ekran mockup karşılaştırması bir sonraki E4'te yapılacak.
  - V-8 "Yerel karşılık" sütunu var.
- **Takip:**
  - FU-W6-FIXB-CRM-DATA → takip listesinde **W6-FIX-d** (G-3 kişi adı, V-9 etkin indirim eğilimi, V-10 pozisyon adı; CRM verisi).
  - FU-TABCARD-TITLECASE: aynı sekme kartını kullanan 4 CRM ekranı (PotentialThresholds, VisitEvidenceFlags, VisitEvidenceSettings, VisitNotifications); kural ortak `_TabCard` CSS'ine taşınacak.

## §37 CT kabul — W6-FIX-a E2 ACCEPTED (2026-10-23)

- **Alındı:** `wp/vw-w6-fixa` 6a2f2086c → test dalına `f546a9bd2` (20 dosya). Kullanıcının checkout'undaki yerel G-4 / G-5 kopyaları önce atıldı (`git checkout --` yalnız 3 dosya).
- **İnceleme:**
  - **G-12:** gizli değişkenli e-posta istek içinde gönderiliyor; gizlisiz e-postalar kuyrukta kalıyor (F-1 korunuyor).
  - ACTION_LINK kapanışı ilk kodu `ACTION_LINK_NOT_RETRYABLE;first:<kod>` olarak koruyor; maskelenmiş değer iletiye girmiyor.
  - **G-13:** W6 sipariş bağlantısı, onay bağlantısı ve W5 rapor bağlantısı `tenantId` taşıyor. Web, giriş yönlendirmesine yalnız geçerli tek bir guid'i taşıyor.
  - Girişten sonra yalnız yerel adrese dönülüyor; böylece açık yönlendirme (open redirect) de kapandı.
  - **G-5:** `{verb}` kullanılıyor; Web'de `{action}` rota parametresi kalmadığını bir test denetliyor.
  - **G-4:** düzenlemede `legalEntityId` gönderilmiyor.
- **Testler (CT, aynı baş):**
  - Web 2847 / 3 bilinen kırmızı.
  - CRM 3261 / 2 kırmızı: bilinen `VisitPlanningFix2` + `ContactLocationPiiHardening` (FU-CRM-PII-GUID-FLAKE; tek başına 18/18).
  - Platform bildirim testleri 516/0.
  - Ajanın Platform tam takımı: 24 bilinen ortam kırmızısı + 1 kararsız test (`TaskWorkItemContractGuard`, tek başına 14/0).
- **CT bozma denemesi:** W5 rapor bağlantısından `&tenantId=` kaldırıldı (ajan yalnız onay bağlantısını denemişti) → `W6FixaLinkTenantTests.L1_every_link_carries_its_tenant` KIRMIZI. Dosya geri alındı (md5 aynı).
- **Sapma (kabul):** girişli dönüş adresi testi gerçek HTTP değil; `AccountController` host servisleriyle kuruluyor ve ayrıca birim testi var.
- **Açık:** `known_failures.py` kaydında bazı taban kırmızıları "YENİ" görünüyor → kayıt güncellemesi SYNC-3 ile birlikte.
- **Sıradaki:** e-posta bağlantısıyla onayın yeniden E4'ü (G-12 + G-13 birlikte). Avast Posta Kalkanı'nın kapalı olması gerekiyor.
