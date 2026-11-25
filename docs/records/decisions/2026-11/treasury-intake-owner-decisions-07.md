# Treasury intake — sahip kararları 07

Önceki dosyalar: `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01…06.md`. Bu dosya, açık PR #434'ün 06 dosyasıyla çakışmamak için ayrı açıldı; numara D145 oradan devam eder. Bu kayıt onay satırı değildir.

## D145 — DCP-011 kod alanı altı üyeye; D137 taslağının yerine (2026-11-16)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-16: "Add all 6 (Recommended)"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Ölçüm: `origin/main` `c3547081a`.

1. **D145/1 — kapsam:** DCP-011 `runtime_code_scope` MOD-0289'a ek olarak MOD-0126 (TRM-S1), MOD-0119, MOD-0120, MOD-0124 (TRM-S2; D139) ve MOD-0217'yi (TRM-S4; D134) kapsar. Ölçüldü: altısı `ready-for-dev`, sürüme bağlı CT-APR-01 satırları `execution/approvals/approvals.tsv`'de (0126 `4fc00697…`, 0119 `c6e72989…`, 0120 `bbdc0ae1…`, 0124 `c9935483…`, 0289 `0056f86e…`, 0217 `46b25e6c…`) ve paketlerin bugünkü sha'larıyla aynı.
2. **D145/2 — koşullar:** bir üyenin kodu yalnız o paketin sürümüne bağlı onay satırı main'deyken, §8 teslim sırasıyla (TRM-S2 MOD-0126 DL-1'den sonra), dilim sırasıyla, paketin kendi dilim kapıları kapanınca ve dilimin WP prompt'u kapıdan geçince yazılır. Kod alanı bu DCP sürümünün CT-APR-01 satırı main'e girince yürürlüğe girer; o ana kadar `168c71b0…` (yalnız MOD-0289) geçerlidir.
3. **D145/3 — D137:** 2026-11-05 D137 taslağı (yalnız MOD-0217; yerel dal `docs/ct/treasury-dcp011-mod0217-code-01` @ `f4b5ab78a`, main'e gitmedi, incelenmedi) bu kararla yerini D145'e bırakır; D137 numarası kullanılmaz.
4. **D145/4 — F-1:** 2026-11-12 bağımsız incelemenin F-1 düşük bulgusu (üye tablosu, §8.1 TRM-S1 / S2 / S4 ve §8 madde 8'de bayat "draft") bu sürümde kapandı.
5. **D145/5 — sınır:** MOD-0269 (dış sağlayıcı, pack yok) ve MOD-0057 (park) kod alanı dışında. Üyeler, teslim sırası, AC aralıkları ve kapsam dışılar değişmedi; ürün kodu yok. Bağımsız inceleme (CT-APR-02) ve DCP onay satırı (CT-APR-01, onaylayan `bestepullukcu-alt`) bu sürüm için gerekir.

## D146 — D82/2 TRM-14 dilim kapısı `MDM-0220-DL1` kabulüyle açılır; D10/2 yerine MDM-önce teslim (2026-11-16)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-16: "Open on MDM-0220-DL1 (Recommended)"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Soru kaynağı: `docs/records/decisions/2026-11/mdm-mod0220-treasury-notice-01.md` ve MOD-0220 §22 "D82/2 gate — what the MDM side delivers" (`1b1a0680…`, onay satırı main'de, PR #408; check: onaylı).

1. **D146/1 — kapı:** dört finans paketinin (MOD-0126, MOD-0119, MOD-0120, MOD-0124) DL-1 dilim kapısı TRM-14 (D82/2, D135/2), MDM WP'si `MDM-0220-DL1` kabul edilince kapanmış sayılır: kod main'de, AC-T1…T5 geçti (tüketici okuma biçimi testi AC-T3, AC-T4'ün MDM WP kısmı dahil). TRM-14'ün tam kapanışı (her tüketici sonucunun sahibinin kanıtına bağlanması, §22.6a) Treasury DL-1'ini bekletmez; o sonuçlar her finans paketinin kendi DL-1 kabulünde kanıtlanır.
2. **D146/2 — D10/2:** D10/2'deki "MOD-0220 değişikliği MOD-0126 ile birlikte kodlanır" ifadesinin yerini MDM-önce teslim (`MDM-0220-DL1`, `docs/records/decisions/2026-11/mdm-mod0220-s22-delivery-owner-decision-01.md`) alır; Treasury bunu kabul eder.
3. **D146/3 — bugünkü durum:** `MDM-0220-DL1` MDM kuyruğunda `DISCOVERED` (`execution/domains/master-data-management/work-packs/CT-QUEUE.tsv`, `c3547081a`); TRM-14 henüz kapalı değil — finans DL-1'leri onu bekler.
4. **D146/4 — sınır:** finans paketlerinin metni, AC'leri ve `status`'u değişmedi; bu karar kapının hangi kanıtla kapandığını belirler. Bildirim: MDM CT ve Beste'ye (mesaj Natig'den).

## D147 — WP-0289-DL1 P-13: Ali'nin XMC-001 onayı (2026-11-17)

Onay kanıtı: Ali (MDM sahibi) yazılı cevabı, K1 / gmg-10 tarafından 2026-11-17'de Treasury Intake CT oturumuna (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`) aktarıldı. Bu satır, prompt v1.7'nin P-13 dağıtım kapısının ve D143/4'ün istediği dağıtım kaydıdır (onay prompt metnine yazılmaz). Ölçüm: `origin/main` `f20344ae4`.

1. **D147/1 — onay:** Ali: "MOD-0289 (Banka / Şube) DL-1 için XMC-001 cevabım: evet, D143/3'teki kapsamla."
2. **D147/2 — kapsam:** yalnız MOD-0290 alanındaki dört dosya — `LocalAuditIntent.cs`, `AuditIntentClaimedPayload.cs`, `AuditIntentDeliveryProcessor.cs`, `CentralMappingRollout.cs`.
3. **D147/3 — koşul:** değişiklik yalnız eklemeli (D142/2, D143/1–2); ürün niyetlerinin alanları, kayıt biçimi ve davranışı değişmez; eski biçimli kayıtlar okunmaya devam eder. Bunlar testle ve sabotajla gösterilir (DL-1 kabul kanıtı).
4. **D147/4 — sonuç:** P-13 kapandı; WP-0289-DL1 prompt v1.7 (`8651fba3…`) dağıtılabilir. Dağıtım günü ölçümü: Expected HEAD `6be9bc3d6` ile `f20344ae4` arasında `services/Diten.MdmService/**` ve Platform `TrustedSourceAuditIntentOperationMap.cs` değişmedi; izinli yollardan yalnız `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` (+4/−1, MOD-0040-FU02 A-10) ve `tests/architecture/audit-ledger/CT-DECISIONS.md` (+1) değişti — DL-1 kabulünde dal main ile güncellenirken birleşir; prompt ve Expected HEAD değişmez.

## D148 — RCS-001 §3 MOD-0217 öneki `AGR` kurala yazıldı; D114 main'den temiz dalla (2026-11-19)

Onay kanıtı: Beste (Treasury hattının sahibi ve kayıt kodu standardının sahibi) yazılı mesajı, K1 / gmg-10 tarafından 2026-11-19'da Treasury Intake CT oturumuna (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`) aktarıldı: "Kayıt kodu standardı bizim; onayı Beste veriyor, Ali onayı gerekmiyor. `.antigravity` değişikliği için Beste'nin onayı bu mesajla verilmiştir." Ölçüm: `origin/main` `85e818e3c`.

1. **D148/1 — kural satırı:** `.antigravity/rules/record-coding-standard.md` §3, yalnız Sözleşme (MOD-0217) satırı: `CTR` → `AGR`. Metin, 2026-10-31'de Ali'nin yazılı onayıyla (Y-36) yazılan D114 metniyle birebir aynıdır; o kayıt yalnız `chore/ct/treasury-rcs-agr-01` dalındaydı (`fbbdcdff0`, main'e gitmedi) ve bu dal onun yerine main'den temiz hazırlandı. Başka satır ve sahiplik değişmez.
2. **D148/2 — tutarlılık:** kod tarafı zaten `AGR` kullanıyor (`services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs`, MOD-0217, `mdm.contracts.create`; `Mdm` bağlaması `BNK, BAC, AGR`); main'deki standart satırı `CTR` diyordu. `CTR`, MOD-0144 Procurement sözleşmelerinin kodu olarak kalır (D94/1).
3. **D148/3 — ad alanı notu (Y-36):** `AGR` harfleri QMS belge listesindeki "Agreement" belge türüyle aynıdır (`GMG-COM-AGR-0001` gibi); kayıt kodu ad alanı ayrıdır, çakışma yoktur.
4. **D148/4 — sınır:** satırdaki "kod yok" ifadesi onaylanan metnin parçasıdır ve değiştirilmedi; kod servisi main'de olduğundan bu ifadenin güncellenmesi kural sahibinin kararıdır. Uyarlayıcı kopyası yok (`sync_adapters.py --check`: 33 uyarlayıcı eşleşiyor). PR'ı Beste onaylar ve birleştirir.

## D149 — D144 sahip teyidi: MOD-0018 sahibi (Ali) `crm.account.lookup-validation` bağımlı anahtarını teyit etti (2026-11-19)

> **Numara notu (2026-11-23, INT-MAIN-SYNC-01, Beste karar 2=a):** main'deki D148 ile çakıştığı için D148 → D149; commit 94d3cd2ac mesajındaki 'D148' bu kararı kastediyor. İçerik değişmedi.

Onay kanıtı: Ali (MOD-0018 sahibi) yazılı cevabı, Beste (`bestepullukcu-alt`, K2; Treasury hattının devralan sahibi, devir PR #442) tarafından 2026-11-19'da MVP-1 / MVP-5 Control Tower sohbetine aktarıldı: "MOD-0018-FU27 D144: Teyit ediyorum — crm.account.lookup-validation yalnız rol 14 ve 16'ya bağımlı anahtar olarak eklenir." Bu kayıt onay satırı değildir.

1. **D149/1 — teyit:** D144/1 (bağımlı anahtar yalnız rol 14 `TreasuryArOperator` ve rol 16 `TreasuryArOperationsManager`) ve D144/2 (bağımlı anahtar sayısı 7 → 8) MOD-0018 sahibi tarafından teyit edildi. D144/4'teki "sahip teyidi" adımı kapandı; takip tablosundan düşer.
2. **D149/2 — sıra:** FU27 (`2153140c…`, `draft`) için kalan adımlar: güncel sürüme bağlı bağımsız inceleme satırı (CT-APR-02; 2026-11-09 inceleme dosyası `docs/records/reviews/MOD-0018-FU27/2026-11-09-2153140c.md` bu sürüm içindir — sürüm değişmediyse satırı araçla yazılır), ardından CT-APR-01 onay satırı (yazan ≠ onaylayan; FU27 MOD-0018 eki olduğu için onaylayan MOD-0018 sahibi Ali / `GMG-ERP`), D80/2. Paket metni, AC'ler ve `status` bu kayıtla değişmez.
3. **D149/3 — sınır:** D144/4'teki ara dönem kuralı (kiracı yöneticisi izni bu iki role elle verebilir) FU27 kodu gelene kadar geçerli kalır.

## D150 — TRM-S4 (MOD-0217) öne alındı; DCP-011 2026-11-23 revizyonu (D145/2 sırası, F-1) (2026-11-23)

Kaynak: Beste (Platform sahibi, Treasury CT işi bizde — r10), 2026-11-23: "WP-0217: öne alalım". Bu kayıt onay satırı değildir.

1. **D150/1 — sıra:** TRM-S4 (MOD-0217 DL-1) §8 dilim sırasında TRM-S1 / TRM-S2 / TRM-S3'ün sonraki dilimlerini **beklemez**; WP-0289-DL1
   kabulünden sonra sıradaki Treasury dilimi olarak açılır. D145/2'deki "§8 teslim sırası" koşulu MOD-0217 için bu sırayla okunur;
   diğer üyelerin sırası (TRM-S2 MOD-0126 DL-1'den sonra; TRM-S3 MOD-0140 1.3.0'dan sonra) değişmedi.
2. **D150/2 — kalan bağlar:** MOD-0217'nin kendi dilim kapıları (Ready-for-dev Checklist '— DL-1 için' maddeleri; MOD-0023 iş akışı
   bağı DL-3 kapısında, öne alma onu değiştirmez) ve sürüme bağlı onay satırı (`46b25e6c…`) aynen geçerli. RCS-001 kayıt kodu portu MOD-0289 ile tektir: MOD-0289 DL-1'i
   açtı, MOD-0217 tüketir (paket Repo Scope); ikinci tanım yazılmaz.
3. **D150/3 — F-1:** 2026-11-16 bağımsız incelemenin (`docs/records/reviews/DCP-011/2026-11-16-293fdab5.md`) F-1 düşük bulgusu
   (üye olmayanlar tablosunda MOD-0220 "belge PASS; §22 `proposed`") bu revizyonda güncel duruma çekildi: §22 onaylı (`1b1a0680…`,
   onay satırı main'de), teslim `MDM-0220-DL1` (D146; MDM kuyruğunda `DISCOVERED`).
4. **D150/4 — sınır:** üyeler, kod alanı üyeleri, AC aralıkları, kapsam dışılar ve paket metinleri değişmedi; ürün kodu yok. Bu DCP sürümü
   için yeni bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan `bestepullukcu-alt`) gerekir; o satır main'e girene kadar
   `293fdab5…` sürümü (D145) geçerlidir ve MOD-0217 kodu yine yalnız o sürümün koşullarıyla yazılabilir.

## D151 — K4: dört finans paketinin düşük bulguları bir sonraki gerçek revizyona (2026-11-23)

Kaynak: Beste, 2026-11-23 (CT önerisi kabul: "tamam uygun"). Bu kayıt onay satırı değildir.

1. **D151/1 — bulgular:** 2026-11-06 bağımsız incelemelerinin F-1 / F-2 düşük bulguları (paket başına 2, toplam 8):
   `docs/records/reviews/MOD-0126/2026-11-06-4fc00697.md`, `MOD-0119/2026-11-06-c6e72989.md`, `MOD-0120/2026-11-06-bbdc0ae1.md`,
   `MOD-0124/2026-11-06-c9935483.md` — F-1 SEC-002 maddesinde D132/1 döneminden kalan XMC-001 / aday modu ifadeleri (aynı maddedeki D139
   kapanışı onları geçersiz kılıyor), F-2 D139 revizyon notu (5 Kasım) ile karar kaydı (6 Kasım) tarih farkı.
2. **D151/2 — karar:** bulgular şimdi işlenmez; şimdi işlemek dört paketin sürüme bağlı onay satırlarını (`4fc00697…`, `c6e72989…`,
   `bbdc0ae1…`, `c9935483…`) yalnız metin düzeltmesi için geçersiz kılar. Her paketin bir sonraki gerçek revizyonunda kapatılır; inceleme
   dosyalarındaki 2026-11-20 son tarihi bu kararla "paketin bir sonraki revizyonu" olur. Davranış, AC ve kapsam etkisi yok (incelemelerin
   kendi değerlendirmesi).

## D152 — WP-0217-DL1 hazırlık kararları (K-1…K-6, P-6 / P-7 / P-8 / P-9) (2026-11-23)

Kaynak: Beste, 2026-11-23 — CT önerileri kabul ("önerilerin ile devam et"). Taslak: WP-0217-DL1 v0.1 (CT scratch; depoya dağıtım günü alınır).
Bu kayıt onay satırı değildir; paket metni, AC'ler ve `status` değişmez.

1. **D152/1 — K-1 manifest modül kodu:** `contract-lifecycle-management-clm` (paket slug'ı; MOD-0289 deseni). FU27 DL-3 / DL-4 aynı değeri kullanır;
   Ali'ye bildirilir.
2. **D152/2 — K-2:** D141/2–3 deseni MOD-0217'ye de uygulanır: `Contract` / `ContractVersion` `MdmDocumentClassMaps` içinde `StrictByDesignRoots`'a,
   koleksiyonlar teslim deposu ve `AuditIntentTenantPartitionDiscoveryRepository` listesine eklenir (yalnız ekleme; MOD-0290 dosyalarında D147
   kapsamı dışına çıkılmaz — gerekirse DUR).
3. **D152/3 — K-3 `MasterDataAuditOperation` üye adları:** `ContractCreated`, `ContractUpdated`, `ContractDeleted`, `ContractVersionCreated`,
   `ContractVersionUpdated`, `ContractVersionValueWritten`, `ContractValueRead` (yalnız ekleme, var olan numaralar değişmez).
4. **D152/4 — K-4 okuma niyeti:** Platform `AuditOperation` enum'una yalnız ekleme olarak `Read` değeri eklenir; `ContractValueRead` → `Read`.
   Platform sahibi kararı (Beste). Eşleme ve enum eki P-9 eş işinde yapılır.
5. **D152/5 — K-5 AC-23 Mongo değişkeni:** paketin "suite'in kendi URI değişkeni" ifadesi var olan `DITEN_TEST_MONGOD` + `DITEN_REQUIRE_OWNED_MONGO=1`
   ile karşılanır (27017 reddi `MdmTestMongo`'da).
6. **D152/6 — K-6 uygulayıcı:** WP-0217-DL1 kodunu merkez geliştirici oturumu yazar (Treasury işi bizde — r10); MDM sahibi Ali'ye bilgi verilir
   (paketteki Y-2 TBD bu kararla kapanır).
7. **D152/7 — P-6 / P-7:** WP-0289-DL1 deseni: DL-1 kabulü FU27 DL-3 / DL-4 kodunu beklemez; `mdm.contracts.*` izinlerinin rol şablonu bağı FU27
   gelince yapılır, o zamana kadar kiracı yöneticisi izinleri elle verir (D144/4 ara dönem kuralı deseni). FU27 kapıları FU27'nin kendi kabulünde
   kapanır.
8. **D152/8 — P-8:** dev ortamında MDM kayıt kodu kimlik bilgisi `AGR`'yi zaten taşıyor (başlatma betiği, BPT / BNK / BAC / AGR) — kapalı.
9. **D152/9 — P-9:** Platform merkezi denetim eşlemesine Contract / ContractVersion satırları (D152/3–4) eş iş olarak açılır:
   `P12-PLATFORM-CONTRACT-AUDIT-MAP-01` (P-12 dersi: DL-1 kabulünden önce hatta olur).
10. **D152/10 — sıra:** D150 onay satırı + WP-0289-DL1 kabul kaydı main'den temiz bir docs dalıyla PR'a gider (Beste onayı); WP-0217-DL1 Expected
    HEAD o PR'ın main'e girdiği commit olur.
