# PVG ilk dalga — bağımsız doğrulama kaydı (WP-0230-DL2A, WP-0230-DL3A, WP-0230-DL8)

> Control Tower kaydı, 2026-10-23. Kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır (ct-sop7-transitions: yapılandırılmış
> ajan raporu → bağımsız doğrulayıcı → doğrulama PASS).
> **Kabul kaydı değildir.** `ACCEPTED`, kod `main`'e girdikten sonra künyeli CT kabul kaydıyla verilir (CT-OPS-03,
> `scripts/acceptance_record.py`).
> Modül: MOD-0230 Case Intake & Triage. Paket sha256 `10fdbe5c…`, `pack_approval.py check` çıkış 0.

## Özet

| WP | Kod commit'i (dal) | İşçi raporu | Bağımsız doğrulayıcı | Sonuç |
|---|---|---|---|---|
| WP-0230-DL2A (BG-12, BG-13) | `d2b3a4534f5a5471daea3866ddf1fa3cf5efaa17` (`feature/pvg/wp-0230-dl2`, ebeveyn `95563dae1`) | `WP-0230-DL2A-isci-raporu.md` | Ayrı alt ajan; yazan oturumdan farklı | PASS — kabul edilebilir |
| WP-0230-DL3A (BG-8) | `5ab4afde42075761a5024383c5ae4914f6fae5be` (`feature/pvg/wp-0230-dl3`, ebeveyn `95563dae1`) | `WP-0230-DL3A-isci-raporu.md` | Ayrı alt ajan; yazan oturumdan farklı | PASS — kabul edilebilir |
| WP-0230-DL8 (BG-16) | Kod değişikliği yok; ölçülen ağaç `95563dae1` (`feature/pvg/wp-0230-dl8`) | `WP-0230-DL8-isci-raporu.md` | CT'nin kendi ölçümü (Faz 1) + işçi log'larının CT okuması (Faz 2) | PASS — kabul edilebilir |

**Bayt eşleşmesi.** Commit'lerdeki dosyaların git blob kimlikleri, bağımsız ölçümde kullanılan kopyalarla birebir aynı:
- DL2A: 11 dosyanın 11'i.
- DL3A: 3 dosyanın 3'ü (`34c354a3`, `a034f642`, `efa3dc4d`).

Yani ölçülen kod, commit edilen koddur.

## WP-0230-DL2A

- **Değişiklik kümesi:** 11 dosya, hepsi izinli yollarda; korunan yola dokunulmamış. Karşılaştırma `git archive 95563dae1` ile yapıldı. Boşluk sorunu yok.
- **Testler:**
  - Kendi süitleri: RegPvBase 97/97 (taban 88), Api 36/36. CaseProcessing 23, MeddraCoding 40 ve SignalManagement 43 testin hepsi yeşil.
  - Paketteki üç BG-12 testi ve dört CT eki test adıyla mevcut ve geçiyor.
  - `known_failures.py check`: `pvg-regpv-base` çıkış 0, `pvg-api` çıkış 0.
- **Mevcut testler:** Hiçbir iddia zayıflatılmamış. `Repository_mutations_are_tenant_scoped` eşdeğer, hatta daha sıkı hale gelmiş.
- **Sabotajlar (doğrulayıcı kendisi uyguladı, her biri geri alınıp `cmp` ile doğrulandı):**
  - A1 (depoda koşulsuz yazma): 7 test kırmızı.
  - A2 (üç çağrı yerinin her biri ayrı ayrı: update, triage, route): her biri adıyla verilmiş testleri kırmızıya çevirdi.
  - B (GetById ve List için ayrı ayrı): kırmızı.
  - C (sürüm yoksa karşılaştırma atlanıyor): kırmızı.
  - D (kilitsiz yazma): 3 koşunun 3'ünde kırmızı.
  - Geri aldıktan sonra 3 koşuda 97/97 yeşil.
- **CT kararları kodda uygulanmış:**
  - Sürüm yoksa (null) çakışma sayılıyor.
  - Çakışma kodu `PVG_INTAKE_DRAFT_VERSION_CONFLICT`.
  - Başka kiracının kaydı "bulunamadı" dönüyor.
  - Karşılaştırma ve yazma tek atomik adımda (`TryUpdate`).
- **Bilinen açıklar (işçi raporunda yazılı ve ölçümle doğrulandı):**
  1. Create sonucu sürüm taşımıyor.
  2. GET ve liste JSON'unda artık `version` alanı görünüyor.
  3. Sürüm göndermeyen update, triage ve route istekleri DL-4'e kadar 409 dönüyor. Bugün tümünü reddeden izin kapısı bunu gizliyor. **İzin veren adaptörler DL-4'ten önce devreye alınmamalı.**
- **CT notu:** Eşzamanlılık testinde `Barrier.SignalAndWait` ve `Task.WhenAll` için süre sınırı yok. Risk düşük. DL2B'ye bırakıldı; erteleme gerileme yaratmaz, çünkü test bugün de yeşil ve yarışı yakalıyor.

## WP-0230-DL3A

- **Değişiklik kümesi:** 3 izinli dosya (`Program.cs`, `PvgServiceApiHost.cs`, `PvgServiceApiHostTests.cs`). Test dosyasından silinen satır yok. Eski üç koruma testi değişmeden duruyor.
- **Testler:**
  - Api 58/58, hem düz koşuda hem `ASPNETCORE_ENVIRONMENT=Development` ile.
  - RegPvBase 88/88; diğer üç PvgService süiti yeşil.
  - Sekiz CT eki test mevcut. Theory satır sayıları 5 / 10 / 2.
  - `known_failures.py check`: `pvg-api` çıkış 0, `pvg-regpv-base` çıkış 0.
- **Sabotajlar:**
  - Prompt'taki dokuz sabotajın (S1–S9) hepsi beklenen testleri kırmızıya çevirdi. Bunların arasında "Program kurulum yöntemini atlıyor" ve "`/health/ready` eski sabit lambdaya döndü" de var.
  - Doğrulayıcı ayrıca beş bağımlılığı listeden tek tek çıkardı (L1–L5). Her biri ayrı ayrı kırmızı verdi.
- **Bilinen açıklar (kabul kaydına girecek):**
  1. Kayıtlı ama çözülemeyen bağımlılık yakalanmıyor. İşçi raporunda yazılı.
  2. Sıfırdan farklı süreç çıkış kodu E2 düzeyinde ölçülmüyor. İşçi raporunda yazılı.
  3. **CT eki:** `Program.cs`'nin `ASPNETCORE_ENVIRONMENT` ve `ASPNETCORE_URLS` okuması hiçbir testle korunmuyor. Doğrulayıcının E1, E2, E3 ve E5 sabotajlarında testler yeşil kaldı. Okuma bugün doğru, yalnız korunmuyor. Kök neden prompt'un süreç ortam değişkeni yasağı.
  4. **CT eki:** Tek bağımlılık listesi yalnız okumayla doğrulandı. Değerler ve biçim testle sabit.
  5. **CT eki:** Production ortamında hazırlığın 200 dönen yolu test edilmiyor. Theory yalnız Development ve Staging satırları içeriyor.
- **DL3B'ye girdi:** Ortam adı boş dize verilirse host üretim benzeri sayılmıyor ve üretim dışı adaptörlerle açılıyor (`PvgServiceApiHost.cs:41`, `:153`). Eski davranışla aynı. Paket satır 660 yalnız "unset" durumunu tanımlıyor.

## WP-0230-DL8

- **Faz 1 (E2), CT'nin kendi ölçümü:**
  - Ölçüm, Gateway dosyaları `95563dae1` ile aynı olan CT worktree'sinde yapıldı (`git diff --stat 95563dae1 -- gateway/` boş).
  - Dört MOD-0230 route'u paketin 1210–1214. satırlarıyla birebir.
  - Gateway süiti 313/313 geçti.
  - Sabotaj: `{intakeDraftId}` girdisine `DELETE` eklendi. Kırmızı testler:
    - `PvgCaseIntakeTriage_ForbiddenRoutesAreAbsent`
    - `PvgCaseIntakeTriage_RouteFamilyMapsOnlyApprovedTemplates` (`Expected ["GET","PUT"] / Actual ["DELETE","GET","PUT"]`)
  - Geri alındı; ağaç temiz.
- **Faz 2 (E3), sahibin `canli-onay`ı ile (tek-CT kuralı, kart §7a/5):**
  - Gateway ve servis aynı GET isteğine aynı yanıtı verdi: `409` ve `PVG_TENANT_CONTEXT_REQUIRED`, gövde birebir aynı.
  - CT işçinin log'unu okudu. Ocelot isteği `localhost:5011/api/v1/pv-case-intake-triage`'e eşlemiş. Gateway, Development'taki geliştirme kiracı bypass'ını uyguladı; bu mevcut davranış ve bu işin konusu değil.
  - Denemeden sonra 5000 ve 5011 boş.
- **Bilinen açık:** Smoke yalnız kiracısız GET'i kanıtlıyor. Kiracı ve token ile uçtan uca akış ile paketin tarayıcı smoke maddesi bu işin kapsamında değil.

## DOĞRULA'daki mimari kontrol (DL2A madde 5, DL3A madde 4)

- `tests/architecture` için `known_failures.py check` sonucu çıkış 1. Durum her iki işçi ağacında ve `95563dae1` tabanında birebir aynı:
  - Aynı 3 test kırmızı: `EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`, `EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous`, `NoCodeFilePointsIntoDocsOutsideTheFiveFolders`.
  - Mesajlar aynı; hiçbirinde PVG geçmiyor.
  - Sınıflama aynı: KISMEN-DÜZELDİ 1, YENİ-AYRINTI 2.
- **CT kararı:** Bu sonuç taban borcudur, bu iş paketlerinden gelmiyor. Doğrulama PASS'ini engellemez.
  - İş paketlerinin kendi süitleri (`pvg-regpv-base`, `pvg-api`, `gateway`) çıkış 0 veriyor.
  - `tests/known-failures.tsv` güncel değil. Bu dosya PVG'nin değil, depo test altyapısının; PVG CT ona dokunmaz (XMC-001) ve konuyu sahibe bildirir.
  - Erteleme gerilemeye yol açmaz: kırmızılar bugün de var ve PVG kodu onlara bir şey eklemiyor.

## Sonraki adım

Üçü de entegrasyon gerektiriyor (kod `main`'e PR ile girer). Sıra şöyle:
1. `VERIFIED → INTEGRATION_READY`: PR açılır, `main` birleştirilir, kontroller yeniden koşulur.
2. `INTEGRATED`: PR birleşir.
3. `ACCEPTED`: künyeli kabul kaydı yazılır ve `acceptance_record.py check` çıkış 0 verir.

Push ve PR sahibin açık onayıyla yapılır.
