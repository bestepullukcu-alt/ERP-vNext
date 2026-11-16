# Uygulama planı — kartları bağlama (sonraki adım; bu adımda YAPILMADI)

Dayanak: tasarım belgesi "`.ees/` klasör düzeni ve yerleşim kuralı" (12 Kasım, iş 4 adım 1) ve sahibin 16 Kasım onayı
(karar 3: yeni ev `.ees/`, 8 ajan + 6 beceri bağlanır, 35 tanım arşive). Ölçümler `origin/main` @ `6ef2c884b`, 2026-11-16.
Bu adımda eski kart, SOP, `AGENTS.md`, `.antigravity/**`, `adapters.json` ve `sync_adapters.py` değiştirilmedi.

## A. Adımlar (sıra önemli)

| # | Ne | Değişen dosyalar | Not |
|---|---|---|---|
| A1 | Üretim listesini `.ees/`'e taşı | `.antigravity/adapters.json` → `.ees/adapters.json` (+ `"source_root": ".ees"`) | `.antigravity/**` kullanıcı onayı ister (korunan yol) |
| A2 | Üreticiyi kaynak kökünü okuyacak hâle getir | `.antigravity/scripts/sync_adapters.py`: 13 sabit `.antigravity` geçişinin ~5'i (MANIFEST yolu, beceri varlık denetiminin kökü, yeniden üretme komutu, üretim satırı) `source_root`'tan okunur | betiğin yeri: `scripts/ees/` önerisi, **karar** (aşağıda B1) |
| A3 | 8 ajanın asıl tanımını yaz | `.ees/ajanlar/`: `isci`, `denetci`, `module-pack-author`, `l10n-agent`, `security-agent`, `integration-agent`, `belge-ajani`, `is-analisti` | gövdenin başında taşıdığı kart (işçi ya da denetçi); `seviye:` alanı; `module-pack-author`'a ortak bulgu listesi girdisi (eski kural RV-14) ve dilim tablosu kuralı (MPA:25) |
| A4 | 6 becerinin asıl tanımını yaz | `.ees/beceriler/`: `kule`, `hat` (gövde = kart), `modul-arastir` (eski `control-tower` araştırma bölümü), `resx-check`, `kullanim-kilavuzu`, (6.: i18n betikleri ayrı karar, B2) | `control-tower` çapaları `instruction_budget`'tan kalkar |
| A5 | Üreticiye iki yeni hedef türü | `sync_adapters.py`: (a) `tier_map` — seviye → araç başına model/efor (`model-seviyeleri.md`); (b) kart → beceri/ajan gövdesi | alt ajan çekirdeği kendiliğinden yükler mi **ölçülmedi**; ölçülene dek çekirdek her alt ajan gövdesinin başına da eklenir (49 satır) |
| A6 | Standartları kartlarla uyumla | `AGENTS.md` (Çekirdek bloğu yerine `cekirdek.md`'nin üretilen kopyası; madde 8 Ç23'e), `.antigravity/rules/git-safety.md` (§4a/§5a Ç23'e), `module-pack-standard.md` (SOP listeleri kararı çıkarsa yeni "koşullu bölümler") | yön tersine döner: bugün çekirdek blok `AGENTS.md`'den üretiliyor; yarın `cekirdek.md` → `AGENTS.md` |
| A7 | Bütçe satırları | `instruction_budget`: beş kart için `max_bytes` (çekirdek 10.000, kule 10.800, hat 13.200, işçi 7.200, denetçi 6.000 — satır sınırı × ~120 bayt; çekirdeğin satırları uzun olduğu için 10.000) ve satır sınırı denetimi (70 / 90 / 110 / 60 / 50) | bugünkü kartlar: 8.593 / 5.453 / 7.275 / 3.661 / 2.005 bayt; 49 / 38 / 49 / 23 / 13 satır |
| A8 | CI tetik yolları | `.github/workflows/agent-adapters.yml`: `paths`'e `.ees/**`; komut yolu A2'ye göre | `.github/workflows/` kod sahibi incelemesi ister |
| A9 | Kod sahipleri | `.github/CODEOWNERS`: `/.ees/` satırı (bugünkü talimat satırlarıyla aynı sahip listesi); Gemini üretilirse `/GEMINI.md` ve `/.gemini/` | sahip listesindeki "kişi başına tek hesap" (yönetici C4) ayrı iş |
| A10 | Arşiv | 35 tanım (11 ajan, 11 beceri, 13 iş akışı) arşive; `.antigravity/rules/GEMINI.md` yeniden adlandırılır ya da arşive (Gemini bağlam dosyasıyla aynı ad) | önce `AGENTS.md` §7'deki `@orchestrator` ve çekirdek 6–7'deki `integration-agent` atıfları değişmeli; `i18n-localization/scripts/*checker.py` arayüz uyum CI'sında çağrılıyor, yolu birlikte taşınır |
| A11 | Eski kart ve SOP | pilot hat yeni kartlarla geçince arşive; kod sahibi satırları ve emekli araçlar (onay defteri, istem kapısı, kuyruk, kabul kaydı, durum raporu) iş 4–5 ile kalkar | o güne dek eski kart yürürlükte |

## B. Açık uygulama kararları (sahibin; teknik, küçük)

| # | Soru | Önerim |
|---|---|---|
| B1 | Üretici betik nerede dursun? | `scripts/ees/sync_adapters.py` (diğer araçlar `scripts/` altında; kod sahibi satırı var) |
| B2 | `i18n-localization` betikleri hangi becerinin parçası? | betikler CI aracı olarak `scripts/` altına; metin `l10n-agent`'a |
| B3 | `.ees/` adı yöneticiye teyit edildi mi? | PR'dan önce teyit (plan notunda yazılı) |

## C. Etkilenecek testler ve kontroller

| Test / kontrol | Neden etkilenir | Ölçüm (2026-11-16, bu dal) |
|---|---|---|
| `sync_adapters.py --check` (agent-adapters CI, Ubuntu + Windows) | kaynak yolu, yeni hedefler, bütçe satırları | bugün `OK 13 adapter(s) match their sources`; `.ees/` yönetilen klasör olmadığı için yeni dosyalar onu bozmuyor |
| `scripts/tests/test_sync_adapters_budget.py` (phase1) | bütçe dosya listesi ve `AGENTS.md` başlıkları değişir | A6–A7 ile birlikte güncellenir |
| `scripts/tests/test_ct_card_commands.py` (phase1) | eski kule kartının komutlarını okuyor | A11'de kart arşive giderken emekli olur ya da yeni karta bağlanır |
| `DocsPathGuardTests` (mimari, phase1) | `.ees/` altındaki `.json`, `.html`, `.py` gibi kod uzantılı dosyaları tarar (`.md`'yi taramaz); `docs/` altında beş klasör dışına giden yol kırmızıdır | `.ees/` eklenmişken 229/229 yeşil; sabotaj kanıtı `akibet-ozet.md` §5 |
| Arayüz uyum CI'sı | i18n betik yolu taşınırsa (A10) | — |
| `prompt_gate.py`, `pack_readiness.py`, `review_zip.py` | eski kart/SOP yollarını okuyor | iş 4–5'te emekli; o güne dek dosyalar yerinde |

## D. Yetki sırasında ne değişir

Bugün (`AGENTS.md` §1): Module Pack > Domain Config > AGENTS.md > `.antigravity/` > Archive. Kart ve SOP sırada yok.

Yeni (çekirdek Ç1–Ç3): **0.** kırmızı çizgiler ve yönetici rehberi › **1.** modül paketi › **2.** alan ayarı › **3.** EES
kartları (`.ees/kartlar/`; kökteki `AGENTS.md` bunun üretilen kopyası) › **4.** mühendislik standartları
(`.antigravity/rules/`, ileride `.ees/standartlar/`) › **5.** arşiv. MVP planı sırada değildir (Ç3). Değişenler: 0. satır
yeni; kartlar sıraya girer; SOP çıkar; `.antigravity/` "kurallar" yerine yalnız "mühendislik standartları" olur.

## E. Kod sahipleri listesi

- Ekle: `/.ees/` (aynı sahip listesi). İleride: `/GEMINI.md`, `/.gemini/`.
- Kalır: `/AGENTS.md`, `/CLAUDE.md`, `/.claude/…`, `/.agents/`, `/.codex/`, `/.antigravity/`, `/.github/…`.
- Kalkar (ilgili iş bitince): kule kartı ve SOP satırları (A11), kuyruk dosyaları, emekli araç satırları.
- **Doğrulanmalı:** ana dal korumasında kod sahibi incelemesi zorunlu mu (hesabın yönetici yetkisi yok, API 404). Kapalıysa
  kod sahibi listesi yalnız inceleme ister; R4'ün "araç kısmen zorlar" hükmü buna bağlı.
