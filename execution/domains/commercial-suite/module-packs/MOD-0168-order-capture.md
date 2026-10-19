---
id: MOD-0168
name: Order Capture
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
form_field_count: 9
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-10-20 (D1–D17 + §20.3 AÇIK kararları; WP-VW-W6 K-O1…K-O5; F8-a ve PLT-W6 önkoşul)"
status: ready-for-dev
owner: module-pack-author
branch: feature/crm/mod-0168-order-capture
started: 2026-10-19
target: "ready-for-dev onayından sonra CT belirler"
first_scope: "Eczane ön siparişi (WP-VW-W6 · W6-BE-a + W6-BE-c + W6-WEB-a)"
screens:
  - module: PreOrders
    data_mode: server
dependencies:
  - MOD-0156 (Price Lists & Discount Guardrails — değerlendirme API'si + fiyat çözümü + ara kur; aynı serviste, süreç içi; paket MOD-0156-price-lists-discount-guardrails.md, draft)
  - MOD-0149 (Customer 360 / Account — eczane ve depo = Account id, K-W13; salt okuma)
  - MDM (product-master-bundle v1.1 SKU — getSku seviyesiz yol, lifecycleStatus; LegalEntity CountryCode / BaseCurrencyCode; salt okuma)
  - MOD-0288 / Platform (birincil koltuk → tüzel kişilik, K-L3 / K-L8 PLT-ORG-SEAT; Reports-to yönetici çözümleyicisi, K-W9 / K-N2)
  - Platform Notifications (W5 PLT-NOTIFY iç ucu `POST api/internal/notifications/events/dispatch`, K-N1)
  - MOD-0023 Workflow / Görev Merkezi (onay görevi, W3-CFG deseni)
  - MOD-0262 (Document Binary Store — eczacı imzası + depo belgesi PDF; CRM'deki mevcut HttpVisitEvidenceStore yolu)
  - RCS-001 (kayıt kodu MG-ORD-000123 — ortak üretici bugün yok, bkz. §20 AÇIK-3)
  - MOD-0018 (RBAC — commercial.* anahtarları; tanım bu pakette, tohum / atama F-RBAC)
  - DEV-0001 (Golden Reference Compact — B liste yüzeyi)
---

# MOD-0168 — Order Capture · ilk kapsam: eczane ön siparişi

> **DRAFT — kod yetkisi YOK.** `status: draft` · `runtime_code_allowed: false` · `flip_approved_by` boş. Kullanıcı bu paketi
> okuyup onaylayınca Control Tower `ready-for-dev`'e çevirir (AGENTS çekirdek kural 7). Bütün kararlar **KİLİTLİ**
> (§19 / §20: DESIGN-GAP-W6 §5 D1–D16 + §7, WP-VW-W6 K-O1…K-O5); yalnız §20.2'deki **AÇIK** noktalar kullanıcı /
> sahip cevabı bekler.
>
> **Dayanaklar:** [WP-VW-W6](../work-packs/WP-VW-W6-pharmacy-pre-order.md) (K-O1…K-O5) ·
> [DESIGN-GAP-W6](../work-packs/mockups/visit-workspace/DESIGN-GAP-W6-2026-10-19.md) (§1 ekranlar, §2 kurallar, §3 farklar,
> §4 standartlar, §5 D1–D16, §6 iş bölümü, §7 karar) · [BRIEF-W6](../work-packs/mockups/visit-workspace/BRIEF-W6-pharmacy-pre-order.md) ·
> [ROADMAP-visit-workspace](../work-packs/ROADMAP-visit-workspace.md) K-W9 / K-W10 (a)–(h) / K-W13 · mockup
> `W6 Eczane On Siparis (standalone).html` (açılmış: `decoded-w6/W6-on-siparis.dc.html`).
>
> Yetki sırası: **Blueprint 8.7** > bu paket > [domain-config](../domain-config.md) > [crm-sor-boundary](../crm-sor-boundary.md) >
> `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Kimlik kapısı ve yer kararı

### 0.1 DCP-002 — GEÇTİ (2026-10-19)

```text
$ py -3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0168 --name "Order Capture"
OK  MOD-0168: proven against Blueprint/registry.
EXIT=0
```

Kimlik Blueprint 8.7'de `MOD-0168 | Order Capture` (Commercial Suite (CRM + O2C), Order-to-Cash (Bridge)); kayıt
tablosunda `reserved / planned`, sahip domain `commercial-suite`. Yeni MOD / FU / CAND-CAP **açılmadı**. Paket, modülün
kendi kimliğiyle (`MOD-0168`) yazılır; ilk kapsam künyedeki `first_scope` alanında ve §1'de sınırlanır. Sonraki kapsamlar
(Program T3 doğrudan satış kanalı vb.) bu paketin yeni sürümüyle ya da FU ile gelir.

### 0.2 Yer — `Diten.CrmService`, CRM modüllerinden ayrı sınır (K-O1, D17)

- 8.7: MOD-0168 = Commercial Suite, Order-to-Cash (Bridge). Kayıt tablosu: sahip domain `commercial-suite`; o domain'in servisi
  `Diten.CrmService` (domain-config).
- **Ayrı sınır:** kendi özellik klasörü (`Features/Commercial/PreOrders/**`), kendi koleksiyonları (`commercial_*`), kendi izin
  ad alanı (`commercial.*`, K-O2), kendi API yolu (`/api/commercial/...`). CRM modüllerinin (`Features/Visit*`, `Account` …)
  koduna yazmaz; onları yalnız mevcut okuma yollarıyla tüketir. Program T2'de taşınabilirlik bu sınırla korunur (API yolu
  değişmez, yalnız Gateway hedefi değişir).
- **SoR sınırı — AÇIK nokta olarak KALIR:** kayıt tablosundaki *"SoR boundary with Finance / Order-Mgmt = EA-TBD"* notu
  çözülmedi; Program T3'e kalır (§2.3, §20.2 AÇIK-1). İlk kapsamda sorun yaratmaz: ön sipariş bizim satış siparişimiz
  değildir (K-O3).

---

## 1. Modül özeti

### 1.1 Ne yapar (ilk kapsam)

Temsilci, eczanenin siparişini **ön sipariş** olarak alır; sipariş seçilen **depoya** (ecza deposu / distribütör, CRM
Account'u) iletilir, **faturayı depo keser** (K-O3). Akış:

1. Temsilci ekran A'da eczane, depo, teslim tarihi ve SKU satırlarını (adet, MF, iskonto %) girer. Şirket, ülke, para birimi
   ve fiyat modu **sistemden** gelir ("Sistemden 🔒" şeridi; K-O4). Birim fiyat kilitlidir.
2. Her satır canlı olarak MOD-0156 değerlendirme API'siyle denetlenir: **sınır içi / onay gerekir (yumuşak) / girilemez
   (sert)**. Sert sınır, imza yokluğu ya da satır yokluğu gönderimi kapatır.
3. Eczacı imzası alınır (MOD-0262'ye ikili kayıt). Gönderimde fiyat **sabitlenir** (D12); onay gerekip gerekmediği
   kiracı ayarı + kural sonucuyla belirlenir (D1).
4. Onay gerekiyorsa Görev Merkezi'ne Workflow görevi + onaycıya tek kullanımlık e-posta bağlantısı gider (D16, D3, D4).
   Onaycı = temsilcinin birincil koltuğunun Reports-to pozisyonundaki kişi(ler); koltuk boşsa bir üst (K-W9).
5. Onaylanan sipariş **sistem adımıyla** depoya iletilir: depo nüshası PDF + e-posta, eczaneye kopya (W6-BE-c). İletim
   başarısızsa durum **İletim başarısız**, yeniden deneme vardır (DESIGN-GAP §3).
6. Satış operasyonu depo cevabını elle işaretler: Teslim edildi / Eksik teslim (kısmi adet + not) (D9).
7. Onaylanmış ya da iletilmiş sipariş gerekçeyle iptal edilir; depoya ve eczaneye iptal e-postası gider (D5). Reddedilen
   sipariş kopyalanıp yeni sipariş açılabilir (D6).

### 1.2 Ne değildir

- **Bizim satış siparişimiz / faturalama / tahsilat değildir** (Program T3; MOD-0169). Ekranda "fatura" ya da "satış
  siparişi" dili kullanılmaz; terimler **ön sipariş**, **depoya iletildi** (BRIEF #1).
- **EDI / depo entegrasyonu yoktur**; depo cevabı elle işaretlenir.
- **ATP / stok / rezervasyon yoktur** (K-W10 h): bizim stoğumuz hareket etmez; MOD-0172 / MOD-0173 / depo / lojistik ile bağ
  kurulmaz.
- **Fiyat listesi, MF / iskonto kuralı ve kur tablosu sahibi değildir** — MOD-0156 (§2.2). MOD-0168 yalnız değerlendirme
  sonucunu siparişe yazar.
- Teklif (quote) ve Deal Desk istisna akışı değildir (MOD-0157 / MOD-0284; kural dışı istisna şimdilik yalnız yönetici
  onayı).
- Mobil uygulama değildir; web ekranı tablette çalışır, mobil sözleşme notu §22 / W8.

### 1.3 Kullanıcılar

| Rol | Ne yapar | Ekran |
|---|---|---|
| Temsilci | ön sipariş açar, imza alır, gönderir; kendi siparişlerini görür; onaylanmış / iletilmiş siparişini iptal eder | A, B |
| Yönetici (Reports-to) | ekibinin siparişlerini görür; onaylar / reddeder / değişiklik ister; ekibinin siparişini iptal eder | B, C, C-bağlantı |
| Satış operasyonu | bütün siparişleri görür; depo cevabını işaretler; başarısız iletimi yeniden dener; kiracı ayarlarını yönetir | B, ayarlar |
| Hepsi | depo belgesini (E) önizler / indirir | E |

---

## 2. Sahiplik ve sınırlar

### 2.1 Kapsam içi / dışı

| Kapsam | Karar |
|---|---|
| **Kapsam içi** | `PreOrder` kaydı (+ satırlar, olay kaydı, imza, onay, iletim, iptal, depo cevabı) · durum makinesi (§12) · önizleme / değerlendirme köprüsü (MOD-0156'yı süreç içi çağırır) · onay (Workflow görevi + onay bağlantı belirteci) · kiracı ön sipariş ayarları · kayıt kodu (`MG-ORD-…`) · depo belgesi PDF (depo nüshası + eczane kopyası) · e-posta olayları (onay talebi, depoya iletim, eczane kopyası, kendiliğinden onay bilgisi, değişiklik iste, red, iptal) · liste / ayrıntı / önizleme API'leri · görünürlük (temsilci / ekip / tümü) · ekranlar A, B, C, C-bağlantı, E · Gateway rotaları + rota kapsama testi · izin anahtarları (tanım) |
| **Kapsam dışı** | Bizim satış siparişimiz, faturalama, tahsilat (Program T3, MOD-0169) · EDI · ATP / stok (MOD-0172 / 0173) · teklif (MOD-0157) · Deal Desk (MOD-0284) · fiyat listesi / kural / kur **yazımı** (MOD-0156) · ekran D (MOD-0156) · MOD-0188 talep planlamaya olay sinyali (W6 dışı, K-W10 h) · vekâlet (D10: sonra, Workflow) · kampanya bağı (MOD-0165; D8 notu) · mobil uygulama (W8 notu) |

### 2.2 SoR — sahip olunan / tüketilen

| Nesne | Sahip | Bu pakette |
|---|---|---|
| **`PreOrder`** (+ `PreOrderLine[]`, `PreOrderEvent[]`, imza, onay, iletim, iptal, depo cevabı) | **MOD-0168** | **AÇILIR** (8.7 SoR: *orders, order intake logs, order validation outcomes*) |
| **`PreOrderSettings`** (kiracı ön sipariş ayarları) | **MOD-0168** | **AÇILIR** |
| **`PreOrderApprovalLink`** (onay bağlantı belirteci) | **MOD-0168** | **AÇILIR** |
| Fiyat listesi, MF / iskonto kuralı, değerlendirme mantığı, ara kur | MOD-0156 | **SÜREÇ İÇİ TÜKETİLİR** (`IPricingEvaluator`, `IPriceResolver`, `IFxRateReader` — MOD-0156 §4.6). Sonuç siparişe **anlık görüntü** olarak yazılır; MOD-0168 kural hesaplamaz |
| Eczane ve depo (Account, K-W13) | MOD-0149 | **SALT OKUMA** (ad, şehir, adres, GLN, eczane kodu, vergi no, e-posta, tür) — siparişe anlık görüntü |
| Eczane segmenti (BRD `pharmacy-segment`, D8) | MOD-0149 alanı + MOD-0048 seti | **SALT OKUMA**, MOD-0156'ya girdi olarak geçer (§20.2 AÇIK-5) |
| SKU (product-master v1.1) | MDM | **SALT OKUMA** — `getSku` seviyesiz yol (X-08), `lifecycleStatus`; `Retired` girmez (K-O3) |
| Tüzel kişilik (ülke, para birimi, ad, vergi no) | MDM | **SALT OKUMA** — temsilcinin koltuğundan (K-L3) |
| Koltuk, Reports-to, ekip | MOD-0288 / Platform | **SALT OKUMA** (PLT-ORG-SEAT K-L8; yönetici çözümleyicisi K-N2) |
| Onay görevi | MOD-0023 Workflow | **TÜKETİLİR** — örnek başlatma, karar geri çağrısı (W3-CFG deseni) |
| E-posta gönderimi | Platform Notifications | **TÜKETİLİR** — iç uç (K-N1); CRM SMTP kullanmaz |
| İmza görseli ve PDF ikilisi | MOD-0262 | **TÜKETİLİR** — `HttpVisitEvidenceStore` / `DocumentRepositoryCallerContext` deseni |
| Kayıt kodu sayacı | RCS-001 ortak servis | **TÜKETİLİR** — bugün yok (§20.2 AÇIK-3) |

### 2.3 Kalıcı yasaklar

```text
Ön sipariş bizim satış siparişimize / faturaya dönüşür          ❌  K-O3: transfer siparişi; fulfilment = depot
Stok hareketi, rezervasyon, ATP çağrısı                          ❌  K-W10 h
MOD-0168 kural / MF / iskonto / KDV hesabını kendisi yapar      ❌  MOD-0156 değerlendirme API'si tek yer (D7, D8)
Temsilci birim fiyatı, şirketi, ülkeyi, para birimini seçer     ❌  K-O4: sistemden; fiyat kilitli
Retired SKU siparişe girer                                       ❌  K-O3, X-08
Onay bekleyen sipariş düzenlenir                                 ❌  DESIGN-GAP §2
Olay kaydı silinir ya da yeniden yazılır                         ❌  DESIGN-GAP §3 (değişiklik iste dahil)
Oturum açmadan e-posta bağlantısıyla onay                        ❌  D4
Kural metni sunucuda birleştirilmiş cümle olarak döner           ❌  DESIGN-GAP §4: gerekçe kodu + parametre; metin resx 7 dil
Para birimleri arası toplam / sıralama                           ❌  DESIGN-GAP §3 operasyon listesi
CRM e-posta sunucusu / SMTP                                      ❌  K-N1
Kullanıcı e-posta adresi CRM'de saklanır                         ❌  K-N2 (depo / eczane Account e-postası iş verisidir, kullanıcı değil)
Finance / Order-Mgmt SoR'u bu pakette çözülür                    ❌  EA-TBD → Program T3 (AÇIK-1)
ocelot.json / RBAC tohumu / kayıt tablosu bu paketle yazılır     ❌  ilan edilir; integration-agent / F-RBAC / CT
```

---

## 3. Sahip olunan nesneler

| Katman | Nesne | Not |
|---|---|---|
| **Kök (aggregate)** | `PreOrder` (`EntityBase`) + gömülü `PreOrderLine[]` · `PreOrderEvent[]` · `PreOrderPartySnapshot` (eczane, depo, satan şirket, temsilci) · `PreOrderPriceSnapshot` · `PreOrderFxSnapshot?` · `PreOrderSignature?` · `PreOrderApproval?` · `PreOrderTransmission?` · `PreOrderCancellation?` · `PreOrderDepotResponse?` + `IPreOrderRepository` (Mongo, sınıf eşlemesi **ZORUNLU** — CRM yeni kök GUID alt türü tuzağı) | Alanlar §4.1 |
| **Kök** | `PreOrderSettings` (`EntityBase`, kiracı başına tek) + `IPreOrderSettingsRepository` | §4.3 |
| **Kök** | `PreOrderApprovalLink` (`EntityBase`) + `IPreOrderApprovalLinkRepository` | §4.4; belirteç yalnız özetiyle saklanır |
| **Komutlar** | `CreatePreOrderCommand` (taslak) · `UpdatePreOrderCommand` (taslak) · `DiscardPreOrderCommand` (taslağı iptal) · `CapturePreOrderSignatureCommand` · `SubmitPreOrderCommand` · `DecidePreOrderCommand` (`approve` \| `reject` \| `request-changes`) · `CancelPreOrderCommand` · `CopyPreOrderCommand` · `TransmitPreOrderCommand` (sistem adımı + elle yeniden deneme) · `RecordPreOrderDepotResponseCommand` · `UpdatePreOrderSettingsCommand` · `ConsumePreOrderApprovalLinkCommand` (bağlantıdan gelen kararın ön adımı) | Her biri §21'de |
| **Sorgular** | `GetPreOrderListQuery` (sayfalı, sunucu modu) · `GetPreOrderByIdQuery` · `GetPreOrderContextQuery` (A için "Sistemden" şeridi: şirket, ülke, para birimi, fiyat modu, depo listesi, onaycı önizlemesi) · `PreviewPreOrderQuery` (satırları MOD-0156'ya değerlendirtir, kaydetmez) · `GetPharmacyRecentPreOrdersQuery` (son 3) · `GetPharmacyOrderHistoryQuery` (C için 6 ay) · `GetPreOrderDocumentQuery` (E: depo / eczane nüshası) · `GetPreOrderSettingsQuery` · `ResolvePreOrderApprovalLinkQuery` | |
| **Sözlük (alan içi)** | `PreOrderStatus` (§12) · `PreOrderPriceMode` (`priced` \| `quantity`) · `PreOrderFulfilment` (`depot`; ileride `direct`, T3) · `PreOrderDecision` · `PreOrderApprovalMode` (`always` \| `auto-within-limits`) · `PreOrderDepotResponseStatus` (`delivered` \| `partial`) · `PreOrderEventType` · `PreOrderReasonCodes` (hata kodları, §13.2) | Alan içi, kapalı küme; küme dışı → 400 |
| **Uçlar** | `PreOrdersController` (`/api/commercial/pre-orders/**`), `PreOrderSettingsController` (`/api/commercial/pre-order-settings`), `PreOrderApprovalLinksController` (`/api/commercial/pre-order-approval-links/**`), Workflow geri çağrı tüketicisi | §15 |
| **İzinler** | `commercial.pre-orders.*`, `commercial.pre-order-settings.*` — **YALNIZ TANIM** | §14 |
| **Bildirim olayları** | `commercial.pre-order.*` (Platform kataloğu `CrmManifestProvider.NotificationEvents`, sahip MOD-0168, 7 dil şablon) | §4.6 |
| **Workflow** | objectType `commercial.pre-order`, şablon kodu ayarı `Commercial:PreOrders:Workflow:TemplateCode` = `COM-PREORDER-APPROVAL` | §4.5; canlı şablon W6-CFG adımında |
| **Ön yüz** | `Views/Commercial/PreOrders/**` (A, B, C, C-bağlantı, E) + `Views/Commercial/PreOrderSettings/**` + aynı köken vekil + 7 dil resx | §11 |
| **Sahip OLUNMAYAN** | fiyat / kural / kur (MOD-0156) · Account (MOD-0149) · SKU / tüzel kişilik (MDM) · koltuk / pozisyon (Platform) · Workflow motoru · e-posta altyapısı · ikili depo |

---

## 4. Varlık alanları ve normatif kurallar

### 4.1 `PreOrder` (`EntityBase`, koleksiyon `commercial_pre_orders`)

> `TenantId` JWT'den sunucuda çözülür, yükte gelmez; kiracılar arası → 404. Silme yalnız soft delete. `Version` iyimser
> eşzamanlılık belirteci (bayat yazım → 409). Tarih tuzağı: iki `DateTimeOffset` alanı birlikte sıralanmaz (CRM paralel
> dizi 500); takvim alanları `DateOnly`.

| # | Alan | Tür | Kural |
|---|---|---|---|
| 1 | `Id` | Guid | `EntityBase` |
| 2 | `TenantId` | Guid | sunucu |
| 3 | `Code` | string | `MG-ORD-000123` biçimi; **taslak ilk kaydedilince** verilir (D15); tek sıra; boşa düşen numara geri verilmez (RCS-001 §5.8); `ŞİRKET` öneki satan tüzel kişiliğin kısa kodu (RCS-001 §5.4). Sınıf **OTOMATİK** (değiştirilemez) — gerekçe: yüksek hacim, iş atfı numarayla yapılır |
| 4 | `Status` | string | `PreOrderStatus` (§12) |
| 5 | `Fulfilment` | string | ilk kapsamda yalnız `depot` (K-O3) |
| 6 | `PriceMode` | string | `priced` (satan şirketin yürürlükte listesi varsa) \| `quantity` (yoksa) — K-O4; taslakta canlı, gönderimde sabit |
| 7 | `Seller` | `PreOrderPartySnapshot` | satan tüzel kişilik: `LegalEntityId`, kısa kod, ad, adres, vergi no — **temsilcinin bugün yürürlükteki birincil koltuğundan** (K-L3; PLT-ORG-SEAT). Koltuk yoksa sipariş açılamaz (§13.2 `pre_order_no_seat`) |
| 8 | `CountryCode` / `LocalCurrencyCode` | string | MDM `LegalEntity.CountryCode` / `BaseCurrencyCode` (K-W10 e) |
| 9 | `Currency` | string? | **fiyat listesinin para birimi** (K-O4, K-W10 g); adetli modda null |
| 10 | `Pharmacy` | `PreOrderPartySnapshot` | `AccountId` (MOD-0149) + ad, şehir, adres, GLN, eczane kodu, vergi no, e-posta, segment kodu — gönderimde anlık görüntü |
| 11 | `Depot` | `PreOrderPartySnapshot` | `AccountId` (tür distribütör / ecza deposu, K-W13) + ad, e-posta — gönderimde anlık görüntü |
| 12 | `Rep` | `{ UserId, DisplayName, PositionId }` | oluşturan temsilci (string kimlik değil Guid kullanıcı kimliği; MOD-0288 sahibi) |
| 13 | `VisitId` | Guid? | isteğe bağlı ziyaret bağı (takvimden / E4c'den açıldıysa); "ziyaretten bağımsız sipariş" kalır (DESIGN-GAP §3) |
| 14 | `RequestedDeliveryDate` | DateOnly? | teslim tarihi |
| 15 | `Note` | string? | ≤ 1000; depo nüshasında görünür (DESIGN-GAP §3 E belgesi) |
| 16 | `Lines` | `PreOrderLine[]` | §4.1.1; en az 1 satır gönderim şartı; en çok 50 satır |
| 17 | `Totals` | `{ Net, Vat, Gross, EffectiveDiscountPct }` | yalnız fiyatlı modda; MOD-0156 sonucundan; para birimi = `Currency` |
| 18 | `Fx` | `PreOrderFxSnapshot?` | `{ LocalCurrency, Rate, RateDate, Source, LocalGross }` — yalnız `Currency ≠ LocalCurrencyCode` ve kur varsa; **gönderimde yazılır, sonra değişmez** (K-W10 g); kur yoksa null, sipariş yine verilir |
| 19 | `Price` | `PreOrderPriceSnapshot?` | `{ PriceListId, PriceListVersion, Name, Kind (official \| company), ValidFrom, Tier }` — **gönderimde sabitlenir** (D12); taslakta yalnız önizleme |
| 20 | `Evaluation` | `{ Outcome (ok \| soft \| hard), Findings[], EvaluatedAt, RuleSetFingerprint }` | gönderim anındaki MOD-0156 sonucu (8.7 SoR "order validation outcomes") |
| 21 | `ApprovalRequirement` | `{ Required, Reasons[] (tenant-setting \| soft-exceeded), ApproverPositionId, ApproverUserIds[], FallbackLevel }` | gönderimde hesaplanır (D1, K-W9); C'deki "Neden onayımda?" kaynağı |
| 22 | `Signature` | `PreOrderSignature?` | `{ BinaryRef (MOD-0262), SignerName, SignedAt, CapturedByUserId }`; "Değişiklik iste" ile **temizlenir** (D2) |
| 23 | `Approval` | `PreOrderApproval?` | `{ WorkflowInstanceId, Decision, DecidedByUserId, DecidedAt, Reason?, Auto (bool), Channel (work-center \| email-link) }`; `Auto = true` → belge ve çizelge "Kendiliğinden onaylandı (kiracı ayarı)" (DESIGN-GAP §3) |
| 24 | `Transmission` | `PreOrderTransmission?` | `{ Attempts[{ At, DispatchIds[], Status, ReasonCode? }], LastStatus, TransmittedAt?, DepotDocumentRef (PDF, MOD-0262), PharmacyCopyRef }`; alıcı özeti adres içermez |
| 25 | `Cancellation` | `PreOrderCancellation?` | `{ ByUserId, At, Reason (zorunlu), NotifiedDepot, NotifiedPharmacy }` (D5) |
| 26 | `DepotResponse` | `PreOrderDepotResponse?` | `{ Status (delivered \| partial), PartialLines[{ LineNo, DeliveredQuantity }], Note?, ByUserId, At }` — yalnız satış operasyonu (D9) |
| 27 | `CopiedFromPreOrderId` | Guid? | kopyadan açıldıysa (D6) |
| 28 | `Events` | `PreOrderEvent[]` | **yalnız ekleme**; `{ At, Type, ByUserId?, Payload (kod + parametre) }`; silinmez, değişiklik iste dahil (DESIGN-GAP §3); A'daki durum zaman çizelgesinin kaynağı |
| 29 | `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted`/`DeletedAt`/`Version` | — | `EntityBase` |

#### 4.1.1 `PreOrderLine`

| Alan | Tür | Kural |
|---|---|---|
| `LineNo` | int | 1'den başlar |
| `SkuId` | Guid | product-master v1.1 SKU; seviyesiz `getSku` (X-08) |
| `SkuLevel` | string | sunucunun çözdüğü seviye (`Lsku` \| `FinishedGood` \| `Gsku`), yanıttan yazılır |
| `Sku` | `{ Name, Form, PackQuantity, Gtin?, Code }` | anlık görüntü; barkod = FinishedGood satış kutusu GTIN'i (LSKU'da `gtin` P6'ya kadar null — sözleşme notu O-6); seçicide ad, form, kutu adedi, barkod |
| `Quantity` | int | ≥ 1 |
| `Mf` | `{ Buy (a), Free (b) }?` | "10+2" yazımı; a ≥ 1, b ≥ 1; MOD-0156 oranı b / (a + b) |
| `FreeQuantity` | int | MOD-0156'dan: floor(adet / a) × b (D7) |
| `DiscountPct` | decimal? | yalnız fiyatlı mod; 0 ≤ x < 100; 0,1 hassasiyet |
| `UnitPrice` / `VatRate` / `NetAmount` / `VatAmount` | decimal? | yalnız fiyatlı mod; MOD-0156 fiyat çözümünden (kademe = ülke ayarı, varsayılan **eczacı alış**, D11) |
| `MfRatioPct` / `CombinedDiscountPct` | decimal? | MOD-0156'dan; 0,1'e yuvarlı (D7) |
| `Outcome` | string | `ok` \| `soft` \| `hard` — satırdaki en kötü sonuç |
| `Findings` | `{ Code, Params{} }[]` | MOD-0156 gerekçe kodları (MOD-0156 §4.6.3); metin istemcide resx |
| `RuleRef` | `{ RuleId, RuleVersion }?` | uygulanan en özel kural |

### 4.2 Hesap ve gösterim kuralları (normatif; hesap MOD-0156'da)

- MOD-0168 tutar ve kural **hesaplamaz**; `PreviewPreOrderQuery` ve `SubmitPreOrderCommand` aynı `IPricingEvaluator`
  çağrısını yapar. İstemci aynı sonucu gösterir; istemci tarafı hesap yalnız anlık önizleme içindir, kaydedilmez.
- Gösterim: sayı ve tarih kullanıcının kültürüyle (`tr-TR` sabit değil); para birimi kodu her tutarın yanında; TR'de
  (liste TRY = yerel) yerel karşılık satırı çıkmaz; BAE'de (liste USD, yerel AED) "≈ … AED · kur 3,6725 · 19.10.2026 ·
  kaynak" satırı çıkar; adetli modda tutar ve kur gösterilmez, "Fiyatı depo uygular" notu çıkar (DESIGN-GAP §2, BRIEF #9–10).
- Gönderim anı = fiyat sabitleme anı (D12): taslak, yeni liste yürürlüğe girdikten sonra gönderilirse yeni fiyatı alır.

### 4.3 `PreOrderSettings` (kiracı başına tek, koleksiyon `commercial_pre_order_settings`)

| Alan | Varsayılan | Kural |
|---|---|---|
| `ApprovalMode` | `always` | `always` \| `auto-within-limits` (D1). `auto-within-limits`: kural aşımı yoksa gönderim → Onaylandı (Auto) → iletim |
| `ApprovalLinkTtlHours` | 48 | 12–168 (D3) |
| `PriceTierByCountry` | `{}` → her ülke için `pharmacist-purchase` | `depot` \| `pharmacist-purchase` \| `public` (D11, K-W10 f) |
| `TransmissionRetryMax` | 5 | 0–10; zamanlayıcı yeniden denemesi (K-N7 deseni) |
| `Version`, audit | — | sürüm kontrollü; değişiklik denetim kaydına yazılır |

### 4.4 `PreOrderApprovalLink` (koleksiyon `commercial_pre_order_approval_links`)

| Alan | Kural |
|---|---|
| `PreOrderId`, `PreOrderVersion` | bağlantı o sürüme bağlı; sipariş değişirse (değişiklik iste → yeni gönderim) eski bağlantı geçersiz |
| `TokenHash` | belirteç yalnız SHA-256 özetiyle saklanır; düz belirteç yalnız e-postada |
| `ApproverUserIds[]` | gönderimde çözülen onaycılar (K-W9) |
| `ExpiresAt` | gönderim + `ApprovalLinkTtlHours` (D3) |
| `ConsumedAt`, `ConsumedByUserId` | **tek kullanım** |
| Kural | Bağlantı **oturum açtırır** (SSO); oturum açan kullanıcı `ApproverUserIds` içinde VE `commercial.pre-orders.approve` sahibi değilse 404 (varlık sızdırmaz). Kullanılmış / süresi dolmuş / geçersizleşmiş bağlantı ayrı hâl ekranı gösterir (DESIGN-GAP §3: süresi dolmuş hâli eklenir). Oturumsuz onay **yok** (D4) |

### 4.5 Onay — Workflow + yönetici çözümleyicisi (D10, D16, K-W9)

```text
SubmitPreOrderCommand
  ├─ MOD-0156 değerlendirme (sert varsa 422 pre_order_hard_limit — gönderilmez)
  ├─ onay gerekir mi?  ApprovalMode = always  → evet (sebep tenant-setting)
  │                    auto-within-limits     → yalnız soft varsa evet (sebep soft-exceeded); yoksa Onaylandı (Auto)
  ├─ evet ise: onaycı = lineManagerOf(temsilci)  — Platform çözümleyicisi (K-N2: birincil koltuk → ReportsToPositionId →
  │            pozisyonu tutanlar, kişinin kendisi hariç; koltuk boşsa bir üst, döngü korumalı)
  │            bulunamazsa 422 pre_order_no_approver (gönderim olmaz; ekran A "Onay gerekir mi?" kutusu bunu önceden söyler)
  ├─ Workflow örneği: objectType commercial.pre-order, şablon COM-PREORDER-APPROVAL,
  │                   CandidatePrincipalIds = çözülen onaycılar (Platform StartWorkflowInstance çalışma anı adayları)
  ├─ onay bağlantısı (§4.4) + e-posta olayı commercial.pre-order.approval-requested
  └─ Status = pending_approval

Karar (Görev Merkezi ya da bağlantı → C ekranı)  → DecidePreOrderCommand
  - pozisyonda birden çok kişi: İLK karar geçerli (D10); ikinci karar 409 pre_order_already_decided (Version + durum koşulu)
  - SoD: temsilci kendi siparişini onaylayamaz (Workflow WithoutStarter + komut denetimi)
  - approve → approved → TransmitPreOrderCommand (sistem)
  - reject (gerekçe zorunlu) → rejected (son durum; kopyalanabilir, D6)
  - request-changes (gerekçe zorunlu) → draft; imza temizlenir (D2); olay kaydı korunur
```

- Workflow karar geri çağrısı ile C ekranındaki karar **aynı komutu** çağırır (iki yol, tek kural). Görev Merkezi derin
  bağlantısı: `/Commercial/PreOrders/Approve/{id}`.
- Vekâlet yok (D10 — sonra, Workflow).

### 4.6 E-posta olayları (W6-BE-c; Platform Notifications, K-N1…K-N3)

| Olay kodu | Alıcı (Kime / Bilgi) | Ne zaman | Not |
|---|---|---|---|
| `commercial.pre-order.approval-requested` | onaycı(lar) (`LineManagerOf` temsilci) | onay gerekince | tek kullanımlık bağlantı; yanıt adresi temsilci |
| `commercial.pre-order.auto-approved` | temsilci (Kime), yönetici (Bilgi) | kendiliğinden onayda | bilgi amaçlı (DESIGN-GAP §3 ek şablon) |
| `commercial.pre-order.changes-requested` | temsilci | değişiklik iste | gerekçe değişkeni |
| `commercial.pre-order.rejected` | temsilci | red | gerekçe değişkeni |
| `commercial.pre-order.transmitted-depot` | depo e-postası (Account; dış adres) | iletimde | **depo nüshası PDF eki** (§20.2 AÇIK-4) |
| `commercial.pre-order.pharmacy-copy` | eczane e-postası (Account) | iletimde | eczane kopyası PDF eki |
| `commercial.pre-order.cancelled` | depo + eczane (iletildiyse) · temsilci + yönetici | iptalde | gerekçe değişkeni (D5) |

- Şablonlar Platform tohumunda **7 dilde**; içerik 06 mockup'ının `sip_*` şablonlarından + W6 eklerinden (değişiklik iste,
  iptal, kendiliğinden onay bilgisi — DESIGN-GAP §3). Değişkenler gösterim metnidir, üretici biçimler (PLT-NOTIFY).
- **İş akışını asla bozmaz** (K-N8): onay / gönderim e-posta başarısız olsa da tamamlanır; iletim başarısızlığı yalnız
  iletim durumunu etkiler (§12). Çağrı 3 sn sınırlı, 1 kez yeniden denenir, istisna fırlatmaz.
- Tekillik: (kiracı, olay, sipariş, iletim denemesi) başına bir gönderim.

### 4.7 Depo belgesi (E; W6-BE-c)

- İki nüsha: **depo nüshası** ve **eczane kopyası**; A4, tek sayfa önizleme + PDF.
- İçerik: satan şirket (ad, adres, VKN), depo, sipariş no, tarih, temsilci, eczane (ad, GLN, eczane kodu, adres, vergi no),
  satırlar (ürün, barkod, adet, MF, iskonto %, birim fiyat, KDV oranı, satır toplamı), toplamlar, yerel karşılık (kur + tarih
  + **kaynak**), eczacı imzası görseli, onaylayan (ya da "Kendiliğinden onaylandı (kiracı ayarı)"), sipariş notu,
  "Bu bir ön sipariştir; faturayı depo düzenler" notu, iletim izi.
- **Depo nüshasında etkin indirim % gizlenir** (iç bilgi; DESIGN-GAP §3).
- PDF iletimde üretilir, MOD-0262'ye yazılır, `Transmission.DepotDocumentRef` / `PharmacyCopyRef` ile bağlanır; önizleme
  aynı şablondan okunur (iki kopya şablon yasak).
- Metinler 7 dilde, belgenin dili = alıcının dili (depo / eczane için kiracı varsayılanı; ülke dili sonra).

### 4.8 Görünürlük (BRIEF #12, D10)

| Kim | Ne görür | Nasıl |
|---|---|---|
| Temsilci | `Rep.UserId == çağıran` | `commercial.pre-orders.read` |
| Yönetici | kendi + Reports-to altındaki herkes (D10) | `commercial.pre-orders.read` + Platform ekip okuması (K-W7 / W5 "ekip okuma deseni") |
| Satış operasyonu | tümü | `commercial.pre-orders.read-all` |

- Kapsam dışı kayıt → **404** (kiracılar arası ile aynı; varlık sızdırmaz). MOD-0018-FU15 gerçek veri kapsamı çözücüsü
  gelene kadar kapsam komut / sorgu içinde bu kuralla uygulanır (crm-rbac-integration-plan §7).
- B listesinde "Onayımı bekleyenler" süzgeci: `Status = pending_approval` ve çağıran `ApproverUserIds` içinde.

---

## 5. Repo kapsamı (paket onaylanınca)

```text
── services/Diten.CrmService/ ──
src/Diten.CrmService.Domain/Entities/Commercial/PreOrder.cs                    (YENİ — kök + gömülü tipler + sözlük)
src/Diten.CrmService.Domain/Entities/Commercial/PreOrderSettings.cs            (YENİ)
src/Diten.CrmService.Domain/Entities/Commercial/PreOrderApprovalLink.cs        (YENİ)
src/Diten.CrmService.Domain/Repositories/Commercial/IPreOrder*Repository.cs    (YENİ ×3)
src/Diten.CrmService.Application/Features/Commercial/PreOrders/
├── Commands/ · Queries/ · Handlers/{CommandHandlers,QueryHandlers}/ · Validators/
├── PreOrderPermissions.cs           (commercial.pre-orders.* / commercial.pre-order-settings.* sabitleri)
├── PreOrderStateMachine.cs          (§12 geçişleri TEK yerde)
├── PreOrderReasonCodes.cs
├── PreOrderApprovalService.cs       (onaycı çözümü + Workflow + bağlantı)
├── PreOrderTransmissionService.cs   (PDF + bildirim + yeniden deneme)
└── PreOrderModels.cs                (bütün DTO / VM tek dosyada)
src/Diten.CrmService.Infrastructure/Persistence/Commercial/**                  (YENİ — depolar, sınıf eşlemesi, indeksler)
src/Diten.CrmService.Infrastructure/Commercial/PreOrderDocumentRenderer.cs     (YENİ — PDF; tek şablon)
src/Diten.CrmService.Infrastructure/Commercial/PreOrderTransmissionRetryService.cs (YENİ — IHostedService + PeriodicTimer, K-N7 deseni)
src/Diten.CrmService.Api/Controllers/Commercial/PreOrders*Controller.cs        (YENİ ×3)
tests/Diten.CrmService.Application.Tests/Commercial/PreOrders/**               (YENİ)

── services/Diten.Platform/ ── (yalnız CrmManifestProvider satırları — K-N3 / W3-BE-c deseni)
src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs  (menü sayfaları + NotificationEvents + izin anahtarları)
(Platform bildirim şablon tohumu — 7 dil; PLT-NOTIFY deseni)

── frontend/Diten.Web/ ──
Controllers/Commercial/CommercialPreOrdersController.cs   (aynı köken vekil)
Views/Commercial/PreOrders/{Index,Create,Edit,Details,_Form,_Filter,_DataTable,_IndexL10n,PreOrdersIndex.cs}
Views/Commercial/PreOrders/{Approve,ApprovalLink,Document}.cshtml       (C, C-bağlantı, E)
Views/Commercial/PreOrderSettings/**
wwwroot/assets/js/Commercial/PreOrders/**  ·  wwwroot/assets/js/Commercial/PreOrderSettings/**
Resources/Views/Commercial/PreOrders/*.{ar,en,es,fr,ru,tr,zh}.resx  (+ PreOrderSettings)

── gateway/ ── (İLAN — integration-agent yazar)
gateway/Diten.ApiGateway/ocelot.json                         (§15 rotaları)
gateway/Diten.ApiGateway.Tests/VwW6PreOrderRouteCoverageTests.cs   (rota kapsama satırları — §15)

── Bu paket (bugün geçerli tek yazım yüzeyi) ──
execution/domains/commercial-suite/module-packs/MOD-0168-order-capture.md
```

---

## 6. Korunan yollar

| Yol | Neden |
|---|---|
| `.antigravity/**` | küresel mühendislik sistemi (RCS-001 §3 `ORD` satırı dahil → AÇIK-3) |
| `services/Diten.CrmService/**/Features/{Visit*,PlannedVisit,Account,Contact,Territory,Segment*}/**` | CRM modülleri; yalnız mevcut okuma yollarıyla tüketilir, git diff ∅ |
| `services/Diten.CrmService/**/Features/Commercial/Pricing/**` | MOD-0156; yalnız `IPricingEvaluator` / `IPriceResolver` / `IFxRateReader` arayüzleriyle tüketilir |
| `services/Diten.Platform/**` (CrmManifestProvider satırları ve bildirim şablon tohumu dışında) | başka domain |
| `services/Diten.MdmService/**` | MDM; yalnız sözleşmeli okuma |
| `gateway/**/ocelot.json` | integration-agent |
| RBAC katalog / tohum / `rolePermissions` | F-RBAC |
| `execution/registries/**` | CT |
| `frontend/Diten.Web/Views/Shared/_Layout*.cshtml`, `Archive/**` | DONUK; menü manifestten gelir |
| Mongo elle düzenleme | yasak (GUID alt türü tuzağı) |

---

## 7. Bağımlılıklar

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0156** değerlendirme + fiyat çözümü + kur okuma | süreç içi DI | **draft paket** (bu WP) | MOD-0168 kodu, MOD-0156'nın §4.6 arayüzleri yazılmadan derlenmez → W6-BE-b önce ya da aynı dalga (WP §6) |
| **MOD-0149** Account (eczane, depo) | salt okuma | shipped | tür = distribütör / ecza deposu süzgeci (K-W13); eczane alanları GLN / eczane kodu / vergi no / e-posta — eksik alan BE-a ölçer |
| **MDM** product-master v1.1 | salt okuma (HTTP, Gateway) | main'de | `getSku` seviyesiz; `searchStockableItems?status=Active` seçici; `bulkReadSkus` |
| **MDM** LegalEntity | salt okuma | main'de | ülke, para birimi, ad, vergi no, kısa kod (RCS-001 §5.4 — alan yoksa AÇIK-3) |
| **Platform** PLT-ORG-SEAT (K-L8) | salt okuma | **shipped** (W3L) | koltuk → tüzel kişilik |
| **Platform** yönetici çözümleyicisi (K-N2) | salt okuma | PLT-NOTIFY ile **yazılıyor** (iç `ILineManagerResolver`) | CRM'in **onaycı kimliklerini** okuyacağı bir uç bugün yok → AÇIK-2 |
| **Platform** Notifications iç ucu (K-N1) | çağrı | PLT-NOTIFY yazılıyor | dış adres alıcısı var; **ek (PDF) yok** → AÇIK-4 |
| **MOD-0023** Workflow | çağrı + geri çağrı | shipped | çalışma anı adayları (`CandidatePrincipalIds`) destekleniyor (ölçüldü: `StartWorkflowInstanceHandler`); canlı şablon W6-CFG |
| **MOD-0262** ikili depo | çağrı | FU01 BUILT (canlı doğrulanmadı) | CRM `HttpVisitEvidenceStore` deseni |
| **RCS-001** ortak kod üretici | çağrı | **yok** | AÇIK-3 |
| **AUD-001** CRM denetim izi | — | CRM izleri `aday` | §21; AÇIK-6 |
| **MOD-0018-FU15** veri kapsamı | — | planned | §4.8 geçici uygulama |
| **DEV-0001** Golden Compact | şablon | shipped | B listesi |

---

## 8. Çalışma zamanı kısıtları

- **Kiracı:** her okuma / yazma `ITenantContext` ile; kiracılar arası ve kapsam dışı → 404.
- **Süreç içi:** MOD-0156 DI ile çağrılır, Gateway üzerinden kendine HTTP çağrısı yapılmaz.
- **Dış çağrılar (MDM, Platform):** kullanıcı token'ıyla Gateway üzerinden (WorkingCalendar / W3L emsali); zaman aşımı → kontrollü
  hata. Ekran A'nın bağlamı (koltuk, liste) erişilemezse sipariş açılmaz, kullanıcıya neden söylenir (sahte varsayılan yok).
- **Eşzamanlılık:** `Version`; karar, iptal, depo cevabı, iletim durum + sürüm koşullu tek yazım (`findOneAndUpdate`).
- **İşlem:** gönderim (sipariş + bağlantı) iki kök yazar → `SupportsTransactionsAsync` korumalı işlem + telafi (CRM bağımsız
  Mongo tuzağı); Workflow ve e-posta işlemden **sonra** (K-N8).
- **Yeniden deneme:** `PreOrderTransmissionRetryService` (`IHostedService` + `PeriodicTimer`, varsayılan 15 dk, atomik claim,
  artan bekleme, `TransmissionRetryMax`); enjekte `TimeProvider`; testlerde kapalı.
- **Para:** `decimal`; yuvarlama MOD-0156'da; MOD-0168 yeniden yuvarlamaz.
- **Tarih:** `ExecutedAt` benzeri tek `DateTimeOffset`; iki `DateTimeOffset` birlikte sıralanmaz; `RequestedDeliveryDate` `DateOnly`.
- **Kişisel veri:** hasta verisi yok. Eczacı adı + imza görseli iş verisidir; imza ikilisi MOD-0262'de, siparişte yalnız referans.

---

## 9. Yerleşim ve kabuk sözleşmesi

`shell: tenant` ⇒ her Razor sayfasında **açıkça** `Layout = "_LayoutTenantShell";` (`_ViewStart`'a güvenilmez).

| Ekran | Biçim | Not |
|---|---|---|
| **B** liste | **Golden Compact DataTable** (`data-dt-standard="v2"`, `data-dt-data-mode="server"`): `_Filter` kartı, `DitenDataTable.createList`, Görünümü Kaydet, iskelet, sağ üstte standart birincil düğme "Yeni ön sipariş", hızlı görünüm offcanvas (satırlar + zaman çizelgesi; satış operasyonunda depo cevabı) | süzgeçler: tarih aralığı, durum, temsilci (temsilcide yok), eczane, depo, ürün, ülke / şirket, "Onayımı bekleyenler"; sütunlar: no, tarih, eczane + şehir, depo, temsilci, satır sayısı, toplam (para birimiyle), isteğe bağlı yerel karşılık, etkin indirim, durum (+ depo cevabı), kural uyarısı simgesi; Excel dışa aktarma; **para birimleri arası toplam / sıralama yok** |
| **A** oluştur / düzenle | Compact `Create` / `Edit` / `_Form` — duyarlı sayfa (tablet + telefon öncelikli); cihaz çerçevesi ve A'nın kendi üst çubuğu **alınmaz** | "Sistemden 🔒" şeridi, eczane, satırlar (kural rozeti + ileti, fiyat kaynağı satırı, MF satırı, SKU bilgisi), son 3 sipariş, depo, teslim tarihi, not, sipariş özeti, "Onay gerekir mi?" kutusu (onaycı + boş koltuk yedeği), imza, Gönder (sert / imza yok / satır yok → kapalı + açıklama), gönderimden sonra durum zaman çizelgesi, "İptal et ve yeni sipariş aç" / "Kopyala ve yeni sipariş aç" |
| **Details** | Compact `Details` | salt okuma sipariş + zaman çizelgesi + belge bağlantısı |
| **C** onay | `Approve.cshtml` (Görev Merkezi derin bağlantısı) | "Neden onayımda?", "Siz … Reports to pozisyonundasınız", satırlarda girilen ↔ sınır, 6 aylık grafik + tablo, Onayla / Reddet / Değişiklik iste (son ikisi gerekçe zorunlu) — **kendi breadcrumb'ı yok** |
| **C-bağlantı** | `ApprovalLink.cshtml` | oturum açtıktan sonra C'nin sade hâli; "Bu bağlantı bir kez kullanılabilir · süresi {48} saat"; kullanılmış / süresi dolmuş / geçersiz hâlleri |
| **E** belge | `Document.cshtml` (menü öğesi değil, siparişe ait) | depo nüshası ↔ eczane kopyası düğme grubu; iletim izi |
| Ayarlar | `Views/Commercial/PreOrderSettings/` tek form | §4.3 alanları |

**UI standartları (DESIGN-GAP §4 — standart kazanır):** başlık her sayfada Golden başlık `div.mb-3 > h5.mb-0 + p.mb-0.text-muted`
(`h4` yok) · sekme gerekiyorsa ortak `_TabCard` (wcn-tabcard) · bütün form alanları `.diten-field` simgeli · renk
`--bs-primary` (sabit `#0b1a8c` YOK; uyarı / hata renkleri anlamsal) · RTL için mantıksal özellikler (çekmece yönü, satır
kenarı, oklar) · sayı / tarih kültürle · menü manifestten (`CrmManifestProvider`; `_LayoutTenantShell`'e elle `<li>` yok) ·
yetkisiz kullanıcıya iskelet çizilmez, yönlendirme yok (UAS-001).

**Menü anahtarları** (normalize biçim; `check_pack_nav_keys`): `Nav.Page.PREORDERS`, `Nav.Page.PREORDERSETTINGS`. Menü
sayacı (onayımı bekleyen sayısı) manifest sayaç deseniyle.

---

## 10. Backend dosya düzeni

Golden Compact birebir: `Features/Commercial/PreOrders/{Commands,Queries,Handlers/CommandHandlers,Handlers/QueryHandlers,Validators}` +
tek `PreOrderModels.cs`. Adlandırma: komut `{Fiil}PreOrderCommand` (record), sorgu `Get{…}Query` (record), işleyici
`{Fiil}PreOrderHandler` (class, sonek YOK), doğrulayıcı `{Fiil}PreOrderValidator` (sonek YOK). Durum geçişleri yalnız
`PreOrderStateMachine`'de (iki kopya yasak). DataTable modülünde `BulkDelete` zorunluluğu: **uygulanmaz** — sipariş
silinmez (olay kaydı + iz), yalnız taslak iptal edilir; gerekçe bu satırdır. `Response<T>` zarfı + `CustomBaseController`.
Dört pipeline davranışı. Sınıf eşlemesi (gömülü tipler dahil) **zorunlu**.

---

## 11. Ön yüz dosya sözleşmesi (Compact)

```text
Views/Commercial/PreOrders/
├── Index.cshtml · Create.cshtml · Edit.cshtml · Details.cshtml · _Form.cshtml
├── _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml · PreOrdersIndex.cs
├── Approve.cshtml · ApprovalLink.cshtml · Document.cshtml           (Compact dışı ekler — C, C-bağlantı, E)
wwwroot/assets/js/Commercial/PreOrders/{index,form,approve,document}.js + *.l10n.js
Resources/Views/Commercial/PreOrders/*.{ar,en,es,fr,ru,tr,zh}.resx
```

Compact'ta `_CreateEditOffcanvas.cshtml` **YASAK**; hızlı görünüm offcanvas'ı Golden Compact'ın liste hızlı görünümüdür.
Kural iletileri (MOD-0156 gerekçe kodları) `PreOrders` resx'inde **7 dilde**: `Rule.{code}` + parametre yer tutucuları.
Liste ekranına dokunan iş `verify_datatable_page.py --area Commercial --module PreOrders --format gaps` koşturur, sapmaları
numaralı listeyle sorar (BL-440). Sayfa yardımı (UXH-001): kodlar `COM-PREORDER-LIST`, `COM-PREORDER-FORM`,
`COM-PREORDER-APPROVE`, `COM-PREORDER-SETTINGS` için `Help.{KOD}.Purpose/Audience/Step{n}/Step{n}Title` 7 dilde; tur **yok**
(beş akıştan biri değil). `.resx` değişikliği tam fleet yeniden başlatma ister.

---

## 12. Durum makinesi

```text
              ┌──────────── request-changes (gerekçe; imza temizlenir) ───────────┐
              ▼                                                                     │
 (yeni) ──► draft ──submit──► pending_approval ──approve──► approved ──(sistem)──► transmitted ──► [depo cevabı]
              │   │                 │                          ▲   │                     │
              │   │                 └──reject (gerekçe)──► rejected (SON; kopyalanabilir)│
              │   └─submit, onay gerekmez (auto-within-limits + sınır içi)──► approved (Auto)
              │                                               │   └─(iletim hatası)──► transmission_failed
              └─discard──► cancelled (taslak; numara boşa düşer, D15)      transmission_failed ──retry──► transmitted
                                                                │
   approved | transmitted | transmission_failed ──cancel (gerekçe; D5)──► cancelled (SON)
```

| Durum | Anlam | Düzenlenir mi | Çıkış |
|---|---|---|---|
| `draft` | Taslak; numara verilmiş (D15) | **evet** (oluşturan temsilci) | submit · discard |
| `pending_approval` | Onay bekliyor | **hayır** (DESIGN-GAP §2) | approve · reject · request-changes |
| `approved` | Onaylandı (ayrı durum — DESIGN-GAP §3) | hayır | sistem iletimi · cancel |
| `transmission_failed` | İletim başarısız | hayır | retry (zamanlayıcı / satış operasyonu) · cancel |
| `transmitted` | Depoya iletildi | hayır; yalnız depo cevabı eklenir | cancel |
| `rejected` | Reddedildi (SON) | hayır | — (kopya yeni kayıttır) |
| `cancelled` | İptal edildi (SON) | hayır | — (kopya yeni kayıttır) |

- Depo cevabı ayrı bir durum değildir; `transmitted` üzerinde `DepotResponse` alanıdır (B'de durum rozetinin yanında).
- Geri dönüş yalnız `pending_approval → draft` (değişiklik iste). Başka ters geçiş yok.
- Her geçiş `Events[]`'e bir satır ekler (`submitted`, `auto-approved`, `approval-requested`, `approved`, `rejected`,
  `changes-requested`, `transmission-attempted`, `transmitted`, `transmission-failed`, `cancelled`, `depot-response-recorded`,
  `copied-from`). Olay satırı silinmez.
- Kopyalama (D6): `rejected` ve `cancelled` siparişten yeni `draft` açar (yeni numara; satırlar, eczane, depo kopyalanır;
  fiyat, imza, onay kopyalanmaz). "İptal et ve yeni sipariş aç" = cancel + copy (iki komut, tek kullanıcı eylemi).
- İptal yetkisi (D5): siparişin temsilcisi ve temsilcinin yöneticisi (Reports-to ekibi) — `commercial.pre-orders.cancel`.

---

## 13. Doğrulama kuralları ve hata yolları

### 13.1 Alan doğrulama

| Alan | Kural | Hata |
|---|---|---|
| `Pharmacy.AccountId` | zorunlu; kiracıda var; tür eczane | 400 `pre_order_pharmacy_invalid` |
| `Depot.AccountId` | zorunlu (gönderimde); tür distribütör / ecza deposu; e-posta adresi var (iletim şartı) | 400 `pre_order_depot_invalid` · 422 `pre_order_depot_no_email` |
| `Lines` | gönderimde ≥ 1, ≤ 50; aynı SKU iki satırda olamaz | 400 `pre_order_lines_invalid` |
| `Lines[].SkuId` | MDM'de var, `lifecycleStatus` `Active`; `Retired` yasak | 400 `pre_order_sku_retired` / `pre_order_sku_not_found` |
| `Lines[].Quantity` | tam sayı ≥ 1 | 400 |
| `Lines[].Mf` | a ≥ 1, b ≥ 1, ikisi de tam sayı | 400 `pre_order_mf_invalid` |
| `Lines[].DiscountPct` | yalnız fiyatlı mod; 0 ≤ x < 100; en çok 1 ondalık | 400; adetli modda gönderilirse 400 `pre_order_discount_not_allowed` |
| `RequestedDeliveryDate` | bugün ya da sonrası (kiracı saat dilimi) | 400 |
| `Note` | ≤ 1000 | 400 |
| `Signature` | gönderim şartı | 422 `pre_order_signature_required` |
| Karar gerekçesi | reject / request-changes: zorunlu, ≤ 500 | 400 |
| İptal gerekçesi | zorunlu, ≤ 500 | 400 |
| Depo cevabı | `partial`: ≥ 1 satır, 0 ≤ teslim < sipariş adedi; not ≤ 500 | 400 |
| Ayarlar | §4.3 aralıkları | 400 |

### 13.2 Hata yolları (en az: mükerrer, eksik, yetkisiz, eşzamanlılık)

| Senaryo | Beklenen |
|---|---|
| Sert sınır aşılmış satırla gönderim | 422 `pre_order_hard_limit` + satır bulguları (kod + parametre); kayıt `draft` kalır |
| İmzasız gönderim | 422 `pre_order_signature_required` |
| Temsilcinin birincil koltuğu yok / Platform erişilemez | sipariş açılmaz: 422 `pre_order_no_seat` / 503 `pre_order_context_unavailable` |
| Onaycı bulunamadı (koltuk zinciri boş) | 422 `pre_order_no_approver`; ekran A önceden uyarır |
| Onay bekleyen siparişi düzenleme | 409 `pre_order_not_editable` |
| Aynı siparişe ikinci karar (iki onaycı) | ilk geçerli; ikinci 409 `pre_order_already_decided` (D10) |
| Temsilcinin kendi siparişini onaylaması | 403 / 404 (SoD) |
| Bağlantı: süresi dolmuş / kullanılmış / başka sürüm | ayrı hâl ekranı; karar yazılmaz |
| Bağlantı: oturum açan kullanıcı onaycı değil | 404 |
| Bayat `Version` ile güncelleme / karar / iptal | 409, sessiz üzerine yazma yok |
| Gerekçesiz red / değişiklik iste / iptal | 400 |
| `rejected` / `cancelled` siparişi iptal ya da onaylama | 409 `pre_order_invalid_transition` |
| İletim: Platform erişilemez | `transmission_failed` + yeniden deneme; onay geri alınmaz (K-N8) |
| Depo cevabını operasyon dışı kullanıcı işaretler | 403 (D9) |
| `transmitted` olmayan siparişe depo cevabı | 409 |
| Kiracılar arası / kapsam dışı sipariş kimliği | 404 |
| Retired SKU içeren taslağın gönderimi (taslaktan sonra emekliye ayrıldı) | 400 `pre_order_sku_retired`; satır işaretlenir |
| Liste yürürlükten kalktıktan sonra eski önizlemeyle gönderim | gönderimde yeniden değerlendirilir; fiyat o anki listeden (D12) |

---

## 14. Yetki sözleşmesi

PKS-001 biçimi (3 segment, lowercase-dotted); ad alanı `commercial.*` (K-O2; PKS-001 §4 satırı bu WP'de yazıldı). Aktör
`tenant_user`. Bütün uçlar `[HasPermission]`; ekranlar `Perms.Has(...)` ile — yetkisiz iskelet yok (UAS-001).

| Anahtar | Katman | Korur |
|---|---|---|
| `commercial.pre-orders.read` | T1 | liste, ayrıntı, belge önizleme, bağlam, önizleme sorgusu — **kendi + ekip** kapsamı (§4.8) |
| `commercial.pre-orders.read-all` | T3 | tüm siparişler (satış operasyonu) |
| `commercial.pre-orders.create` | T1 | taslak oluştur / düzenle / iptal (yalnız oluşturan), imza al, kopyala |
| `commercial.pre-orders.submit` | T3 | gönder (onaya / kendiliğinden onay) |
| `commercial.pre-orders.approve` | T2 | onayla / reddet / değişiklik iste (yalnız çözülen onaycı; SoD) |
| `commercial.pre-orders.cancel` | T3 | onaylanmış / iletilmiş / iletimi başarısız siparişi gerekçeyle iptal (temsilci + yöneticisi, D5) |
| `commercial.pre-orders.record-depot-response` | T3 | depo cevabı (satış operasyonu, D9) |
| `commercial.pre-orders.retry-transmission` | T3 | başarısız iletimi elle yeniden dene (satış operasyonu) |
| `commercial.pre-orders.export` | T2 | Excel dışa aktarma |
| `commercial.pre-order-settings.read` | T1 | kiracı ayarlarını oku |
| `commercial.pre-order-settings.update` | T1 | kiracı ayarlarını değiştir (denetim kaydı) |

- **Okuma / yazma / onay / yayımlama ayrımı:** okuma (`read`, `read-all`) · yazma (`create`, `submit`, `cancel`,
  `record-depot-response`, `retry-transmission`) · onay (`approve`) · yayımlama bu modülde yok (fiyat / kural yayımlama
  MOD-0156).
- Rol önerisi (tohum değil): Temsilci = read, create, submit, cancel · Yönetici = read, approve, cancel · Satış operasyonu =
  read, read-all, record-depot-response, retry-transmission, export, settings.read/update.
- Tohum, atama ve kiracı yetki betiği **bu pakette yok** → F-RBAC (W6-CFG grant betiği, dry-run varsayılan).

---

## 15. Gateway / API rota kararı

**Karar: Gateway değişikliği GEREKLİ** (`/api/commercial/*` için catch-all yok). `ocelot.json`'u yalnız integration-agent yazar;
**her BE istemi kendi rotalarını ve rota kapsama satırlarını içerir** (W3L / W5 dersi: W5-BE rotasız teslim edildi → 9a906e860).
Kural: **yeni uç = ocelot yöntemi + kapsama satırı aynı iste.** Her rota çifti OPTIONS içerir. Gövdesiz 204 cevaplar vekilde
`IsBodilessStatus` korumasıyla.

```text
Upstream → Downstream (Diten.CrmService)                                          İzin
GET    /api/commercial/pre-orders                       (liste, sayfalı)             read | read-all
POST   /api/commercial/pre-orders                       (taslak)                     create
GET    /api/commercial/pre-orders/context               (A şeridi + onaycı önizleme) create
POST   /api/commercial/pre-orders/preview               (değerlendirme, kaydetmez)   create
GET    /api/commercial/pre-orders/{id}                                                read | read-all
PUT    /api/commercial/pre-orders/{id}                  (taslak)                     create
POST   /api/commercial/pre-orders/{id}/discard                                        create
POST   /api/commercial/pre-orders/{id}/signature                                      create
POST   /api/commercial/pre-orders/{id}/submit                                         submit
POST   /api/commercial/pre-orders/{id}/decision         (approve|reject|request-changes) approve
POST   /api/commercial/pre-orders/{id}/cancel                                         cancel
POST   /api/commercial/pre-orders/{id}/copy                                           create
POST   /api/commercial/pre-orders/{id}/retry-transmission                             retry-transmission
POST   /api/commercial/pre-orders/{id}/depot-response                                 record-depot-response
GET    /api/commercial/pre-orders/{id}/document?copy=depot|pharmacy                   read | read-all
GET    /api/commercial/pre-orders/{id}/pharmacy-history  (6 ay)                       approve | read-all
GET    /api/commercial/pre-orders/export                                              export
GET    /api/commercial/pre-order-settings                                             pre-order-settings.read
PUT    /api/commercial/pre-order-settings                                             pre-order-settings.update
GET    /api/commercial/pre-order-approval-links/{token}  (oturum açtıktan sonra çözüm)  approve
```

- Ocelot: `/api/commercial/pre-orders`, `/api/commercial/pre-orders/{everything}`, `/api/commercial/pre-order-settings`,
  `/api/commercial/pre-order-approval-links/{everything}` — her biri ilgili yöntemler + OPTIONS.
- **Rota kapsama testi:** `gateway/Diten.ApiGateway.Tests/VwW6PreOrderRouteCoverageTests.cs` — yukarıdaki **her** (yol, yöntem)
  satırı bir `[InlineData]`; sabotaj kanıtı: bir rota çıkarılınca test kırmızı.
- Web: aynı köken vekil `/Commercial/PreOrders/api/...` (proxy-profile); tarayıcı servis portuna gitmez.
- Platform iç uçları (`api/internal/notifications/events/dispatch`) Gateway'den geçmez (K-N1, `X-Internal-Api-Key`).

---

## 16. Kabul ölçütleri

**AC-FLOW — akış**
- [ ] **AC-FLOW-1** Temsilci şirket / ülke / para birimi / fiyat modu seçemez; değerler koltuk → tüzel kişilik → MDM'den gelir
      (K-O4, K-L3); koltuksuz kullanıcıda sipariş açılmaz.
- [ ] **AC-FLOW-2** Taslak ilk kaydedilince `MG-ORD-` biçiminde numara alır; iptal edilen taslağın numarası yeniden kullanılmaz (D15).
- [ ] **AC-FLOW-3** Sert sınır, imza yokluğu ya da satır yokluğunda gönderim sunucuda reddedilir (istemci kapatması yetmez).
- [ ] **AC-FLOW-4** `ApprovalMode = always` (varsayılan) → her gönderim onaya gider; `auto-within-limits` + sınır içi →
      `approved (Auto)` ve iletim; yumuşak aşım her iki modda onaya gider (D1).
- [ ] **AC-FLOW-5** Onaycı = Reports-to pozisyonundaki kişi(ler); koltuk boşsa bir üst; ilk karar geçerli, ikincisi 409 (K-W9, D10).
- [ ] **AC-FLOW-6** Değişiklik iste → `draft`, imza temizlenir, olay kaydı silinmez (D2, DESIGN-GAP §3).
- [ ] **AC-FLOW-7** Onaylanan sipariş sistem adımıyla iletilir; başarısızlık `transmission_failed` + yeniden deneme; onay geri alınmaz.
- [ ] **AC-FLOW-8** İptal gerekçe ister; iletilmiş siparişte depo + eczane iptal e-postası olayı üretilir (D5).
- [ ] **AC-FLOW-9** Reddedilen / iptal edilen sipariş kopyalanınca yeni numara ile `draft` açılır; fiyat / imza / onay taşınmaz (D6).
- [ ] **AC-FLOW-10** Depo cevabını yalnız `record-depot-response` sahibi işaretler; kısmi teslimde satır adedi + not (D9).

**AC-PRICE — fiyat ve kural (MOD-0156 tüketimi)**
- [ ] **AC-PRICE-1** Gönderimde fiyat, kur ve değerlendirme sonucu siparişe anlık görüntü olarak yazılır; sonra liste / kur / kural
      değişse de sipariş belgesi aynı kalır (D12, K-W10 g).
- [ ] **AC-PRICE-2** `Features/Commercial/PreOrders/**` içinde MF / iskonto / birleşik / KDV formülü yok (yalnız MOD-0156
      arayüz çağrısı) — yapısal test.
- [ ] **AC-PRICE-3** Fiyatlı / adetli mod satan şirketin yürürlükte listesine göre seçilir; adetli modda iskonto alanı reddedilir.
- [ ] **AC-PRICE-4** Kural iletileri sunucudan **kod + parametre** gelir; sunucu yanıtında birleştirilmiş cümle yok; 7 dil resx.

**AC-LINK — onay bağlantısı (D3, D4)**
- [ ] **AC-LINK-1** Bağlantı oturum açtırır; oturumsuz karar yolu yok; tek kullanım; süre kiracı ayarı (12–168, varsayılan 48).
- [ ] **AC-LINK-2** Süresi dolmuş, kullanılmış ve geçersizleşmiş (değişiklik iste sonrası) bağlantı ayrı hâl gösterir, karar yazmaz.
- [ ] **AC-LINK-3** Belirteç düz metin saklanmaz (yalnız özet).

**AC-VIS / AC-SEC**
- [ ] **AC-VIS-1** Temsilci yalnız kendi, yönetici ekibini, `read-all` tümünü görür; kapsam dışı kimlik 404.
- [ ] **AC-SEC-1** Kiracılar arası her uç 404; her uç `[HasPermission]`; anahtarlar §14 listesiyle birebir.

**AC-DOC / AC-UI / AC-GW**
- [ ] **AC-DOC-1** Depo nüshasında etkin indirim % yok, sipariş notu var; kendiliğinden onayda "Kendiliğinden onaylandı (kiracı ayarı)".
- [ ] **AC-UI-1** B Golden Compact (`verify_datatable_page` sapmaları numaralı listede raporlanır); her sayfa `_LayoutTenantShell`
      açıkça; h5 başlık; `.diten-field`; `--bs-primary`; Arapçada (RTL) çekmece / kenar / ok doğru.
- [ ] **AC-UI-2** Para birimleri arası toplam ya da sıralama yok; yerel karşılık yalnız kur varsa.
- [ ] **AC-GW-1** §15'teki her (yol, yöntem) Gateway'de ve kapsama testinde.
- [ ] **AC-L10N-1** Bütün görünen metin 7 dilde (sayfa resx, kural iletileri, e-posta şablonları, PDF metinleri, yardım).

---

## 17. Test beklentileri (sabotaj kanıtlı)

- **Birim / uygulama:** durum makinesi (her izinli geçiş + her yasak geçiş 409) · gönderim (sert → 422; imza; satır) · onay modu
  (always / auto) · onaycı çözümü (sahte Platform: tek, çok, boş koltuk → üst, bulunamadı) · ilk karar geçerli (eşzamanlı iki
  karar) · değişiklik iste imzayı temizler, olayları korur · kopyalama · iptal yetkisi (temsilci, yönetici, başkası) · depo
  cevabı · iletim başarısız → yeniden deneme (enjekte `TimeProvider`) · bağlantı (süre, tek kullanım, sürüm, yanlış kullanıcı) ·
  görünürlük (kendi / ekip / tümü) · kiracı yalıtımı.
- **Kalıcılık (gerçek mongod):** kök gidiş-dönüş + sınıf eşlemesi (GUID alt türü) · indeksler (`TenantId+Code` tekil,
  `TenantId+Status+CreatedAt`, `TenantId+Rep.UserId`, `TenantId+Pharmacy.AccountId`) · `$ne` kısmi indeks yok · işlem desteksiz
  ortamda telafi.
- **Gateway:** `VwW6PreOrderRouteCoverageTests` (her satır).
- **Web:** xUnit + RunNode (vitest burada çalışmıyor): kural ileti kodu → resx metni, adetli modda tutar alanı yok, RTL sınıfları.
- **Sabotaj (en az 5, `.bak` + touch; her biri kırmızı görülmeli):** (1) onay bekleyen siparişin düzenlenmesine izin ver;
  (2) değişiklik isteğinde imzayı temizleme; (3) `auto-within-limits`'te yumuşak aşımı da kendiliğinden onayla; (4) bağlantıyı
  ikinci kez kullanılabilir yap; (5) gönderimde fiyat anlık görüntüsünü yazma (önizlemeden kopyala); (6) bir Gateway rotasını
  çıkar. Sabotaj, ajanın kendi yazmadığı bir kuralı da kapsar (CT kendi sabotajını ayrıca yapar).
- **Tam takım:** CRM tam test + mimari (`tests/architecture/TenantArchitecture.ArchitectureTests`, `-o` repo içi) + Gateway testleri;
  `py -3 scripts/known_failures.py check` ile taban karşılaştırması; sayılar gerçek koşudan okunur.
- **Doğrulayıcılar:** `verify_module_id --check-id MOD-0168` exit 0 · `verify_datatable_page --area Commercial --module PreOrders`
  · `check_pack_nav_keys` · resx-check (7 dil).
- **Duman (kullanıcı, E4):** TR fiyatlı (sınır içi / yumuşak / sert), BAE USD → AED kur satırı, KG adetli; onay Görev Merkezi'nden
  ve bağlantıdan; değişiklik iste; red + kopya; iletim (depo + eczane e-postası, PDF); iptal; depo cevabı; Arapça RTL; mockup
  görsel karşılaştırması (yan yana).

---

## 18. Ready-for-dev kontrol listesi

- [x] DCP-002 kimlik kapısı GEÇTİ (§0.1)
- [x] Kayıt tablosu kontrol edildi: `MOD-0168` kanonik, sahip `commercial-suite`
- [x] Golden Compact referansı seçildi (form alanı 9: eczane, depo, teslim tarihi, not, ziyaret bağı, satır SKU, adet, MF, iskonto) — `data_mode: server`
- [x] Künye zorunlu alanları dolu; tek `id`, tek `status`
- [x] Layout açıkça `_LayoutTenantShell` (§9)
- [x] Backend / ön yüz dosya düzeni Golden Compact (§10, §11)
- [x] Doğrulama kuralları + ≥ 4 hata yolu (§13)
- [x] İzin listesi + aktör (§14); Gateway kararı + kapsama testi şartı (§15)
- [x] Kabul ölçütleri test edilebilir (§16); test beklentileri sabotajlı (§17)
- [x] Denetlenen olaylar tablosu her yazma komutunu bir kez içeriyor (§21)
- [x] Sayfa yardım kodları yazılı; tur yok (§11)
- [x] D1–D16 + K-O1…K-O5 KİLİTLİ (§19, §20.1)
- [x] **§20.2 AÇIK-2 / AÇIK-3 / AÇIK-4 / AÇIK-6 kararları** — §20.3 (kullanıcı 2026-10-20); önkoşullar PLT-W6 + F8-a
- [x] **Ayrı karar:** `status: ready-for-dev` + `runtime_code_allowed: true` + `flip_approved_by` — kullanıcı onayıyla CT yapar — **YAPILDI 2026-10-20 (kullanıcı onayı)**

---

## 19. Uygulama notları / karar kaydı

### 19.a Bağlam (yeniden açılmaz)

| # | Karar | Kaynak |
|---|---|---|
| K-O1 | Sipariş = MOD-0168; fiyat / kural / değerlendirme / kur = MOD-0156; ikisi `Diten.CrmService` içinde ayrı sınırla | WP-VW-W6, D17 |
| K-O2 | İzin ad alanı `commercial.*` (PKS-001 §4 satırı) | kullanıcı 2026-10-19 |
| K-O3 | Transfer siparişi: depoya iletilir, faturayı depo keser, `fulfilment = depot`; stok hareketi yok; ürün kimliği v1.1 SKU, `Retired` girmez | K-W10 b / h |
| K-O4 | Ülke kapsamı MDM tüzel kişilikleri; satan şirket koltuktan; fiyatlı / adetli mod; tutar liste para biriminde, yerel karşılık kur anlık görüntüsü | K-W10 e / f / g |
| K-O5 | D1–D16 kullanıcı kabulü | DESIGN-GAP §7 |
| K-W9 | Onaycı / e-posta alıcısı = Reports-to; koltuk boşsa bir üst | ROADMAP |
| K-W13 | Depo = CRM Account (tür distribütör / ecza deposu) | ROADMAP |
| Paket kararı | API yolu `/api/commercial/...`, klasör `Features/Commercial/PreOrders`, koleksiyon `commercial_*` — K-O1 "ayrı sınır"ın somut biçimi | bu paket |

### 19.b D-soruları — KİLİTLİ (MOD-0168'e düşenler)

| # | Kilitli karar | Paketteki etkisi |
|---|---|---|
| **D1** | Sınır içi kendiliğinden onay = kiracı ayarı; **varsayılan her zaman yönetici onayı** | §4.3 `ApprovalMode = always`; §4.5; AC-FLOW-4 |
| **D2** | "Değişiklik iste" var; imza temizlenir, temsilci yeniden imza alır | §12; AC-FLOW-6; sabotaj (2) |
| **D3** | Bağlantı süresi 48 sa (kiracı ayarı 12–168) | §4.3, §4.4; AC-LINK-1 |
| **D4** | Oturumsuz onay **yok**; bağlantı oturum açtırır; tek kullanım + süre geçerli | §4.4; AC-LINK-1; sabotaj (4) |
| **D5** | İletimden sonra iptal: temsilci ve yöneticisi; gerekçe zorunlu; depoya + eczaneye iptal e-postası | §12, §4.6; AC-FLOW-8 |
| **D6** | Reddedilen sipariş "Kopyala ve yeni sipariş aç" | §12; AC-FLOW-9 |
| **D7** | MF oranla denetlenir; bedava kutu floor(adet / a) × b; yüzdeler 0,1 | MOD-0156'da hesap; MOD-0168 sonucu yazar (§4.1.1) |
| **D8** | En özel kural kazanır; dönem = geçerlilik tarihleri; eczane segmenti Account'taki BRD `pharmacy-segment` | MOD-0156'da; MOD-0168 segmenti girdi verir (AÇIK-5) |
| **D9** | Depo cevabını satış operasyonu işaretler: Teslim edildi / Eksik teslim (kısmi adet + not) | §4.1 #26; §14 |
| **D10** | Ekip = Reports-to altı; çok onaycıda ilk karar; vekâlet sonra | §4.5, §4.8; AC-FLOW-5 |
| **D11** | Fiyat kademesi ülke ayarı, varsayılan eczacı alış | §4.3 `PriceTierByCountry` |
| **D12** | Fiyat gönderimde sabitlenir | §4.1 #19; AC-PRICE-1; sabotaj (5) |
| **D13** | Liste yayımlama + kural değişikliği dört göz | MOD-0156 (bu pakette yok) |
| **D14** | Ara kur tablosunu satış operasyonu girer | MOD-0156 (bu paket yalnız okur) |
| **D15** | Numara taslak kaydedilince; iptal edilen taslağın numarası boşa düşer | §4.1 #3; AC-FLOW-2 |
| **D16** | Onay Görev Merkezi (Workflow) + e-posta bağlantısı; onaycıyı Platform yönetici çözümleyicisi bulur | §4.5, §4.6 |
| **D17** | Yer: Blueprint'teki yerinde → CrmService, ayrı sınır | §0.2 |

---

## 20. D-soruları çözüm tablosu, AÇIK noktalar ve takipler

### 20.1 Çözüm tablosu — KİLİTLİ (kaynak DESIGN-GAP-W6 §5 + §7, kullanıcı 2026-10-19)

| ID | ✅ Karar | Reddedilen seçenek |
|---|---|---|
| D1 | kiracı ayarı, varsayılan `always` | sabit kendiliğinden onay |
| D2 | değişiklik iste var; imza temizlenir | düğme yok (yalnız red) · imza kalır (mockup) |
| D3 | 48 sa, 12–168 | sabit süre |
| D4 | oturum açtıran bağlantı | oturumsuz onay (mockup) |
| D5 | temsilci + yöneticisi, gerekçeli, bildirimli | yalnız temsilci, gerekçesiz, bildirimsiz (mockup) |
| D6 | kopyala ve yeni aç | kopyalanamaz son durum (mockup) |
| D9 | yalnız satış operasyonu; kısmi adet + not | yönetici + operasyon (mockup) |
| D10 | Reports-to altı ekip; ilk karar; vekâlet sonra | tüm onaycıların onayı |
| D11 | ülke ayarı, eczacı alış | sabit kademe |
| D12 | gönderimde | taslakta / onayda |
| D15 | taslakta numara | gönderimde numara |
| D16 | Görev Merkezi + bağlantı; Platform çözümleyicisi | yalnız e-posta |
| DESIGN-GAP §3 | Onaylandı ayrı durum; iletim sistem adımı; İletim başarısız + yeniden deneme | "Onayla" = hemen ilet (mockup) |

### 20.2 AÇIK noktalar (yeni D-sorusu değil; kod öncesi cevap / sahip işi gerekir)

| # | Nokta | Neden açık | CT önerisi |
|---|---|---|---|
| **AÇIK-1** | Finance / Order-Mgmt SoR sınırı | kayıt tablosu: *"SoR boundary with Finance/Order-Mgmt = EA-TBD"* | **Program T3'e kalır** (K-W10 b, D17). İlk kapsamda etki yok: transfer siparişi, fatura yok |
| **AÇIK-2** | CRM'in onaycı **kimliklerini** okuyacağı Platform ucu | PLT-NOTIFY yalnız Platform içinde `ILineManagerResolver` + bildirim alıcısı `LineManagerOf` getiriyor; Workflow adayları, ekran A "onaycı kim" ve C "Siz … Reports to pozisyonundasınız" için CRM'e kimlik + ad + pozisyon dönen bir okuma gerekir | Platform'da `GET api/platform/organization/seats/{userId}/line-managers` (K-L8 koltuk ucunun eki, aynı yetki kuralı) — ayrı PLT işi, W6-BE-a'dan önce |
| **AÇIK-3** | RCS-001 ortak kod üretici + `ORD` önekinin RCS-001 §3'e kaydı + tüzel kişilik kısa kodu | kural "ortak servisten gelir" diyor; 2026-10-07 ölçümü: ortak üretici yok, `MG-` öneki hiçbir yerde yok; `ORD` satırı `.antigravity/**` (korunan) değişikliği ister; MDM'de kısa kod alanı ölçülmedi | Kullanıcı kararı: (a) önce ortak üretici (ayrı paket) ya da (b) W6'da CRM içinde §5.8 desenli sayaç (kiracı × şirket öneki × `ORD`), ortak üretici gelince ona geçer. `ORD` satırı için ayrı onaylı `.antigravity` değişikliği |
| **AÇIK-4** | Depo / eczane e-postasına **PDF eki** | PLT-NOTIFY iç uç sözleşmesinde (`InternalEventDispatchRequest`) ek alanı yok; Platform e-posta işi ek destekliyor (`NotificationDispatchAttachment`, kalıcı ek sınırı 256 KB) | İç uca isteğe bağlı `attachments[]` (dosya adı, tür, içerik ya da MOD-0262 referansı) — PLT-NOTIFY eki, W6-BE-c'den önce |
| **AÇIK-5** | Eczane segmenti alanı (D8) | D8 "kurum kaydındaki BRD `pharmacy-segment` setinden" diyor; MOD-0149 Account'ta bu alan ve BRD seti ölçülmedi / yok | MOD-0149'a alan eklemek başka modül işi (XMC-001) → kullanıcıya sorulur; set yayımı veri sahibi işi. Alan yokken kurallar segmentsiz (genel) çalışır |
| **AÇIK-6** | Denetim kaydı (AUD-001) | CRM'de kabul edilmiş iz yok (bütün izler `aday`); kural §5 K4: ortak iletici gelene kadar izi olmayan serviste yeni yazma komutu engelli. Proje uygulaması: kullanıcı kararı 2026-10-06 — CRM denetim bağlantısı Faz 8'de (AUD-CRM-1) | §21 tablosu `aday` yol (`crm-commercial-yayinci`, `HttpCrmAuditPublisher`, SourceModule MOD-0168) ile yazıldı; komutlar Faz 8 borcuna girer — kullanıcının bu paket için de aynı istisnayı onaylaması gerekir |

### 20.3 Takipler (F-*)

- **F-GW** — §15 rotaları + kapsama testi (her BE isteminin içinde).
- **F-RBAC** — anahtar kataloğu + W6-CFG grant betiği (dry-run varsayılan; kullanıcı `--apply` + yeniden giriş).
- **F-WF** — W6-CFG: `COM-PREORDER-APPROVAL` Workflow şablonu canlı oluştur + yayımla (W3-CFG deseni; çalışma anı adayları).
- **F-NOTIFY** — Platform şablon tohumu 7 dil (`commercial.pre-order.*`), AÇIK-4 eki.
- **F-MOBILE** — §22 mobil sözleşme notu (W8).
- **F-T3** — `fulfilment = direct` kanalı ve bizim satış siparişimiz (Program T3; AÇIK-1).
- **F-DEMAND** — onaylanan adetler MOD-0188'e ikincil talep sinyali (W6 dışı; MVP-4 sahibine sorulur).
- **F-DELEGATION** — onay vekâleti (Workflow; D10).
- **F-W10** — marka gösterimi MDM'den (W10, en sonda).

---

### 20.3 KARARLAR — kullanıcı 2026-10-20 (§20.2'yi geçersiz kılar)
| AÇIK | Karar |
|---|---|
| AÇIK-1 | Bilgi: Finance / Order-Mgmt SoR sınırı Program T3'e kalır (değişiklik yok). |
| AÇIK-2 | **EVET.** Platform'a "kullanıcının yöneticileri" okuma ucu: `GET api/platform/organization/seats/{userId}/line-managers` (K-L8 koltuk ucunun eki, aynı yetki kuralı: kendisi ya da `platform.organization-seats.read`); yanıt: kimlik + ad + pozisyon + çözüm biçimi (LineManager / ManagerChain / None), e-posta yok. **PLT-W6** işi, W6-BE-a'dan ÖNCE. Gateway + Platform.Common kiracı yolu listeleri (W3L dersi). |
| AÇIK-3 | **(b) EVET.** Numara CRM içinde RCS-001 §5.8 desenli sayaçla: kiracı × şirket öneki × `ORD` (en az 6 hane, sıfırlanmaz, taslakta verilir — D15); ortak üretici (RCS-001 §5.12) gelince ona geçer. RCS-001 §3 tablosuna `ORD` satırı kullanıcı onayıyla eklendi (2026-10-20). Şirket öneki MDM tüzel kişilik kısa kodundan; alan yoksa W6-BE-a raporlar ve DUR. |
| AÇIK-4 | **EVET.** PLT-W6: iç olay ucuna isteğe bağlı `attachments[]` (dosya adı, tür, MOD-0262 belge referansı ya da içerik; Platform'un mevcut `NotificationDispatchAttachment` sınırlarıyla). W6-BE-c'den ÖNCE. |
| AÇIK-5 | **EVET — ilk sürüm segmentsiz.** Kurallar segment alanı olmadan çalışır; segment desteği (Account alanı + BRD `pharmacy-segment`) sonraki dilim. |
| AÇIK-6 | **Kullanıcı: "denetim kayıtlarını yapalım."** MOD-0168 yazma komutları Faz 8 borcuna GİRMEZ; denetimli yazılır. Önkoşul **F8-a** (AUD-001 K4 ortak iletici + işaret `Diten.Building.Blocks` içinde, CRM benimsemesi, yol `b`: `POST /api/internal/audit/append`, Platform'da CRM kategori / işlem tanımları, anahtar boşsa sessiz değil — K8). §21'deki `aday` yolları F8-a ile `b` olur; W6-BE komutları işareti taşır. F8-a W6-BE-a'dan ÖNCE kabul edilir. |

Sıra: **PLT-W6 ∥ F8-a → W6-BE-a → W6-BE-b ∥ W6-BE-c → W6-WEB-a ∥ W6-WEB-b.**

## 21. Denetlenen olaylar (AUD-001)

> **Servisin denetim altyapısı bugün var mı?** `tests/architecture/audit-ledger/Diten.CrmService.md` `## İzler`: bütün izler
> `aday` (yalnız uygulama günlüğü; `Crm:Audit:Mode=http` ayarı yok). Kabul edilmiş a / b / c izi **yok** → bu bir
> **bağımlılık / engeldir** (§7, §20.2 AÇIK-6). Aşağıdaki "Yol" sütunu, Faz 8'de (AUD-CRM-1) `b`'ye çevrilecek iletimi gösterir;
> bugün `aday`dır.

| Komut | Olay adı | Nesne | Yol | Değişen alanlar | İstisna |
|---|---|---|---|---|---|
| CreatePreOrderCommand | commercial.pre-order.created | PreOrder | aday→b | After: Code, Pharmacy, Depot, Lines | — |
| UpdatePreOrderCommand | commercial.pre-order.updated | PreOrder | aday→b | Before/After: Lines, Depot, Note, Date | — |
| DiscardPreOrderCommand | commercial.pre-order.discarded | PreOrder | aday→b | Status | — |
| CapturePreOrderSignatureCommand | commercial.pre-order.signature-captured | PreOrder | aday→b | Signature.BinaryRef, SignerName (görsel değil) | — |
| SubmitPreOrderCommand | commercial.pre-order.submitted | PreOrder | aday→b | Status, Price, Fx, Evaluation, ApprovalRequirement | — |
| DecidePreOrderCommand | commercial.pre-order.decided | PreOrder | aday→b | Decision, Reason, Channel, Status | — |
| CancelPreOrderCommand | commercial.pre-order.cancelled | PreOrder | aday→b | Status, Reason | — |
| CopyPreOrderCommand | commercial.pre-order.copied | PreOrder | aday→b | CopiedFromPreOrderId, yeni Code | — |
| TransmitPreOrderCommand | commercial.pre-order.transmission-attempted | PreOrder | aday→b | Transmission.LastStatus, DispatchIds (adres yok) | — |
| RecordPreOrderDepotResponseCommand | commercial.pre-order.depot-response-recorded | PreOrder | aday→b | DepotResponse | — |
| UpdatePreOrderSettingsCommand | commercial.pre-order-settings.updated | PreOrderSettings | aday→b | Before/After: tüm alanlar | — |
| ConsumePreOrderApprovalLinkCommand | commercial.pre-order.approval-link-consumed | PreOrderApprovalLink | aday→b | ConsumedAt, ConsumedByUserId | — |

- Kayda **yazılmayanlar:** bağlantı belirteci (düz ya da özet), imza görseli, e-posta adresleri.
- K2 sınıfı (kimlik / yetki / kiracı durumu / GxP / KVKK özel nitelikli) komut yok.
- Bu komutlar `## Bilinen borç` listesine **eklenemez** (liste yalnız küçülür); AUD-001 mimari testi bu komutlar için kırmızı
  olur → kullanıcının Faz 8 istisnası (AÇIK-6).

---

## 22. Mobil not yeri (W8)

> W8 her fazın sonunda mobil sözleşme notu ister (ROADMAP W8). Bu bölüm W6 kabulünden sonra CT tarafından doldurulur; bugün
> yalnız iskelet.

- Mobilin kullanacağı uçlar: `context`, `preview`, taslak oluştur / güncelle, `signature`, `submit`, liste (kendi), ayrıntı,
  `cancel`, `copy`. Onay mobilde yok (Görev Merkezi / bağlantı).
- Çevrimdışı taslak: istemcide; numara sunucuda ilk kayıtta (D15).
- İmza: görüntü yükleme sözleşmesi (MOD-0262 boyut / tür sınırları).
- Kural iletileri: gerekçe kodu + parametre; mobil kendi 7 dil metnini taşır.
- Açık: (W8'de yazılır).

---

## Teslim

Paket **`status: draft`** — `runtime_code_allowed: false`, `flip_approved_by` boş. DCP-002 kapısı GEÇTİ. D1–D16 + K-O1…K-O5
KİLİTLİ; AÇIK-1 (T3'e kalır), AÇIK-2…AÇIK-6 kod öncesi cevap bekler. Hazırlıkta Golden Reference **compact** şablon alındı;
bilinçli sapmalar: C / C-bağlantı / E ek sayfaları ve `BulkDelete` yokluğu (gerekçeleri §9–§11). Kullanıcı onayından sonra CT
`ready-for-dev`'e çevirir; ardından W6-BE-a / W6-BE-c / W6-WEB-a istemleri WP-VW-W6'ya eklenir.
