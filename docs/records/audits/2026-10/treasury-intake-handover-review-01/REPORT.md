---
record_id: TREASURY-INTAKE-HANDOVER-REVIEW-01
kind: inceleme kaydı (level-1 paket kabulü için girdi) — karar değildir
measured_at_local: 2026-10-23
measured_by: Treasury Intake Control Tower (Claude Code) + 6 bağımsız salt-okunur inceleyici (alt ajan; model Opus 5.5, oturum varsayılan effort)
requested_by: depo sahibi (K1), "transfer olunmaya çalışılan bilgileri tam analiz et; her şeyin doğru şekilde devir için hazırlandığını kontrol et"
destination_ref: origin/main 96bedc89374dd8fd06550c49ae2fc65ca07e6298 (+ dal chore/ct/treasury-intake-ct-01, yalnız belge)
inputs: /Users/natig/Projects/treasury-transfer-handoff/ (depo dışı; değiştirilmedi) · kaynak /Users/natig/Projects/ERP-vNext (yalnız git show / ls-tree / grep / log / diff)
rules: TP-001 v1.5 (TASLAK; sahip kararı D6 — level-1 taslak kurala göre)
---

# Treasury devri — tam inceleme (11 paket + kaynak test kanıtı)

## 1. Sonuç

**Hiçbir paket BLOCKED değil** (TP-001 §6 koşullarının hiçbiri yok). **1 paket koşulsuz, 10 paket koşullu level-1
kabule uygun.** Üç pakette kaynak tarafın yeni bir sürümü önerilir, çünkü eksik bilgi kaynak tarafındır ve paket
değiştirilemez (TP-001 §4a): **MOD-0126** (SoD anlatılmamış), **MOD-0119** (canlıya alındı raporları anlatılmamış),
**MOD-0289** (işlem güvenceleri kırık testlere dayanıyor). Level-1 yazılım çalışır demek değildir (TP-001 §5).

## 2. Bütünlük (Treasury Intake CT ölçtü)

| kontrol | sonuç |
|---|---|
| 8 SHA256SUMS (`shasum -a 256 -c`) | 271/271 OK; listede olmayan dosya yok |
| `source-build-evidence-v1.0` SHA256SUMS `7ec2cd33…536eb` | 82/82 OK |
| Handoff baytları = kaynak commit baytları | batch1 37, batch2 37, batch3 40, batch4 43, batch5 27, batch5b 43, batch6 34 — hepsi eşit (blob karşılaştırması); kanıt 81/81 eşit `0efc2ba1` |
| 11 manifest | brief'teki SHA'larla eşit; girdiler tutuyor |
| Kaynak bağlamaları (SOURCE-BINDINGS) | hepsi git nesnesine çözüldü; çözülemeyenler yalnız §N'de "untracked" açıklananlar (MOD-0126 237, MOD-0124 121, MOD-0057 20, MOD-0269 9) |
| 11 kabul kaydı (KABUL) + 11 sahip özet kararı | hepsi var; her karar kendi manifest SHA'sını gösteriyor |
| 8 kaynak commit | hepsi var, hiçbiri pushlanmamış (yalnız bu makinede) |
| Kanıt doğru kodu mu ölçtü | MOD-0119, 0289, 0140 kaynak dosyaları bağlanan commit ile `da4abb20` arasında 0 fark; MOD-0120 AR dosyaları 0 fark (ortak derleme dosyaları değişmiş) |

## 3. Paket başına karar önerisi

| paket | öneri | kontrol (doğru/toplam) | en önemli bulgu |
|---|---|---|---|
| MOD-0220 v1.2 | **KABUL** | 26/28 (2 satır kayması) | kaynak ve Recovery MOD-0220 pack'leri farklı; paket belirtmiyor (M) |
| MOD-0057 v1.0 | KOŞULLU | 18/18 | §O taşıma izni yanlış kayda dayanıyor (M); `*` izni ve `"unknown"` aktör yazılmamış (L) |
| MOD-0217 v1.0 | KOŞULLU | 15/15 | 6 test kırık, 18 işlem testi koşmadı — §H işlem iddiası test edilmemiş (M) |
| MOD-0124 v1.0 | KOŞULLU | 13/13 | Recovery dosya sayıları "verilmedi" deniyor, oysa bağlanan sıra belgesinde var (M) |
| MOD-0269 v1.0 | KOŞULLU | 14/16 | kodda sabit varsayılan kiracı `…0001` (M); kanıt özeti "TEST pass" abartılı — 1 MOD-0269 testi başarısız (M) |
| MOD-0120 v1.0 | KOŞULLU | 22/23 | kardeş paketle çalışma ağacı çelişkisi (M); süresiz denetim sınırlaması level-2'yi bloklar (M) |
| MOD-0149 v1.0 | KOŞULLU | 16/17 | "kaynakta 0 dosya" sayısı yanlış (17 dosya; arama hatası), sonuç doğru (M) |
| MOD-0140 v1.0 | KOŞULLU | 30/30 | kaynak mükerrer kuralı anlatılmamış — Recovery K-2 tekilliğiyle çakışabilir (M); §D "here" değerleri kaynağa ait (M) |
| **MOD-0126 v1.1** | KOŞULLU, **v1.2 önerilir** | 22/22 | **SoD / onaylayıcı kuralları pakette yok (H)**; belirsiz sonuç eksik anlatılmış (M) |
| **MOD-0119 v1.0** | KOŞULLU, **v1.1 önerilir** | 24/25 | **kaynakta "canlıya alındı" raporları var, paket söylemiyor (H)** |
| **MOD-0289 v1.5** | KOŞULLU, **v1.6 önerilir** | 30/30 | **§H işlem güvenceleri kırık testlere dayanıyor (H)**; bağlanan commit tek başına derlenmeyebilir (M) |

Toplam: 233 nokta kontrol edildi, 228 tam doğru; 5'i satır kayması ya da eksik ifade (H = yüksek, M = orta, L = düşük).

## 4. Yüksek önemli bulgular (ayrıntı)

**H1 — MOD-0126 SoD anlatılmamış (T15/T16).** Pakette ve CONTRACT-STATE'te SoD yalnız `waive_sod` izin adı olarak
geçiyor. Kaynakta tam kural seti var (S `da4abb20`): `PaymentSodPolicyService.cs:38-66` (hazırlayan ve gönderen
onaylayamaz; eşik üstü `tier2_approve`), `:78-104` (muafiyet: izin + gerekçe + kanıt);
`PaymentProposals/Contracts/SodPolicyContracts.cs` `Tier2AmountThreshold = 50_000m` — para birimi yok, yalnız
kodda (Treasury Intake CT ayrıca okudu); ikinci, null olabilen eşik `ApproverPolicyContracts.cs:13` — iki eşik
ayrışabilir.

**H2 — MOD-0119 canlı kullanım raporları (T21/T22).** S `218acaf5`:
`reports/MOD-0119/MOD0119-POST-CUTOVER-CLOSURE-M1-report.md:7` "closed as production-live and post-cutover observed"
(Treasury Intake CT ayrıca okudu); `MOD0119-PRODUCTION-CUTOVER-GO-NOGO-M1-report.md` "GO WITH WAIVER". Pakette
"cutover" yalnız "onaylanmayanlar" listesinde geçiyor. Canlı kaynak verisi var mı (§Q veri taşıma) ve NoOp denetim
kaydı canlıyken mi bağlıydı — bilinmiyor. Base pack gateway rotalarını yetkilendirmiyor ama rotalar var (ikinci
açıklanmamış pack-kod farkı).

**H3 — MOD-0289 işlem güvenceleri (T33/T16).** §H aktarılan F02/PAIR güvencelerini yazıyor; bu akışların testleri
`da4abb20`'de kırık ya da koşmadı (AccountCreateTransactionHttp 16/16, AccountCreateTransactionMongo 27,
ContractBootstrap 55/80 başarısız; LegalEntityAccountAggregate 18 fixture eksik). 110 bağlı dosya `218acaf5` →
`da4abb20` arasında değişmedi; R `e4c93c29` 86/86 farklı bir `Mod0289Controller.cs` blob'unu ölçüyor. `218acaf5`'in
kendi commit mesajı "tek başına derlenmeyebilir" diyor; §O bunu açıklamıyor.

## 5. Kaynak kanıt özetindeki düzeltilecekler

- Bağlanan commit etiketi yanlış: SUMMARY/RESULTS MOD-0119, 0120, 0140 için "S da4abb20" diyor; paketler
  `218acaf5` / `66bde312` bağlıyor.
- MOD-0140 §G davranışı S'de değişmiş (`TenantResolutionMiddleware` çelişen başlıkta 400; `Program.cs` pipeline
  sırası) — S koşusu bağlanan davranışı kanıtlamaz; R4 (`e4c93c29`, 47/47) kanıtlar.
- MOD-0269 "TEST pass" abartılı: `ConnectivityPendingApprovalGuardTests` (Mod0269 import'lu) R2S'de başarısız,
  "sahibi belirsiz" sınıflanmış.
- MOD-0217 SUMMARY "MDM 239 passed" ile R3 trx 345 passed uzlaştırılmadı.

## 6. Ortak boşluklar (bütün devir)

1. Resmi API sözleşmesi 10 pakette yok, MOD-0057'de kısmi (10/34), MOD-0220'de var.
2. ACK: 0 alındı; hepsinin borçlusu sahip (K1).
3. §O taşıma izni birçok pakette (`MOD-0057`, `0119`, `0120`, `0140`) "packet authoring or dispatch"ı onaylamayan
   bir kayda dayanıyor; gerçek izin sahip özet kararlarıdır — level-1 kaydı izin kaynağı olarak özet kararını gösterir.
4. Birçok §D, Recovery değerlerini "verilmedi" diyor, oysa bağlanan sıra belgesinde var (MOD-0124, 0126, 0149, 0289).
5. Kaynak commit'ler yalnız bu makinede; kalıcılık için `git bundle` önerilir.
6. Paketi olmayan kaynak kodu aynı Treasury servisinde: MOD-0125 Cash Forecasting, MOD-0146 — kod alınırken dışarıda
   tutulur.

## 7. Recovery tarafına düşenler (§M/§Q/§T, level-1 koşulları)

- MOD-0126: SoD tek ve para birimli eşikle yeniden tanımlanır; iş akışı başlatma idempotency anahtarı ve belirsiz
  sonuç durumu; `[HasPermission]`; banka referansları yalnız boş-olmayan-Guid kontrolünde.
- MOD-0269: sabit kiracı varsayılanı taşınmaz (çekirdek kural 2).
- MOD-0289: outbox `TenantId = Guid.Empty` (`Mod0289BankService.cs:2450`) taşınmaz; tedarikçi durumu MOD-0140
  sözleşmesinden ve LE ilişkisinden okunur, koleksiyondan değil.
- MOD-0140: kaynak `Supplier` tek LE snapshot + kiracı geneli durum; Recovery ilişki bazlı durum — §Q eşleme,
  mükerrer temizliği (vergi no), durum eşlemesi (Suspended/Archived ↔ OnHold/Blocked/Inactive).
- MOD-0120: süresiz denetim sınırlaması taşınırsa §S sapma kaydı (sahip + bitiş) şart.
- MOD-0057: `*` izni ve `"unknown"` aktör.

## 7a. Ek 1 (2026-10-23, kaynak CT bildirimi sonrası) — bağlanan commit kusuru

Kaynak CT bildirdi (kayıt: kaynak R `f9291e6b`, `treasury-218acaf5-reassessment-20261023/`): MOD-0289 v1.5 ve
**MOD-0220 v1.2**'nin bağladığı S `218acaf5`'teki dosyalar o commit'te tanımlı olmayan türlere başvuruyor (MOD-0289'da
34, MOD-0220'de 36 `.cs` dosyası); aynı baytlar S `da4abb20`'de eksiksiz ve derleniyor. Baytlar etkilenmiyor, etiket
yanlış. Yeni sürümler kaynak kuyruğunda READY: MOD-0289 v1.7 (TRM-B7-03-R1), MOD-0220 v1.3 (TRM-B7-04).

- **§3 değişikliği:** MOD-0220 önerisi "KABUL" → **"KOŞULLU — v1.3'ü bekle"**; MOD-0289 → **v1.7'yi bekle** (v1.6 REWORK).
- Bu inceleme kusuru yakalamadı: kontrollerimiz bayt eşitliğini ve satır iddialarını ölçtü, bağlanan commit'in tek başına
  tutarlı olup olmadığını (tür başvuruları) ölçmedi. MOD-0289 için "tek başına derlenmeyebilir" uyarısı §4 H3'te vardı.
- **MOD-0289 test hataları:** kaynak CT'ye göre 61 hata (F02 HTTP 16, F02 Mongo 27, PAIR 18) kanıt koşusunun sağlamadığı
  fixture önkoşullarında duruyor; F02/PAIR güvenceleri için ne lehte ne aleyhte kanıt. §4 H3 "doğrulanmamış" olarak kalır.
- **MOD-0003 düzeltmesi tam doğru değil:** kaynak CT "planned / missing satırı kaynak kaydıdır; Recovery değeri 0 dosya /
  yok" diyor. Recovery ölçümü (origin/main 96bedc893): `execution/registries/module-id-registry.md:61` satırı **var**
  ("planned / missing", kimlik rezerve) **ve** kod 0 dosya. İkisi birlikte doğru.
- MOD-0126 v1.2 ve MOD-0119 v1.1 kaynakta VERIFIED, henüz teslim edilmedi.

## 7b. Ek 2 (2026-10-24) — Batch 7 yeniden yayımları doğrulandı

Girdi: `source-batch7-v1.0/`, SHA256SUMS `ce48b3729a478aaa294661ca7603ee82d483af60002665baeb1fb161b6f97e58` — 82/82 OK,
listede olmayan dosya yok; 81/81 dosya kaynak commit `182855b11d2e0397f1ee8988bada297600d633aa` baytlarıyla eşit.

| paket | manifest | bağlamalar (Treasury Intake CT, dışa aktarılmış ağaçlarla sha256) | istenen düzeltme |
|---|---|---|---|
| MOD-0126 v1.2 | `660ab4c0…cabcc8` | 293 çözüldü; 237 §N untracked; 4 göreli H dosyası | ✅ SoD ve onaylayıcı politikası ölçülmüş ve satırlı (§E, §G, §H.2): `PaymentSodPolicyService.cs:38-66`, `:78-112`, `Tier2AmountThreshold = 50_000m`, ikinci politika `ApproverPolicyContracts.cs`; `WorkflowStartFailed` (8 yer); destination sayıları 199; MOD-0217 ortaklığı. Banka referansları yalnız boş-olmayan-Guid; LE uygunluğu yok — açıkça yazılı |
| MOD-0119 v1.1 | `f075aeaf…f581c1d31` | 73 çözüldü | ✅ §J.1 üç cutover raporu satır alıntılı. **"Production-live" yerel geliştirme yığınında**: OBSERVATION raporu :17/:25 "local stack … no separate production environment", :95 "no real users"; GO-NOGO :114 "no schema or data added". Bağlanan commit `218acaf5` derlenmedi (uyarı başta yazılı) |
| MOD-0289 v1.7 | `58be78ae…b6f7b5647` | 218 çözüldü; Recovery `7a1e4d76` rapor bağlaması sha256 `ac415946…` eşit | ✅ `da4abb20`'ye yeniden bağlandı (40 yer), fixture önkoşulları (14), `Guid.Empty` (4), MOD-0003 "planned / missing" (7), derlenmeme uyarısı |
| MOD-0220 v1.3 | `4c593a62…ae04b9` | 149 çözüldü | ✅ `da4abb20`'ye yeniden bağlandı (38 yer). Kaynak/Recovery pack farkı pakette yok (Recovery §M'de kaydedilecek; istenmemişti) |

**§3 değişikliği:** dört paket için öneri **KOŞULLU KABUL** (H1–H3 kapandı; kalan koşullar §7'deki Recovery-tarafı
maddeleri). MOD-0220 v1.3 koşulu: kaynak ve Recovery pack farkı §M'de. Eski sürümler (v1.1, v1.0, v1.5, v1.2) tarih
olarak kalır; level-1 kaydı yeni sürümlerin manifest SHA'sını gösterir. Kanıt özeti v1.2 (F02/PAIR) kaynakta bekliyor.

Not: MOD-0289 v1.7 yazarı Recovery inceleme raporunu "tek bir izinli git show" ile okuduğunu yazıyor (veri olarak).

## 8. Ölçülmeyenler

Çalışma zamanı, derleme ve testlerin yeniden koşusu; §N'deki commit edilmemiş kayıtlar; Blueprint hücreleri ve
`verify_module_id.py`'nin kaynakta yeniden koşusu (inceleyicilere Python izni verilmedi; Recovery'de keşif koştu,
11/11 çıkış 3); CONTRACT-STATE satırlarının tamamı (örneklendi). İki inceleyici `git show` çıktısını kendi geçici
klasörüne yazdı (depo dışı); bir inceleyici salt okunur `merge-base --is-ancestor` kullandı.
