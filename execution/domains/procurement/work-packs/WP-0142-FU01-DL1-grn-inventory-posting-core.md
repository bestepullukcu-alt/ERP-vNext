# WP-0142-FU01-DL1 — Mal kabul (GRN) gerçek stok kaydı + lot açma: çekirdek (arka uç, DL-1)

- **Kimlik:** WP-0142-FU01-DL1 · **Modül:** MOD-0142-FU01 GRN Inventory Posting and Lot Creation (procurement,
  `Diten.ProcurementService`, kiracı modülü; ana modül MOD-0142).
- **Paket:** `execution/domains/procurement/module-packs/MOD-0142-FU01-grn-inventory-posting-lot-creation.md` **r4**, LF sha256
  `e6bd75a6bcd3860ed7ca6aec80ba11eb44b1695e55efa1729b61932496c540dd`.
- **Bağımsız inceleme:** `docs/records/reviews/MOD-0142-FU01/2026-11-25-e6bd75a6.md` (KOŞULLU, F-1…F-6; `reviews.tsv` satırı CT dalında,
  commit `aacfbe681`).
- **Sahip kararı:** `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §1 (Beste, 2026-11-27): r4 kabul; O-R3-1 = evet,
  O-R3-2 = evet, O-R3-3 = A, S-4 = mevcut lot kullanılır; inceleme bulguları F-1…F-6 **bu WP'nin giriş koşulu** olarak kapanır.
- **Dilim:** DL-1 (paket "Teslim dilimleri" DL-1 satırı; Bölüm 22 DL-1 kapısı). **Kanıt seviyesi:** E4.
- **Sahip:** merkezi ekip geliştirici oturumu (MVP-2 sahibi Beste). **CT doğrulayıcı:** MVP-2 CT. **Durum:** taslak — başlama kapısı
  kapalı (Bölüm 1).
- **Dil:** bu dilimde kullanıcıya görünen ekran metni yoktur (Web DL-3'tür). API hata iletileri mevcut Procurement desenindeki
  gibi koddur; yeni kullanıcı metni çıkarsa 7 dil (en, tr, fr, es, zh, ar, ru) şarttır.

Bu belge kod, commit ya da dağıtım izni değildir. Kod yalnız Bölüm 1'deki kapıların hepsi main'de kapandıktan sonra yazılır.

## 1. Başlama kapısı (hepsi `main`'de olmalı)

Ölçüm: `origin/main` `85e3b1eec` (2026-11-27 `git fetch`).

| # | Kapı | Bugün | Kim |
|---|---|---|---|
| G-1 | Paket r4, inceleme dosyası, `reviews.tsv` satırı ve karar kaydı `ct-owner-decisions-2026-11-27.md` main'de | **yok** — yalnız CT dalında (`1208410dc`); main'deki pakette r4 metni yok | CT (kayıt PR'ı) |
| G-2 | Resmi onay satırı (CT-APR-01, HIGH) `e6bd75a6…` hash'ine; onaylayan Ali (`GMG-ERP`), yazan `bestepullukcu-alt` (yazan ≠ onaylayan) | **yok** — `approvals.tsv`'de MOD-0142-FU01 satırı yok (main ve CT dalı) | Ali |
| G-3 | Onay satırı notu Bölüm 22 DL-1 kapısının istediği önerileri **açıkça** kabul eder: S-4, O-R3-1, O-R3-2, O-R3-3 (Beste kararıyla kapandı — notta karar kaydına atıf yeter) **ve S-9, S-12** | **S-9 ve S-12 karar kaydında geçmiyor.** Onay notu ikisini adıyla kabul etmezse Beste S-9'u, CT S-12'yi ayrıca cevaplar; biri açık kalırsa DL-1 başlamaz (paket Bölüm 22) | Ali (not) / Beste (S-9) / CT (S-12) |
| G-4 | C-1: `module-id-registry.md`'de MOD-0142-FU01 satırı | **yok** (main'de ölçüldü) | CT |
| G-5 | `python3 scripts/pack_approval.py check` main'de bu pakete "onaylı — HIGH" der | onay satırı yok | CT ölçer |

Kapılardan biri açıksa **dur**; dal açma, kod yazma. CT başlama mesajında kapıların kapandığı commit'leri verir.

## 2. Bu WP'ye yazılan kararlar

| # | Karar | Kaynak |
|---|---|---|
| S-4 | Aynı SKU + aynı lot numarası (harf duyarsız) ikinci teslimatta **mevcut lot yeniden kullanılır**; TRACE 409 `LOT_ALREADY_EXISTS` → `listLots` birebir eşleşme (paket 8.2 adım 3, AC-4) | Beste 2026-11-27 |
| O-R3-1 | API, yukarı akış yazımından önce konumu LOCATION'da doğrular (koşulsuz LE eşleşmesi dahil); mal kabul rolüne `inventory.locations.read` (rol ataması C-5, kod değil) | Beste 2026-11-27 |
| O-R3-2 | `CompensationBlocked` çıkışı: yeniden gönderim / Details'ten yeniden deneme ya da `procurement.grn.reverse` sahibinin notla elle çözümü (`ManuallyResolved`); kayıt başına en çok 5 deneme (`Grn:MaxPostingAttempts = 5`) | Beste 2026-11-27 |
| O-R3-3 | A: kaydedene anında 409 + ileti, Details rozeti ve çöz düğmesi (DL-3), yapılandırılmış hata günlüğü satırı (alarm C-8); Görev Merkezi bildirimi FU02 | Beste 2026-11-27 |
| S-9 | (Onay notuyla kesinleşir — G-3) Http modunda geçersiz / eksik `Inventory:ReferenceBaseUrl` açılışı durdurur (AC-30) | paket önerisi |
| S-12 | (Onay notuyla kesinleşir — G-3) GRN komutları K2; başarısız akış için ek merkezi kayıt yok (paket Bölüm 21) | paket önerisi |

## 3. Amaç ve kapsam

**Amaç (paket DL-1):** GRN'i gerçek MOD-0173 INVENTORY `postMovement` ve MOD-0174 TRACE `createLot` uçlarına Gateway (5000) üzerinden,
kullanıcının token'ıyla bağlamak; LOCATION doğrulaması; `Inventory:Mode`; Draft-önce durum makinesi (devam, kira ve kira yenileme,
telafi, telafi engeli, elle çözüm ucu, kayıttan yeniden deneme ucu); hata eşlemesi; silme kuralı ve silinmiş kaydın anahtarı; altı
komutun merkezi denetimi.

**Kapsanan kabul ölçütleri (paket "Teslim dilimleri" altı):** AC-1…AC-8, AC-10, AC-11, AC-17, AC-18, AC-20…AC-28, AC-30, AC-31, AC-33,
AC-34 **ve bu WP'nin WP-AC-1…WP-AC-6'sı** (Bölüm 4). Sabotajlar: paket Bölüm 17 S1…S44'ün DL-1'e düşenleri **ve** WS-1…WS-12 (Bölüm 4).

**Yeni uçlar (DL-1):** `POST /api/grn/{grnId}/compensation/resolve` (`procurement.grn.reverse`), `POST /api/grn/{grnId}/posting/retry`
(`procurement.grn.create`). Mevcut dört uç değişmez. Yeni izin anahtarı yok.

**Kapsam dışı:** DL-2 (REVERSAL eşlemesi, AC-9), DL-3 (Web: anahtar kuralı, depo / konum seçicisi, Details rozeti / düğmeleri, 7 dil),
DL-4 (canlı uçtan uca, E5); GRN-EVENT 1.1.0 sözleşme dosyası (ayrı sözleşme PR'ı C-3, S-3 — DL-1 kodu beklemez); INVENTORY / TRACE /
LOCATION kodu ve sözleşmeleri; servis token'ı; Görev Merkezi bildirimi; `frontend/**`.

## 4. Giriş koşulu işleri — inceleme `e6bd75a6` F-1…F-6 (DL-1'in ilk işleri)

Kurallar paketin r4 metnini değiştirmez; paketin **açık bıraktığı ya da çelişkili yazdığı** noktayı tek davranışa bağlar (sahip kararı
2026-11-27: bulgular DL-1 iş paketinin giriş koşulu olarak kapanır). Bu altı iş diğer DL-1 işlerinden **önce** yapılır, her biri kendi
commit'inde AC + sabotajla gelir. Paketle çelişen bir okuma çıkarsa dur, CT'ye yaz.

### F-1 (ORTA) — silme yazımı koşullu olmalı

Ölçüm (`origin/main`): `GrnRepository.DeleteAsync` / `BulkDeleteAsync` (`GrnRepository.cs:94-122`) süzgeci yalnız Tenant + LE +
`IsDeleted = false` + `Id`; `DeleteGrnHandler.cs:22-34` ve `BulkDeleteGrnHandler.cs:38-52` durumu okuyup koşulsuz yazıyor.

**Kural:** silme yazımı tek koşullu güncellemedir — `TenantFilter` + `Id` + `Version = v` (okunan) + silinebilir durum + kira boş ya da
süresi geçmiş. Silinebilir durum: `PostingState ∈ {NotStarted, Compensated}` **ya da** `PostingState` alanı yok ve `Status = Draft`
(FU01 öncesi belge; paket Bölüm 4 türetmesi). Yazım tutmazsa (`ModifiedCount = 0`) kayıt silinmişler dahil **bir kez** yeniden okunur:
silinmiş → 404 `NOT_FOUND`; kira süren → 409 `GRN_POSTING_IN_PROGRESS`; silinemez durum → 409 `INVALID_STATE`; yalnız `Version`
değişmiş (durum hâlâ silinebilir, kira boş) → 409 `GRN_POSTING_IN_PROGRESS` (kayıt üzerinde başka iş oldu; kullanıcı yeniden dener).
Yeniden okuma tekrarlanmaz. BulkDelete için kural F-5'tedir.

- **WP-AC-1 (gerçek Mongo, `ProcurementMongoReplicaSet`):** (a) `Compensated` Draft okunur; aynı anahtarla yeniden gönderim kirayı alır
  (sahte INVENTORY ilk çağrıda bekletilir); ardından silme → 409 `GRN_POSTING_IN_PROGRESS`, belge silinmez, yeniden deneme
  tamamlanınca kayıt `Posted` (ya da kural gereği `Compensated`); (b) silme handler'ı durumu okuduktan sonra, yazımdan önce
  (test kancası) kayıt `Uncertain`'a geçirilir → 409 `INVALID_STATE`, belge silinmez; (c) silinebilir `Compensated` ve parmak izsiz
  `Draft` belge silinir (AC-10 ile tutarlı). AC-33'ün ters yönüdür; AC-33 yeşil kalır.
- **WS-1:** silme süzgecinden `Version` koşulunu kaldır → WP-AC-1 (b) kırmızı. **WS-2:** silme süzgecinden kira koşulunu kaldır →
  WP-AC-1 (a) kırmızı (belge silinir).

### F-2 (ORTA) — telafiden sonra yeni deneme sınırı

Ölçüm: paket 8.2 madde (viii) `Compensated`'da `Grn:MaxPostingAttempts` denetler; madde (vii) (`CompensationPending` /
`CompensationBlocked` → telafiyi bitir → `PostingAttempt+1`) ve 2c (Retry) denetlemez.

**Kural (O-R3-2 kararındaki "en çok 5 deneme"nin her yola uygulanması):** (vii) yolunda telafi bitip kayıt `Compensated`'a geçtiğinde,
yeni deneme başlamadan önce (viii)'deki denetim uygulanır: `PostingAttempt = Grn:MaxPostingAttempts` ise yeni deneme başlamaz,
kayıt `Compensated` kalır (kira boşaltılır), yanıt 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED`; LOCATION / TRACE / INVENTORY'ye **yeni
deneme çağrısı** yapılmaz (telafinin kendi REVERSAL çağrıları yapılmış olabilir). Aynı kural form yolu (aynı anahtar) ve Retry ucu
için aynıdır. Altındaysa paketin (vii) davranışı aynen sürer (2a, `PostingAttempt+1`). Seçilmeyen: "Retry telafiyi bitirip dursun" —
ana veri düzeltilince aynı gövdeyle yeni deneme başarılı olabilir (paket (viii) gerekçesi), sınır zaten çift sayısını 5 ile kapatır.

- **WP-AC-2:** `PostingAttempt = 5` olan `CompensationPending` kayıt (a) aynı anahtarla form yolundan, (b) Retry ucundan sürdürülür →
  telafi biter, kayıt `Compensated`, 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED`, sahte LOCATION / TRACE çağrı sayısı 0 ve INVENTORY'de yalnız
  telafi REVERSAL'ları; `PostingAttempt = 4` iken aynı akış `PostingAttempt = 5` ile yeni deneme yapar. AC-26 ve AC-31 (h) bu vakaları
  da kapsar.
- **WS-3:** (vii) yolundaki sınır denetimini kaldır → WP-AC-2 (a) kırmızı. **WS-4:** yalnız Retry yolunda kaldır → WP-AC-2 (b) kırmızı.

### F-3 (DÜŞÜK) — silinmiş kayıtta anahtar kuralı yalnız anahtar yolunda

**Kural:** paket 2b'nin son cümlesindeki "silinmişse 409 `IDEMPOTENCY_KEY_REUSED`" **yalnız `RecordGrnCommand` (Idempotency-Key ile gelen
kayıt yolu)** içindir. `RetryGrnPostingCommand`, `ResolveGrnCompensationCommand`, `ReverseGrnCommand` ve `DeleteGrnCommand`'da kira ya da
koşullu yazım tutmayıp yeniden okumada kayıt silinmiş bulunursa **404 `NOT_FOUND`** döner (bu uçlarda anahtar kavramı yok; 2c ve
AC-31 (g) ile aynı).

- **WP-AC-3 (gerçek Mongo):** Retry, Resolve ve Reverse için yarış vakası — handler kaydı okur, kira almadan önce (test kancası) kayıt
  silinir → 404 `NOT_FOUND`, yukarı akış çağrısı 0, belge değişmez; aynı yarış `RecordGrn` yolunda 409 `IDEMPOTENCY_KEY_REUSED` (AC-33).
- **WS-5:** Retry yolunda silinmiş dalını anahtar yolununkiyle değiştir → WP-AC-3 Retry vakası 409 alır, kırmızı.

### F-4 (DÜŞÜK) — `Reversed` kayıtların `PostingState`'i

**Kural:** türetme (paket Bölüm 4 son paragrafı) tamamlanır: `Posted → Completed`, **`Reversed → Completed`**, `Draft → NotStarted`.
FU01 sonrası `ReverseGrn` `PostingState`'i değiştirmez (`Completed` kalır); ters kayıt `Status = Reversed` ve `ReverseStartedAt` ile
görünür. Böylece adım 2'de: parmak izsiz `Reversed` kayda aynı anahtar → (ii) `Completed` dalı → 201 mevcut GRN (yanıtta `Status =
Reversed`; bugünkü davranış `RecordGrnHandler.cs:48-55` ile aynı); parmak izli `Reversed` kayda aynı gövde + aynı kullanıcı → (iv) 201
mevcut GRN, farklı gövde ya da kullanıcı → (iii) 409 `IDEMPOTENCY_KEY_REUSED`. Hiçbirinde yukarı akış çağrısı ve kira yok.

- **WP-AC-4:** (a) FU01 öncesi biçimde Mongo'ya doğrudan yazılmış `Status = Reversed`, `PostingState` ve `RequestFingerprint` alanı
  olmayan belgeye aynı anahtar → 201, `Status = Reversed`, çağrı 0; (b) FU01 ile kaydedilip ters çevrilmiş GRN'in belgesinde
  `PostingState = Completed`; aynı anahtar + aynı gövde → 201, farklı gövde → 409; çağrı 0.
- **WS-6:** türetmede `Reversed`'ı `NotStarted`'a eşle → WP-AC-4 (a) 409 alır, kırmızı.

### F-5 (DÜŞÜK) — BulkDelete tek davranış

Ölçüm: `BulkDeleteGrnHandler.cs:38-52` silinemeyen kayıtları sessizce atlıyor, 200 + sayı döndürüyor; paket AC-10 409 diyor.

**Kural (hep ya da hiç):** istenen kimlikler kiracı + LE kapsamında okunur. Bulunamayan, başka kiracı / LE'nin ya da zaten silinmiş
kimlikler sessizce atlanır (bugünkü davranış, bilgi sızdırmaz). Bulunanlardan **biri bile** silinemezse (durum ya da kira) hiçbiri
silinmez: 409 — kira süren varsa `GRN_POSTING_IN_PROGRESS`, yoksa `INVALID_STATE`; yanıt ayrıntısı silinemeyen `GrnId`'leri ve kodlarını
listeler. Hepsi silinebilirse tek Mongo işleminde her biri F-1'in koşullu yazımıyla silinir; toplam `ModifiedCount` beklenenden azsa
işlem geri alınır ve F-1'in yeniden okuma eşlemesiyle 409 döner. N ≥ 1 → 200 + N ve **tek** merkezi denetim satırı (AC-24); N = 0
(hiçbiri bulunamadı) → 200 + 0, satır yok. Boş liste → mevcut 400 davranışı.

- **WP-AC-5:** (a) karma küme {silinebilir ×2, `Uncertain` ×1} → 409 `INVALID_STATE`, ayrıntıda yalnız `Uncertain` kayıt, üç belge de
  silinmemiş, denetim satırı 0; (b) {silinebilir ×2, kirası süren ×1} → 409 `GRN_POSTING_IN_PROGRESS`; (c) {silinebilir ×3} → 200 + 3,
  tek satır; (d) {bilinmeyen ×2} → 200 + 0, satır yok; (e) (c) kümesinde yazım sırasında bir belgenin `Version`'ı değiştirilir → işlem
  geri alınır, 0 silinmiş, 409.
- **WS-7:** "biri silinemezse hiçbiri" denetimini kaldır (eski atlama davranışı) → WP-AC-5 (a) kırmızı. **WS-8:** işlem içi toplam sayı
  denetimini kaldır → WP-AC-5 (e) kırmızı.

### F-6 (DÜŞÜK) — Retry ucu ve AC-31 (g) / AC-33 için eksik sabotajlar

- **WP-AC-6:** AC-31 (g) ve AC-33 vakaları gerçek Mongo'da koşar (paket Bölüm 17); aşağıdaki sabotajların her biri kendi vakasını
  kırmızıya çevirir ve geri alınınca yeşile döner.
- **WS-9:** Retry eyleminden `[HasPermission("procurement.grn.create")]`'i kaldır → AC-31 (g) izinsiz 403 vakası kırmızı.
- **WS-10:** Retry aramasından Tenant / LE süzgecini kaldır (`TenantFilter` yerine yalnız `GrnId`) → AC-31 (g) başka kiracı / LE 404
  vakası kırmızı.
- **WS-11:** Retry aramasından `IsDeleted = false`'u kaldır → AC-31 (g) silinmiş kayıt 404 vakası kırmızı.
- **WS-12:** Retry'da kira almayı atla → AC-31 (d) kirası süren kayıt vakası kırmızı (409 yerine yukarı akış çağrısı > 0).
- AC-33'ün silinmiş anahtar dalı paket S34 ile, yarış vakası (kira alınamayınca silinmişler dahil yeniden okuma) **WS-13** ile:
  2b'de yeniden okumayı `TenantFilter`'lı okumaya çevir → AC-33 yarış vakası 409 `GRN_POSTING_IN_PROGRESS` döner, kırmızı.

## 5. Adım 0 — dal

- Taban `origin/main` (Bölüm 1 kapılarından sonraki uç). Dal: paket künyesindeki `feature/procurement/mod-0142-fu01-grn-inventory-posting`.
- Doğrula: onay satırını taşıyan commit ve paket r4 (LF sha256 `e6bd75a6…`) dalda; `pack_approval.py check` "onaylı — HIGH". Değilse dur.
- Dilim başı commit'ini kaydet; "önce" ölçümleri (test sayısı, satır numaraları) bu commit'te alınır. WP'deki satır numaraları
  `origin/main` `85e3b1eec`'tendir, dilim başında yeniden ölçülür.

## 6. Dokunulacak dosyalar

Paket Bölüm 5 "DL-1 / DL-2 (backend)" listesinin DL-1 kısmı (REVERSAL zarfı `ReverseGrnHandler` eşlemesi DL-2'dir; DL-1 yalnız
`ReverseGrn`'in kira, silme ve denetim kurallarını alır). Bu WP'nin ekledikleri:

- `…Domain/Repositories/IGrnRepository.cs` ve `…Persistence/Repositories/GrnRepository.cs` — F-1 koşullu silme ve F-5 işlem içi toplu
  silme imzaları (beklenen sürüm, silinebilir durum, kira koşulu); F-3 için silinmişleri gören yeniden okuma (pakette zaten
  `GetByIdempotencyKeyIncludingDeletedAsync` var; `GrnId` ile karşılığı gerekirse aynı kuralla).
- `…Handlers/CommandHandlers/{DeleteGrn,BulkDeleteGrn}Handler.cs` — F-1, F-5.
- Test kancaları yalnız test projesinde (depo sahtesi ya da `ProcurementApiFactory` üzerinden ara adım bekletme); üretim kodunda test
  bayrağı yok.

**Dokunulmayacaklar:** `.antigravity/**`; `services/Diten.InventoryService/**`, `services/Diten.SupplyChainService/**`,
`services/Diten.MdmService/**`, `services/Diten.AuthService/**`; `inventory-bundle`, `trace`, `location`, `product-master-bundle`
sözleşmeleri; `grn-event.openapi.yaml` (C-3 ayrı PR); `gateway/**/ocelot.json`; `frontend/**`; `GrnIndexConfiguration.cs` (diff boş,
AC-33); MOD-0140 satırları (`Persistence/DependencyInjection.cs`, `Program.cs`, `ProcurementApiFactory.cs` paylaşılan dosyalarında yalnız
INVENTORY / TRACE / LOCATION kayıt satırları); `InvoiceMatch/**` (MOD-0143). Listede olmayan başka modül dosyası gerekirse dur, CT'ye
yaz (XMC-001).

## 7. Test ve kanıt

- **Suite:** Procurement test projesinin **tamamı** (süzgeçsiz, `~filter` yok) + mimari testler; dilim başı ve dilim sonu sayıları
  rapora. Yeni kırmızı yok; dilim başında kırmızı olan test varsa aynı gün main'de ölçülür ve rapora yazılır.
- **Yukarı akış:** INVENTORY, TRACE ve LOCATION için `HttpMessageHandler` sahteleri; parmak izi denetleyen sahte INVENTORY alan
  listesini `RequestFingerprint.cs`'ten okur (paket Bölüm 17). Canlı Inventory / TRACE çağrısı yok (canlı kanıt DL-4).
- **Gerçek Mongo:** kira, silme yarışları, denetim outbox'ı, tarih gidiş-dönüşü ve WP-AC-1 / 3 / 5 `ProcurementMongoReplicaSet` ile.
  Atlanan (skip) Mongo testi kabul edilmez; rapora koşan / atlanan sayısı yazılır.
- **Ortam notu:** yeni worktree'de sözleşme dosyaları CRLF gelirse Procurement sözleşme testleri sahte kırmızı verir; worktree
  `-c core.autocrlf=false` ile açılır.
- **Sabotaj kanıtı:** paket S1…S44'ün DL-1'e düşenleri ve WS-1…WS-13; her biri için bozuk hâlde kırmızı test adı + geri alınmış hâlde
  yeşil, kanıt dosyasında çıktıyla. Sabotajsız guard kabul edilmez.
- **C-4:** `tests/architecture/audit-ledger/Diten.ProcurementService.md` "Bilinen borç"tan dört GRN satırı bu dilimin PR'ında çıkar;
  yeni iki komut (Resolve, Retry) deftere girer.

## 8. Teslim raporu

Dilim başı / sonu commit'leri; F-1…F-6'nın her biri için commit + WP-AC sonucu + sabotaj çıktısı; paket AC listesinin durumu (yeşil /
DL-4'e kalan); suite sayıları (önce / sonra, atlanan); XMC-001 dışına çıkılmadığının diff özeti; açık kalanlar (S-3 → C-3, C-5 rol
ataması, C-8 alarm). Push ve PR kullanıcının açık onayıyla; push öncesi `origin/main` birleştirilir ve ölçüm yenilenir.
