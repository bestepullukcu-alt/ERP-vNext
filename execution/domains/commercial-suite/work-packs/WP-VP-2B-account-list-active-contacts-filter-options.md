# WORK PACKAGE — WP-VP-2B · İş yeri listesi: aktif kişi sayısı, "kişisi olanlar" filtresi, filtre seçenekleri (mobil R1–R3)

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** mobil talep [BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md](mobile/2026-10-06-account-list/BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md) + [CT yanıtı](mobile/2026-10-06-account-list/MOBILE-ANSWERS-2026-10-06-account-list.md) · [yol haritası](ROADMAP-visit-planning.md) Faz 2b.
> - **Kullanıcı (2026-10-07):** "Faz 2b paketini yaz".
> - **Kapsam:** yalnız CRM backend (Web ekranı değişmez; Hedefler / liste yeniden tasarımı Faz 4).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-2b`, dal `wp/vp-2b`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bağlam (CT kod okuması, 2026-10-07)
- **Liste ucu:** `GET /api/crm/accounts` (`AccountController.cs:23-39`; `crm.account.read`). Parametreler: `search, page, pageSize, sortBy, sortDir, status, accountType, territoryNodeId, countryScope`.
  - Depo: `IAccountRepository.ListAsync(... accountIdScope ...)` — bölge filtresi kapsam kümesini `Id IN` olarak geçiyor.
  - Arama artık `TurkishInsensitivePattern` (WP-VP-FIX-2).
  - Alt ağaç kapsamı WP-VP-2'de geldi.
- **Temsilci ucu:** `GET /api/crm/visit-plan/my-accounts` (`GetMyAccountsQuery.cs`; WP-VP-2 B-2). Öğe: `MyAccountItemDto(AccountId, AccountName, AccountType, CityRef, DistrictRef, Latitude, Longitude)`.
- **Bağlantılar:** `account_contact_links` (canlıda 140.945 satır). Dizinler: `tenant_account`, `tenant_contact`, `active_natural`, `primary`.
- **Aktif kuralı:** `RelationshipLifecycle.IsClosed` — durum `ended` / `inactive` (trim, büyük / küçük harf duyarsız) = kapalı; silinmiş satır sayılmaz; `ValidFrom` / `ValidTo` bakılmaz.
- **Hesabın kişi listesi:** `GET /accounts/{id}/contacts` kişisi silinmiş bağlantıyı göstermez.
- **Bölge seçenekleri:** `crm.territory.node.read` istiyor (yalnız 97c5 Admin); saha kullanıcısı okuyamıyor.

## NE
### R1 · `activeContactCount` (her iki uçta)
- `AccountListItemDto` ve `MyAccountItemDto`'ya **ek** `int ActiveContactCount`.
- **Kural:** silinmemiş bağlantı + `!RelationshipLifecycle.IsClosed(Status)` + kişisi silinmemiş. `/accounts/{id}/contacts`'ın aktif saydığıyla **birebir aynı** (ortak kural / yardımcı; ikinci kopya yazma).
- **Yalnız dönen sayfa için**, tek toplama: `TenantId = sunucu kiracısı` + `AccountId IN (sayfa kimlikleri)`. Satır başına sorgu YOK.
- Kişi silinmişliği için de sayfa başına sabit sayıda okuma.

### R2 · `hasActiveContacts=true|false` (her iki uçta)
- **Yok:** bugünkü sonuç. **true:** sayı ≥ 1. **false:** sayı = 0.
- Diğer tüm filtrelerle (search, status, accountType, territoryNodeId, countryScope; `my-accounts`'ta bölge kapsamı + type) **VE** ile birleşir.
- `total` / `totalCount` filtreyi yansıtır; `unfilteredTotal` kiracı geneli kalır. Sayfalama ve sıralama aynı.
- **Geçersiz değer → 400** (doğrulama hatası, makine kodu `invalid_has_active_contacts`).
- **Uygulama ipucu:**
  - kiracının aktif bağlantılı hesap kimlik kümesi tek toplamayla (distinct `AccountId`, aktif kuralı + kişi silinmemiş) çıkarılır;
  - bugünkü `accountIdScope` mekanizmasıyla kesiştirilir: `true` → IN; `false` → NIN ya da tümleyen; mevcut kapsamla birlikte.
- **Ölç:** canlı veride (salt okuma, 140K bağlantı) küme hesabının süresi.
  - **> 1 sn** ise DUR + seçenekleri raporla: önbellek / dizin / ön hesap.
  - Gerekirse dizin önerisi `{ TenantId: 1, AccountId: 1, IsDeleted: 1 }`; dizini **ekleme**, önerip raporla.

### R3-a · `GET /api/crm/accounts/filter-options` (`crm.account.read`)
- Yalnız okuma, kiracı sınırlı. Parametreler `scope=mine|all`, `search` (isteğe bağlı, Türkçe duyarsız).
- **Dönüş:**
  - `territories[]`: `{nodeId, code, name, level}`. **Şu an geçerli** hesap kapsamı olan düğümler, ad sırasıyla.
  - `accountTypes[]`: hesaplarda bulunan tür kodları (yalnız kod; etiket mobil tarafında MOD-0048 consumable-sets'ten).
- **`scope=mine`:** çağıranın geçerli temsilci atamalarının kapsadığı düğümler (exact / subtree, WP-VP-2 kuralı) ve o hesaplardaki türler. Ataması yoksa `territoryStatus: unassigned` + `all` ile aynı liste (K-5).
- **`scope=all`:** kiracının tüm kapsamı (bölge dışı ekleme ve yönetici için).
- **Varsayılan:** `mine`.
- Bölge hiyerarşisinde yazma alanı (durum, kural vb.) dönmez.

### Kapsam dışı (bu pakette YOK)
- Web ekranı (Hesaplar ızgarası / Hedefler) değişmez. İstenirse Faz 4.
- R4 (TR etiket verisi) → yol haritası 0.5.

## KORU / YAPMA
- Yalnız **ekleme**: mevcut alan adları, zarf, parametreler aynı; yeni parametre yokken yanıt **birebir** bugünkü.
- **Yeni yazma komutu YOK** (AUD-001 26 sabit). Göç / seed / grant / yeni dizin YOK (öneri serbest).
- Kiracı her sorguda sunucudan. İstemciden gelen kimlik kapsamı genişletmez. Başka kiracının bağlantısı sayıyı değiştirmez.
- Sayım `crm.account.read` altında (CT kararı; yalnız sayı, kişi verisi yok).
- `my-accounts` sahiplik / bölge kuralı (WP-VP-2) aynen.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM Application (2283/0/5 + PII flake), Web (699/0; dokunulmadıysa koşmak yeterli), mimari (38/1; 26 sabit). Build 0 hata.

**Yeni testler (üretim koduyla; talebin §8 senaryoları):**
1. 2 aktif + 1 `ended` + 1 `Inactive` (karışık harf) → 2.
2. Tek aktif bağlantısı silinmiş kişiye → 0 ve `hasActiveContacts=false`'ta görünür.
3. Silinmiş bağlantı sayılmaz.
4. Sayı = `/accounts/{id}/contacts` aktif sayısı (aynı veriyle iki yoldan).
5. `true` + search + accountType + territoryNodeId → kesişim + doğru `total`.
6. 60 eşleşme / pageSize 25 → 25 / 25 / 10, tekrar yok, sıra kararlı.
7. Başka kiracıda aynı hesap kimliği + aktif bağlantılar → sayılmaz, dönmez.
8. Sayfa başına sabit sayıda veritabanı turu (pageSize'dan bağımsız; sahte depoda sayaç).
9. Geçersiz `hasActiveContacts` → 400.
10. `my-accounts` + `hasActiveContacts` + bölge kapsamı kesişimi.
11. `filter-options`:
    - `mine`: yalnız atama kapsamındaki düğümler;
    - `all`: tümü;
    - atamasız → `unassigned` + tümü;
    - saha kullanıcısı (`crm.account.read` var, `crm.territory.node.read` yok) → 200;
    - oturumsuz → 401.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Kapalı durum kontrolünü kaldır → test 1 kırmızı.
2. Sayımı satır başına sorguya çevir → test 8 kırmızı.
3. Kiracı filtresini toplamadan çıkar → test 7 kırmızı.

**Canlı ölçüm:** salt okuma; R2 küme süresi + bir sayfanın toplam süresi, rapora.

### E4 (CT, fleet; kullanıcı girişi; ayrı sekme; salt okuma)
- `GET /api/crm/accounts?hasActiveContacts=true|false` toplamları (true + false = filtresiz toplam).
- Bir hesabın sayısı = kişi listesindeki aktifler.
- Beste ile `my-accounts?hasActiveContacts=true`.
- `filter-options?scope=mine` → Beste'nin 4 ilçesi.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/security-agent.md]
WP: WP-VP-2B · İş yeri listesi: aktif kişi sayısı, "kişisi olanlar" filtresi, filtre seçenekleri (mobil R1–R3)
Repository: C:\tmp\vp-2b (worktree) · Branch: wp/vp-2b · commit bu dala, push YOK · yalnız CRM backend

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-2B-account-list-active-contacts-filter-options.md — önce tamamını oku. Ayrıca: …/mobile/2026-10-06-account-list/{BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md, MOBILE-ANSWERS-2026-10-06-account-list.md} · …/WP-VP-2-rep-scope-names-territory-derivation.md (§37) · …/WP-VP-FIX-2-edit-keeps-targets-turkish-search.md (§37) · services/Diten.CrmService/src/**/{Api/Controllers/CRM/AccountController.cs, Features/Account/**, Features/VisitPlanning/MyAccounts/GetMyAccountsQuery.cs, Features/Territory/AccountAssignments/**, Domain/Entities/RelationshipLifecycle.cs, Domain/Repositories/{IAccountRepository,IAccountContactLinkRepository}.cs, Persistence/Repositories/{AccountRepository,AccountContactLinkRepository,ContactRepository}.cs} · .antigravity/rules/audit-trail-standard.md · memory crm-accounts-serverside-grid, crm-classmap-rejects-unknown-elements.

NE:
R1 — AccountListItemDto + MyAccountItemDto ek int ActiveContactCount: silinmemiş bağlantı + !RelationshipLifecycle.IsClosed + kişisi silinmemiş (= /accounts/{id}/contacts aktif sayısı, ortak kural); yalnız dönen sayfa için tek toplama (TenantId + AccountId IN sayfa), satır başına sorgu YOK.
R2 — hasActiveContacts=true|false her iki uçta: diğer filtrelerle VE, total filtreli, unfilteredTotal kiracı geneli, sayfalama/sıralama aynı; geçersiz → 400 invalid_has_active_contacts; kiracının aktif-bağlantılı hesap kümesi tek toplamayla, mevcut accountIdScope ile kesişim; canlı veride (salt okuma, 140K bağlantı) süre ölç, >1 sn ise DUR + seçenek raporla; dizin ÖNER, ekleme.
R3-a — GET /api/crm/accounts/filter-options (crm.account.read; scope=mine|all, search Türkçe-duyarsız): territories[{nodeId,code,name,level}] (geçerli hesap kapsamı olan düğümler) + accountTypes[] (yalnız kod); mine = çağıranın geçerli temsilci ataması kapsamı (exact/subtree), atamasız → territoryStatus unassigned + tümü; varsayılan mine; yazma alanı dönmez.
KORU/YAPMA: yalnız ekleme, yeni parametre yokken yanıt birebir aynı; Web DOKUNMA; YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit); göç/seed/grant/yeni dizin YOK; kiracı sunucudan; sayım crm.account.read altında; my-accounts sahiplik/bölge kuralı aynen.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — CRM Application (2283/0/5 + PII flake) · Web (699/0, koş) · mimari (38/1, 26 SABİT); build 0 hata. Yeni testler WP Acceptance 1–11 (talep §8 senaryoları). Sabotaj 1–3 (kırmızı kanıtla, geri al). Canlı salt okuma süre ölçümü rapora.
Commit: "feat(crm): WP-VP-2B — account list active-contact count + hasActiveContacts filter + filter-options (mobile R1-R3)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), canlı süre ölçümü, mobil sözleşmesine eklenen alan/parametre/uç listesi (ad + tip + örnek). §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `529a6761d` (ajan `000212eb4`, test dalının iki belge commit'i üzerine rebase, ff). Push: test dalı.

**CT K13:**
| Paket | Taban | Sonuç |
|---|---|---|
| CRM Application | 2289 | **2303/0/5** (PII flake bu koşuda çıkmadı) |
| Mimari | 38/1 (26) | **38/1 (26 sabit)** |
| CRM Api derleme | — | 0 hata |
| Web | 699/0 | dokunulmadı |

**Kod okuması:**
- Tek kural `RelationshipLifecycle.IsActiveLink` (silinmemiş + kapalı değil + kişi var); depo tarafı regex `ClosedStatusPattern` aynı listeden üretiliyor (kayma yok). `$not` regex durumu boş bağlantıyı aktif sayar → `IsClosed(null) = false` ile tutarlı.
- Sayfa sayımı iki okuma (`AccountId IN` + kişi id'leri); iki okumada da bellekte kiracı koruması.
- `hasActiveContacts`: yoksa küme hiç hesaplanmıyor; `true` kapsamı daraltır, `false` kapsamdan çıkarır ya da `Id NIN`. Boş kapsam → boş sonuç (depo `Count == 0` kısa devresi).
- `filter-options` yalnız `crm.account.read`; `mine` = `RepTerritoryCoverage` (my-accounts ile aynı kural, ortak yardımcıya taşındı), atanmamış → kiracı geneli + `unassigned`; `all`; geçersiz → 400 `invalid_filter_scope`; arama `TurkishInsensitivePattern`.
- Yeni yazma komutu yok, dizin yok.

**CT sabotajı (ajanınkinden ayrı):** kişi-var kontrolünü düşür + `false` dalında kapsamdan çıkarmayı kaldır → **4 test kırmızı** (`…soft_deleted_contact…`, `…contacts_projection`, `True_is_ANDed…`, `My_accounts_filter_is_ANDed…`). Geri alındı.

**Açık uçlar (ajan raporu, kabul):** belgesi olmayan kişiye giden bağlantı `true` kümesine girer ama satır sayımı 0 (canlıda 0 kayıt); aynı adlı hesaplarda sayfa sınırı Mongo doğal sırasına bağlı (bugünkü davranış).

**E4 (CT, bekliyor; fleet yeniden başlatma — CRM değişti):** `accounts?hasActiveContacts=true` toplamı ≈ 16.390; `my-accounts?hasActiveContacts=true` Beste kapsamında; satırlarda `activeContactCount`; `filter-options?scope=mine` → Beste'nin 4 ilçesi; `hasActiveContacts=x` → 400.

### §37 ek — E4 ACCEPTED (2026-10-07, CT, salt okuma)
- `accounts?hasActiveContacts=true` 16.390 + `false` 26.984 = 43.374 (tümü) ✓; satırlarda `activeContactCount` ✓; `x` → 400 `invalid_has_active_contacts` ✓.
- `my-accounts?hasActiveContacts=true` (Beste) `assigned`, 799 / 1.535 + sayımlar ✓.
- `filter-options`: Web vekili yok (yalnız mobil) → tarayıcıdan denenemedi; birim testleri yeşil.
