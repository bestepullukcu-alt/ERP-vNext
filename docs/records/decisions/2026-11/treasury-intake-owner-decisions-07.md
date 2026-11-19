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
