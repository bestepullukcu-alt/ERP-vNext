# WP-0040-FU02-DL3 — Kayıt Kodu Servisi DL-3: İKİSİ DE motoru

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Paket: `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md`
r15, LF sha256 `c4499f37d93c083af17d4baa961a0bc2b31974aaab74ef433808cf853672ab47` · Onay: approvals.tsv (Ali / GMG-ERP, 2026-11-13, d05ac45a8) ·
İnceleme: `docs/records/reviews/MOD-0040-FU02/2026-11-13-c4499f37.md` (UYGUN)

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`. CT etiketi `ct/line-ff-2026-11-13k`. Etiket dalının atası ise devam;
  değilse `git merge --ff-only ct/line-ff-2026-11-13k`; olmazsa `git merge --no-ff ct/line-ff-2026-11-13k`; çakışmada abort ve CT'ye yaz.
- Paketin LF sha256'sını ölç; c4499f37 değilse dur.
- Paketi oku: 2.5, 4.5, 8.17, 21.5, 16 (AC-62…AC-75), 17 (S-93…S-116), 18.1 (DL-3 kapıları), 19.7, 24, 24.2.

## 1. Kapsam
Paketin DL-3'ü (İKİSİ DE motoru): süreç içi port `IInProcessRecordCodeAssignments` (önizleme, sahiplenme, değişiklik, kilit, geçmiş),
`record_code_claims` defteri, TTY'nin kataloğa girmesi. Uç, Gateway rotası, ekran, yeni izin anahtarı **yok** (Q-15). Tüketici bağlama DL-4.

## 2. Sahip ve CT kararları (bağlayıcı)
- Q-8…Q-16 = A (kanıt `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md`). **Q-9 = A:** elle kod
  oluşturmada da gerekçe ister (düzeltme satırı 8ffc78333). Pakette "Q-9 kayıt metni düzeltmesi açık" ifadeleri kapandı sayılır.
- A-20 (CT, kabul): dört yeni denetim belirteci `platform-islem-ici` izine. Defter (`tests/architecture/audit-ledger/Diten.Platform.md`)
  ve `AuditTrailStandardTests.AcceptedTrails` sabit satırı **aynı commit'te**, DL-3 kodundan önce ya da aynı commit'te (CT kabul koşusu
  bunu tam takım yeşil olarak ister; F-8).
- **İnceleme F-1 (CT kararı):** `LockIfUnlockedAsync` filtresi ön okunan kodu taşır (`Code` eşit). Eşleşmezse satır yeniden okunur ve
  409 `RECORD_CODE_CONFLICT` döner; çağıranın görmediği yeni kod kilitlenmez. AC-70 / AC-72'ye bu vakayı ekle (ön okumadan sonra araya
  giren değişiklik → 409, kilit yok) ve sabotajını yaz (filtreden Code'u çıkar → vaka kırmızı). Paket metni sonraki revizyonda hizalanır.
- TTY katalog satırı ile MOD-0049-FU01'in `RuleAheadOfCatalogue["BPT"]` satırı (63f046c92) `RecordCodeTypeCatalogTests`'te birlikte kalır.

## 3. Kurallar
- Kiracı: her sorgu `RecordCodeTenantPredicate`; başka kiracı 404 tek gövde (AC-72, K-72 kurulumu).
- Denetim işlem içi (DL-1 deseni); retler kısa ayrı işlemde, Outcome kuralı 21.2.
- DL-2 koduna ikinci kopya kuralı: AC-73 eşlik testi (S-115).
- Korunan yollar: `.antigravity/**`, `ocelot.json`, Building.Blocks'a dokunma. Başka modüle dokunmadan önce sor (XMC-001); MOD-0024
  (Görev) dosyaları değişmez, TasksController yalnız yansıma testiyle okunur.
- Yalnız açık yolla stage; hook atlanmaz; push / PR yok.

## 4. Test ve kanıt
- Platform `Diten.Platform.Application.Tests` filtresiz tam koşu (ilgili Mongo testleri gerçek replica set; 27017 yok;
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`). Dilim başı ve sonu sayılarını raporla; dilim başında kırmızı
  olanları adıyla yaz (CT aynı gün main ile karşılaştırır).
- Her sabotaj: uygula → hedef vaka kırmızı → geri al → yeşil. S-93…S-116 ve F-1 kilit sabotajı.
- AC-67 (a): çift ihlalde Mongo'nun bildirdiği index adını rapora ölçüm olarak yaz.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.

## 5. Rapor
Commit sha'ları; AC-62…AC-75 her biri için test adı ve sonuç; sabotaj tablosu; tam koşu sayıları; A-20 defter commit'i; 18.1'deki
sözleşme bilgi notu (RECORD-CODE-ALLOCATE §4 farkı) için önerilen metin; açık noktalar.
