# WP-MVP1-FUB2 — kanıt (2026-10-10, CT bekliyor)

> Dal `feature/mvp1-fub2`, taban `fa4c4e188`, worktree `C:\Users\Ali\erp-vnext-wt-mvp1-fub2`. Yalnız yerel; push / PR yok.
> Betikler: `mvp1-fub2-scripts-2026-10-10.md`. Ekran görüntüleri: `mvp1-fub2-shots/`.

## 0. Özet

| madde | sonuç |
|---|---|
| FU-0175-ITEM-01 — not değişikliği geçmiş satırı | **KAPANDI.** `NoteUpdated` + `noteChange` (QUARANTINE 1.2.0) |
| FU-0175-ITEM-03 — GxP not kilidi | **KAPANDI.** Açık karar varken 409 `QUARANTINE_DECISION_ALREADY_PENDING`; ekranda kilit açıklaması |
| O-ITEM-4 — `updateHold` tekrarı | **KAPANDI.** Aynı anahtar ilk sonucu döner; başka gövde → 400 |
| PRODUCT-MASTER draft.11 | **ALINDI** — `00a4d4268`; doğrulayıcı + Prism yeşil; tüketici davranışı değişmedi |
| FU-0022-CT-01 — esign 1.0.1 notu | **KAPANDI.** Yalnız açıklama |
| FU-0175-ITEM-02 — varsayılan rol `auth.users.lookup` | **BLOCKED** — varsayılan rol değişikliği mevcut kiracılara ulaşıyor (§2.1) |
| O-ITEM-3 — hareket satırlarında kaydeden adı | **BLOCKED** — izinli olmayan bir sözleşme değişikliği gerekiyor (§2.2) |

**Commit'ler:**
- `f35197c5a` — sözleşmeler: QUARANTINE 1.2.0, ESIGN 1.0.1, PRODUCT-MASTER draft.11, README;
- `52176c83e` — InventoryService ve testler;
- `6b5382e23` — Web ve testler;
- belge commit'i (bu dosya, betikler, ekranlar, pack §19 notları).

## 1. Ölçümler (uygulamadan önce)

### 1.1 `updateHold` hata listesi → not kilidi kodu

- 1.1.0'da `updateHold` `x-error-codes: [QUARANTINE_HOLD_CLOSED, QUARANTINE_CONFLICT]` taşıyordu. Kilit kodu yoktu.
- `QUARANTINE_DECISION_ALREADY_PENDING` sözleşmede zaten vardı (`createDecision`, `x-error-codes` haritası `409`).
- Bu yüzden yeni kod eklenmedi. Var olan kod `updateHold` listesine eklendi (1.2.0, eklemeli).
- Kilidin kaynağı hold'un `ActiveDecisionId` alanı:
  - `submitDecision` bunu kilitler (`DecisionHandlers.cs` ~400–430);
  - `QuarantineReopen.ReopenAsync` her terminal durumda (ret, iptal, zaman aşımı, geri çekme, başlatma hatası) bırakır;
  - `Applied` durumunda hold `Decided` olur, kilit de kalkar (`QuarantineApplyEngine.cs:216–218`).
- Sonuç: "onay bekliyor" ve "onaylandı ama uygulanmadı" durumlarının ikisi de kilitli. Taslak karar kilitlemez (test).

### 1.2 Varsayılan rol şablonu → hangi rol karantina anahtarı alıyor

- Kaynak: `DefaultRolePermissionTemplate.SelectFor`.
- **Admin:** `AdminModules = access-governance, legal-entity, crm-account, crm-contact, esign`. Karantina anahtarı yok. `auth.users.lookup` zaten var (`TenantSelfServicePermissions`).
- **Viewer:** Tenant kapsamlı her `read` anahtarını alıyor, yani `quarantine.holds.read`. `auth.users.lookup` yok; kod yorumu bunu açıkça yazıyor: "Viewer does not" (WP-INFRA-AUTH-ACCOUNT-KIND-01).
- **Sonuç:** karantina anahtarı alan tek varsayılan rol Viewer. Değişiklik Viewer'a `auth.users.lookup` eklemek olurdu. Mevcut kiracı etkisi §2.1'de.

### 1.3 O-ITEM-3 → "kaydeden" verisinin kaynağı

- **Kayıt Detayı hareket tablosu:**
  - "Kullanıcı" sütunu `MovementLink.byUserName` alanını okuyor (`details.js:67`).
  - QUARANTINE `MovementLink` alanları: `transactionId, transactionNumber, movementType, atUtc, from/toStockStatus, from/toLocationId, quantity, byUserName`. Kullanıcı kimliği yok.
  - Servis bu alanı hiç doldurmuyor (`QuarantineMapping.Movement`). Ad da bilmiyor.
- **Stok hareketleri listesi** (`/StockMovements`, Gateway `GET /api/inventory/transactions`):
  - INVENTORY `TransactionItem` alanlarında kullanıcı yok (`inventory-bundle.openapi.yaml:401–424`).
  - `InventoryTransaction.CreatedBy` alanı veride var ama sözleşmede yok.
- **Stok defteri** (`/StockLedger`) bir bakiye projeksiyonu; satırında kaydeden yok.
- Sonuç: Web'in `display-label` ile adı çözebilmesi için satırda bir kullanıcı kimliği gerekiyor; bunun için sözleşme değişikliği şart (§2.2).

### 1.4 PRODUCT-MASTER

- `origin/chore/ct-round-2` başı: `6ddda3cc2` (2026-10-09).
- İki dosyaya dokunan son commit: `00a4d4268` (draft.11). Dosyalar başta da aynı blob.
- `product-master-bundle.openapi.yaml`: `35b12bcf…` → `1afccae4…`.
- `product-master-bundle.v1.0.openapi.yaml`: `1b3c00d9…`, değişmedi.
- Alma yöntemi: `git checkout 00a4d4268 -- <iki dosya>`. Dosyalar düzenlenmedi, MDM kodu alınmadı.

## 2. BLOCKED

### 2.1 FU-0175-ITEM-02 — BLOCKED: varsayılan rol değişikliği mevcut kiracıları etkiliyor

Hata protokolü: "varsayılan rol değişikliği mevcut kiracıları etkiliyorsa → BLOCKED". Ölçüm (kod okuması):

1. **`RoleProvisioningService.EnsureDefaultRolesAsync`**
   - Şablonu her çağrıda var olan Admin / Viewer rollerine **eksik olanları ekleyerek** yeniden uyguluyor (`RoleProvisioningService.cs:31–68`, "existing grants are skipped").
   - Bu çağrı yalnız yeni kiracıda yapılmıyor:
     - **`POST /internal/events/tenant-admin-invited`** (`InternalEventsController.cs:97–110`).
       - Platform bunu `InviteTenantAdminUserCommand` ile gönderiyor (`TenantsController.cs:172`, `AdminUserInvitationService.cs:128`).
       - Bu, platform yöneticisinin **var olan bir kiracıya** herhangi bir zamanda yönetici davet etmesi.
       - Davet anında o kiracının Viewer rolü yeni anahtarı alır.
     - **`POST /internal/events/tenant-activated`**: çift kayıt koruması kiracı değil olay kimliği üzerinden (inbox `EventId`). Kiracının yeniden etkinleştirilmesi yeni bir olaydır ve şablon yeniden uygulanır.
     - `RegisterCommandHandler` yalnız Viewer rolü yoksa çağırıyor. Bu yeni kiracı yolu, sorun yok.
2. **`DataSeeder.AssignBaselineAsync`** her AuthService açılışında tohum kiracının (`00000000-…-0001`) rollerini şablondan tamamlıyor.
   - Bu CT'nin FU-0022-SEED-01 ile bildiği ve CT-D10'a istisna olarak kaydettiği yol.
   - Ama tohum kiracı da mevcut bir kiracı.

Yani Viewer'a `auth.users.lookup` eklemek, "yalnız yeni kiracılar; mevcut kiracılara otomatik grant yok" kuralını (1) üzerinden bozuyor. Şablona dokunulmadı.

**Not:** aynı yol CT-D10'un `DefaultSignerPermissions` anahtarlarını da bugün mevcut kiracılara taşıyor. Bu, CT-D10 (b)'nin bilinmeyen bir açığı; yeni bir şey değil.

**Ek bilgi (karar CT'nin):**
- `auth.users.lookup` yalnız `display-label` değil. Kiracının kullanıcı arama listesini (`GET /api/users/lookup`, id + ad) ve hesap doğrulamasını (`account-assertion`: aktiflik + hesap türü) da açıyor (`UsersController.cs:87–124`).
- Viewer'a vermek bu iki okumayı da her izleyiciye açar.

**Seçenekler (CT kararı):**
- **(A)** Yalnız yeni kiracı garantisi:
  - `EnsureDefaultRolesAsync`'e "yeni kiracı" bayrağı eklenir. `tenant-activated` ilk olayı ve `Register` true geçer; `tenant-admin-invited` false geçer.
  - "Yalnız yeni kiracıya" anahtar kümesi (`NewTenantViewerPermissions`) yalnız true iken uygulanır.
  - `DataSeeder` bu kümeyi uygulamaz.
  - Dokunduğu yerler: AuthService Application + Api + Persistence; bu WP'nin diff kapsamının dışında.
- **(B)** Kural gevşetilir: CT, "davet / yeniden etkinleştirme / tohum kiracı" yollarıyla yayılmayı kabul eder (CT-D10'daki gibi). Şablon tek satırla değişir.
- **(C)** Anahtar varsayılan rolde değil, karantina kurulumunda (Ayarlar) açık bir grant ile verilir.

### 2.2 O-ITEM-3 — BLOCKED: izinli olmayan bir sözleşme değişikliği gerekiyor

- WP'nin "YAPMA" listesinde: "Sözleşmelerde yukarıdakiler dışında değişiklik". QUARANTINE 1.2.0 için izin verilenler yalnız geçmiş tipi değişikliği ve eklemeli 409 kodu.
- Adı göstermek için satırda bir kullanıcı kimliği gerekiyor (§1.3). Satırda bugün yalnız kimliksiz `byUserName` var.
- Alanı kimlik ile doldurmak sözleşme alanının anlamını bozar. Servisin ad çözmesi InventoryService'e yeni bir dış bağımlılık getirir (AuthService) ve "ITEM display-label deseni" Web tarafında.
- Kanıt: ekran `04-history-and-movements-user-column-empty.jpg` ("Kullanıcı" sütunu boş).

**Seçenekler (CT kararı):**
- **(A)** QUARANTINE 1.3.0 (eklemeli): `MovementLink.by: UserRef`.
  - Servis bunu `InventoryTransaction.CreatedBy` ile doldurur.
  - Web, var olan `QuarantinePeople` ile adı doldurur (`by` zaten `UserIds` içinde toplanıyor).
  - Çözülemezse kısaltılmış kimlik + "bilinmeyen kullanıcı" gösterilir.
  - `byUserName` kullanımdan kalkar.
- **(B)** Stok hareketleri listesi için INVENTORY 1.2.2 (eklemeli): `TransactionItem.createdBy`. Web'de aynı çözücü kullanılır.

## 3. Uygulama

- **Sözleşme (QUARANTINE 1.2.0, eklemeli):**
  - `HistoryAction` + `NoteUpdated`;
  - isteğe bağlı `HistoryEntry.noteChange` → yeni `NoteChange` şeması: `previousNoteLength`, `noteLength`, `previousAttachmentCount`, `attachmentCount`;
  - `updateHold` `x-error-codes` + `QUARANTINE_DECISION_ALREADY_PENDING` ve bir `description`;
  - x-changelog satırı + README satırı.
  - **Özetin tanımı:** notun karakter uzunluğu (trim sonrası; 0 = not yok) ve ek sayısı, önce / sonra. Metin geçmişe yazılmaz; önceki ve yeni metin `inventory_audit` satırında kalır.
  - Doğrulayıcı: `The_1_2_0_change_is_the_note_history_row_and_the_note_lock_code_and_nothing_else` yeni 1.1.0 fikstürüne karşı ölçüyor. Fikstür, taban dosyanın bayt bayt kopyası; SHA-256 sabitlendi.
- **ESIGN 1.0.1:** yalnız `info` (sürüm, changelog, bir x-implementation-notes satırı "Visibility"). Yol, şema, kod, izin değişmedi.
- **InventoryService (`UpdateHoldHandler`):**
  - kilit: `ActiveDecisionId != null` → 409, hiçbir şey yazılmaz;
  - geçmiş satırı: hold + denetim + `NoteUpdated` satırı tek işlemde yazılır;
  - tekrar: ilk çağrının denetim satırından sürüm / not / ekler, gövdenin geri kalanı güncel okuma. Bu, karar tekrarının deseni: kayıt + güncel olaylar.
  - Değişiklik yoksa hâlâ hiçbir şey yazılmaz ve anahtar kaydedilmez (ITEM'den beri böyle).
- **Web (Kayıt Detayı):**
  - `activeDecision` varken "Notu düzenle" çizilmez; yerine kilit açıklaması gelir (yalnız `quarantine.holds.review` sahibine, UAS-001).
  - Geçmişte "Analiz notu güncellendi" ve "Not: a → b karakter · Ek: c → d" satırı.
  - 3 anahtar × 7 dil.
- **PRODUCT-MASTER draft.11, test uyarlaması (üretim kodu değişmedi):**
  - Varsayılan (ilk) örnek artık v1.1 biçiminde. v1.0 cevabı adlı `v1.0-mock` örneği.
  - `The_default_v1_0_shaped_answer…` → `The_v1_0_mock_answer…` (`Prefer: example=v1.0-mock`).
  - Yeni `Without_a_Prefer_header_the_draft_11_mock_answers_the_v1_1_example`.
  - Global Ürün örneği deprecated `lotControlled` / `serialControlled` alanlarını taşıyor ("şüphede evet", OD-1):
    - test `Assert.Null` → `(true, true)` oldu;
    - GSKU reddi değişmedi, çünkü `serialControlled: true` hâlâ GSKU'yu stoklanamaz yapıyor.
  - v1.0 geri uyumluluk testi v1.0 dosyasıyla kaldı.

## 4. Testler

| suite | önce | sonra | not |
|---|---|---|---|
| InventoryService (kendi replica set'i; `INVENTORY_TEST_MONGO` boş) | 829 (tam koşuda 814 + 15 Prism zaman aşımı; Prism yalnız koşuda 15/15) | **839 / 839** | +7 HoldNoteLock, +2 sözleşme, +1 Prism |
| Web | 696 | **699 / 699** | +3 HoldNoteLockWeb |
| gateway | 107 / 108 | 107 / 108 | bilinen 5065 |
| AuthService Application (`DITEN_ITEST_MONGOD_BIN_DIR`) | 1063 | 1063 | kod değişmedi (§2.1) |
| tests/architecture | 18 | 18 | |
| Platform (yalnız `~ESignatureContractTests`) | 5 | 5 | 1.0.1 testi yeniden adlandırıldı |

- Uyarılar: Inventory 0 → 0, Web 21 → 21. Yeni uyarı yok. Release derlemesi 0 hata.
- Taban koşusundaki 15 Prism hatası "Prism 90 s'de dinlemedi" zaman aşımıydı (tam suite yükü altında). Aynı derlemede yalnız koşu 15/15 yeşil. Sonraki tam koşuda da yeşil.
- `AccountKindAcceptanceHost.Tests` koşulmadı. Platform yalnız filtreli koşuldu.

### 4.1 Önce-kırmızı senaryolar (taban kodunda, `red-inventory-1.txt` / `red-web.txt`)

| senaryo | bugün | sonra |
|---|---|---|
| not değişikliği → geçmiş satırı | satır yok (`Assert.Single` boş) | 1 `NoteUpdated` satırı, `by` = A, `noteChange` 0→6 / 0→1 |
| PendingApproval varken not düzenleme | **200** (not kaydedildi) | **409** `QUARANTINE_DECISION_ALREADY_PENDING`, denetim satırı yok |
| karar sonuçlanınca (Rejected / Cancelled) | (kilit yok) | 200 + satır |
| aynı anahtar, araya başka düzenleme | güncel sürümü döndü (2, beklenen 1) | ilk sonuç (sürüm, not, ek, ETag) |
| aynı anahtar, başka gövde | 400 | 400 `IDEMPOTENCY_KEY_REUSED` |
| taslak karar kilitlemez | 200 | 200 |
| Web: geçmiş etiketi / kilit / noteChange | 3 test kırmızı | yeşil |
| yeni kiracının rolünde `auth.users.lookup`; mevcut kiracı değişmez | — | **BLOCKED** (§2.1), test yazılmadı |
| hareket listesinde kaydedenin adı | — | **BLOCKED** (§2.2) |
| Prism draft.11 | 2 test kırmızı (örnek verisi) | 66 / 66 |

### 4.2 Sabotajlar (`sabotage.js`; her biri geri alındı, `git status --porcelain` boş)

| | sabotaj | test | sonuç |
|---|---|---|---|
| (a) | not kilidi kaldırıldı (`if (false && …)`) | `While_a_submitted_decision_waits_for_approval_…` | **kırmızı** 1/1, temiz |
| (b) | geçmiş satırı yazılmıyor | `A_note_change_writes_one_NoteUpdated_history_row_…` | **kırmızı** 1/1, temiz |
| (c) | varsayılan rol satırı | — | uygulanmadı: kod yok (§2.1 BLOCKED) |
| (d) | ilk sonuç tekrarı kaldırıldı (güncel okuma) | `The_same_key_answers_the_first_result_…` | **kırmızı** 1/1, temiz |
| (e) | hareket listesinde ad çözücü | — | uygulanmadı: kod yok (§2.2 BLOCKED) |
| (a-web) | sayfa açık kararda "Notu düzenle"yi çiziyor | `While_a_decision_is_open_the_note_edit_is_not_drawn_…` | **kırmızı** 1/1, temiz |

## 5. Çalışma zamanı probu (kendi yığın, yerleşik tarayıcı)

**Yığın.** Q5 yığınından türetildi. Kendi süreçleri:
- mongod (rastgele port, replica set, asla 27017);
- Auth 15056, Platform 15057, Gateway 15191 (geçici ocelot kopyası), Inventory 15176, Web 15001;
- **kendi `pm-stub.js`'im** 14036 ve LE taklidi 14037. İkisi de `node --max-http-header-size=65536` ile; Prism ve filonun 4010'u kullanılmadı.

Kullanıcılar:
- A (John Doe) karar verir;
- B (Jane Smith) onaylar;
- ikisinin de rolü `fub2-qa-*` (30 anahtar). `auth.users.lookup` bu rolde açıkça verildi, çünkü FU-0175-ITEM-02 BLOCKED.

Giriş `/account/login` üzerinden.

| # | adım | beklenen | sonuç |
|---|---|---|---|
| H0 | yığının iki giriş karantinası | 2 kayıt | KRN-2026-0001 / KRN-2026-0002 ✅ |
| 1 | A · Kayıt Detayı (KRN-0001) → "Notu düzenle" → kaydet | geçmişte "Analiz notu güncellendi · Not: 0 → 50 karakter · Ek: 0 → 0" | ✅ ekran 01; API N1: tek satır, `by` = A |
| 2 | A karar taslağı → MOD-0022 imza (ESIG-2026-000001, çizim) → gönder | 202 PendingApproval | ✅ D1 (QKR-2026-0001); hold 2 için de D2 (ESIG-2026-000002) |
| 3 | not düzenleme (API) / Kayıt Detayı | 409 `QUARANTINE_DECISION_ALREADY_PENDING`; "Notu düzenle" yok, kilit açıklaması var | ✅ L1, L2; ekran 02 |
| 4 | B · WorkCenter → QKR-2026-0001 **Reddet** | kilit kalkar, düzenleme 200 | ✅ O1; ekran 03 (geçmişte "Onay reddedildi · Jane Smith", "Notu düzenle" geri geldi) |
| 5 | B · WorkCenter → QKR-2026-0002 **Onayla** (imza ESIG-2026-000003, "Onaylıyorum") | karar Applied, kayıt Decided; düzenleme 409 `QUARANTINE_HOLD_CLOSED` | ✅ O2; ekran 05 |
| 6 | aynı anahtar tekrarı (API) | ilk sonuç | ✅ R1: ilk v7 → sonra v8 → tekrar v7 "tekrar sınaması 1", ETag `W/"7"`; başka gövde 400 |
| 7 | stok defteri / hareket listesinde kaydeden adı | — | **BLOCKED** (§2.2); ekran 04'te "Kullanıcı" sütunu boş |

**"Onayla → kilit kalkar" üzerine.** Onay uygulandığında hold `Decided` olur. Not, var olan `QUARANTINE_HOLD_CLOSED` kuralıyla kapanır; düğme de kilit açıklaması da çizilmez (ekran 05). Kilidin kalkıp düzenlemenin geri geldiği yol ret / geri çekme / iptal yoludur (adım 4).

**Probun dürüst kaydı (`probe-api.json`, tüm satırlar korunuyor):**
- **İlk `decide` koşusu FAIL** oldu (D1/D2, L1/L2):
  - imza görünümü `none` gönderilmişti, karantina politikası buna izin vermiyor → 409 `ESIGN_APPEARANCE_NOT_ALLOWED`;
  - karar gönderilemedi (400 `QUARANTINE_SIGNATURE_REQUIRED`), kilit de oluşmadı;
  - kilit denemesi notu 200 ile "onay beklerken not" yaptı. Ekran 02'de görünen not budur.
  - Prob çizimli imzaya çevrildi ve aynı taslaklarla yeniden koşuldu: PASS.
- **İlk `check-open 2` FAIL** oldu (409 `…ALREADY_PENDING`, `DecisionPending`). WorkCenter'ın "İmza kaydedildi" penceresindeki "Tamam" henüz tıklanmamıştı, yani onay çağrısı gitmemişti. Bu ölçüm, onay beklerken kilidin hâlâ durduğunu da gösteriyor. "Tamam"dan sonra karar Applied oldu ve ikinci koşu PASS.
- Tarayıcı paneli zaman zaman çizimi durdurdu. WorkCenter onay / imza adımları bu yüzden sayfa içi JS ile düğme tıklayarak sürüldü; şifre alanına klavyeyle yazıldı.

**Sırlar.** Test şifresi yalnız `<out>/pw.txt`'deydi. Loglarda 0 eşleşme. Kapanışta silindi.

## 6. Süreçler ve PID'ler

| | 4010 | 5000 | 5001 | 5056 | 5057 | 5058 | 5059 | 5060 | 5061 | 5066 | 27017 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| WP başı | 19992 | 30340 | 44056 | 36324 | 10036 | 29368 | 13764 | 34120 | 22340 | 22796 | 4904 |
| prob öncesi | 19992 | 30340 | 44056 | 36324 | 10036 | 29368 | 13764 | 34120 | 22340 | 22796 | 4904 |
| sonra | 19992 | 30340 | 44056 | 36324 | 10036 | 29368 | 13764 | 34120 | 22340 | 22796 | 4904 |

- Filoya istek gönderilmedi; filo durdurulmadı.
- Kendi yığınım: auth 22316, platform 14756, gateway 9312, pm-stub 43512, le-stub 23940, inventory 23852, web 12760, artı EphemeralMongo mongod.
- Yığın yalnız kendi listesini kapattı; yedisi de `gone`. Kendi portlarımın (15056/15057/15191/15176/14036/14037/15001) hiçbiri dinlemiyor.
- Test suite'lerinin geçici mongod'larını suite'in kendisi açıp kapattı.

## 7. Diff kapsamı

`git diff --stat fa4c4e188..HEAD`, belge commit'inden önce 27 dosya:
- sözleşmeler: quarantine, esign, product-master, README;
- InventoryService: Domain + Application, testler, 1.1.0 fikstürü;
- Web: Quarantine;
- Platform: yalnız `ESignatureContractTests.cs`. Bu esign sözleşme testi; 1.0.1 sürümünü sabitliyor.

Belge commit'i bunlara ekler:
- `docs/records/audits/2026-10/mvp1-fub2-*` dosyaları ve `mvp1-fub2-shots/`;
- MOD-0175, MOD-0173 ve MOD-0022 pack'lerinin §19 FUB2 notları.

AuthService, MDM ve e-İmza kodu değişmedi. Stok defteri / hareket listesi Web'i de değişmedi; ikisi BLOCKED.

## 8. Açıklar (CT için)

- **O-FUB2-1 = §2.1** — FU-0175-ITEM-02: seçenek A / B / C. Bir de mevcut CT-D10 açığı var: `tenant-admin-invited` / `tenant-activated` imza anahtarlarını mevcut kiracılara taşıyor.
- **O-FUB2-2 = §2.2** — O-ITEM-3: QUARANTINE `MovementLink.by` ve/veya INVENTORY `TransactionItem.createdBy`.
- **O-FUB2-3 — tekrar cevabının kapsamı:** ilk sonuç olarak sürüm, not ve ekler döner; geçmiş, hareketler ve karar alanları güncel okumadır (karar tekrarı deseni). CT tam anlık görüntü isterse ayrı bir sonuç deposu gerekir.
- **O-FUB2-4:** varsayılan rol kodu değişmediği için AuthService testleri değişmedi. İki test bugün "Viewer `auth.users.lookup` almaz" diyor (`DefaultRolePermissionTemplateTests.cs:343`, `AccountKindSeedTests.cs:53`); seçenek B seçilirse bunlar da güncellenir.
