---
id: MOD-0040-FU02
name: Record Code Service
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: draft
approval_status: pending
owner: Beste (platform-shared-services, K2)
branch: feature/pss/mod-0040-fu02-record-code-service
started: 2026-10-30
target: 2026-11-06
form_field_count: 0
parent: MOD-0040
parent_meaning: Blueprint 8.8 MOD-0040 Canonical ID & Correlation Standard — repodaki MOD-0040 / MOD-0040-FU01 takma adları değil
backlog: BL-565
rule: RCS-001
consumer_module: MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289, MOD-0217, MOD-0178
contract_draft: docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md
revision: r3 — bağımsız inceleme 02 KOŞULLU (0 ENGEL, 2 ORTA, 9 DÜŞÜK; hedef e49a67359, pack blob b18184f0, bulgu dosyası özeti 46aeb4a5) bulguları F-1…F-11 işlendi; r2 — inceleme 01 UYGUN DEĞİL (bulgu dosyası özeti 277fd253) bulguları ve sahip kararları K-8r, K-9…K-12 işlendi
code_start: yok — CT-APR-02 bağımsız inceleme kaydı ve CT-APR-01 onay satırı olmadan kod izni yoktur
---

# MOD-0040-FU02 — Record Code Service (ortak kayıt kodu servisi, RCS-001)

> **Taslak — kod başlatma izni değildir.** `status: draft`, `approval_status: pending`. Bu paket, aynı sürümünün
> **CT-APR-02 bağımsız incelemesi** (`execution/approvals/reviews.tsv` + bulgu dosyası) ve **CT-APR-01 onay satırı**
> (`execution/approvals/approvals.tsv`, paketin bu sürümünün SHA-256'sına bağlı) main'e girmeden ve künye
> `ready-for-dev` olmadan hiçbir kod, yapılandırma, gizli bilgi ya da veri değişikliğini yetkilendirmez. Bir WP paketin
> tamamını alamaz; dilim tablosundaki (Bölüm 23) bir ya da birkaç dilimi alır. DL-2 ve sonrası bu sürümde **kod izni
> taşımaz** — her biri kendi paket revizyonu ve onayıyla açılır.
>
> **Revizyon r3 (2026-10-30).** Bağımsız inceleme 02 (hedef `e49a67359`, pack blob `b18184f0…`; bulgu dosyası
> `docs/records/reviews/MOD-0040-FU02/2026-10-30-b18184f0.md` @ `bbd30dd87`, özet `46aeb4a5…`) sonucu **KOŞULLU**
> (0 ENGEL, 2 ORTA, 9 DÜŞÜK). Bulgular F-1…F-11 (CT onaylı inceleyen önerileri) bu sürüme işlendi; kapanış yerleri
> Bölüm 19.2'de (`F-n@46aeb4a5`). İnceleme 02'nin notu: iki orta bulgu kapanmadan CT-APR-01 önerilmez; r3 doğrulama
> incelemesine (03) gider.
>
> **Revizyon r2 (2026-10-30).** Bağımsız inceleme 01 (hedef `74c62a076`, pack blob `219f30c9…`; bulgu dosyası özeti
> `277fd253…`) sonucu **UYGUN DEĞİL** (1 ENGEL, 4 ORTA, 7 DÜŞÜK). Bulgular F-1…F-12 ve sahip kararları K-8r, K-9…K-12 işlendi;
> kapanış yerleri Bölüm 19.1'de (`F-n@277fd253` — POLICY-REVIEW-START.md'ye göre `@` öneki önceki **bulgu dosyasının**
> özetidir; r2'de yanlışlıkla pack blob'u `219f30c9` yazılmıştı, r3'te düzeltildi). r1'deki "denetim aktörü Service"
> kararı geri alındı (K-8r): aktör isteği yapan kullanıcıdır.
>
> **Kimlik (sahip kararı Beste 2026-10-30).** `parent = Blueprint MOD-0040 (Canonical ID & Correlation Standard)`,
> **repodaki MOD-0040 / MOD-0040-FU01 takma adları değil.** `execution/registries/module-id-registry.md:136` satırındaki
> MOD-0040 eski bir takma addır (Tenant Organization Foundation → MOD-0288) ve `:137` satırındaki MOD-0040-FU01 de eski
> takma addır (→ MOD-0288-FU01); `:354` notu bu ayrımı yazar, `:353` notu ise eski anlamı hâlâ "reserved" diye anar
> (A-12). Bu paket kayıt dosyasına **yazmaz**; önerilen satır Bölüm 0'dadır, satırı CT yazar.
>
> **Kaynak.** RCS-001 (`.antigravity/rules/record-coding-standard.md`, tamamı okundu), sahip kararları (Bölüm 0.1),
> Treasury v0 çağrı sözleşmesi (KARAR D59/1, Y-8 — `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610`,
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:80-85`). Bütün satır
> numaraları `origin/main @ 6d49f5823` üzerinde numaralı okumayla ölçüldü (2026-10-30); çıkarım olan yerler "çıkarım"
> ya da "CT önerisi" diye işaretlidir.

## 0. Kimlik ve ön kontrol (DCP-002)

| Adım | Sonuç (2026-10-30, `origin/main @ 6d49f5823`) |
|---|---|
| Blueprint araması | `Blueprint_Data` satırı **MOD-0040**: Module Name `Canonical ID & Correlation Standard`; Domain `1) Platform & Shared Services`; Suite `Integration & Interoperability`; Capability Group `Interface Governance`; Wave `W-2`; SoR `Canonical ID definitions, correlation rules (standard)`; Placement `Control-Plane Standard`; SoR Notes: "Historical project alias: MOD-0040 was once used for Tenant Organization Foundation. That legacy meaning is retired; canonical MOD-0040 remains Canonical ID & Correlation Standard." (Blueprint 8.8, sha256 `edbcb725…5939`) |
| Ön kontrol | `python3 -P -E -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK  MOD-0040-FU02: proven against Blueprint/registry.` · **çıkış 0** |
| Keşif | `… --discover "record code"` → `KEŞİF RAPORU v2`; ÖZET: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 3 · BİLGİ 0; **çıkış 3** ("inceleme gerekli"); rapor-sha256 `21e60a99332f19ae59b1bcb00b355b15f036302809ff4b3bb38dcf8cef617c49`; 447 ref incelendi |
| Keşif bulguları (3, görev) | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q234 (MOD-0183 form alanı), Q406 (kanıtsız paket uyarısı), Q503 (MOD-0186 DL-1) — üçü de "record" / "code" kelimelerinden eşleşti; kayıt kodu servisiyle ilgisi yok (karar: **mükerrer değil**, gerekçe: konu başka modül) |
| Blueprint ek araması | `Blueprint_Data` içinde `number range / numbering / document sequence / code generat / record code / business reference / identifier` deseni: yalnız MOD-0290 (`identifier` kelimesi, ürün kimliği ailesi — RCS-001 §4 KALSIN). Numaralandırma için ayrı Blueprint modülü yok |
| FU02 repoda geçiyor mu | `git grep -n "MOD-0040-FU02" origin/main` → **0 satır**; `git log --all --oneline -S "MOD-0040-FU02"` → **0 commit** (r1 öncesi). Araç farklı adla yeniden kullanımı yakalamaz; FU02 başka hiçbir dalda kullanılmamış |
| Ebeveyn kararı | Yeni modül değil; Blueprint MOD-0040'ın (Canonical ID definitions SoR) FU'su. Sahip kararı (Beste 2026-10-30) |

**Önerilen kayıt satırı (CT yazar; bu paket kayıt dosyasına dokunmaz):**

`| MOD-0040-FU02 | Record Code Service | record-code-service | Follow-up | planned / reserved |  | MOD-0040 | platform-shared-services | Child of **Blueprint** MOD-0040 (Canonical ID & Correlation Standard, W-2) — NOT the repo alias row MOD-0040 (Tenant Organization Foundation → MOD-0288) nor MOD-0040-FU01 (→ MOD-0288-FU01). RCS-001 shared record-code service (BL-565). Pack: MOD-0040-FU02-record-code-service.md (draft). Owner decision Beste 2026-10-30. |`

> ⚠ **Kayıt çözümleme riski (ölçüldü):** kayıt dosyasında `MOD-0040` satırı `Deprecated Alias → MOD-0288`'dir ve `:353`
> notu "`MOD-0040` is reserved for Tenant Organization Foundation…" der. Ebeveyni kayıttan çözen bir araç ya da okuyucu
> FU02'yi yanlışlıkla MOD-0288'e bağlayabilir. CT satırı yazarken ayrım notunu (`:354`) satıra da yazar ve `:353`'ü
> düzeltir (A-12).

## 0.1 Sahip kararları (bağlayıcı)

| # | Karar (Beste 2026-10-30) | Pakette nerede |
|---|---|---|
| K-1 | **Yapı:** Diten.Platform içinde **ayrı** servis/özellik (`Features/RecordCodes`). Doküman kimlik üreticisi (`DocumentIdentifierAllocationService`) **genişletilmez**; doküman kodları KALSIN. Yalnız deseni örnek alınır | Bölüm 2, 5, 6, 19 |
| K-2 | **v1 kapsamı** (hedef: servis v1 **2026-11-06** main'de; kesin sözleşme **2026-11-03**): ayırma (allocate) iç ucu + atomik sayaç (`findOneAndUpdate` + `$inc`, RCS-001 §5.8) + önek kapsamları + RCS-001 §3'teki bütün **OTOMATİK** türlerin önek kaydı + şirket kısa kodu okuması + idempotency + denetim + kiracı izolasyonu. **v1 DIŞI** (sonraki dilimler, kod izni yok): mevcut modüllerin (Organizasyon Birimi, Pozisyon, Görev Türü, PPM, Abonelik Planı) bağlanması; İKİSİ DE türlerinde öneri + gerekçeli değişiklik + AUD-001 kaydı; Tüzel Kişilik kısa kodu önerisi; ekranlar. KALSIN türler ve sınıflandırılmamış mevcut üreticiler (Inventory, e-İmza, Procurement, SupplyChain, DMS) v1'de **değişmez** | Bölüm 2, 23 |
| K-3 | **Uç** (v0 Treasury'ye verildi; gövde ve hata kodlarında değişiklik **yalnız ekleme**): `POST /api/internal/record-codes/allocate`, servisten servise; kiracı gövdeden **değil**, güvenilir bağlamdan. Gövde `{ "recordType", "prefixScope", "legalEntityId", "idempotencyKey" }`. 200 `{ code, sequence }`; aynı anahtar + aynı gövde → aynı kod; farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. Hatalar: 422 `RECORD_TYPE_UNKNOWN`; 422 `RECORD_CODE_SCOPE_INVALID`; 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; 409 `GROUP_PREFIX_MISSING`; başka kiracının şirketi 404; erişilemez 503 (fail-closed). Yanıt zarfı için bkz. A-17 (F-5) | Bölüm 3, 8, 12, 13, sözleşme taslağı |
| K-4 | **prefixScope**: `LegalEntity` (varsayılan; `legalEntityId` zorunlu; önek şirket kısa kodu) · `Group` (`legalEntityId` boş; önek kiracının grup öneki, RCS-001 §5.3) · `Platform` (`legalEntityId` boş; öneksiz). Tür başına izinli kapsamı **servis** belirler: BNK yalnız Group; BAC LegalEntity (şirket hesabı) + Group (tedarikçi hesabı); PAY, APD, ARI, CPS, CTP, AGR yalnız LegalEntity; PLN Platform. Sayaç (kiracı, önek, tür) başına (§5.1) | Bölüm 4.4, 8.4, 22 |
| K-5 | **Şirket kısa kodu (KARAR D91):** MOD-0220 `LegalEntity.Code` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11`), `^[A-Z]{2,5}$`; MDM lookup-validation üzerinden (kiracı + aktif). Geçersiz → 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; MDM erişilemez → 503. Önek koda bir kez yazılır (§5.2). **Platform'da ayrı kısa kod tablosu YOK** | Bölüm 8.3 |
| K-6 | **Şirket öneki biçimi:** RCS-001 §1 bağlayıcı. K1 (`gmg-10`) "tek grup öneki" / "öneksiz"i değerlendiriyor; **2026-11-02**'ye kadar karar gelmezse sözleşme mevcut kurala göre dondurulur | Bölüm 24 (A-1) |
| K-7 | **MOD-0217 öneki `CTR` → `AGR`.** Procurement MOD-0144 bugün `CTR-` + 8 hex üretiyor (`services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Contract/Handlers/CommandHandlers/CreateContractHandler.cs:130-131`); aynı önek iki türde olamaz (§5.5). RCS-001 §3 satır değişikliği ayrı adımdır (`.antigravity`); bu paket yalnız `AGR`'yi kullanır. MOD-0144 / MOD-0217 kapsam çakışması açık soru | Bölüm 7, 22, 24 |
| ~~K-8~~ | ~~r1: aktör `Service`~~ — **K-8r ile değişti** | — |
| **K-8r** | **Aktör:** denetim kaydında aktör = isteği yapan **kullanıcı** (`TenantUser`, iletilen kullanıcı token'ının `sub`'ı) — `Service` değil. `CanonicalTransactionalAuditOutboxWriter` bunu bugün principal'dan zaten çıkarıyor (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs:156-185`). İç uç kimliği için mevcut Platform deseni ölçülür ve ona uyulur; yeni gizli bilgi/yapılandırma pakette yazılır, **değer uydurulmaz** | Bölüm 8.2, 14, 21 |
| **K-9** | **Kaynak servis:** denetim kaydının `SourceService` alanı = çağıran servisin adı (ör. `Diten.FinanceTreasuryService`). Yazıcıdaki sabit `SourceService = "Diten.Platform"` (`CanonicalTransactionalAuditOutboxWriter.cs:39`, kullanım `:138`) **eklemeli** genişletilir: `TransactionOwnedAuditIntent`'e isteğe bağlı kaynak servis alanı; boşsa bugünkü gibi `"Diten.Platform"` (diğer bütün kullanıcılar değişmez). **Güvenlik:** değer istek gövdesinden/başlığından **asla** alınmaz; yalnız doğrulanmış tüketici kimlik bilgisinden (kimlik bilgisi → servis adı eşlemesi, kapalı liste) gelir | Bölüm 8.2, 8.11, 21; AC-28 |
| **K-10** | **Ret kaydı:** reddedilen istekler denetime `Outcome = Denied` (yetki / kapsam / kimlik) ya da uygun olduğunda `Failed` ile yazılır. Ret iş işlemi olmadığından kayıt **ayrı kısa bir işlemle** (ayrı oturum) yazılır; yazıcıya **yalnız ret için** eklemeli bir yol. Ret hiçbir zaman kod üretmez. Hangi retlerin yazıldığı tabloyla belirlenir (Bölüm 21.2) | Bölüm 8.11, 21; AC-19, AC-29 |
| **K-11** | **Platform kapsamı** (öneksiz kod, `prefixScope = Platform`): sözleşmede kalır, **DL-1'de uygulanmaz** — denetim boş `TenantId`'yi reddeder (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditOutboxWriteRequest.cs:28-30`) ve bugün tüketici yok. DL-1'de `prefixScope = Platform` → 422 `RECORD_CODE_SCOPE_INVALID` **(tür izni adımı geçtiyse;** değerlendirme sırasında tür izni kapsamdan önce geldiği için `PLN` + Platform 403 `RECORD_CODE_FORBIDDEN` döner — r3, inceleme 02 F-9), sözleşmede "DL-4'te açılır" notuyla. Uygulama DL-4'te, PLN bağlanmasıyla | Bölüm 8.5, 8.7; AC-8, AC-25 (DL-4) |
| **K-12** | **Önceki öneriler kabul:** (a) `AGR` kalır — doküman tür kodundaki `AGR` ile biçimler farklı, çakışma yok (Bölüm 22); (b) tür kaydı **kodda** (`RecordCodeTypeCatalog`); (c) grup öneki **kiracı ayarı**, bugün `GMG`, belge `OrgPrefix` ile aynı değer — r3'te (inceleme 02 F-7) değerin **tek kaynağı belge `OrgPrefix`'idir**; kiracı ayarı onun kilitlenen bir kopyasıdır, ayrı düzenlenebilir değer yoktur (Bölüm 4.3 / 8.12); (d) `PLN` katalogda, bağlanması DL-4; (e) kimlik deseni **P2** = kimlik bilgisi + iletilen kullanıcı token'ı, F-4 azaltıcılarıyla (Bölüm 8.2) | Bölüm 4, 8, 22 |
| A-3 sahibi | `.antigravity` kural dosyaları **Ali**'nin sorumluluğunda (Beste notu): RCS-001 §3 `AGR` satırı ve izinli kapsam sütunu için sahip "Ali (`.antigravity`) + Treasury PR'ı" | Bölüm 24 (A-3) |
| RCS | 6 hane alt sınır, `999999`'dan sonra 7 hane (§5.7); boşluk kabul, numara geri verilmez, arşivli kod tekrar kullanılmaz (§5.8); yalnız ASCII `A–Z`, `0–9`, `-` (§5.10); içe aktarmada sayaç en büyük numaranın üstünden (§5.11) — **v1'de içe aktarma ucu yok**, kural DL-6 | Bölüm 8, 16, 23 |

## 1. Module Summary

**Amaç.** RCS-001 §5.12'nin "tek ortak kod üretme servisi": kullanıcıya görünen iş kodunu (`{ŞİRKET}-{TÜR}-{SAYAÇ}`,
ör. `MYG-PAY-000042`) tek yerden, atomik, kiracı izole, idempotent ve denetimli olarak verir. Bugün ortak üretici yok;
dört ayrı sayaç var (CRM hesap, CRM kampanya, MDM ürün, Doküman — RCS-001 §5.12) ve Treasury (6 paket) ile WMS
(MOD-0178) beşinci/altıncı bir kopya yazmamak için bu servisi bekliyor (D59/1; MOD-0178 Q-07 B, FU-0178-RCS-01).

**Kullanıcı.** Servisler, bir kullanıcı adına: v1'de Diten.FinanceTreasuryService (PAY, APD, ARI, CPS, CTP) ve
Diten.MdmService (BNK, BAC, AGR). Ayırma her zaman bir kullanıcının başlattığı Create'in içinde olur; çağıran servis
kullanıcının token'ını iletir; denetimde aktör o kullanıcıdır, kaynak servis çağıran servistir (K-8r, K-9).
Diten.InventoryService (WMS) DL-8 ile bağlanır.

**v1 kapasite özeti (DL-1).**

1. `POST /api/internal/record-codes/allocate` — tek iç uç, Gateway'e açılmaz.
2. Atomik sayaç `(kiracı, önek, tür)` başına; hiç sıfırlanmaz; 6 hane alt sınır, sonra 7+ hane.
3. İki etkin önek kapsamı: `LegalEntity` (MDM `Code`), `Group` (kiracının grup öneki ayarı). `Platform` sözleşmede var,
   DL-1'de tür izni geçtiyse 422, izinli çağıranı olmayan türde (`PLN`) 403 (K-11).
4. Tür önek kaydı: kod kataloğu (RCS-001 §3 OTOMATİK türler + `AGR` + `PLN`), tür başına izinli kapsam, izinli
   çağıran ve gerekli kullanıcı izni; §3'e testle sabitlenir.
5. İdempotency: `(kiracı, çağıran servis, idempotencyKey)` başına tek kod; gövde parmak iziyle.
6. Denetim: her yeni ayırma, ayırma defteriyle **aynı işlemde** merkezi denetim kutusuna yazılır (aktör kullanıcı, kaynak
   servis çağıran); kiracısı bilinen her ret ayrı kısa bir işlemle `Denied` / `Failed` yazılır.
7. Kiracı izolasyonu: kiracı doğrulanmış kullanıcı token'ının `tenant_id`'si; bütün okuma/yazmalar **tam eşitlik**
   `TenantId = token kiracısı`; askıdaki kiracıya kod verilmez.
8. Kimlik bilgisi sızmasına karşı: tüketici başına izinli tür kümesi, kullanıcıda modülün oluşturma izni, hız sınırı,
   sır döndürme / iptal.

## 2. Ownership and Boundaries

### 2.1 In scope (DL-1 = v1)

- Ayırma iç ucu, istek yürütücüsü (kimlik + kiracı + izin), komut, doğrulayıcı, işleyici.
- Üç kiracı kapsamlı koleksiyon (`TenantScopedEntity`): sayaç `record_code_counters`, ayırma defteri
  `record_code_allocations`, grup öneki ayarı `record_code_group_prefixes` (Bölüm 4).
- Tür önek kaydı (kod kataloğu `RecordCodeTypeCatalog`) ve onu RCS-001 §3'e sabitleyen test.
- MDM'den şirket kısa kodu okuyan yeni okuyucu (`MdmLegalEntityShortCodeReader`); mevcut
  `MdmLegalEntityReferenceValidator` **değişmez** (MOD-0288'in).
- Denetim yazıcısının **eklemeli** genişletilmesi (K-9 kaynak servis, K-10 ret yolu) — Platform domain'i içinde,
  XMC-001 gerekmez (Bölüm 5, 21).
- Grup öneki ayarının yazma yolu: Platform yöneticisi için UI'sız, gövdesiz bir **hizalama** ucu — kiracı ayarına etkin
  belge `OrgPrefix`'ini kopyalar; kullanılana kadar yeniden hizalanabilir, kullanıldıktan sonra kilitli (r3, F-7).
- Hız sınırı: yürütücü içinde, (kimlik bilgisi, kiracı) bölümlü; değeri A-16 (r3, F-5).
- Sözleşme taslağı `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`; dondurma ayrı CT adımı
  (2026-11-03).

### 2.2 Out of scope (v1 dışı — kod izni yok)

- **Platform kapsamı** (öneksiz kod, `PLN`) — DL-4 (K-11).
- **Mevcut modüllerin bağlanması** (ORG, POS → DL-2; TTY, PPM `PFL/PRG/PRJ/INI/INV`, PLN → DL-4). Bu modüller v1'de
  bugünkü elle kod davranışını korur.
- **İKİSİ DE akışı** (öneri + gerekçeli değişiklik + AUD-001 eski/yeni kod + gerekçe) → DL-3.
- **Tüzel Kişilik kısa kodu önerisi** ve RCS-001 §5.4 kullanım kilidi (MDM tarafı) → DL-5.
- **İçe aktarma / sayaç yüksek su işareti** (§5.11), WMS geçici sayacından devam → DL-6.
- **Ekranlar** → DL-10. v1'de hiçbir `.cshtml`, rota, menü yok.
- **Kodun kayda bağlanması / tüketilmesi** (MDM `CodeReservation`'ın "tüket" adımı, §5.8): v1 yalnız **ayırır**;
  kullanılmayan kod bir boşluktur (§5.8 kabul eder) — FU-RCS-04.
- **KALSIN türler** (CRM `ACC`/`CMP`, MDM ürün ailesi `GP/GS/LS/FG/IT`, doküman `UID` ve `GMG-QMS-…`): kod verilmez
  (422 `RECORD_TYPE_UNKNOWN`).
- **Sınıflandırılmamış mevcut üreticiler** (Inventory, e-İmza, Procurement `CTR-` + 8 hex, SupplyChain, DMS): değişmez (A-4).
- Arka plan işlerinden (kullanıcı token'ı olmayan) çağrı → DL-9.

### 2.3 Ownership decision

- Sahip: **Beste** (platform-shared-services, K2). Platform kodu bu domain'in
  (`execution/domains/platform-shared-services/domain-config.md` "Domain-Level Repo Scope": `services/Diten.Platform/**`).
- SoR: Blueprint MOD-0040 "Canonical ID definitions". Kayıt türünün kendisi (ödeme partisi, banka…) tüketicinin SoR'udur;
  bu servis kodu verir, kaydı tutmaz.
- Denetim yazıcısı (MOD-0021 altyapısı) Platform domain'indedir; değişiklik yalnız eklemelidir, sahibi Beste.
- MDM `LegalEntity.Code` MOD-0220'nindir; bu servis yalnız okur.
- Domain-config In-Scope listesinde MOD-0040-FU02 yok (ölçüldü); ekleme CT adımı (A-12).

## 3. Owned Objects

| Tür | Ad | Not |
|---|---|---|
| Entity | `RecordCodeCounter` | `TenantScopedEntity`; Bölüm 4.1 |
| Entity | `RecordCodeAllocation` | `TenantScopedEntity`; ekleme-yalnız defter; Bölüm 4.2 |
| Entity | `RecordCodeGroupPrefix` | `TenantScopedEntity`; kiracı başına tek satır; belge `OrgPrefix`'inin kilitlenen kopyası; Bölüm 4.3 |
| Kod kataloğu | `RecordCodeTypeCatalog` | Domain, değişmez; Bölüm 4.4 |
| Değer | `RecordCodeFormat` | biçimleme + doğrulama |
| Enum | `RecordCodePrefixScope` | `LegalEntity = 1`, `Group = 2`, `Platform = 3` (JSON'da ad) |
| Repository | `IRecordCodeCounterRepository` | `NextAsync(tenantId, prefix, recordType)` |
| Repository | `IRecordCodeAllocationRepository` | `InsertAsync(session, tenantId, …)`, `FindByIdempotencyKeyAsync(tenantId, caller, key)`, `FindByIdAsync(tenantId, allocationId)`, `FindLatestPrefixForLegalEntityAsync(tenantId, legalEntityId)`, `ExistsPrefixForOtherLegalEntityAsync(tenantId, prefix, legalEntityId)` — güncelleme / silme / değiştirme yok |
| Repository | `IRecordCodeGroupPrefixRepository` | `GetAsync(tenantId)`, `AlignIfUnlockedAsync(tenantId, orgPrefix, …)`, `LockAsync(session, tenantId, prefix)` — silme yok |
| Port (Application) | `ILegalEntityShortCodeReader` | sonuç: `Found(code, approvalStatus?)` · `NotFound` · `Unavailable(reason)` |
| Komut | `AllocateRecordCodeCommand` | yazma komutu; `IAuditableCommand` değil (denetim işleyicide, Bölüm 21) |
| Komut | `AlignRecordCodeGroupPrefixCommand` | gövdesiz; kiracı ayarını etkin belge `OrgPrefix`'ine hizalar; `IAuditableCommand` + `IAuditMetadataProvider` (yol a) |
| Sorgu | `GetRecordCodeGroupPrefixQuery` | yönetici ucu `GET` |
| DTO | `RecordCodeAllocationResult` (`Code`, `Sequence`) | `RecordCodesModels.cs` |
| API (iç) | `POST /api/internal/record-codes/allocate` | `InternalRecordCodesController`; Gateway'e açılmaz |
| API (yönetici) | `PUT` (gövdesiz hizalama) / `GET /api/admin/tenants/{tenantId:guid}/record-code-group-prefix` | `TenantRecordCodeGroupPrefixController`; mevcut `/api/admin/tenants/{everything}` rotası |
| Yapılandırma | `TrustedRecordCodeCredential` | tüketici başına kimlik bilgisi bağlaması; değer depoda yok |
| Hız sınırlayıcı | `RecordCodeAllocateRateLimiter` | yürütücü içi `PartitionedRateLimiter`, bölüm (tüketici servis, kiracı); ASP.NET `AddRateLimiter` / `OnRejected` kullanılmaz (r3, F-5); değer A-16 |
| Denetim yazıcısı (eklemeli) | `TransactionOwnedAuditIntent.SourceService` (isteğe bağlı), `TransactionOwnedAuditSourceServices` (izinli ad kümesi), `ITransactionalAuditRefusalWriter` (yeni arayüz, `CanonicalTransactionalAuditOutboxWriter` uygular) | K-9, K-10; r3 F-3, F-4 |
| Denetim çağrısı | `RecordCodeAuditIntent.EnqueueAsync` / `.EnqueueRefusalAsync` | yeni belirteç; CT defter kararı (A-10) |
| İzin | `platform.record-code-group-prefixes.read`, `platform.record-code-group-prefixes.update` | PKS-001 Tier 1; yalnız Platform aktörü |
| Frontend rota | — | yok (`shell: none`) |

## 4. Entity Fields

Ortak taban `BaseEntity` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:3-14`) ve
**`TenantScopedEntity`** (`EntityVariations.cs:8-11`, `required Guid TenantId`). r1'deki `HybridEntity` kaldırıldı
(inceleme 01 F-2): Platform kapsamı DL-1'de yok (K-11), kiracısız satır da yoktur; `HybridRepository`'nin "`TenantId` null
**veya** mevcut kiracı" filtresi (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs:96-109`)
bu özellikte kullanılmaz. DL-4 kiracısız satırın tabanını ayrıca kararlaştırır.

**Kiracı yüklemi kuralı (inceleme 01 F-2/F-3; r3 inceleme 02 F-1 ile düzeltildi).** Üç deponun **her** okuma ve yazma
yöntemi `Guid tenantId`'yi açık parametre olarak alır ve filtre **tam eşitliktir** `TenantId == tenantId`. Depo şu üç
durumda `InvalidOperationException` atar ve hiçbir şey okumaz/yazmaz: (a) `tenantId == Guid.Empty`; (b)
`ITenantContext.IsPlatformContext == true` (`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/ITenantContext.cs:10`,
`SetPlatformContext` ile kurulan bağlam); (c) `tenantId ≠ ITenantContext.TenantId`. **Platform sentinel kimliği
`00000000-0000-0000-0000-000000000001` reddedilmez:** bu kimlik aynı zamanda varsayılan / tohum iş kiracısıdır
(`services/Diten.Platform/src/Diten.Platform.Domain/Entities/SystemTenantRules.cs:12`, `…/Persistence/Configurations/TenantSeed.cs:17`,
`PositionSeed.cs:20`; AuthService'te varsayılan ve geliştirme kiracısı — CT ölçümü, inceleme 02); o kiracının
`tenant_user` token'ıyla gelen istek her iş kiracısı gibi kod alır (AC-38). Platform yöneticisi token'ı zaten 403 alır
(`actor_type ≠ tenant_user`, Bölüm 8.2). Kiracı değeri işleyiciye yürütücüden (token) gelir; komut DTO'sunda kiracı
alanı yoktur.

**Silme yok:** soft delete dahil hiçbir silme yolu yoktur — kod asla yeniden kullanılmaz (§5.8); `IsDeleted` daima
`false`, `DeletedAt` uygulanmaz (gerekçe: silinebilir kayıt "arşivli kodu tekrar kullanma" kuralını zayıflatır).

### 4.1 RecordCodeCounter — `record_code_counters`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | evet | token kiracısı; `Guid.Empty` yasak |
| `Prefix` | string | evet | LegalEntity: MDM kısa kodu (`^[A-Z]{2,5}$`) · Group: grup öneki |
| `RecordType` | string | evet | `^[A-Z]{3}$`, katalogda |
| `LastNumber` | long | evet | son verilen numara; `$inc` 1; asla azalmaz, sıfırlanmaz |

Index: `ux_record_code_counters_tenant_prefix_type` — `(TenantId, Prefix, RecordType)` unique. Anahtarda `LegalEntityId`
yok (§5.1). MOD-0178 §22'nin "önek devri" notundaki durum (serbest kalan kısa kodun başka şirkete geçip aynı sayaçtan
devam etmesi) bu serviste r3'ten itibaren 409 ile durdurulur (Bölüm 8.13).

### 4.2 RecordCodeAllocation — `record_code_allocations` (ekleme-yalnız defter)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | evet | **ayırma kimliği (`allocationId`)**: her yeni ayırma denemesinde yeni Guid; denetim kutusu anahtarı bundan türer (Bölüm 8.6) |
| `TenantId` | Guid | evet | token kiracısı |
| `CallerService` | string | evet | kimlik bilgisinin kapalı listedeki servis adı (Bölüm 8.2, ör. `Diten.FinanceTreasuryService`) |
| `IdempotencyKey` | string | evet | Bölüm 12 (istemcinin anahtarı; yalnız bu defterde, kiracı + çağıran kapsamında tekil) |
| `RequestFingerprint` | string | evet | SHA-256 hex: `recordType` `\n` `prefixScope` `\n` `legalEntityId` (`D` biçimi ya da boş) |
| `RecordType`, `PrefixScope`, `LegalEntityId?`, `Prefix`, `Sequence` (long), `Code` | — | evet | `Prefix` koda bir kez yazılır (§5.2) |
| `OnBehalfOfUserId` | Guid | evet | token `sub` |
| `CorrelationId` | string | evet | isteğin korelasyonu |
| `AllocatedAtUtc` | DateTimeOffset | evet | UTC |

Index'ler: `ux_record_code_allocations_tenant_caller_key` `(TenantId, CallerService, IdempotencyKey)` unique;
`ux_record_code_allocations_tenant_code` `(TenantId, Code)` unique; `ix_record_code_allocations_tenant_type_prefix_seq`
`(TenantId, RecordType, Prefix, Sequence desc)` (DL-6 için); **r3 (inceleme 02 F-11):**
`ix_record_code_allocations_tenant_le_time` `(TenantId, LegalEntityId, AllocatedAtUtc desc)` ve
`ix_record_code_allocations_tenant_prefix_le` `(TenantId, Prefix, LegalEntityId)` — kısa kod kullanım kilidinin ara dönem
denetimi için (Bölüm 8.13).

### 4.3 RecordCodeGroupPrefix — `record_code_group_prefixes` (kiracı ayarı)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | evet | yönetici ucunun rota kiracısı (var olan, silinmemiş kiracı; yoksa 404); `Guid.Empty` ve platform bağlamı yasak (Bölüm 4) |
| `GroupPrefix` | string | evet | **etkin belge `OrgPrefix`'inin kopyası** — elle girilmez; `^[A-Z]{2,5}$`, ASCII, normalleştirme yok |
| `AlignedAtUtc` / `AlignedBy` | — | evet | son hizalamanın zamanı / Platform aktörü |
| `LockedAtUtc` | DateTimeOffset? | hayır | ilk `Group` ayırması **aynı işlemde** yazar; doluysa değer kilitlidir |

Index: `ux_record_code_group_prefixes_tenant` `(TenantId)` unique.

**Tek kaynak (r3, inceleme 02 F-7).** Grup önekinin değerinin tek kaynağı belge kodlarının `OrgPrefix`'idir
(`DocumentCodingOptions.OrgPrefix`, `…/DocumentManagementIdentifiersModels.cs:45`, bölüm `DocumentManagement:Coding`
`:43`). Ayrı düzenlenebilir bir değer **yoktur**: yönetici ucu gövde almaz, yalnız etkin `OrgPrefix`'i kiracı ayarına
kopyalar (hizalama). Kiracı ayarı iki şey için vardır: (1) Group kapsamının bu kiracıda açıkça devreye alındığını
kaydetmek (kayıt yoksa 409 `GROUP_PREFIX_MISSING` — servis `OrgPrefix`'ten kendiliğinden doldurmaz); (2) önek bir kez
kullanıldıktan sonra **kilitlemek** (RCS-001 §5.4 dili: "bir kez herhangi bir kaydın önekinde kullanıldıktan sonra
değiştirilemez").

- **Kilitsizken düzeltme = `OrgPrefix`'i değiştirmek:** operatör `DocumentManagement:Coding:OrgPrefix` yapılandırmasını
  değiştirir, sonra hizalama ucunu yeniden çağırır; kiracı ayarı yeni değere güncellenir (önceki/sonraki değer yol a ile
  denetlenir).
- **Kilitten sonra:** hizalama ucu, etkin `OrgPrefix` kilitli değerden farklıysa 409 `GROUP_PREFIX_LOCKED` döner ve
  değişmez; aynıysa 200 (değişiklik yok).
- **Kilitten sonra `OrgPrefix` değişirse:** her `Group` ayırması 409 `GROUP_PREFIX_MISMATCH` alır (kod verilmez).
  **Kurtarma yolu:** (a) önerilen ve tek yetkili yol — `OrgPrefix` yapılandırması kilitli değere geri alınır (belge
  kodları ile kayıt kodları aynı grup önekini taşımalıdır, RCS-001 §5.3); (b) grup önekinin bilinçli olarak değiştirilmesi
  (yeni önekle yeni sayaç) bu paketin kapsamında değildir — sahip kararı ve ayrı iş (FU-RCS-16). Bu durum Platform
  günlüğüne **hata** düzeyinde yazılır.
- Silme yok. Kodda varsayılan yok (`OrgPrefix`'in kod varsayılanı `"GMG"`, belge kodlarının bugün kullandığı etkin değer
  olarak okunur; servis bunu kendiliğinden kiracıya yazmaz — yalnız Platform yöneticisinin hizalamasıyla).

### 4.4 Tür önek kaydı — `RecordCodeTypeCatalog` (kodda, K-12 b)

Gerekçe: §3 tablosu `.antigravity` altında onaylı bir kuraldır; kataloğu §3'e sabitleyen test kural ile çalışma zamanı
arasındaki her sapmayı kırmızı yapar.

| TÜR | Kayıt türü (RCS-001 §3) | Sınıf | Sahip modül | İzinli kapsam | İzinli çağıran (v1) | Kullanıcıda gerekli izin (F-4) |
|---|---|---|---|---|---|---|
| `ORG` | Organizasyon Birimi | OTOMATİK | MOD-0288 | LegalEntity (A-8) | yok (DL-2) | DL-2'de |
| `POS` | Pozisyon (kadro) | OTOMATİK | MOD-0288 | LegalEntity (A-8) | yok (DL-2) | DL-2'de |
| `PAY` | Ödeme Partisi | OTOMATİK | MOD-0126 | LegalEntity | FinanceTreasury | `finance-treasury.payment-batches.create` |
| `APD` | AP Belgesi | OTOMATİK | MOD-0119 | LegalEntity | FinanceTreasury | `finance-treasury.ap-invoices.create` |
| `ARI` | AR Alacak Faturası | OTOMATİK | MOD-0120 | LegalEntity | FinanceTreasury | `finance-treasury.receivable-invoices.create` |
| `CPS` | Nakit Pozisyonu Anlık Görüntüsü | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | `finance-treasury.cash-positions.create` |
| `CTP` | Nakit Eşik Politikası | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | `finance-treasury.cash-position-threshold-policies.create` |
| `BNK` | Banka | OTOMATİK | MOD-0289 | **yalnız Group** | Mdm | `mdm.banks.create` |
| `BAC` | Banka Hesabı | OTOMATİK | MOD-0289 | LegalEntity (şirket hesabı) **ve** Group (tedarikçi hesabı) | Mdm | `mdm.bank-accounts.create` |
| `AGR` | Sözleşme | OTOMATİK | MOD-0217 | LegalEntity (yöneten şirket) | Mdm | `mdm.contracts.create` |
| `PLN` | Abonelik Planı | İKİSİ DE (§5.3) | Platform | Platform (DL-4) | yok (DL-4) | DL-4'te |

İzin adları MOD-0018-FU27 taslağındaki anahtarlardır (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`,
ölçüldü); FU27 onayıyla kesinleşir, değişirse bu tablo aynı gün güncellenir (A-11). `APD` alacak dekontu da
`ap-invoices.create` ile açılır (çıkarım; Treasury teyidi A-11).

Katalogda olmayan her değer — KALSIN (`ACC`, `CMP`, `GP`, `GS`, `LS`, `FG`, `IT`, `UID`), İKİSİ DE (`TTY`, `PFL`,
`PRG`, `PRJ`, `INI`, `INV`), eski `CTR`, WMS'in henüz §3'te olmayan `PUT` / `PCK` / `OUT` / `PKG`'si — 422
`RECORD_TYPE_UNKNOWN`. `ORG`, `POS`, `PLN` katalogdadır ama izinli çağıranı yoktur: HTTP'den 403 `RECORD_CODE_FORBIDDEN`.

## 5. Repo Scope

**DL-1 (v1) — tam dosya yolları.** N = yeni, M = yalnız belirtilen eklemeli değişiklik. Kompozisyon dosyalarının
(`Program.cs`, `DependencyInjection.cs`) SHA-256'sı sabitlenmez (module-pack-standard §13).

API (`services/Diten.Platform/src/Diten.Platform.API/`):

- N `Controllers/Internal/InternalRecordCodesController.cs`
- N `Controllers/Admin/TenantRecordCodeGroupPrefixController.cs`
- N `Security/ITrustedRecordCodeRequestExecutor.cs`
- N `Security/TrustedRecordCodeRequestExecutor.cs`
- N `Security/ITrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/TrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/ITrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeAllocateRequestParser.cs`
- N `Security/RecordCodeAllocateRateLimiter.cs` — yürütücü içi `System.Threading.RateLimiting.PartitionedRateLimiter`
  (singleton), bölüm anahtarı (tüketici servis adı, token kiracısı); **`PeopleSearchRateLimit.cs` ve ASP.NET
  `AddRateLimiter` / `EnableRateLimiting` kullanılmaz** — oradaki `OnRejected` tek kancadır ve politika dışı reddi
  `RATE_LIMITED` diye yazar (`…/Security/PeopleSearchRateLimit.cs:23`, `:38-46`); bu özellik kendi 429
  `RECORD_CODE_RATE_LIMITED` cevabını yürütücünün ret yolundan yazar (r3, inceleme 02 F-5)
- N `Configuration/TrustedRecordCodeCredentialOptions.cs`
- N `Models/RecordCodes/AllocateRecordCodeTransportRequest.cs`
- M `Program.cs` — yalnız DI kaydı ve seçenek bağlama satırları

Application (`services/Diten.Platform/src/Diten.Platform.Application/`):

- N `Features/RecordCodes/Commands/AllocateRecordCodeCommand.cs`
- N `Features/RecordCodes/Commands/AlignRecordCodeGroupPrefixCommand.cs`
- N `Features/RecordCodes/Queries/GetRecordCodeGroupPrefixQuery.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/AlignRecordCodeGroupPrefixHandler.cs`
- N `Features/RecordCodes/Handlers/QueryHandlers/GetRecordCodeGroupPrefixHandler.cs`
- N `Features/RecordCodes/Validators/AllocateRecordCodeValidator.cs`
- N `Features/RecordCodes/Validators/AlignRecordCodeGroupPrefixValidator.cs`
- N `Features/RecordCodes/RecordCodesModels.cs`
- N `Features/RecordCodes/RecordCodeReasonCodes.cs`
- N `Features/RecordCodes/RecordCodeAuditIntent.cs`
- N `Contracts/RecordCodes/ILegalEntityShortCodeReader.cs`
- M `DependencyInjection.cs` — yalnız kayıt satırları; **ret yazıcısı burada ayrı** `services.AddScoped<ITransactionalAuditRefusalWriter, CanonicalTransactionalAuditOutboxWriter>();`
  satırıyla, mevcut `ITransactionalAuditOutboxWriter` kaydının (`:329-330`) yanında kaydedilir (r3, inceleme 02 F-3).
  `DependencyInjectionSmokeTests.cs:281-282` (`ITransactionalAuditOutboxWriter` için tek kayıt, uygulama
  `CanonicalTransactionalAuditOutboxWriter`) değişmeden yeşil kalır; Infrastructure DI'da yazıcı kaydı **yoktur**.

**Denetim yazıcısı — yalnız eklemeli; varsayılanlar aynı; mevcut bütün denetim testleri değişmeden yeşil (K-9, K-10):**

- M `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditIntent.cs` — yalnız
  isteğe bağlı `public string? SourceService { get; init; }` ve sınıf yorumunun (`:6-9`) güncellenmesi: "kaynak servis
  kapıda kurulur; intent yalnız `TransactionOwnedAuditSourceServices` kümesindeki bir adı önerebilir, boşsa
  `Diten.Platform`" (r3, inceleme 02 F-4). Dosya `Contracts/Audit` altındadır — **Contracts/Audit'e dokunulur**.
- N `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/ITransactionalAuditRefusalWriter.cs` —
  `Task<bool> TryEnqueueRefusalAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request,
  AuditOutcome outcome, CancellationToken ct)`; `outcome ∈ {Denied, Failed}`, `Succeeded` → `TransactionOwnedAuditIntentInvalidException`.
- N `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TransactionOwnedAuditSourceServices.cs` —
  **kapalı izinli kaynak servis adı kümesi** (r3, F-4): `Diten.Platform`, `Diten.FinanceTreasuryService`,
  `Diten.MdmService` (bugün `Diten.Platform` + bu servisin tüketici kimlik bilgisi eşlemesindeki adlar; DL-8'de
  `Diten.InventoryService` eklenir). Küme yalnız bu pakete bağlı revizyonla büyür.
- M `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs` —
  (a) `:138`: `SourceService: string.IsNullOrWhiteSpace(intent.SourceService) ? SourceService : intent.SourceService`;
  `:39` sabiti aynı; doluysa değer `TransactionOwnedAuditSourceServices` kümesinde **birebir** (sıralı karşılaştırma,
  kırpma yok) olmalıdır, değilse `TransactionOwnedAuditIntentInvalidException` (hiçbir şey yazılmaz); (b) sınıf
  `ITransactionalAuditRefusalWriter`'ı da uygular: aynı doğrulamalar (`:81-92`), aynı aktör çözümü (`:156-185`), aynı kaynak
  servis kuralı, aynı yük kurucu (`:117-140`), tek fark `Outcome` = verilen ret sonucu (bugün `:131` sabit `Succeeded`).
  Mevcut `TryEnqueueAsync` imzası ve davranışı değişmez.
- `ITransactionalAuditOutboxWriter.cs`, `ITransactionalAuditOutboxStore.cs`, `AuditOutboxWriteRequest.cs` **değişmez**.
- `TransactionOwnedAuditPayloadExemptions.cs` listesine **ekleme yapılmaz** (liste yalnız küçülür, 2026-11-13'te biter;
  `:9-13`); elle yük yolu kullanılmaz — `RecordCodeAuditIntent` her zaman `Intent` taşır.

Domain (`services/Diten.Platform/src/Diten.Platform.Domain/`):

- N `Entities/RecordCodes/RecordCodeCounter.cs`
- N `Entities/RecordCodes/RecordCodeAllocation.cs`
- N `Entities/RecordCodes/RecordCodeGroupPrefix.cs`
- N `Enums/RecordCodes/RecordCodePrefixScope.cs`
- N `Features/RecordCodes/RecordCodeTypeCatalog.cs`
- N `Features/RecordCodes/RecordCodeFormat.cs`
- N `Repositories/IRecordCodeRepositories.cs`

Infrastructure (`services/Diten.Platform/src/Diten.Platform.Infrastructure/`):

- N `Persistence/Repositories/RecordCodeRepositories.cs`
- N `Persistence/Schema/PlatformSchemaManifest.RecordCodes.cs`
- M `Persistence/Schema/PlatformSchemaManifest.cs` — yalnız `.Concat(RecordCodesCollections)`
- M `Persistence/Schema/SchemaProfile.cs` — yalnız `RecordCodes = 14`
- M `Persistence/Schema/PlatformCollections.cs` — yalnız üç koleksiyon adı sabiti
- N `Services/Mdm/MdmLegalEntityShortCodeReader.cs`
- M `DependencyInjection.cs` — yalnız depo ve MDM `HttpClient` kayıt satırları (denetim yazıcısı kaydı **yok**)

Yapılandırma: M `services/Diten.Platform/src/Diten.Platform.API/appsettings.json` — yalnız `TrustedRecordCodeCredential`
bölümünün **boş** iskeleti ve hız sınırı değeri (A-16 kapanınca); `appsettings.Development.json`'a gizli değer yazılmaz.
`DocumentManagement:Coding` bölümüne dokunulmaz.

Testler (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/`):

- N `RecordCodes/RecordCodeFormatTests.cs`
- N `RecordCodes/RecordCodeTypeCatalogTests.cs`
- N `RecordCodes/AllocateRecordCodeHandlerTests.cs`
- N `RecordCodes/RecordCodeAllocationMongoTests.cs`
- N `RecordCodes/RecordCodeTenantIsolationMongoTests.cs`
- N `RecordCodes/RecordCodeAuditDeliveryMongoTests.cs`
- N `RecordCodes/RecordCodeActorProbeHttpTests.cs` — DL-1'in **ilk adımı** (AC-41)
- N `RecordCodes/InternalRecordCodesHttpTests.cs`
- N `RecordCodes/TrustedRecordCodeRequestExecutorRefusalTests.cs`
- N `RecordCodes/RecordCodeAllocateRateLimiterTests.cs`
- N `RecordCodes/MdmLegalEntityShortCodeReaderTests.cs`
- N `RecordCodes/RecordCodeGatewayExposureTests.cs`
- N `RecordCodes/TenantRecordCodeGroupPrefixHttpTests.cs`
- N `Audit/TransactionOwnedAuditSourceServiceTests.cs`
- N `Audit/TransactionalAuditRefusalWriterTests.cs`
- M `Authorization/LoginOnlyEndpointGuardTests.cs` — yalnız `AnonymousInternalControllers`'a `InternalRecordCodesController`
  (gerekçe yorumuyla; `:77-128` deseni)
- M `Schema/PlatformSchemaManifestTests.cs` — yalnız yeni profilin beklenen sayımı (gerekirse)
- Mevcut denetim testleri ve `DependencyInjectionSmokeTests.cs` **değiştirilmez**; `"Diten.Platform"` varsayılanını
  `TransactionOwnedAuditDoorTests.cs:55` ve `TransactionOwnedAuditDeliveryMongoTests.cs:73` kanıtlar.

**DL-1'in dışında, CT / sahip adımı (geliştirici yazmaz):** `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler`
(`platform-islem-ici` satırının belirtecine `RecordCodeAuditIntent.EnqueueAsync/RecordCodeAuditIntent.EnqueueRefusalAsync`)
+ `tests/architecture/audit-ledger/CT-DECISIONS.md` + `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`
sabitleri (A-10); `.antigravity/rules/record-coding-standard.md` §3 (A-3, Ali + Treasury PR'ı);
`execution/registries/module-id-registry.md` ve domain-config (A-12); sözleşmenin dondurulması (2026-11-03).

## 6. Protected Paths

- `.antigravity/**` — RCS-001 §3 değişikliği Ali'nin (A-3).
- Doküman kimlik üreticisi: `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/**`,
  `…/Infrastructure/Persistence/Repositories/DocumentManagementIdentifierRepositories.cs`,
  `…/Domain/Entities/DocumentManagement/DocumentIdentifierSequenceCounter.cs` — genişletilmez (K-1). `DocumentCodingOptions`
  **okunur**, değiştirilmez (Bölüm 8.12).
- `…/Infrastructure/Services/Mdm/MdmLegalEntityReferenceValidator.cs` — değişmez.
- `…/API/Security/TrustedLegalEntityScope*.cs`, `TrustedServiceTokenValidationExtensions.cs`, `TrustedSourceAuditIntent*.cs` — emsal, değişmez.
- Denetim altyapısında Bölüm 5'te adı geçen eklemeler **dışındaki her şey**: özellikle `Contracts/Audit/AuditActorTypeResolver.cs`,
  `Contracts/Audit/TransactionOwnedAuditCommands.cs`, `Contracts/Audit/ITransactionalAuditOutboxWriter.cs`,
  `Contracts/Audit/ITransactionalAuditOutboxStore.cs`, `Contracts/Audit/AuditOutboxWriteRequest.cs`,
  `Features/Audit/TransactionOwnedAuditPayloadExemptions.cs` (ekleme yasak), `Domain/Enums/AuditEnums.cs` (yeni kategori yok).
- `services/Diten.MdmService/**`, `services/Diten.AuthService/**`, `services/Diten.ProcurementService/**`,
  `services/Diten.InventoryService/**`, `services/Diten.Building.Blocks/**` ve diğer domain servisleri.
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent'ın; bu paket rota eklemez.
- `frontend/Diten.Web/**` — v1'de ekran yok.
- `execution/registries/**`, `tests/architecture/audit-ledger/**`, `docs/roadmap/backlog/**` — CT'nin.

## 7. Dependencies

| Bağımlılık | Bugünkü durum (ölçüldü) | DL-1 için |
|---|---|---|
| MDM lookup-validation `GET /api/legal-entities/{id}/lookup-validation` (izin `mdm.legal-entities.read`) | var — `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-54`; yok / silinmiş / başka kiracı / aktif değil → 404; yanıt `LegalEntityLookupDto` 1.0.0 (`…/Features/LegalEntity/LegalEntityModels.cs:100-106`) — **`approvalStatus` yok** | gerekli; değişiklik istenmez |
| **MDM lookup-validation v1.1 (`approvalStatus`)** | taslak (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`); v1.1'de alan yoksa yanıt geçersizdir → 503 (`:69`) | **açık bağımlılık, sahibi MOD-0220 sahibi** (r3, inceleme 02 F-8): v1.1 yayınlanana kadar servis yalnız aktiflik denetler ve TC-3 zorunludur; v1.1 yayınlandığı sürümde bu servisin okuyucusu da "alan yok → 503" kuralına geçirilir (kod değişikliği, FU-RCS-15) |
| Çağıranın kullanıcı token'ında `mdm.legal-entities.read` | MDM, iletilen kullanıcı token'ını yetkilendirir (Platform emsali `MdmLegalEntityReferenceValidator.cs:142-152`); finans rolleri bağımlı izin olarak alır (MOD-0018-FU27 K-4 / D72) | LegalEntity kapsamı için gerekli; yoksa MDM 403 → 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| Kullanıcı token'ında `permission` iddiaları | FU21 bunları okuyor (`TrustedLegalEntityScopeJwtContext.cs:40`) | gerekli (Bölüm 4.4 son sütunu) |
| Treasury ve MDM izin anahtarları (Bölüm 4.4) | MOD-0018-FU27 taslağında | adlar FU27 onayıyla kesinleşir (A-11) |
| Platform Mongo işlemi (replica set) | var — `PlatformTransactionExecutor.cs`, `IPlatformTransactionSession` | gerekli |
| Denetim kutusu kapısı `CanonicalTransactionalAuditOutboxWriter` | var (`:37-186`); aktör principal'dan (`:156-185`); `SourceService` sabit (`:39`, `:138`); `Outcome` sabit `Succeeded` (`:131`); kayıt Application DI `:329-330` | **eklemeli genişletme** (Bölüm 5) |
| `audit_outbox` tekillik index'i | `ux_audit_outbox_idempotency_key` **küresel tekil** (`…/Persistence/Schema/PlatformSchemaManifest.AccessGovernance.cs:118-120`); çakışmada depo `false` döner (`…/Persistence/Repositories/AuditOutboxRepository.cs:159-161`) | anahtar istemci anahtarından türetilmez (Bölüm 8.6, r3 F-2) |
| Yeni belirteç kabulü (`platform-islem-ici` iz satırına `RecordCodeAuditIntent.*`) | defterde yok (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`) | **DL-1 main'e girmeden önce CT kararı** (A-10) |
| Kiracı durumu | `GetTenantStatusQuery` — yalnız `TenantStatus.Active` canlı (`…/Features/Tenants/Handlers/GetTenantStatusQueryHandler.cs:18-29`) | gerekli (askıdaki kiracı) |
| Hız sınırlayıcı | `System.Threading.RateLimiting` (çerçeve); `PeopleSearchRateLimit` emsali yalnız okunur, kullanılmaz | gerekli; değer A-16 |
| Belge `OrgPrefix` | `DocumentCodingOptions.OrgPrefix` (`…/DocumentManagementIdentifiersModels.cs:45`, kod varsayılanı `"GMG"`; `appsettings*.json`'da bölüm yok — ölçüldü) | grup önekinin **tek kaynağı** (Bölüm 4.3) |
| MDM kısa kodunun değiştirilebilirliği | bugün değiştirilebilir — güncelleme işleyicisi yeni `Code`'u kırpıp yalnız tekilliğini denetler (`…/LegalEntity/Handlers/CommandHandlers/UpdateLegalEntityHandler.cs:36-37`); §5.4 kilidi yok | ara dönem azaltıcısı bu serviste (Bölüm 8.13); MDM kilidi **A-18** (MDM sahibi Ali + Beste, 2026-11-06) |
| RCS-001 §3 `AGR` satırı + izinli kapsam sütunu | §3 bugün `CTR` (`record-coding-standard.md:70`) | DL-1'i bekletmez: katalog testinde adlı tek istisna; DL-7'de kalkar (A-3) |
| Tüketici kimlik bilgileri (`FinanceTreasury`, `Mdm`) | yok | uçtan uca kabul için gerekli; değerler ortam gizli bilgisi, depoda yok |
| BL-501 ortak iletici | MOD-0178 §8.8: olduğu gibi | **kullanılmaz** — bu komut Platform içindedir |

### 7.1 Tüketici yükümlülükleri (tüketici paketlerine yazılır; bu paketin AC'si değildir)

| # | Yükümlülük | Kim | Neden |
|---|---|---|---|
| TC-1 | Çağrıyı başlatan kullanıcının bearer token'ını iletmek; `X-Tenant-Id` göndermemek | bütün tüketiciler | Bölüm 8.2 |
| TC-2 | 200 dışı her cevap, zaman aşımı ya da port bağlı değilse Create'i yapmamak; kendi sayacını / yedek kodu üretmemek; zaman aşımı, 429 ve 503 sonrası **aynı** `idempotencyKey` ile yeniden denemek | bütün tüketiciler | D59/1, RCS-001 §6 |
| TC-3 | **Tüzel kişilik onay ön kontrolü:** MDM lookup-validation v1.1 (`approvalStatus`) yayınlanana kadar şirketin **onaylı** olduğunu kendi Create'inde denetlemek — bu servis o güne kadar yalnız "aktif"i garanti eder (r3, inceleme 02 F-8) | MOD-0126 / 0119 / 0120 / 0124 (D10/1 tablosu zaten yazıyor), MOD-0289, MOD-0217 | Bölüm 7, 8.3 |
| TC-4 | **`BAC` kapsam seçimi çağıranda:** MDM bağdaştırıcısı şirket hesabında `prefixScope = LegalEntity` + `legalEntityId` = hesabın sahibi şirket, tedarikçi hesabında `prefixScope = Group` + `legalEntityId = null` gönderir; tüketici AC'si: iki hesap türü için doğru kapsam ve sahte allocator'la sabotaj (eşleme ters çevrilince kırmızı) | MOD-0289 (MDM bağdaştırıcısı, A-11) | servis hesabın türünü bilmez |
| TC-5 | `BNK` için `prefixScope = Group`; port imzasına `prefixScope` eklemek (v0 portunda yok) | MOD-0289, Treasury `IRecordCodeAllocator` (A-11) | K-4 |
| TC-6 | `reason_code`'u kendi kaynak dosyasıyla 7 dilde çevirmek; `errors[]` metnini göstermemek | bütün tüketiciler | Bölüm 8.9 |
| TC-7 | Kendi Create kaydına `idempotencyKey` ve verilen `code`'u yazmak (iki denetim kaydının eşleşmesi) | bütün tüketiciler | Bölüm 21 |
| TC-8 | 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`'i kullanıcıya "şirket kısa kodu değişmiş, MDM yöneticisine başvurun" olarak göstermek | bütün LegalEntity tüketicileri | Bölüm 8.13 |

**Lookup & Reference Data kararı (module-pack-standard §13.1):** Platform lookup kullanılmaz; yeni Platform lookup
anahtarı yok. Şirket kısa kodu MDM/reference sınırındadır, MDM'den okunur, Platform'da kopyası tutulmaz (ayırma
defterindeki `Prefix` alanı verilmiş kodun parçasıdır, kısa kod tablosu değildir). Hardcoded yedek liste yok.

## 8. Runtime Constraints

### 8.1 Port, rota, servis

- Diten.Platform, port **5057** (AGENTS.md §3). Uç yalnız `/api/internal/*`; Platform `TenantResolutionMiddleware` bu yolu
  atlar (`InternalAuditController.cs:13`) — kiracıyı ve kiracı durumunu yürütücü kurar/denetler.
- Gövde ≤ **4 KB** → aşarsa 413 `RECORD_CODE_REQUEST_TOO_LARGE`.
- Zaman bütçesi: MDM okuması **3 s** (`MdmLegalEntityReferenceValidator.cs:52-57` yorumu, WorkingCalendar emsali), isteğin
  tamamı **5 s**; dolarsa 503. Sözleşme 503 der; emsallerin 504'ü kullanılmaz.

### 8.2 İç uç kimliği — P2 (K-8r, K-12 e) ve kimlik bilgisi sızmasına karşı azaltıcılar (F-4)

Ölçülen üç desen: P1 ortak sır `X-Internal-Api-Key` (`InternalAuditController.cs:24, 92-111`) kiracıyı **gövdeden** alır
(`:56-70`) → uymaz. P3 RS256 servis kimliği token'ı (`TrustedServiceTokenValidationExtensions.cs:16-23`,
`TrustedSourceAuditIntentServiceIdentity.cs:32-55`) Auth'ta yalnız `Diten.MDM` için verilir
(`services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17`)
ve kullanıcı token'ı taşımaz → DL-9. **Seçim P2** (MOD-0018-FU21 deseni; `TrustedLegalEntityScopeRequestExecutor.cs:12-92`,
`TrustedLegalEntityScopeJwtContext.cs:11-41`, `TrustedLegalEntityScopeCredentialAuthenticator.cs:58-95`).

**Başlıklar (her biri tam bir kez):** `X-Record-Code-Credential-Id`, `X-Record-Code-Credential`, `X-Record-Code-Audience`
(`TRUSTED_RECORD_CODE_ALLOCATE`), `Authorization: Bearer <kullanıcı JWT'si>`.

**401 / 403 eşlemesi — FU21 ile birebir:**

| Durum | FU21 kaynağı | Cevap |
|---|---|---|
| Üç kimlik bilgisi başlığından biri yok ya da birden çok | `TrustedLegalEntityScopeRequestExecutor.cs:36-41` | 401 `RECORD_CODE_UNAUTHENTICATED` |
| Kimlik bilgisi yapılandırması geçersiz (boş, tekrarlı, katalog dışı tür, kaynak servis kümesi dışı ad) | `…CredentialAuthenticator.cs:63-66` (`Denied`) | 403 `RECORD_CODE_FORBIDDEN` |
| Kimlik bilgisi **iptal** (`IsRevoked`), tanımlayıcı boş/bilinmiyor, sır boş ya da yanlış (döndürme penceresi dışı) | `:68-73`, `:83-94` (`Unauthenticated`) | **401** `RECORD_CODE_UNAUTHENTICATED` |
| Bağlamanın `ConsumerService` / `AllowedAudience`'ı ya da hedef kitle başlığı uyuşmuyor | `:76-80` (`Denied`) | 403 `RECORD_CODE_FORBIDDEN` |
| `X-Tenant-Id` başlığı var | `TrustedLegalEntityScopeJwtContext.cs:11-16` (`Forbidden` = kimliği doğrulanmış sayılır, `ITrustedLegalEntityScopeJwtContext.cs:20-21`) | 403 `RECORD_CODE_FORBIDDEN` |
| Kullanıcı token'ı yok / geçersiz / süresi dolmuş | `:18-22` | 401 `RECORD_CODE_UNAUTHENTICATED` |
| Token'da tam bir `tenant_id` / `sub` yok, Guid değil, boş, ya da `actor_type ≠ tenant_user` | `:25-38` | 403 `RECORD_CODE_FORBIDDEN` |
| Hız sınırı aşıldı | yeni (r3 F-5) | 429 `RECORD_CODE_RATE_LIMITED` |
| Kiracı aktif değil (askıda, sağlanıyor, devre dışı, yok) | yeni (inceleme 01 F-12) | 403 `RECORD_CODE_TENANT_NOT_ACTIVE` |
| Tür bu kimlik bilgisinin izinli kümesinde değil | FU21 `AllowsPair` emsali `:68-72` | 403 `RECORD_CODE_FORBIDDEN` |
| Kullanıcıda türün gerekli izni yok | FU21 `HasExactPermission` emsali `:68-72` | 403 `RECORD_CODE_PERMISSION_MISSING` |

**Kiracı, kullanıcı ve denetim aktörü (K-8r; r3 inceleme 02 F-6).** Kiracı = doğrulanmış token'ın `tenant_id`'si;
`TenantScope.Begin(_tenantContext, jwtTenant)` (`TrustedLegalEntityScopeRequestExecutor.cs:74` deseni). Gövdede
`tenantId` üyesi 400; sorgu dizesi okunmaz. Yürütücü kullanıcı token'ını FU21'deki gibi **açık**
`HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)` ile doğrular (`TrustedLegalEntityScopeJwtContext.cs:18`)
ve `HttpContext.User`'ı doğrulanmış principal'a ayarlar; bu, MediatR işleyicisi ve denetim kapısının
`ITenantAuthorizationContext`'i çözülmeden **önce** olur (o bağlam principal'ı kurucusunda okur —
`…/Infrastructure/Authorization/JwtTenantAuthorizationContext.cs:24`; emsal: iş akışı yürütücüsü principal'ı dispatch'ten
önce ayarlıyor, `TrustedWorkflowConsumerRequestExecutor.cs:126-138`). Aktörün gerçekten `TenantUser` + `sub` çıktığı
DL-1'in **ilk adımında** gerçek HTTP ana makinesinde üretim yazıcısıyla ölçülür (AC-41); ölçüm başarısızsa DL-1 durur ve
paket revizyonuna döner.

**Kaynak servis kapalı listesi (K-9; r3 inceleme 02 F-4).** Kimlik bilgisi bağlaması → servis adı:

| Bağlama | `ConsumerService` | Denetimdeki `SourceService` / defterdeki `CallerService` | İzinli türler |
|---|---|---|---|
| `FinanceTreasury` | `DITENFINANCETREASURYSERVICE` | `Diten.FinanceTreasuryService` | `PAY`, `APD`, `ARI`, `CPS`, `CTP` |
| `Mdm` | `DITENMDMSERVICE` | `Diten.MdmService` | `BNK`, `BAC`, `AGR` |

Eşleme **kodda** sabittir; yapılandırma yalnız sırları ve izinli türleri taşır (tablonun alt kümesi olmak zorunda, değilse
yapılandırma geçersiz → bütün istekler 403). Eşlemedeki her ad, denetim kapısının izinli kaynak servis kümesinde
(`TransactionOwnedAuditSourceServices`, Bölüm 5) olmak zorundadır — bunu bir test sabitler. Kapı küme dışı bir adı
istisnayla reddeder; böylece başka bir Platform çağıranı da başka bir servis adına kayıt yazamaz. Gövdede ya da başlıkta
gelen bir servis adı okunmaz; gövdede `sourceService` üyesi 400.

**Sızma senaryosu (inceleme 01 F-4).** Bir kimlik bilgisi sırrı sızarsa saldırganın ayrıca o kiracıda, türün oluşturma
iznini taşıyan geçerli bir kullanıcı token'ına ihtiyacı vardır. Yapabileceği: o kiracıda, o bağlamanın izinli türleri için
numara harcamak. Yapamayacağı: veri okumak, başka kiracıya ya da başka türe kod üretmek, var olan kodu değiştirmek.
Azaltıcılar:

1. Tüketici başına izinli tür kümesi — tür dışı → 403.
2. Kullanıcıda türün oluşturma izni (Bölüm 4.4 son sütun) — yoksa 403 `RECORD_CODE_PERMISSION_MISSING`.
3. **Hız sınırı (r3 F-5):** yürütücü içinde, kimlik bilgisi ve token doğrulandıktan **sonra**, (tüketici servis adı,
   token kiracısı) bölümlü sabit pencere (`System.Threading.RateLimiting.PartitionedRateLimiter`, kuyruk 0). Bir kiracıdaki
   taşkın başka kiracının kovasını boşaltmaz. Aşımda yürütücü 429 `RECORD_CODE_RATE_LIMITED`'i kendi ret yolundan yazar
   (`Response<T>` zarfı); ASP.NET `OnRejected` kancası kullanılmaz. Ret denetime yazılmaz (Bölüm 21.2). **Değer açık karar
   A-16** (CT önerisi 600 istek/dakika/bölüm).
4. Her ayırma denetimde kullanıcı + kaynak servisle görünür (tespit).
5. Döndürme ve iptal (FU-RCS-13): yeni `ActiveSecret`, eskisi `PreviousSecret` + `PreviousValidUntilUtc` (en çok 24 saat
   önerisi, A-16); acil durumda `IsRevoked = true` → anında 401.

**Sınır (kabul):** arka plan işi (kullanıcı token'ı yok) çağıramaz → 401. v1 tüketicileri kullanıcı başlatmalı:
MOD-0124 "Arka plan işi yok" (`MOD-0124-cash-positioning.md:1488`), MOD-0178 S1 görev kaynağı elle
(`MOD-0178-putaway-picking-packing.md:506-514`). İleride → DL-9.

**Yeni yapılandırma (değer yok):** bölüm `TrustedRecordCodeCredential`, bağlama başına `Identifier`, `ConsumerService`,
`AllowedAudience`, `ActiveSecret`, `PreviousSecret`, `PreviousValidUntilUtc`, `IsRevoked`, `AllowedRecordTypes`; hız
sınırı `RecordCodes:RateLimit:PermitsPerMinute`. Sırlar ortam değişkeni / gizli bilgi deposundan (MOD-0012).

### 8.3 Şirket kısa kodu ve uygunluk (K-5, F-9)

- `MdmLegalEntityShortCodeReader`: `GET {MdmService:BaseUrl}/api/legal-entities/{id:D}/lookup-validation`; `X-Tenant-Id`
  = token kiracısı; `Authorization` = iletilen kullanıcı token'ı (`MdmLegalEntityReferenceValidator.cs:104-109, 142-152`);
  tenancy istemcide (`:19-27` uyarısı).
- **Uygunluk (r3, inceleme 02 F-8 ile düzeltildi).** Hedef kural: kod yalnız **aktif VE onaylı** tüzel kişiliğe verilir.
  Bugün MDM main'deki lookup-validation **1.0.0**'dır ve `approvalStatus` alanı yoktur (`LegalEntityModels.cs:100-106`);
  v1.1 taslaktır. Bu yüzden:
  - **v1.1 yayınlanana kadar (DL-1):** servis yalnız **aktiflik** denetler (MDM 404 → 404); onay denetimi **tüketicinin
    zorunlu ön kontrolüdür** (TC-3). Yanıtta `approvalStatus` görünürse yine tabloya göre değerlendirilir (aşağıda).
  - **v1.1 yayınlandığında:** v1.1 kuralı geçerlidir — alan **yoksa** ya da tanımsız değerdeyse yanıt geçersizdir → 503
    (`legal-entity-lookup-validation-v1.1-proposal.md:69`). Bu, bu serviste bir **kod değişikliğidir** (okuyucunun "alan yok"
    dalı 503'e çevrilir); kendiliğinden olmaz. Açık bağımlılık: **MOD-0220 sahibi** v1.1'i yayınlar; Beste aynı sürümde
    FU-RCS-15'i teslim eder; TC-3 o zaman kalkar.

| MDM yanıtı | DL-1 (v1.1 öncesi) cevap |
|---|---|
| 200 tutarlı (`legalEntityId` eşit, `referenceable = true`, `lifecycleState = ACTIVE`), `code` `^[A-Z]{2,5}$`, `approvalStatus` yok | ayırma sürer (onay: TC-3) |
| aynı, `approvalStatus = APPROVED` | ayırma sürer |
| aynı, `approvalStatus ∈ {DRAFT, SUBMITTED, REJECTED}` | 422 `LEGAL_ENTITY_NOT_ELIGIBLE` |
| aynı, `approvalStatus` boş / `null` / dört değer dışı | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| aynı, `code` kalıba birebir uymuyor (normalleştirme yok) | 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` |
| 404 | 404 `LEGAL_ENTITY_NOT_FOUND` (tek gövde) |
| 200 ama `data` yok / ayrıştırılamaz / `legalEntityId` farklı / `referenceable = false` / `lifecycleState ≠ ACTIVE` | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| 400, 401, 403, 5xx, 3 s aşımı, bağlantı yok | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |

- Her yeni ayırmada okunur; önbellek yok. İdempotent tekrar MDM'i çağırmaz.

### 8.4 Biçim ve sayaç

- Kod `{ÖNEK}-{TÜR}-{SAYAÇ}`; `SAYAÇ` en az 6 hane sıfır dolgulu, `999999`'dan sonra `1000000`, kesilmez (§5.7). `long`.
- Sayaç: `findOneAndUpdate` + `$inc LastNumber 1`, `upsert`, `ReturnDocument.After`, filtre `(TenantId == token kiracısı,
  Prefix, RecordType)` — **işlem dışında** (MDM `CodeReservationRepository.cs:549-600` deseni). İlk yaratma yarışında
  E11000 → upsert'süz yeniden dener (MDM `:579`, en çok 3 deneme `:16`). ⚠ Doküman sayacı
  (`DocumentManagementIdentifierRepositories.cs:58-90`) bu kurtarmayı yapmıyor — örnek alınmaz (FU-RCS-12).
- Boşluk kabul (§5.8); ardışıklık garanti değildir, tekillik garantidir; `(TenantId, Code)` unique ikinci savunma.

### 8.5 Değerlendirme sırası

1. Boyut → 413. 2. Kimlik bilgisi + kullanıcı token'ı + `X-Tenant-Id` → 401 / 403 (Bölüm 8.2 tablosu). 3. Hız sınırı
(tüketici servis, kiracı) → 429 `RECORD_CODE_RATE_LIMITED`. 4. Kiracı aktif mi → 403 `RECORD_CODE_TENANT_NOT_ACTIVE`.
5. Gövde → 400 `RECORD_CODE_REQUEST_INVALID`. 6. Tür katalogda mı → 422 `RECORD_TYPE_UNKNOWN`. 7. Tür bu kimlik bilgisine
izinli mi → 403 `RECORD_CODE_FORBIDDEN`. 8. Kullanıcıda türün izni var mı → 403 `RECORD_CODE_PERMISSION_MISSING`.
9. **İdempotency** `(TenantId, CallerService, IdempotencyKey)`: kayıt varsa parmak izi eşit → **200 aynı kod + sequence**
(sayaç, MDM, yeni denetim yok); değil → 400 `IDEMPOTENCY_KEY_REUSED`. 10. Kapsam kuralları → 422 `RECORD_CODE_SCOPE_INVALID`
(DL-1'de `Platform` bu adımda reddedilir). 11. Önek: Group → kiracı ayarı (yoksa 409 `GROUP_PREFIX_MISSING`; etkin belge
`OrgPrefix`'ten farklıysa 409 `GROUP_PREFIX_MISMATCH`); LegalEntity → MDM (Bölüm 8.3), ardından kısa kod kullanım
denetimi (Bölüm 8.13; 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`). 12. Sayaç `$inc` (depo hatası → 503 `RECORD_CODE_UNAVAILABLE`).
13. **Tek işlem** (Bölüm 8.6) → 200.

Yetki (7–8) idempotency'den (9) önce gelir: izni olmayan kullanıcı başkasının anahtarını tekrarlayarak kod okuyamaz.
**Tür izni (7) kapsamdan (10) önce gelir:** izinli çağıranı olmayan bir tür (`ORG`, `POS`, `PLN`) hangi kapsamla
istenirse istensin 403 alır; ör. `PLN` + `Platform` → 403 `RECORD_CODE_FORBIDDEN`, `PAY` + `Platform` → 422
`RECORD_CODE_SCOPE_INVALID` (r3, inceleme 02 F-9).

### 8.6 İşlem ve eşzamanlılık (F-6)

- Tek işlem: defter satırı ekle (yeni `allocationId`) + (Group ise) grup öneki ayarını kilitle (`LockedAtUtc` boşsa yaz;
  filtre `GroupPrefix == kullanılan önek`, eşleşme yoksa işlem iptal ve adım 11'den yeniden) + denetim kutusu satırı.
- **Denetim kutusu anahtarı (r3, inceleme 02 F-2).** `audit_outbox.IdempotencyKey` küresel tekildir
  (`PlatformSchemaManifest.AccessGovernance.cs:118-120`); istemcinin `idempotencyKey`'i **kullanılmaz** (iki kiracı aynı
  istemci anahtarını kullanabilir; önce ret kaydı yazılmış bir anahtarla gelen başarılı ayırma çakışırdı). Emsal
  `PhysicalEntitlementAuditIntent.cs:29` (`physical-entitlement:{requestType}:{intentId:N}`):
  - başarılı ayırma: `record-code:allocated:{allocationId:N}` — `allocationId` defter satırının kimliğidir, her ayırma
    denemesinde yeni Guid;
  - ret kaydı: `record-code:refused:{Guid.NewGuid():N}` — her ret için yeni Guid.
- **`TryInsertAsync` `false` dönerse** (depo E11000'i `false`'a çevirir, `AuditOutboxRepository.cs:159-161`): anahtar
  `allocationId`'den türediği için bu yalnız **aynı ayırmanın** kutu satırı daha önce yazılmışsa olur (commit sonucu
  bilinmeyen bir denemenin tekrarı). Kural: işlem iptal edilir (MongoDB işlem içindeki yazım hatasında işlemi iptal eder —
  sürücü davranışı, bu repoda ölçülmedi), ardından defter `allocationId` ile okunur: satır varsa **zaten yazılmıştır** →
  200 o satırın kodu ve sequence'i (503 değil); satır yoksa (yalnız başka bir yazarın anahtar çakışmasında mümkün) 503
  `RECORD_CODE_UNAVAILABLE` + **hata** logu. Ret yolunda `false` → uyarı logu, cevap değişmez.
- **Geçici çakışma:** `TransientTransactionError` / `WriteConflict` → işlem en çok **3** kez, 5 s bütçe içinde yeniden
  denenir. **Aynı istemci anahtarı yarışı:** defterde `(TenantId, CallerService, IdempotencyKey)` E11000 → işlem iptal,
  kazanan satır okunur, parmak izi eşitse 200 (kazananın kodu), değilse 400. **503 yalnız gerçek erişilemezlikte**
  (bağlantı yok, bütçe doldu, yeniden denemeler tükendi).
- **Boşluk üst sınırı:** aynı anahtarla eşzamanlı **N** istek en çok **N−1** sayaç numarası harcar; hepsi aynı kodu alır.
- Farklı anahtarlı eşzamanlı istekler aynı sayaçta farklı numara alır; sayaç işlem dışında olduğu için sıcak sayaçta işlem
  çakışması doğmaz.

### 8.7 Platform kapsamı (K-11)

DL-1'de `prefixScope = Platform`, tür izni adımını geçmiş her istekte 422 `RECORD_CODE_SCOPE_INVALID` (sözleşme: "DL-4'te
açılır"); izinli çağıranı olmayan tür (`PLN`) bu adıma gelmeden 403 alır (Bölüm 8.5). Gerekçe:
merkezi denetim kutusu boş `TenantId`'yi reddeder (`AuditOutboxWriteRequest.cs:28-30`), kayıt yoksa kod yok kuralı
(Bölüm 21) kiracısız satırı bugün yazamaz; HTTP'de Platform kapsamlı tüketici de yok. DL-4: PLN bağlanmasıyla birlikte
kiracısız sayaç/defter tabanı, platform-global denetim yolu ve süreç içi port kararlaştırılır (AC-25).

### 8.8 Veri kapsamı (SEC-002) — kapsam cümlesi

v1'de kiracı çapında satır döndüren sorgu yoktur (yönetici `GET`'i tek satırlık kiracı ayarıdır, Platform aktörüne;
data-scope-enforcement "Platform yöneticisi yüzeyleri" istisnası). Şirket kapsamı (kullanıcı bu şirket için kayıt
açabilir mi) çağıranın Create komutunda kendi kapsam portuyla denetlenir; Platform bu uçta kapsam sormaz — yetkinin
sınırı kimlik bilgisi + kullanıcının modül izni + kiracı eşitliğidir ve kod şirket verisi sızdırmaz. Tenant-wide izni yok.
DL-10 ekranları kapsam cümlesini o revizyonda yazar.

### 8.9 Yerelleştirme

Platform v1'de kullanıcıya görünen metin üretmez. `errors[]` İngilizce tanılama metnidir ve gösterilmez (TC-6). `.resx` yok.

### 8.10 Günlük

Her ret `reason_code`, kaynak servis, kiracı, korelasyonla **uyarı** düzeyinde loglanır. Kimlik bilgisi sırrı,
`Authorization`, JWT, MDM gövdesi loglanmaz.

### 8.11 Ret kaydı (K-10) — yazım yolu

Ret iş işlemi değildir: kayıt **ayrı kısa bir işlemle** (yeni `IPlatformTransactionSession`) yalnız denetim kutusuna
yazılır — `ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync(session, request, outcome)`. Defter, sayaç ve grup öneki
ayarına bu işlemde **dokunulmaz**; ret yolu hiçbir koşulda kod üretmez ya da döndürmez. Ret kaydı yazılamazsa uyarı
loglanır, cevap değişmez (en iyi çaba, sessiz değil). Hangi retlerin yazıldığı Bölüm 21.2'dedir.

### 8.12 Grup öneki tutarlılığı (K-12 c; r3 inceleme 02 F-7)

- Grup öneki değerinin **tek kaynağı** belge kodlarının `OrgPrefix`'idir (RCS-001 §5.3: "Belge kodlarındaki `OrgPrefix` ile
  aynı değer"). Kiracı ayarı (`record_code_group_prefixes`) onun Platform yöneticisi hizalamasıyla alınmış ve ilk
  kullanımda kilitlenen kopyasıdır; ayrı düzenlenebilir değer yoktur (Bölüm 4.3).
- Her `Group` ayırmasında kiracı ayarı etkin `OrgPrefix` ile karşılaştırılır (bellekte); farklıysa 409
  `GROUP_PREFIX_MISMATCH`, kod verilmez, sayaç ilerlemez.
- Kilitsizken düzeltme = `OrgPrefix`'i değiştirip yeniden hizalamak; kilitten sonra hizalama farklı değerde 409
  `GROUP_PREFIX_LOCKED`. Kurtarma: `OrgPrefix`'i kilitli değere geri almak; bilinçli önek değişikliği FU-RCS-16 (sahip kararı).
- Servis grup önekini hiçbir zaman kendiliğinden doldurmaz; kayıt yoksa 409 `GROUP_PREFIX_MISSING`.
- Sınır: `OrgPrefix` bugün kiracı başına değil, tek yapılandırma değeridir; ikinci bir iş kiracısı farklı grup önekiyle
  gelirse belge tarafının kiracı başına olması gerekir (FU-RCS-14).

### 8.13 Kısa kod kullanım kilidi — ara dönem azaltıcısı (RCS-001 §5.4; r3 inceleme 02 F-11)

- **Risk (ölçüldü):** MDM tüzel kişilik kısa kodu bugün değiştirilebilir (`UpdateLegalEntityHandler.cs:36-37` yalnız
  tekillik denetler); §5.4 "bir kez önekte kullanıldıktan sonra değiştirilemez" kuralının MDM tarafı (DL-5) yok. Serbest
  kalan bir kod başka bir şirkete verilirse sayaç anahtarı `(kiracı, önek, tür)` olduğu için aynı sayaçtan ona da kod
  üretilir ve önek iki şirketi gösterir.
- **Azaltıcı (DL-1, bu serviste):** LegalEntity kapsamında MDM'den okunan kısa kod `K`, ayırma defterine karşı denetlenir:
  (a) bu şirketin (`LegalEntityId = X`) en son ayırmasının `Prefix`'i `K`'den farklıysa — kısa kod kullanıldıktan sonra
  değişmiş — 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; (b) `K` bu kiracıda daha önce **başka** bir şirketin
  (`LegalEntityId ≠ X`; yalnız LegalEntity kapsamlı satırlar — Group satırlarında `LegalEntityId` boştur ve sayılmaz)
  ayırmasında önek olarak kullanılmışsa — kod başka şirkete geçmiş — 409
  `LEGAL_ENTITY_SHORT_CODE_CHANGED`. İkisinde de kod verilmez, sayaç ilerlemez, `Failed` ret kaydı ve **hata** logu yazılır.
  Index'ler Bölüm 4.2. Bu, Platform'da ayrı bir kısa kod tablosu değildir (K-5); verilmiş kodların defteridir.
- Kurtarma: MDM kısa kodu eski değerine geri alınır (yetkili yol), ya da sahip kararıyla bilinçli önek değişikliği
  (DL-5 ile birlikte).
- **Sahip ve tarih:** MDM tarafındaki kalıcı kilit (DL-5) için karar **MDM sahibi (Ali) + Beste, 2026-11-06** (A-18).

## 9. Layout & Shell Contract

`shell: none`. Bu FU hiçbir Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez; hiçbir `.cshtml`
oluşturulmaz, bu yüzden `Layout = "_LayoutPlatformAdmin"` ya da `Layout = "_LayoutTenantShell"` yazılacak dosya yoktur.
DL-10 ekranları Platform yönetici kabuğunda olacaktır (`Layout = "_LayoutPlatformAdmin"`, `Views/Platform/RecordCodes/`).

## 10. Backend File Convention

Golden Reference klasör/ad kuralı (module-pack-standard §4) birebir; `DocumentManagementIdentifiers`'ın `Services/` alt
klasörü örnek alınmaz:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/
├── Commands/
│   ├── AllocateRecordCodeCommand.cs
│   └── AlignRecordCodeGroupPrefixCommand.cs     (IAuditableCommand + IAuditMetadataProvider; gövdesiz)
├── Queries/
│   └── GetRecordCodeGroupPrefixQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── AllocateRecordCodeHandler.cs
│   │   └── AlignRecordCodeGroupPrefixHandler.cs
│   └── QueryHandlers/
│       └── GetRecordCodeGroupPrefixHandler.cs
├── Validators/
│   ├── AllocateRecordCodeValidator.cs
│   └── AlignRecordCodeGroupPrefixValidator.cs
├── RecordCodeReasonCodes.cs
├── RecordCodeAuditIntent.cs
└── RecordCodesModels.cs
```

- Handler/Validator adlarında `Command` / `Query` eki yok.
- İşleyici sırayı yönetir; biçim `RecordCodeFormat`'ta, kapsam kuralı `RecordCodeTypeCatalog`'da (Domain), MDM portta,
  Mongo depoda; kiracı işleyiciye yürütücüden parametre olarak gelir.
- Controller `CustomBaseController`, `Response<T>` (`…/Application/Common/Response.cs:7-16`).
- `AllocateRecordCodeCommand` `IAuditableCommand` değildir: `AuditBehavior` yazmaz, denetim işleyicinin içindedir.

## 11. Frontend File Contract

Yok. `golden_reference: none`, `form_field_count: 0`. Tarayıcı bu uçları çağırmaz. Sayfa yardımı (UXH-001) ve tur gerekmez.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| gövde | evet | JSON nesnesi; bilinmeyen üye (ör. `tenantId`, `sourceService`, `code`) → 400; tekrarlı üye → 400; ≤ 4 KB | — | ayrıştırıcı |
| `recordType` | evet | `^[A-Z]{3}$`, duyarlı; normalleştirme yok | — | katalog (422), kimlik bilgisi (403), kullanıcı izni (403) |
| `prefixScope` | hayır | `LegalEntity` \| `Group` \| `Platform`; yoksa `LegalEntity`; başka değer 400 | — | izinli kapsam; DL-1'de `Platform` tür izni geçtiyse 422 |
| `legalEntityId` | kapsama bağlı | `LegalEntity`: zorunlu, Guid, boş değil · `Group`: yok ya da `null` | — | uyumsuzluk 422; Guid değil 400 |
| `idempotencyKey` | evet | 1–200 karakter, yazdırılabilir ASCII `!`…`~` (A-11) | unique `(TenantId, CallerService, IdempotencyKey)` (yalnız defterde; denetim kutusu anahtarı bundan türemez) | defter araması |
| Grup öneki hizalaması (yönetici ucu) | gövde yok | etkin `OrgPrefix` `^[A-Z]{2,5}$` olmalı, değilse 409 `GROUP_PREFIX_SOURCE_INVALID` | unique `(TenantId)` | kilitli ve farklıysa 409 `GROUP_PREFIX_LOCKED`; kiracı yoksa 404; gövde gönderilirse 400 |
| `Intent.SourceService` (kapı) | hayır | boş ya da `TransactionOwnedAuditSourceServices` kümesinde birebir | — | küme dışı → istisna, hiçbir şey yazılmaz |
| Üretilen kod | — | `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` (DL-1) | unique `(TenantId, Code)` | `RecordCodeFormat` |

## 13. Failure Path to Verify

- **Aynı anahtar, aynı gövde (tekrar)** — 200, ilk kod ve sequence; sayaç ilerlemez; MDM çağrılmaz; yeni denetim satırı yok.
- **Aynı anahtar, farklı gövde** — 400 `IDEMPOTENCY_KEY_REUSED`; sayaç ilerlemez; `Failed` ret kaydı.
- **Eşzamanlı aynı anahtar (N istek)** — hepsi aynı kodu alır (farklı gövdeliler 400); tek defter satırı; geçici yazım
  çakışması yeniden denenir ya da kazanan okunur; 503 yalnız gerçek erişilemezlikte; en çok **N−1** boşluk.
- **İki kiracı aynı istemci anahtarıyla** — ikisi de kendi kodunu alır; denetim kutusu anahtarları çakışmaz (ayırma kimliğinden).
- **Önce ret, sonra aynı anahtarla başarı** (ör. önce 409, MDM düzeltilince tekrar) — başarılı ayırma 200; ret ve başarı
  kayıtları ayrı satırlardır (ret anahtarı yeni Guid).
- **Commit sonucu bilinmeyen deneme tekrarı** — kutu `false` → defter `allocationId` ile okunur → 200 aynı kod; 503 değil.
- **Eşzamanlı farklı anahtarlar** — hepsi farklı kod; ilk sayaç yaratma yarışı 500 üretmez.
- **Tanımsız / KALSIN tür** — 422 `RECORD_TYPE_UNKNOWN`; sayaç yaratılmaz.
- **Tür kimlik bilgisine izinsiz** (Mdm ile `PAY`; herkes için `ORG`/`POS`/`PLN`, hangi kapsamla olursa olsun) — 403 `RECORD_CODE_FORBIDDEN`; `Denied`.
- **Kullanıcıda izin yok** — 403 `RECORD_CODE_PERMISSION_MISSING`; `Denied`; tekrar da reddedilir.
- **Uyumsuz kapsam** (`BNK` + LegalEntity; `PAY` + Group; Group + dolu `legalEntityId`; LegalEntity + boş; tür izni geçmiş `Platform`) — 422 `RECORD_CODE_SCOPE_INVALID`; MDM çağrılmaz; `Denied`.
- **Geçersiz kısa kod** — 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; sayaç yaratılmaz.
- **Kısa kod kullanıldıktan sonra değişmiş / başka şirkete geçmiş** — 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; kod yok.
- **Onaysız şirket** (`approvalStatus` görünür ve `APPROVED` değil) — 422 `LEGAL_ENTITY_NOT_ELIGIBLE`.
- **Grup öneki yok / belge `OrgPrefix`'ten farklı** — 409 `GROUP_PREFIX_MISSING` / `GROUP_PREFIX_MISMATCH`; varsayılana düşülmez.
- **Başka kiracının / olmayan / aktif olmayan şirket** — 404 `LEGAL_ENTITY_NOT_FOUND`; tek gövde; `Denied`.
- **Askıdaki kiracı** — 403 `RECORD_CODE_TENANT_NOT_ACTIVE`; tekrar da reddedilir; kod yok.
- **Varsayılan / tohum kiracı (`…0001`) kullanıcısı** — normal iş kiracısı gibi kod alır.
- **Platform bağlamı ya da `Guid.Empty` kiracı ile depo çağrısı** — istisna; satır yok.
- **MDM erişilemez** — 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`; kod yok; sayaç ilerlemez.
- **Mongo erişilemez / işlem tükenir** — 503 `RECORD_CODE_UNAVAILABLE`; kod yok; alınmış sayaç numarası boşluk kalır.
- **Denetim kutusu satırı yazılamaz (bağlantı)** — işlem iptal, 503; defter satırı yok.
- **Kapıya küme dışı kaynak servis adı** — istisna, hiçbir şey yazılmaz.
- **Yetkisiz aktör** — Bölüm 8.2 tablosu; işleyici çağrılmaz.
- **Gövdede kiracı ya da kaynak servis** — 400; kiracı ve kaynak servis değişmez.
- **Hız sınırı** — 429 `RECORD_CODE_RATE_LIMITED`; başka kiracının kovası etkilenmez.
- **Sayaç sınırı** — `…-999999`, sonra `…-1000000`.
- **Grup öneki hizalama + eşzamanlı ilk ayırma** — biri kazanır: hizalama önce (ayırma yeni öneki kullanır ve kilitler) ya
  ayırma önce (farklı değerle hizalama 409 `GROUP_PREFIX_LOCKED`); sessiz üzerine yazma YOK.
- **Kilitten sonra `OrgPrefix` değişti** — her Group ayırması 409 `GROUP_PREFIX_MISMATCH` + hata logu; kurtarma Bölüm 4.3.

## 14. Authorization Convention

- **İç uç:** `[ApiController]`, `[AllowAnonymous]` (çerçeve düzeyinde), `[Route("api/internal/record-codes")]`; kapı
  yürütücüdedir (Bölüm 8.2). `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesine yalnız her reddi adı
  konmuş bir testi kırmızıya döndüren controller girer (`:99-113` yorumu). `[HasPermission]` yoktur (çağıran servis);
  yürütücü kullanıcının **türe özgü oluşturma iznini** ister (Bölüm 4.4).
- **Aktör:** denetimde `TenantUser` (iletilen token'ın `sub`'ı), kaynak servis kapalı listeden (K-8r, K-9).
- **İzinli çağıran × tür:** Bölüm 8.2 tablosu.
- **Yönetici ucu (gövdesiz hizalama):** `[Authorize(Policy = "PlatformActor")]` (`Controllers/Admin/TenantsController.cs:15-16`
  emsali), `[HasPermission("platform.record-code-group-prefixes.update")]` (PUT) ve `...read` (GET); aktör `platform_admin`.
  Kiracı kullanıcısı 403. Değer yönetici tarafından girilmez; etkin belge `OrgPrefix`'inden kopyalanır (Bölüm 4.3).

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği gereksiz.**

- İç uç Gateway'e açılmaz: `gateway/Diten.ApiGateway/ocelot.json`'da `/internal` geçen rota **0**, genel yakalayıcı yok
  (ölçüldü). Tüketiciler Platform'a servis adresiyle (5057) gider.
- Yönetici ucu mevcut `/api/admin/tenants/{everything}` → 5057 rotasıyla ulaşılır (`ocelot.json` upstream `:1733`).
- `ocelot.json` korunan yol; `RecordCodeGatewayExposureTests` `record-codes` geçen rota olmadığını ölçer.

## 16. Acceptance Criteria

**DL-1 — v1 servis (K-2).** Her maddenin sabotajı maddenin içindedir. Madde 25 DL-4'e aittir (K-11). Madde 41 DL-1'in ilk adımıdır.

- [ ] AC-1 (zorunlu) — Uç yalnız `POST /api/internal/record-codes/allocate`'tır; 401 / 403 eşlemesi Bölüm 8.2 tablosuyla birebirdir (başlık eksik/çift, iptal edilmiş kimlik bilgisi, yanlış sır, token yok/geçersiz → 401 `RECORD_CODE_UNAUTHENTICATED`; geçersiz yapılandırma, yanlış hedef kitle ya da tüketici, `X-Tenant-Id`, eksik token iddiaları → 403 `RECORD_CODE_FORBIDDEN`) ve her retle işleyici hiç çağrılmaz; `InternalRecordCodesController` `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesindedir.
  Sabotaj: yürütücüdeki her ret dalı tek tek kaldırılınca `TrustedRecordCodeRequestExecutorRefusalTests`'ten adı konmuş bir test kırmızı; iptal edilmiş kimlik bilgisi 403'e çevrilince eşleme testi kırmızı.
- [ ] AC-2 (zorunlu) — Kiracı yalnız doğrulanmış kullanıcı token'ının `tenant_id`'sidir: gövdede `tenantId` üyesi 400, sorgu dizesindeki kiracı yok sayılır, MDM'e giden `X-Tenant-Id` token kiracısıdır.
  Sabotaj: kiracıyı sorgu dizesinden okuyan bir değişiklik konunca `InternalRecordCodesHttpTests` kiracı testi kırmızı.
- [ ] AC-3 (zorunlu) — `LegalEntity` kapsamında MDM `code = MYG` olan şirket için `PAY` ilk ayırması 200 ve `data = { "code": "MYG-PAY-000001", "sequence": 1 }`, ikincisi `MYG-PAY-000002` / `2`; `prefixScope` gönderilmeyen v0 gövdesi aynı sonucu verir.
  Sabotaj: dolgu 5'e indirilince ya da varsayılan kapsam kaldırılınca biçim / HTTP testi kırmızı.
- [ ] AC-4 (zorunlu) — Gerçek Mongo'da aynı `(kiracı, MYG, PAY)` için farklı anahtarlı 50 eşzamanlı ayırma 50 farklı kod ve 50 farklı sequence üretir; sayaç belgesi yokken başlayan ilk yarış 500 üretmez.
  Sabotaj: `$inc` yerine oku-artır-yaz konunca çift kod; upsert yarışı kurtarması kaldırılınca ilk-yarış testi kırmızı.
- [ ] AC-5 (zorunlu) — Aynı anahtar + aynı gövde aynı kodu ve sequence'i döner, sayacı ilerletmez, MDM'i çağırmaz, ikinci defter ve denetim satırı yazmaz; aynı anahtar + farklı gövde 400 `IDEMPOTENCY_KEY_REUSED`; aynı anahtarla 10 eşzamanlı istek tek defter satırı ve tek kod üretir, hiçbiri 503 almaz ve sayaç en çok 9 (N−1) numara fazla ilerler; geçici yazım çakışması (`WriteConflict`) sahte depoyla zorlandığında işlem yeniden denenir ve 200 döner.
  Sabotaj: parmak izi karşılaştırması kaldırılınca farklı-gövde testi; idempotency unique index'i kaldırılınca eşzamanlı-anahtar testi; yeniden deneme kaldırılınca geçici-çakışma testi kırmızı.
- [ ] AC-6 (zorunlu) — Sayaç `(kiracı, önek, tür)` başınadır ve sıfırlanmaz: `MYG-PAY`, `GPL-PAY`, `MYG-APD` ve B kiracısının `MYG-PAY`'i bağımsız `000001`'den başlar; zorla düşürülen bir ayırmadan sonra bir sonraki kod bir üst numarayı alır (boşluk kalır, hiçbir kod iki kez verilmez); sayaç deposunda azaltan/sıfırlayan üye yok.
  Sabotaj: sayaç anahtarından önek çıkarılınca bağımsızlık testi; başarısızlıkta sayacı geri alan değişiklik konunca boşluk testi kırmızı.
- [ ] AC-7 (zorunlu) — `LastNumber = 999998` ile tohumlanmış sayaçta iki ayırma `MYG-PAY-999999` ve `MYG-PAY-1000000` üretir; sequence `long`.
  Sabotaj: biçim 6 haneye kesilince biçim ve Mongo testi kırmızı.
- [ ] AC-8 (zorunlu) — Uyumsuz kapsam 422 `RECORD_CODE_SCOPE_INVALID` döner, MDM çağrılmaz, sayaç yaratılmaz: `Group` + dolu `legalEntityId`, `LegalEntity` + boş `legalEntityId` ve DL-1'de **tür izni adımını geçmiş** her `prefixScope = Platform` isteği (ör. `PAY` + `Platform` → 422); izinli çağıranı olmayan tür kapsamdan önce reddedilir (ör. `PLN` + `Platform` → 403 `RECORD_CODE_FORBIDDEN`, sözleşme örneğiyle aynı); tanımsız `prefixScope` 400.
  Sabotaj: `Platform` reddi kaldırılınca `PAY` + `Platform` testi; tür izni adımı kapsam adımının arkasına taşınınca `PLN` + `Platform` testi kırmızı.
- [ ] AC-9 (zorunlu) — Tür başına izinli kapsam: `BNK` yalnız `Group`; `BAC` `LegalEntity` (önek sahip şirketin kısa kodu) ve `Group` (grup öneki); `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `AGR` yalnız `LegalEntity`; `ORG`, `POS` yalnız `LegalEntity`; `PLN` yalnız `Platform`.
  Sabotaj: `PAY`'e `Group` izni eklenince katalog tür×kapsam testi kırmızı.
- [ ] AC-10 (zorunlu) — Grup öneki ayarı hizalanmış (`GMG`, etkin `OrgPrefix` = `GMG`) kiracıda `BNK` ilk ayırması `GMG-BNK-000001`, tedarikçi `BAC` ilk ayırması `GMG-BAC-000001`; ayarı olmayan kiracıda 409 `GROUP_PREFIX_MISSING`, sayaç yaratılmaz; servis grup önekini kendiliğinden (kod sabiti, `OrgPrefix` ya da başka varsayılan) doldurmaz.
  Sabotaj: kayıt yokken `OrgPrefix`'e düşen yedek eklenince 409 testi kırmızı.
- [ ] AC-11 (zorunlu) — MDM `code` `^[A-Z]{2,5}$`'e birebir uymuyorsa (`GMG-CH`, `my`, `M`, `ABCDEF`, `MYĞ`, boş, boşluklu) 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, sayaç yaratılmaz; normalleştirme yok; `MYG`, `GPL`, `GMT` kabul.
  Sabotaj: okuyucuya `Trim().ToUpperInvariant()` eklenince okuyucu testi kırmızı.
- [ ] AC-12 (zorunlu) — A kiracısının token'ıyla B kiracısının şirketi istenince (MDM 404) cevap 404 `LEGAL_ENTITY_NOT_FOUND`; olmayan ve aktif olmayan şirket için gövde aynı; A ve B kiracılarının sayaç belgeleri ve defter satırları doğrudan okunarak hiçbir satırın oluşmadığı gösterilir; MDM'e giden istekte `X-Tenant-Id` = A ve `Authorization` = iletilen token.
  Sabotaj: MDM isteğine şirketin kiracısını gönderen değişiklik konunca kiracılar arası test kırmızı.
- [ ] AC-13 (zorunlu) — MDM kapalı, 5xx, 3 s aşımı, 400/401/403, çelişkili gövde → 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`; Mongo gerçekten erişilemez → 503 `RECORD_CODE_UNAVAILABLE`; hiçbir durumda kod dönmez, MDM hatasında sayaç ilerlemez.
  Sabotaj: 5xx'i 404'e çeviren değişiklik konunca okuyucu testi kırmızı.
- [ ] AC-14 (zorunlu) — Katalog dışı tür (`XYZ`, `CTR`, `ACC`, `CMP`, `GP`, `UID`, `IT`, `TTY`, `PUT`) 422 `RECORD_TYPE_UNKNOWN`; biçimsiz tür (`pay`, `PA`, `PAYX`) 400; katalogda olan ama kimlik bilgisinin izinli kümesinde olmayan tür (Mdm ile `PAY`, FinanceTreasury ile `BNK`, herkes için `ORG`, `POS`, `PLN`) 403 `RECORD_CODE_FORBIDDEN`.
  Sabotaj: kimlik bilgisi × tür denetimi kaldırılınca 403 testi kırmızı.
- [ ] AC-15 (zorunlu) — DL-1'de izinli her (tür, kapsam) için üretilen her kod yalnız ASCII `A–Z`, `0–9`, `-` içerir ve `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$` kalıbına uyar.
  Sabotaj: önek doğrulaması gevşetilip `Ğ` kabul edilince özellik testi kırmızı.
- [ ] AC-16 (zorunlu) — `RecordCodeTypeCatalog` RCS-001 §3'ün bütün OTOMATİK satırlarını (`ORG`, `POS`, `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `BNK`, `BAC`; `CTR` yerine `AGR`) ve §5.3'teki `PLN`'yi birebir içerir; test kuralı `.antigravity/rules/record-coding-standard.md`'den okur; `CTR` → `AGR` adı konmuş tek istisnadır ve §3 güncellenince testten çıkarılmazsa test kırmızıdır; KALSIN türü katalogda yok.
  Sabotaj: kataloğa §3'te olmayan tür eklenince ya da bir OTOMATİK tür çıkarılınca katalog testi kırmızı.
- [ ] AC-17 (zorunlu) — Her yeni ayırma, defter satırıyla **aynı Mongo işleminde** tek denetim kutusu satırı yazar; bu satırın `IdempotencyKey`'i `record-code:allocated:{allocationId:N}`'dir (istemci anahtarı değildir); işleyiciden sonra `audit_events`'te tek satır: `ActorType = TenantUser`, `ActorId` = iletilen token'ın `sub`'ı, `SourceService` = kimlik bilgisinin kapalı listedeki adı (ör. `Diten.FinanceTreasuryService`), `TenantId` = token kiracısı, kategori `PlatformConfiguration`, varlık `RecordCodeAllocation` + defter kimliği, işlem `Create`, sonuç `Succeeded`, metadata `recordType`, `prefixScope`, `legalEntityId`, `prefix`, `code`, `sequence`, `idempotencyKey`; tekrar yeni satır yazmaz; kutu yazımı bağlantı hatasıyla düşerse işlem iptal, 503, defter satırı yok; kutu `false` dönerse (aynı ayırmanın satırı zaten var) defter `allocationId` ile okunur ve 200 aynı kod döner. Aktör ölçümü madde 41'deki ilk adım ölçümüne dayanır.
  Sabotaj: kutu yazımı commit sonrasına taşınınca hata yolu testi; aktör sabit `Service`/`System` yapılınca aktör testi; `false` 503'e çevrilince tekrar-yazım testi kırmızı.
- [ ] AC-18 (zorunlu) — Merkezi denetim BL-501 ile değil Platform içi işlem içi kapıyla yapılır: işleyici `RecordCodeAuditIntent.EnqueueAsync`'i çağırır ve o her zaman `Intent` taşır (elle yük yok; `TransactionOwnedAuditPayloadExemptions` listesi değişmez); CT'nin defter kararıyla `AuditTrailStandardTests` komutu denetlenen sayar; Platform'a iletici kopyası eklenmez; tüketicinin Create kaydıyla eşleşme `idempotencyKey` + `code` üzerinden.
  Sabotaj: işleyiciden yazıcı çağrısı kaldırılınca mimari test komutu "yeni ve denetimsiz" sayar; muafiyet listesine satır eklenince mevcut muafiyet testi kırmızı.
- [ ] AC-19 (zorunlu) — Ret kaydı Bölüm 21.2 tablosuna birebir uyar: "yazılır" denen her ret ayrı kısa bir işlemle tek denetim satırı yazar (`Outcome` tablodaki `Denied` ya da `Failed`, `reason_code` metadata'da, aktör kullanıcı, kaynak servis kapalı listeden, kutu anahtarı her ret için yeni Guid); "yazılmaz" denenler (401, kiracı çözülmeden verilen 403, 400 biçim, 413, 429) yalnız loglanır; hiçbir ret defter, sayaç ya da grup öneki satırı yazmaz ve kod döndürmez.
  Sabotaj: 404 dalından ret yazımı kaldırılınca ret testi; 401'de ret yazdıran değişiklik konunca "yazılmaz" testi kırmızı.
- [ ] AC-20 (zorunlu) — Defter deposunda güncelleme, değiştirme ya da silme üyesi yok; sayaç deposunda yalnız `NextAsync`; grup öneki deposunda yalnız kilitsizken koşullu hizalama ve kilitleme (silme yok); v1'de kodu değiştiren, iptal eden ya da yeniden veren uç yok.
  Sabotaj: defter arayüzüne `DeleteAsync` eklenince yansıma testi kırmızı.
- [ ] AC-21 (zorunlu) — `ocelot.json`'da `record-codes` ya da `/internal` içeren rota yok; `frontend/Diten.Web` altında `record-codes/allocate` çağrısı yok.
  Sabotaj: test kopyasına rota eklenince Gateway testi kırmızı.
- [ ] AC-22 (zorunlu) — Yalnız Platform aktörü gövdesiz `PUT /api/admin/tenants/{tenantId}/record-code-group-prefix` ile kiracının grup öneki ayarını **etkin belge `OrgPrefix`'ine hizalar**; gövde gönderilirse 400; kilitsizken `OrgPrefix` değiştirilip yeniden hizalanınca ayar yeni değere geçer ve önceki/sonraki değer yol a ile denetlenir; ilk `Group` ayırması aynı işlemde `LockedAtUtc`'yi yazar; kilitten sonra farklı `OrgPrefix`'le hizalama 409 `GROUP_PREFIX_LOCKED` ve ayar değişmez, aynı değerle 200 ve değişiklik yok; olmayan kiracı 404; kiracı kullanıcısı 403; eşzamanlı hizalama + ilk ayırma sessiz üzerine yazma üretmez.
  Sabotaj: kilit denetimi kaldırılınca kilitli-hizalama testi; kilidin ayırma işleminin dışına taşınması eşzamanlılık testinde kırmızı.
- [ ] AC-23 (zorunlu) — İstek ve cevap biçimi `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`'deki örneklerle ve hata tablosuyla birebir test edilir: örnek gövdeler kabul, bilinmeyen üye 400, her hata durumunda `statusCode` ve `reason_code` tablodakiyle aynı (401/403 satırları Bölüm 8.2 eşlemesiyle), cevap `data`'sında yalnız `code` ve `sequence`.
  Sabotaj: cevapta `code` → `recordCode` yapılınca sözleşme testi kırmızı.
- [ ] AC-24 (zorunlu) — Üç koleksiyon `SchemaProfile.RecordCodes` ile kayıtlı, `TenantScopedEntity`, Bölüm 4'teki adlı index'ler (kiracı önce, unique olanlar unique) uygulanır; `PlatformSchemaManifestTests` yeşil; mevcut profiller değişmez.
  Sabotaj: `ux_record_code_allocations_tenant_code` unique bayrağı kaldırılınca şema testi ve eşzamanlı ayırmanın kod tekilliği testi kırmızı.
- [ ] AC-25 (zorunlu) — **(DL-4, K-11)** `PLN` bağlanmasıyla Platform kapsamı açıldığında: yalnız izinli kapsamı `Platform` olan tür için, kiracısız sayaç/defter ve platform-global denetim kaydıyla öneksiz `PLN-000001` biçiminde kod verilir; kiracı kapsamlı okumalar kiracısız satırı görmez. DL-1'de bu madde uygulanmaz ve DL-1 kabulünde ölçülmez.
  Sabotaj: (DL-4) kiracı kapsamlı sorguya kiracısız satırı katan filtre konunca kiracı testi kırmızı.
- [ ] AC-26 (zorunlu) — MDM 3 s'de cevap vermezse 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` bütçe içinde döner; istek 5 s'yi aşmaz; günlüklerde sır, `Authorization`, JWT yok.
  Sabotaj: MDM bütçesi kaldırılınca geciktirilmiş sahte MDM'le zaman testi kırmızı.
- [ ] AC-27 (zorunlu) — `dotnet build` Diten.Platform PASS; Platform'un **tam** test takımı (filtre yok), mevcut bütün denetim testleri ve `DependencyInjectionSmokeTests` **değiştirilmeden** ve `tests/architecture/TenantArchitecture.ArchitectureTests` yeşil; atlanan test yok; main'deki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır.
  Sabotaj: yeni bir testin `Skip` ile işaretlenmesi kabul kaydında kırmızı.
- [ ] AC-28 (zorunlu) — Yazıcı genişletmesi (K-9): `TransactionOwnedAuditIntent.SourceService` boşsa kayıttaki `SourceService` `"Diten.Platform"`'dur (mevcut `TransactionOwnedAuditDoorTests` ve `TransactionOwnedAuditDeliveryMongoTests` değişmeden yeşil), doluysa o değerdir; ayırma kaydında değer yalnız kimlik bilgisinin kapalı listedeki adıdır — gövdede `sourceService` üyesi 400, `X-Source-Service` gibi bir başlık okunmaz, kayıt yine kapalı listedeki adı taşır.
  Sabotaj: kaynak servisi gövdeden ya da başlıktan alan değişiklik konunca `TransactionOwnedAuditSourceServiceTests` / HTTP testi kırmızı; varsayılan `"Diten.Platform"` kaldırılınca mevcut kapı testleri kırmızı.
- [ ] AC-29 (zorunlu) — Ret yolu (K-10): `ITransactionalAuditRefusalWriter.TryEnqueueRefusalAsync` yalnız `Denied` ve `Failed` kabul eder (`Succeeded` → geçersiz niyet istisnası), ayırma işleminden **ayrı** bir oturumla yazar, aktörü, kaynak servis kuralı ve doğrulamaları başarı yoluyla aynıdır; yazıcı Application DI'da ayrı `AddScoped` kaydıyla çözülür; ret işleminde defter, sayaç ve grup öneki koleksiyonlarına yazım sıfırdır ve cevapta `data = null`'dır.
  Sabotaj: ret yolu `Succeeded` kabul edince yazıcı testi; ret işleminde defter satırı yazdıran değişiklik konunca Mongo ret testi; ret yazıcısı kaydı kaldırılınca DI testi kırmızı.
- [ ] AC-30 (zorunlu) — Kiracı yüklemi: üç depoda bütün okuma ve yazmalar açık `tenantId` parametresiyle **tam eşitlik** filtresi kullanır; `HybridRepository` ve "`TenantId` null veya kiracı" filtresi kullanılmaz; depo yalnız şu durumlarda istisnaya düşer ve hiçbir satır yazmaz: `tenantId = Guid.Empty`, `ITenantContext.IsPlatformContext = true` (`SetPlatformContext` ile), `tenantId ≠ ITenantContext.TenantId`; platform sentinel kimliği `00000000-0000-0000-0000-000000000001` **reddedilmez**.
  Sabotaj: herhangi bir depoya `HybridRepository` tabanı, `Guid.Empty` kabulü ya da platform bağlamı kabulü konunca `RecordCodeTenantIsolationMongoTests` kırmızı; sentinel kimliğini reddeden bir kural eklenince sentinel kiracı testi kırmızı.
- [ ] AC-31 (zorunlu) — Kiracı yüklemleri **ayrı ayrı** sabotajlanır; iki kiracının ikisi de `GMG` grup önekiyle aynı kod metnini üretebileceği için testler kodu değil **B kiracısının defter satırını ve sayaç belgesini** doğrular: (a) sayaç filtresinden `TenantId` çıkarılınca A ve B'nin `(GMG, BNK)` sayacı tek belgeye düşer ve B'nin ayrı sayaç belgesi testi kırmızı; (b) idempotency aramasından `TenantId` çıkarılınca B'nin aynı anahtarlı isteği A'nın satırını döndürür ve "B'de defter satırı var, `TenantId = B`" testi kırmızı; (c) grup öneki okumasından kiracı parametresi çıkarılınca öneki olmayan B kiracısı A'nın önekini alır ve B'nin 409 testi kırmızı; (d) defter yazımında kiracı token yerine bağlamdan alınıp bağlam değiştirildiğinde satır `TenantId`'si testi kırmızı; (e) `X-Tenant-Id` reddi tek başına kaldırılınca 403 testi kırmızı.
  Sabotaj: beş sabotaj tek tek uygulanır, her biri kendi adlı testini kırmızıya döndürür.
- [ ] AC-32 (zorunlu) — Kullanıcı izni: her tür için Bölüm 4.4'teki izin token'da yoksa 403 `RECORD_CODE_PERMISSION_MISSING` döner, kod verilmez, tekrar da reddedilir; izin varsa sürer; kimlik bilgisinin izinli tür kümesi dışındaki tür 403 `RECORD_CODE_FORBIDDEN`.
  Sabotaj: izin denetimi kaldırılınca izinsiz-kullanıcı testi kırmızı.
- [ ] AC-33 (zorunlu) — Hız sınırı: sınırlayıcı yürütücü içinde, kimlik bilgisi ve token doğrulandıktan sonra (tüketici servis, kiracı) bölümüyle uygulanır; testte yapılandırılan düşük sınır A kiracısında aşılınca 429 `RECORD_CODE_RATE_LIMITED` (`Response<T>` zarfı) döner, işleyici çağrılmaz, sayaç ilerlemez, denetime yazılmaz; aynı anda B kiracısının istekleri sürer; `PeopleSearchRateLimit.cs` değişmez ve oradaki `RATE_LIMITED` kodu bu uçta görünmez; üretim değeri A-16 ile belirlenir.
  Sabotaj: sınırlayıcı kaldırılınca 429 testi; bölüm anahtarından kiracı çıkarılınca B kiracısı testi kırmızı.
- [ ] AC-34 (zorunlu) — Döndürme ve iptal: `PreviousSecret` `PreviousValidUntilUtc`'ye kadar kabul edilir, sonra 401; `IsRevoked = true` anında 401; sır karşılaştırması sabit zamanlıdır (FU21 `:117` deseni).
  Sabotaj: süre denetimi kaldırılınca süresi geçmiş-eski-sır testi kırmızı.
- [ ] AC-35 (zorunlu) — Grup öneki tek kaynağı: `Group` ayırmasında kiracı ayarı etkin belge `OrgPrefix`'inden farklıysa (ör. kilitten sonra yapılandırma değişmiş) 409 `GROUP_PREFIX_MISMATCH`, kod verilmez, sayaç ilerlemez, hata logu yazılır; `OrgPrefix` kilitli değere geri alınınca ayırma yeniden çalışır; hizalama ucunda etkin `OrgPrefix` kalıba uymuyorsa 409 `GROUP_PREFIX_SOURCE_INVALID`.
  Sabotaj: ayırmadaki eşitlik denetimi kaldırılınca uyuşmazlık testi kırmızı.
- [ ] AC-36 (zorunlu) — Tüzel kişilik uygunluğu (v1.1 öncesi): sahte MDM yanıtında `approvalStatus` yoksa ya da `APPROVED` ise ayırma sürer; `DRAFT`, `SUBMITTED`, `REJECTED` → 422 `LEGAL_ENTITY_NOT_ELIGIBLE`; boş, `null` ya da tanımsız değer → 503; aktif olmayan şirket her durumda 404. "Alan yok → 503" geçişi MDM v1.1 yayınıyla FU-RCS-15'te yapılır ve bu maddeyi o revizyon günceller.
  Sabotaj: `approvalStatus` denetimi kaldırılınca `REJECTED` testi kırmızı.
- [ ] AC-37 (zorunlu) — Askıdaki kiracı: `GetTenantStatusQuery` kiracıyı `Active` görmüyorsa (Suspended, Provisioning, Deactivated, yok) cevap 403 `RECORD_CODE_TENANT_NOT_ACTIVE`'dır, idempotent tekrar da reddedilir, sayaç ilerlemez, `Denied` ret kaydı yazılır; aktif kiracı sürer.
  Sabotaj: kiracı durumu denetimi kaldırılınca askıdaki-kiracı testi kırmızı.
- [ ] AC-38 (zorunlu) — Varsayılan / tohum kiracı: `00000000-0000-0000-0000-000000000001` kiracısının `tenant_user` token'ıyla (kiracı Active) `PAY` ayırması 200 döner; sayaç ve defter satırı `TenantId = …0001` taşır ve denetim kaydı yazılır.
  Sabotaj: depoya ya da yürütücüye sentinel kimliğini reddeden kural eklenince bu test kırmızı.
- [ ] AC-39 (zorunlu) — Denetim kutusu anahtarı istemci anahtarından türemez: (a) A ve B kiracıları aynı istemci `idempotencyKey`'iyle ayırma yapınca ikisi de 200 alır ve iki ayrı kutu satırı yazılır; (b) aynı istemci anahtarıyla önce 409 alan (ret kaydı yazılmış) istek, sorun giderilince aynı anahtarla tekrarlandığında 200 alır ve başarı kutu satırı yazılır; (c) aynı `allocationId` için kutu `false` dönerse cevap 200 ve aynı koddur.
  Sabotaj: kutu anahtarı istemci anahtarından türetilince (a) ve (b) 503 alır ve testler kırmızı.
- [ ] AC-40 (zorunlu) — Kaynak servis değer denetimi: kapı `Intent.SourceService` dolu ve `TransactionOwnedAuditSourceServices` kümesinde değilse (`Diten.Unknown`, `diten.mdmservice`, baş/son boşluklu ad) `TransactionOwnedAuditIntentInvalidException` atar ve kutuya hiçbir şey yazmaz; küme `Diten.Platform` ve kimlik bilgisi eşlemesindeki adları içerir, eşlemedeki her ad kümededir (test); `TransactionOwnedAuditIntent` sınıf yorumu yeni kuralı söyler.
  Sabotaj: küme denetimi kaldırılınca küme-dışı-ad testi; eşlemeye kümede olmayan bir ad eklenince eşleme testi kırmızı.
- [ ] AC-41 (zorunlu) — **DL-1'in ilk adımı (diğer bütün maddelerden önce):** gerçek HTTP ana makinesinde (`SignedTenantTokenHttpHost` deseni) üretimdeki `CanonicalTransactionalAuditOutboxWriter` ve gerçek Mongo ile, iç uçtan gelen bir ayırmanın kutu satırında `ActorType = TenantUser` ve `ActorId` = iletilen token'ın `sub`'ı olduğu ölçülür (yürütücü kullanıcı token'ını açık `AuthenticateAsync` ile doğrular ve `HttpContext.User`'ı işleyici çözülmeden önce ayarlar). Ölçüm başarısızsa DL-1 durur ve paket revizyonuna döner.
  Sabotaj: `HttpContext.User` ayarı kaldırılınca kapı aktörü çözemez (ret ya da yanlış aktör) ve ölçüm testi kırmızı.
- [ ] AC-42 (zorunlu) — Kısa kod kullanım kilidi (ara dönem): X şirketi `MYG` önekiyle ayırma yaptıktan sonra MDM kodu `MYX`'e değişirse X için yeni ayırma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; `MYG` daha sonra Y şirketine verilirse Y için ayırma 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; ikisinde kod verilmez, sayaç ilerlemez, `Failed` ret kaydı ve hata logu yazılır; X'in kodu `MYG`'ye geri alınınca ayırma yeniden çalışır; Group kapsamlı satırlar bu denetime girmez.
  Sabotaj: defter denetimi kaldırılınca iki senaryo testi de kırmızı.

**DL-2…DL-10.** Kabul ölçütleri her dilimin kendi paket revizyonunda yeni numarayla eklenir (numaralar silinmez,
yeniden numaralanmaz).

## 17. Test Expectations

| Test sınıfı | Tür | Kapsadığı kabul ölçütleri |
|---|---|---|
| `RecordCodeActorProbeHttpTests` | gerçek HTTP + üretim yazıcısı + gerçek Mongo — **DL-1'in ilk adımı** | AC-41 |
| `RecordCodeFormatTests` | birim | AC-3, AC-7, AC-15 |
| `RecordCodeTypeCatalogTests` | birim (RCS-001 §3'ü `RepoPaths` ile okur) | AC-9, AC-14, AC-16 |
| `AllocateRecordCodeHandlerTests` | birim (sahte depo/okuyucu) | AC-5 (parmak izi, geçici çakışma), AC-8, AC-10, AC-13, AC-19, AC-35, AC-42 |
| `RecordCodeAllocationMongoTests` | gerçek Mongo replica set | AC-4, AC-5 (eşzamanlı), AC-6, AC-7, AC-20, AC-22 (kilit), AC-24, AC-42 |
| `RecordCodeTenantIsolationMongoTests` | gerçek Mongo, iki kiracı + sentinel kiracı | AC-12, AC-30, AC-31, AC-38 |
| `RecordCodeAuditDeliveryMongoTests` | gerçek Mongo + üretimdeki kutu işleyicisi | AC-17, AC-18, AC-19, AC-29, AC-39 |
| `InternalRecordCodesHttpTests` | gerçek HTTP (`SignedTenantTokenHttpHost` deseni) + sahte MDM | AC-1, AC-2, AC-3, AC-8, AC-12, AC-13, AC-23, AC-26, AC-28, AC-32, AC-37, AC-38 |
| `TrustedRecordCodeRequestExecutorRefusalTests` | birim, her ret dalı | AC-1, AC-34 |
| `RecordCodeAllocateRateLimiterTests` | birim + HTTP | AC-33 |
| `MdmLegalEntityShortCodeReaderTests` | birim, sahte HTTP | AC-11, AC-13, AC-26, AC-36 |
| `RecordCodeGatewayExposureTests` | dosya ölçümü | AC-21 |
| `TenantRecordCodeGroupPrefixHttpTests` | gerçek HTTP | AC-22, AC-35 |
| `TransactionOwnedAuditSourceServiceTests` | birim + Mongo | AC-28, AC-40 |
| `TransactionalAuditRefusalWriterTests` | birim + Mongo + DI | AC-29 |
| `LoginOnlyEndpointGuardTests` (liste), `DependencyInjectionSmokeTests` (değişmeden) | mimari / DI | AC-1, AC-27, AC-29 |
| `AuditTrailStandardTests` (CT sabiti) | mimari | AC-18 |
| Tam takım + mimari testler | CI | AC-27 |

**Sabotaj listesi (kabul kaydına koşu kanıtıyla; her satır tek tek uygulanıp geri alınır):**

| # | Sabotaj | Kırmızıya dönmesi gereken |
|---|---|---|
| S-1 | Kimlik bilgisi reddi kaldırılır | ret testleri (adlı) |
| S-2 | Kullanıcı token'ı reddi kaldırılır | aynı |
| S-3 | İptal edilmiş kimlik bilgisi 403'e çevrilir | 401/403 eşleme testi |
| S-4 | `$inc` yerine oku-artır-yaz | eşzamanlı ayırma testi |
| S-5 | Upsert yarışı kurtarması kaldırılır | ilk-yarış testi |
| S-6 | Parmak izi karşılaştırması kaldırılır | farklı-gövde testi |
| S-7 | İdempotency unique index'i kaldırılır | eşzamanlı-anahtar testi |
| S-8 | Geçici çakışma yeniden denemesi kaldırılır | geçici-çakışma testi |
| S-9 | Sayaç anahtarından önek çıkarılır | bağımsızlık testi |
| S-10 | Biçim 6 haneye kesilir | 999999 testi |
| S-11 | `PAY`'e `Group` izni | katalog testi |
| S-12 | Grup öneki yokken `OrgPrefix`'e düşme | 409 testi |
| S-13 | Kısa koda `Trim().ToUpperInvariant()` | okuyucu testi |
| S-14 | MDM 5xx → 404 | okuyucu 503 testi |
| S-15 | Kimlik bilgisi × tür denetimi kaldırılır | 403 testi |
| S-16 | Kataloğa §3 dışı tür | katalog sabitleme testi |
| S-17 | Kutu yazımı commit sonrasına | denetim hata yolu testi |
| S-18 | Aktör sabit `Service` yapılır | aktör testi |
| S-19 | İşleyiciden `RecordCodeAuditIntent.EnqueueAsync` kaldırılır | `AuditTrailStandardTests` |
| S-20 | 404'te ret yazımı kaldırılır / 401'de ret yazılır | ret tablosu testleri |
| S-21 | Defter arayüzüne `DeleteAsync` | yansıma testi |
| S-22 | Gateway rotası (test kopyası) | Gateway testi |
| S-23 | Grup öneki kilit denetimi kaldırılır | kilitli-hizalama testi |
| S-24 | Cevap alanı yeniden adlandırılır | sözleşme testi |
| S-25 | `(TenantId, Code)` unique kaldırılır | şema testi |
| S-26 | MDM bütçesi kaldırılır | zaman testi |
| S-27 | Kaynak servis gövdeden / başlıktan alınır | kaynak servis testi |
| S-28 | Yazıcıdaki `"Diten.Platform"` varsayılanı kaldırılır | `TransactionOwnedAuditDoorTests.cs:55`, `TransactionOwnedAuditDeliveryMongoTests.cs:73` |
| S-29 | Ret yolu `Succeeded` kabul eder / defter yazar | ret yazıcı testleri |
| S-30 | Depoya `HybridRepository`, `Guid.Empty` kabulü ya da platform bağlamı kabulü; ya da sentinel kimliğinin reddi | kiracı izolasyon testi / sentinel kiracı testi |
| S-31 | Sayaç filtresinden `TenantId` çıkarılır | AC-31 (a) |
| S-32 | İdempotency aramasından `TenantId` çıkarılır | AC-31 (b) |
| S-33 | Grup öneki okumasından kiracı parametresi çıkarılır | AC-31 (c) |
| S-34 | Defter yazımında kiracı bağlamdan alınır | AC-31 (d) |
| S-35 | `X-Tenant-Id` reddi tek başına kaldırılır | AC-31 (e) |
| S-36 | Kullanıcı izni denetimi kaldırılır | izinsiz-kullanıcı testi |
| S-37 | Hız sınırlayıcı kaldırılır / bölümden kiracı çıkarılır | 429 testi / B kiracısı testi |
| S-38 | Eski sır süre denetimi kaldırılır | döndürme testi |
| S-39 | Ayırmada `OrgPrefix` eşitliği kaldırılır | uyuşmazlık testi |
| S-40 | `approvalStatus` denetimi kaldırılır | onaysız şirket testi |
| S-41 | Kiracı durumu denetimi kaldırılır | askıdaki kiracı testi |
| S-42 | Kutu anahtarı istemci anahtarından türetilir | AC-39 (a), (b) |
| S-43 | Kutu `false` → 503 | AC-39 (c) / tekrar-yazım testi |
| S-44 | Kaynak servis küme denetimi kaldırılır | küme-dışı-ad testi |
| S-45 | Ret yazıcısının Application DI kaydı kaldırılır | DI testi |
| S-46 | `HttpContext.User` ayarı kaldırılır | aktör ölçüm testi (AC-41) |
| S-47 | Kısa kod kullanım denetimi kaldırılır | AC-42 iki senaryo |
| S-48 | Tür izni adımı kapsam adımının arkasına taşınır | `PLN` + `Platform` 403 testi |

Diğer: kiracı izolasyonu gerçek HTTP + gerçek Mongo ile; soft delete yok; tarayıcı smoke yok; RESX yok; DataTable
doğrulayıcısı uygulanmaz; Mongo testleri Platform test sabitleriyle kendi replica set'ini kullanır.

## 18. Ready-for-dev Checklist

- [x] Golden Reference `none`; arka uç düzeni Golden Reference'a birebir (Bölüm 10).
- [x] Künye alanları dolu; `approval_status: pending`; `code_start` yok; `revision: r3`.
- [x] Layout & Shell: `shell: none` (Bölüm 9). Validation (Bölüm 12), Failure Path (Bölüm 13), Authorization (Bölüm 14),
  Gateway (Bölüm 15), Lookup kararı (Bölüm 7.1), Audited Events (Bölüm 21) yazılı.
- [x] Kimlik ön kontrolü `OK`, çıkış 0 (Bölüm 0).
- [x] `pack_approval.slices()` DL listesi döndürür — `PYTHONUTF8=1 python3 -P -c "import sys; sys.path.insert(0,'scripts'); import pack_approval as p; t=open('execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md',encoding='utf-8').read(); print(p.slices(t)); print(p.kunye(t)['status'])"` → `['DL-1', … 'DL-10']` ve `draft` (r3, bu commit).
- [x] İnceleme 01 bulguları (Bölüm 19.1) ve inceleme 02 bulguları F-1…F-11 (Bölüm 19.2) kapatıldı.
- [ ] Açık kararlar A-1 (2026-11-02), A-17 (2026-11-02), A-11, A-16 (2026-11-03), A-18 (2026-11-06) kapandı ve işlendi.
- [ ] Sözleşme 2026-11-03'te CT adımıyla donduruldu.
- [ ] A-10 CT defter kararı — DL-1 **main'e girmeden önce**.
- [ ] A-12 kayıt satırı, `:353` düzeltmesi ve domain-config satırı CT tarafından yazıldı.
- [ ] CT-APR-02 doğrulama incelemesi (inceleme 03) bu sürüm için; açık ENGEL yok.
- [ ] CT-APR-01 onay satırı (yüksek risk: iç uç, kiracı, şema, kimlik bilgisi, denetim altyapısı; paketi yazmamış `high: evet` onaylayıcı) main'de; künye `ready-for-dev`.

## 19. Implementation Notes

**Örnek alınan çalışan kod (yalnız okundu, `6d49f5823`).**

- Doküman kimlik üreticisi (K-1): `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/Services/DocumentIdentifierAllocationService.cs`
  — sınıf `:19`; yoklama döngüsü `:317-335` (100 deneme `:28`); biçim `:337-343`; elle rezervasyon `:102-153`; iptal = durum
  değişikliği `:157-213`. Sayaç deposu `…/DocumentManagementIdentifierRepositories.cs:52-90` (**E11000 kurtarması yok**);
  silinmişleri de gören yoklama `:22-30`; varlık `DocumentIdentifierSequenceCounter.cs:12-21`; unique index
  `PlatformSchemaManifest.DocumentManagementFollowUps.cs:282-295`; `DocumentCodingOptions` `DocumentManagementIdentifiersModels.cs:41-50`;
  tür kodu çözücüsü `:65` (`QualityTechnicalAgreementSdea → "AGR"`).
- MDM `CodeReservation` deseni: `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs:37-110`
  (önce idempotency, sayaç, ekle, E11000'de kazananı oku), `:549-600` (upsert yarışı kurtarması); varlık `CodeReservation.cs:5-27`.
- Denetim kapısı: `CanonicalTransactionalAuditOutboxWriter.cs` — sabit kaynak `:39`, intent zorunlu `:81-83`, kategori `:84-87`,
  boş hedef kiracı `:89-92`, aktör `:156-185` (principal → `AuditActorTypeResolver`; kullanıcı kimliği yoksa ret), sonuç sabit
  `Succeeded` `:131`, kaynak `:138`; `TransactionOwnedAuditIntent.cs:11-33` (`SystemActor` `:32`); `AuditOutboxWriteRequest.Validate`
  boş `TenantId` reddi `:28-30`; muafiyet listesi `TransactionOwnedAuditPayloadExemptions.cs:9-40` (yalnız küçülür, 2026-11-13).
  Principal kaynağı `JwtTenantAuthorizationContext.cs:24` (`HttpContext.User`, kurucuda).
- `HybridRepository` filtresi `Repositories.cs:96-109` (kullanılmaz); `TenantRepository` `:70-92`.
- FU21 kimlik ve 401/403: Bölüm 8.2 tablosu; sonuç sabitleri `ITrustedLegalEntityScopeCredentialAuthenticator.cs:10-11`,
  `ITrustedLegalEntityScopeJwtContext.cs:17-21`.
- Kiracı durumu: `GetTenantStatusQueryHandler.cs:18-29`. Hız sınırı emsali `PeopleSearchRateLimit.cs:17-57` (`OnRejected` tek kanca, politika dışı ret `RATE_LIMITED` `:23`, `:38-46`) — **kullanılmaz**, yürütücü içi sınırlayıcı (r3 F-5). Denetim kutusu anahtarı emsali `…/Entitlements/Handlers/CommandHandlers/PhysicalEntitlementAuditIntent.cs:29`; kutu tekilliği `PlatformSchemaManifest.AccessGovernance.cs:118-120`, `AuditOutboxRepository.cs:159-161`. Sentinel kimliği `SystemTenantRules.cs:12`. Ret yazıcısı DI: Application `DependencyInjection.cs:329-330`, `DependencyInjectionSmokeTests.cs:281-282`.
- MOD-0220 yanıtı `LegalEntityModels.cs:100-106` (`approvalStatus` yok).
- MOD-0178 tüketici beklentisi: §8.7 (`:815-822`), §22 (`:1956-1974`) — WMS kendi 422 `WMS_COMPANY_CODE_INVALID`'ini döner
  ve bu servisin 409'unu kendi koduna eşler (FU-0178-RCS-01); geçici sayaçtan geçişte merkezi sayaç WMS'in en büyük
  numarasının üstünden başlamalı (§5.11) → DL-6.
- **Önek çakışması (bilgi):** bir şirketin kısa kodu grup önekiyle aynıysa `(kiracı, GMG, BAC)` sayacı ortaktır; kod
  çakışmaz, sahip bilgisi kaydın `LegalEntityId`'sindedir (§5.2).
- **AUD-001 §3 ile K-8r:** kuralın 3. satırı servis çağrısında `ActorType = System` + `SourceService` der
  (`audit-trail-standard.md:103`); bu uçta işlemi bir kişi başlattığı için satır 1 (kim = kullanıcı) ve satır 3'ün
  `SourceService`'i birlikte uygulanır — çelişki yok (r1'in A-15'i kapandı).

### 19.1 Bağımsız inceleme 01 (UYGUN DEĞİL) — kapanış tablosu

Bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-10-30-219f30c9.md`, özet `277fd253…` (`@` öneki önceki bulgu
dosyasının özetidir — POLICY-REVIEW-START.md; r2'de yanlışlıkla pack blob'u `219f30c9` yazılmıştı, r3'te düzeltildi).
İnceleme 02'nin değerlendirmesi: 1, 2, 3, 4, 6, 7, 10, 12 kapandı; 5, 8, 9 kısmen (kalanları inceleme 02'nin 10, 7, 8'i);
11 pack tarafında kapandı, kayıt satırı CT adımı (A-12).

| Bulgu | Seviye | Kapanış | Nerede |
|---|---|---|---|
| F-1@277fd253 | ENGEL | Aktör kullanıcı (K-8r), kaynak servis kapalı listeden (K-9), ret kaydı ayrı işlemle (K-10), Platform kapsamı DL-4 (K-11); yazıcı yalnız eklemeli genişler | Bölüm 0.1, 5, 8.2, 8.7, 8.11, 21; AC-17, AC-18, AC-19, AC-25 (DL-4), AC-28, AC-29; sözleşme §11 |
| F-2@277fd253 | ORTA | `HybridEntity` / `HybridRepository` kaldırıldı; `TenantScopedEntity`; açık `tenantId` + tam eşitlik (r3'te sentinel reddi kaldırıldı — inceleme 02 F-1) | Bölüm 4, 8.7; AC-30 |
| F-3@277fd253 | ORTA | Beş kiracı yüklemi ayrı sabotaj; B'nin defter satırı ve sayaç belgesi doğrulanır | Bölüm 3, 4; AC-12, AC-31 |
| F-4@277fd253 | ORTA | Sızma senaryosu; tüketici başına izinli türler; kullanıcı izni; hız sınırı; döndürme/iptal | Bölüm 4.4, 8.2, 20; AC-14, AC-32, AC-33, AC-34 |
| F-5@277fd253 | ORTA | `Response<T>` zarfı; v0 biçimi **öneri olarak** bağlayıcı değil (r3 — inceleme 02 F-10) | Bölüm 24 (A-17); sözleşme başlığı, §3, §9 |
| F-6@277fd253 | DÜŞÜK | Geçici çakışmada yeniden deneme / kazananı okuma; N−1 boşluk | Bölüm 8.6, 13; AC-5 |
| F-7@277fd253 | DÜŞÜK | 401/403 FU21 ile birebir | Bölüm 8.2; AC-1, AC-23 |
| F-8@277fd253 | DÜŞÜK | Grup öneki kilidi (r3: tek kaynak `OrgPrefix` — inceleme 02 F-7) | Bölüm 4.3, 8.12; AC-22, AC-35 |
| F-9@277fd253 | DÜŞÜK | Aktif + onaylı (r3: v1.1 bağımlılığı — inceleme 02 F-8) | Bölüm 7, 7.1, 8.3; AC-36 |
| F-10@277fd253 | DÜŞÜK | `BAC` kapsam seçimi tüketicide | Bölüm 7.1 (TC-4, TC-5) |
| F-11@277fd253 | DÜŞÜK | `module-id-registry.md:353` A-12'de | Bölüm 0, 24 (A-12) |
| F-12@277fd253 | DÜŞÜK | Askıdaki kiracı DL-1'de | Bölüm 8.2, 8.5; AC-37 |

### 19.2 Bağımsız inceleme 02 (KOŞULLU) — kapanış tablosu

Bulgu dosyası `docs/records/reviews/MOD-0040-FU02/2026-10-30-b18184f0.md` @ `bbd30dd87`, özet `46aeb4a5…`
(`git show bbd30dd87:<yol> | sha256sum`, 2026-10-30). Hedef r2 pack blob `b18184f0…`. Kapanışlar inceleyenin CT onaylı
önerileridir; sahip kararı gerektirmedi.

| Bulgu | Seviye | Kapanış | Nerede |
|---|---|---|---|
| F-1@46aeb4a5 | ORTA | Sentinel kimliği `…0001` reddedilmez (varsayılan / tohum iş kiracısı); depo yalnız `Guid.Empty` ve platform bağlamını (`IsPlatformContext`) reddeder; sentinel kiracıda normal ayırma | Bölüm 4; AC-30, AC-38; S-30 |
| F-2@46aeb4a5 | ORTA | Kutu anahtarı istemci anahtarı değil: başarıda `record-code:allocated:{allocationId:N}`, retde yeni Guid (emsal `PhysicalEntitlementAuditIntent.cs:29`); `TryInsertAsync` `false` → işlem iptal, defter `allocationId` ile okunur, 200 aynı kod (503 değil) | Bölüm 4.2, 8.6, 13; AC-17, AC-19, AC-39; S-42, S-43; sözleşme §11 |
| F-3@46aeb4a5 | DÜŞÜK | Ret yazıcısı Application DI'da ayrı `AddScoped`; Infrastructure DI ifadesi kaldırıldı; `DependencyInjectionSmokeTests.cs:281-282` değişmeden yeşil | Bölüm 5; AC-27, AC-29; S-45 |
| F-4@46aeb4a5 | DÜŞÜK | `TransactionOwnedAuditSourceServices` kapalı küme; küme dışı → istisna; `TransactionOwnedAuditIntent.cs:6-9` yorumu güncellenir | Bölüm 5, 8.2, 12; AC-40; S-44 |
| F-5@46aeb4a5 | DÜŞÜK | Yürütücü içi `PartitionedRateLimiter`, bölüm (tüketici servis, kiracı); 429 `RECORD_CODE_RATE_LIMITED` yürütücünün ret yolundan; `PeopleSearchRateLimit.cs` ve `OnRejected` kullanılmaz; değer A-16 | Bölüm 3, 5, 8.2, 8.5; AC-33; S-37 |
| F-6@46aeb4a5 | DÜŞÜK | Açık `AuthenticateAsync` + `HttpContext.User` işleyiciden önce; aktör DL-1'in ilk adımında gerçek HTTP + üretim yazıcısıyla ölçülür, başarısızsa DL-1 durur | Bölüm 8.2; AC-17, AC-41; S-46 |
| F-7@46aeb4a5 | DÜŞÜK | Grup önekinin tek kaynağı belge `OrgPrefix`'i; ayar gövdesiz hizalama + kilit; kilitsizken düzeltme = `OrgPrefix` değişikliği; kilitten sonra uyuşmazlık 409 + kurtarma yolu (geri alma; bilinçli değişiklik FU-RCS-16) | Bölüm 3, 4.3, 8.12, 14; AC-22, AC-35 |
| F-8@46aeb4a5 | DÜŞÜK | v1.1 yayınlanana kadar yalnız aktiflik + TC-3 zorunlu; v1.1'de alan yoksa 503 (`v1.1-proposal:69`) — kod değişikliği (FU-RCS-15), MOD-0220 sahibine açık bağımlılık; "kod değişikliği gerekmez" kaldırıldı | Bölüm 7, 7.1, 8.3, 20; AC-36 |
| F-9@46aeb4a5 | DÜŞÜK | "Platform DL-1'de 422 (tür izni geçtiyse)"; `PLN` + `Platform` 403; sözleşme örneğiyle hizalı | Bölüm 0.1 (K-11), 8.5; AC-8; S-48; sözleşme §2, §4, §10 |
| F-10@46aeb4a5 | DÜŞÜK | "v0 yanıt biçimi bağlayıcı değildi" **öneri** olarak; Treasury CT teyidi (A-17, 2026-11-02) | sözleşme başlığı, §3, §9, §12 |
| F-11@46aeb4a5 | DÜŞÜK | Ara dönem azaltıcısı: kullanılmış kısa kod değişirse ya da başka şirkete geçerse 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`; MDM kalıcı kilidi A-18, MDM sahibi (Ali) + Beste, 2026-11-06 | Bölüm 4.2, 7, 8.13, 24; AC-42; S-47 |

## 20. Follow-up Items

| # | İş | Sahip | Dilim |
|---|---|---|---|
| FU-RCS-01 | ORG / POS bağlanması (süreç içi port) | Beste | DL-2 |
| FU-RCS-02 | İKİSİ DE motoru: öneri + gerekçeli değişiklik + AUD-001; §5.9 | Beste | DL-3 |
| FU-RCS-03 | TTY, PPM (PPM sahibi Codex), PLN bağlanması + Platform kapsamı | Beste + PPM sahibi | DL-4 |
| FU-RCS-04 | Kodu kayda bağlama / tüketme, kullanılmayan kod raporu | Beste | DL-3 / DL-4 |
| FU-RCS-05 | Tüzel Kişilik kısa kodu önerisi + §5.4 kalıcı kilidi (MDM) — karar A-18 | MDM sahibi (Ali) + Beste | DL-5 (karar 2026-11-06) |
| FU-RCS-06 | İçe aktarma / yüksek su işareti; WMS geçici sayacından devam | Beste + MOD-0178 sahibi | DL-6 |
| FU-RCS-07 | RCS-001 §3 `CTR` → `AGR` + izinli kapsam sütunu | Ali (`.antigravity`) + Treasury PR'ı | DL-7 |
| FU-RCS-08 | WMS türleri `PUT` / `PCK` / `OUT` / `PKG` + Inventory kimlik bilgisi (+ `Diten.InventoryService` kaynak servis kümesine) | Beste + MOD-0178 sahibi | DL-8 |
| FU-RCS-09 | Arka plan çağıranları için servis kimliği token'ı (Auth, Ali) | Beste + Ali | DL-9 |
| FU-RCS-10 | Platform yönetici ekranları | Beste | DL-10 |
| FU-RCS-12 | Doküman sayacındaki E11000 yarışı — MOD-0029 sahibine bildirilir | MOD-0029 sahibi | — |
| FU-RCS-13 | Kimlik bilgisi döndürme / iptal prosedürü (dev-runbook): yeni `ActiveSecret` → eski `PreviousSecret` + `PreviousValidUntilUtc` → tüketici geçişi → eskiyi sil; acil durumda `IsRevoked`; değer depoya yazılmaz | Beste + tüketici sahipleri | DL-1 kabulü |
| FU-RCS-14 | İkinci iş kiracısı farklı grup önekiyle gelirse belge `OrgPrefix`'in kiracı başına olması | Beste + MOD-0029 sahibi | — |
| FU-RCS-15 | MDM lookup-validation v1.1 (`approvalStatus`) yayınlandığı sürümde okuyucunun "alan yok → 503" kuralına geçirilmesi (kod değişikliği) ve TC-3'ün kaldırılması; AC-36 o revizyonda güncellenir | MOD-0220 sahibi (yayın) + Beste (kod) | v1.1 yayını |
| FU-RCS-16 | Grup önekinin kilitten sonra bilinçli değiştirilmesi (yeni önek, yeni sayaç, belge kodlarıyla birlikte) — sahip kararı | Beste + MOD-0029 sahibi | — |

(FU-RCS-11 kapandı: askıdaki kiracı DL-1'e alındı — inceleme 01 F-12, AC-37.)

## 21. Audited Events

Servisin denetim altyapısı (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`): `platform-merkezi` (a, işaret),
`platform-islem-ici` (a, yazıcı; "üçü de tek girişten (`CanonicalTransactionalAuditOutboxWriter`) geçer"),
`esign-islem-ici-denetim` (a, yazıcı). Bu paketin çağrısı da aynı tek girişten geçer; belirteç
`RecordCodeAuditIntent.EnqueueAsync/RecordCodeAuditIntent.EnqueueRefusalAsync` `platform-islem-ici` satırına CT kararıyla
eklenir (A-10). Karar yoksa DL-1 main'e giremez.

### 21.1 Komutlar

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `AllocateRecordCodeCommand` (başarı) | `platform.record-code.allocated` | `RecordCodeAllocation` | a — işlem içi kapı (`RecordCodeAuditIntent.EnqueueAsync` → `ITransactionalAuditOutboxWriter.TryEnqueueAsync`, `Intent.SourceService` = kapalı listedeki ad; kutu anahtarı `record-code:allocated:{allocationId:N}`) | AfterState: `Code`, `Sequence`, `RecordType`, `PrefixScope`, `Prefix`, `LegalEntityId`; Metadata: `IdempotencyKey`, `GroupPrefixLockedNow`; aktör `TenantUser` (`sub`); `SourceService` çağıran; sonuç `Succeeded` | — |
| `AllocateRecordCodeCommand` (ret) | `platform.record-code.allocation-refused` | `RecordCodeAllocation` (kimliksiz) | a — ayrı kısa işlem (`RecordCodeAuditIntent.EnqueueRefusalAsync` → `ITransactionalAuditRefusalWriter`; kutu anahtarı `record-code:refused:{yeni Guid:N}`) | Metadata: `reason_code`, `RecordType`, `PrefixScope`, `LegalEntityId`; sonuç Bölüm 21.2 | — |
| `AlignRecordCodeGroupPrefixCommand` | `platform.record-code.group-prefix.aligned` | `RecordCodeGroupPrefix` | a — `IAuditableCommand` + `IAuditMetadataProvider` (`AuditBehavior`), aktör `PlatformAdministrator`, `TargetTenantId` | Before: önceki `GroupPrefix` (ilk hizalamada yok) · After: `GroupPrefix` (= etkin `OrgPrefix`) | — |

### 21.2 Hangi ret yazılır (K-10)

| Cevap | Yazılır mı | Outcome | Gerekçe |
|---|---|---|---|
| 413 boyut | hayır (log) | — | gövde okunmadan, kiracı bilinmiyor |
| 401 (her türü) | hayır (log) | — | kimliksiz istek: ne kiracı ne kişi adlandırılabilir (`AuditOutboxWriteRequest.cs:28-30` kiracı ister, yazıcı `:156-185` kişi ister); herkesin bir kiracının günlüğünü doldurmasını önler |
| 403 kiracı çözülmeden (yapılandırma geçersiz, hedef kitle / tüketici yanlış, `X-Tenant-Id`, eksik token iddiaları) | hayır (log) | — | aynı gerekçe: doğrulanmış kiracı + kişi yok |
| 429 hız sınırı | hayır (log) | — | taşkın, denetim günlüğünü de taşırır; sınır aşımı Platform logunda sayılır |
| 403 `RECORD_CODE_TENANT_NOT_ACTIVE` | evet | Denied | yetki (kiracı durumu) |
| 403 `RECORD_CODE_FORBIDDEN` (tür izinsiz) | evet | Denied | yetki; sızma tespiti |
| 403 `RECORD_CODE_PERMISSION_MISSING` | evet | Denied | yetki |
| 400 `RECORD_CODE_REQUEST_INVALID` | hayır (log) | — | biçim hatası programcı hatasıdır, iş niyeti okunamaz |
| 400 `IDEMPOTENCY_KEY_REUSED` | evet | Failed | yanlış kullanım, iz bırakmalı |
| 404 `LEGAL_ENTITY_NOT_FOUND` | evet | Denied | kimlik / kapsam; kiracılar arası yoklama izi |
| 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, `LEGAL_ENTITY_SHORT_CODE_CHANGED`, `GROUP_PREFIX_MISSING`, `GROUP_PREFIX_MISMATCH` | evet | Failed | yapılandırma / ana veri tutarsızlığı |
| 422 `RECORD_CODE_SCOPE_INVALID` | evet | Denied | kapsam |
| 422 `RECORD_TYPE_UNKNOWN`, `LEGAL_ENTITY_NOT_ELIGIBLE` | evet | Failed | iş kuralı |
| 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` | evet (en iyi çaba) | Failed | kaynak erişilemez; Platform deposu çalışıyorsa yazılır |
| 503 `RECORD_CODE_UNAVAILABLE` | dener (en iyi çaba) | Failed | depo erişilemiyorsa ret kaydı da yazılamaz → uyarı logu |

- Her ret kaydının kutu anahtarı yeni bir Guid'dir; aynı istemci anahtarıyla gelen sonraki başarılı ayırma onunla çakışmaz.
- **K2 sınıfı değil** (kimlik, rol/izin, kiracı durumu, GxP, KVKK özel veri değişmez). Başarı kaydı yine işlem içidir
  (kayıt yoksa kod yok).
- **Kayda yazılmayan:** sır, `Authorization`, JWT, MDM gövdesi; kişi verisi yok (kimlikler; e-posta/ad kapının maskesiyle).
- **Okuma denetimi:** yok (AUD-001 §7'nin üç sınıfına girmez).
- **BL-501:** kullanılmaz (AUD-001 §5 yol a). Tüketicinin Create kaydı tüketicinin yoluyla; eşleşme `idempotencyKey` + `code` (TC-7).

## 22. Record Codes

Bu paket kendi kullanıcıya görünen kayıt türünü **açmaz**. Servisin verdiği türler ve kuralları Bölüm 4.4'tedir (RCS-001
§6'nın servis tarafı):

- Sınıf: §3'teki sınıfla (OTOMATİK; `PLN` İKİSİ DE, bağlanması DL-4).
- Önek (§5.5): 3 büyük harf, §3'te merkezi; `AGR` için §3 güncellemesi DL-7 (Ali + Treasury PR'ı). **`AGR` kalır (K-12 a):**
  `AGR` harfleri doküman kodunda tür parçası olarak da geçer (`GMG-QMS-AGR-0001`, `DocumentManagementIdentifiersModels.cs:65`),
  ama iki biçim farklıdır — doküman kodu `{ORG}-{ALAN}-{TÜR}-{4 hane}` (dört parça, ikinci parça alan kodu `QMS`), kayıt kodu
  `{ŞİRKET}-{TÜR}-{6+ hane}` (üç parça); aynı dizgi iki biçimde üretilemez, RCS-001 §3/§5.5 tablosunda doküman tür kodları
  yer almaz (doküman KALSIN) — çakışma yok.
- Ortak servis (§5.12): bu paket o servistir; tüketicinin kendi sayacı yasak (§6).
- Değiştirilemezlik: OTOMATİK kod değiştirilemez; servis değiştirme ucu sunmaz.
- Benzersizlik: `(TenantId, Code)` unique.
- Testler: eşzamanlı ayırma (AC-4), yeniden kullanılmaz (AC-6, AC-20), 999999 → 7 hane (AC-7), ASCII (AC-15).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-1 | v1 servis — ilk adım aktör ölçümü; allocate iç ucu, atomik sayaç, LegalEntity ve Group kapsamları, OTOMATİK tür önek kaydı, şirket kısa kodu ve uygunluk okuması, kısa kod kullanım kilidi (ara dönem), idempotency, işlem içi denetim (aktör kullanıcı, kaynak servis, ayırma kimliğinden kutu anahtarı), ret kaydı, denetim yazıcısının eklemeli genişletmesi, kiracı izolasyonu ve askıdaki kiracı, kimlik bilgisi azaltıcıları ve hız sınırı, grup öneki hizalaması | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes | AC-1…AC-24 ve AC-26…AC-42; AC-41 ilk adım (başarısızsa dilim durur); Platform kapsamı tür izni geçtiyse 422 (K-11); hedef main 2026-11-06; sözleşme 2026-11-03'te donar; main'e girmeden önce A-10 CT defter kararı |
| DL-2 | OTOMATİK mevcut modüllerin bağlanması — Organizasyon Birimi (ORG) ve Pozisyon (POS), süreç içi port | Ownership and Boundaries, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; A-8 önkoşul; kod izni yok |
| DL-3 | İKİSİ DE motoru — öneri, gerekçeli değişiklik, AUD-001 eski/yeni kod + gerekçe, RCS-001 §5.9 | Ownership and Boundaries, Audited Events, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; kod izni yok |
| DL-4 | Platform kapsamı ve İKİSİ DE modüllerinin bağlanması — Abonelik Planı (PLN, öneksiz kod), Görev Türü (TTY), PPM (PFL/PRG/PRJ/INI/INV) | Ownership and Boundaries, Runtime Constraints, Acceptance Criteria, Record Codes, Follow-up Items | AC-25 (K-11'den taşındı) ve bu dilimin revizyonunda eklenecekler; kiracısız satır tabanı ve platform-global denetim yolu burada kararlaştırılır; kod izni yok |
| DL-5 | Tüzel Kişilik kısa kodu önerisi ve RCS-001 §5.4 kalıcı kullanım kilidi (MDM tarafı) | Dependencies, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; karar A-18 (MDM sahibi Ali + Beste, 2026-11-06); o güne kadar ara dönem azaltıcısı DL-1'de; kod izni yok |
| DL-6 | İçe aktarma ve sayaç yüksek su işareti (RCS-001 §5.11), WMS geçici sayacından devam | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-01 önkoşulu; kod izni yok |
| DL-7 | RCS-001 §3 tür başına izinli kapsam sütunu ve CTR → AGR satırı | Record Codes, Açık kararlar | sahip Ali (.antigravity) + Treasury PR'ı; katalog testindeki adlı istisna kalkar |
| DL-8 | WMS türleri (PUT, PCK, OUT, PKG) kataloğa ve Inventory kimlik bilgisi | Entity Fields, Authorization Convention, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; FU-0178-RCS-02 önkoşul; kaynak servis kümesine Diten.InventoryService; kod izni yok |
| DL-9 | Arka plan çağıranları için servis kimliği token'ı (P3) | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; Auth değişikliği (Ali); kod izni yok |
| DL-10 | Platform yönetici ekranları (tür kataloğu, sayaç, defter, grup öneki) | Layout & Shell Contract, Frontend File Contract, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda; 2 dil, `_LayoutPlatformAdmin`, SEC-002 kapsam cümlesi; kod izni yok |

Dilim sırası: DL-1 tek başına teslim edilebilir ve ilk adımı aktör ölçümüdür. DL-2 ve DL-4 DL-1'i, DL-4 ayrıca DL-3'ü
bekler. DL-6, MOD-0178'in FU-0178-RCS-01 geçişinden önce main'de olmalıdır. DL-7 DL-1'i bekletmez. DL-8, DL-6 ve
FU-0178-RCS-02'yi bekler. Tüketici kapıları: Treasury paketlerinin DL-1 kabulü bu paketin DL-1 kodunun main'de olmasını
bekler (D59/1); MOD-0289 ve MOD-0217 için ayrıca MDM bağdaştırıcısı, TC-4 ve TC-5 (A-11).

## 24. Açık kararlar

| # | Konu | Seçenekler | CT önerisi / durum | Sahip | Son tarih |
|---|---|---|---|---|---|
| A-1 | Şirket öneki biçimi (K-6) | RCS-001 §1 · tek grup öneki · öneksiz | açık; gelmezse §1'e göre donar | K1 (`gmg-10`) | 2026-11-02 |
| A-2 | MOD-0144 / MOD-0217 kapsam çakışması | — | AÇIK KARAR; DL-1'in `AGR` kullanımını bekletmez | Cihan + Treasury | TBD |
| A-3 | RCS-001 §3 `CTR` → `AGR` satırı ve izinli kapsam sütunu | — | açık; DL-7; o güne kadar katalog testinde adlı istisna | Ali (`.antigravity`) + Treasury PR'ı | 2026-11-03 (öneri) |
| A-3c | `AGR` doküman tür koduyla çakışıyor mu | — | **KAPANDI — K-12 (a)**: biçimler farklı, çakışma yok (Bölüm 22) | Beste | 2026-10-30 |
| A-4 | Sınıflandırılmamış üreticilerin envanteri | — | açık | Treasury (gönderecek) | TBD |
| A-5 | Grup önekinin kaynağı | — | **KAPANDI — K-12 (c)**; r3 (inceleme 02 F-7): tek kaynak belge `OrgPrefix`'i, kiracı ayarı gövdesiz hizalama + kilit (Bölüm 4.3, 8.12) | Beste | 2026-10-30 |
| A-6 | Platform kapsamı | — | **KAPANDI — K-11**: sözleşmede kalır, DL-1'de tür izni geçtiyse 422, uygulama DL-4 | Beste | 2026-10-30 |
| A-7 | `PLN` v1 kataloğunda mı | — | **KAPANDI — K-12 (d)**: katalogda, bağlanması DL-4 | Beste | 2026-10-30 |
| A-8 | `ORG` / `POS` izinli kapsamı | LegalEntity · Group · ikisi | açık; öneri LegalEntity (`OrganizationUnit.LegalEntityId` zorunlu, `OrganizationUnit.cs:9`) | Beste | DL-2 başlamadan |
| A-9 | Tür kaydı kod mu koleksiyon mu | — | **KAPANDI — K-12 (b)**: kodda | Beste | 2026-10-30 |
| A-10 | `platform-islem-ici` iz satırına `RecordCodeAuditIntent.*` belirteci; CT-DECISIONS; `AuditTrailStandardTests` sabiti | — | açık; kabul önerisi | CT | DL-1 main'e girmeden önce |
| A-11 | Dondurma ayrıntıları: `idempotencyKey` biçimi; Treasury ve MDM portuna `prefixScope`; Bölüm 4.4 izin adlarının FU27 ile teyidi (`APD` alacak dekontu dahil) | — | açık; taslaktaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 |
| A-12 | Kayıt satırı (Bölüm 0), `module-id-registry.md:353` notunun düzeltilmesi, domain-config In-Scope satırı | — | açık | CT | paket onayından önce |
| A-13 | İç uç kimlik deseni | — | **KAPANDI — K-12 (e)**: P2 + azaltıcılar (Bölüm 8.2) | Beste | 2026-10-30 |
| A-14 | Ret denetimi | — | **KAPANDI — K-10** (Bölüm 21.2) | Beste | 2026-10-30 |
| A-15 | AUD-001 §3 satır 3 ile aktör | — | **KAPANDI — K-8r**: aktör kullanıcı, `SourceService` çağıran; çelişki yok | Beste | 2026-10-30 |
| A-16 | Hız sınırı değeri (bölüm: tüketici servis + kiracı) ve eski sır penceresi | değer · gerekçe | açık; CT önerisi 600 istek/dakika/bölüm, kuyruk 0; `PreviousValidUntilUtc` en çok 24 saat | Beste | 2026-11-03 |
| A-17 | 200 yanıt zarfı: v0 `{code, sequence}`'i en üst düzeyde yazıyordu; D59/1 ve Y-8 "yanıt dahil yalnız ekleme" der; öneri 1.0.0'ı `Response<T>` zarfının `data`'sında dondurmak | zarf · en üst düzey | açık; öneri zarf — "v0 yanıt biçimi bağlayıcı değildi" bir **öneridir**, teyit gelmeden karar sayılmaz | **Treasury CT** (teyit) | 2026-11-02 |
| A-18 | RCS-001 §5.4 kısa kod kalıcı kullanım kilidi (MDM tarafı, DL-5) — MDM'de kısa kod bugün değiştirilebiliyor (`UpdateLegalEntityHandler.cs:36-37`) | MDM'de kilit · MDM'de değişiklikte uyarı · yalnız Platform azaltıcısı | açık; ara dönemde Platform azaltıcısı (Bölüm 8.13, 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`) | MDM sahibi (Ali) + Beste | 2026-11-06 |
| A-19 | MDM lookup-validation v1.1 (`approvalStatus`) yayını ve bu serviste "alan yok → 503" geçişi | — | açık bağımlılık; o güne kadar TC-3 zorunlu (FU-RCS-15) | MOD-0220 sahibi + Beste | TBD |
