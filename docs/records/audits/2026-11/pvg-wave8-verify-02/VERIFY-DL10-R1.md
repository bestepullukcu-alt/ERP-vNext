# PVG sekizinci dalga — bağımsız doğrulama: WP-0230-DL10-R1

> Control Tower kaydı, 2026-11-02. `DISPATCHED → VERIFIED → INTEGRATED` geçişinin kanıtıdır.

- **Teslim:** dal `feature/pvg/wp-0230-dl10-r1`, commit `9136f47020ff2d02c360f6a93a5fabb0377254cd`. Ebeveyni `d7df5b1b7` (reddedilen teslim `990ceb5fb`'nin merge'ü), Expected HEAD `6422dd703`.
- **İşçi:** alt ajan, Sonnet 5.5 · high istendi, `claude-sonnet-5-5` olarak bildirdi (dağıtım eki 01). Api.Tests 335 → 352, RegPvBase 211; 22 sabotaj, hepsi adıyla kırmızı. Bir ara yeşil bildirdi: S2b ilk çapası gövdeyi geçersiz kılmadı, çapa değiştirildi.
- **Doğrulayıcı:** ayrı alt ajan, Opus 5.5 istendi, `claude-opus-5-5` olarak bildirdi. Effort çağrıda high istendi; ajan "varsayılanla koştum" dedi, doğrulanamadı. Kendi yalıtılmış worktree'si, işçi raporu ona verilmedi.
- **Karar: KABUL EDİLEBİLİR.** ENGEL 0, ORTA 0, DÜŞÜK 2.

## Ölçülenler (doğrulayıcı)

| Ölçüm | Sonuç |
|---|---|
| `Diten.PvgService.Api.Tests` (Debug, test mongod 7.0.28) | 352 geçti, 0 kırmızı |
| `Diten.PvgService.RegPvBase.Tests` | 211 geçti, 0 kırmızı |
| `known_failures.py check` pvg-api, pvg-regpv-base | çıkış 0, YENİ=0 |
| Mimari süit teslim / taban 6422dd703 | 170 geçti, aynı 6 kırmızı (CiServiceTestsGuardTests ×3, DocsPathGuardTests ×1, AuditTrailStandardTests ×2 — Crm ve Platform), Pvg satırı yok |
| `Diten.PvgService` denetim sayımı | Debt 13, Exceptions 0 değişmedi |
| Test adları | 335'in 334'ü aynı adla; tek eksik yeniden adlandırılan test; 18 yeni |
| Kapsam | 10 dosya, hepsi İzinli yollarda; korunan yol, Gateway, frontend, `tests/architecture`, `.github` dokunulmadı; `git diff --check` temiz; sır ya da kişi verisi yok |

Prompt'un 1–4. maddeleri ve H6, N1, J9, J10, J11, J12, N3, C9, G7 test boşlukları karşılandı (madde tablosu doğrulayıcı raporunda).

## Bağımsız sabotaj

15 sabotaj, işçinin listesi tekrar edilmeden: parola sızıntısının üç başka yolu (iç istisna, stderr, log), gizli değer sağlayıcısının üç biçimde devre dışı bırakılması, kiracılar arası veriyi değiştiren yazma (iki biçim), değer kırpma (iki biçim), aktörün başlıktan ya da gövdeden yazılması, snapshot okumasının tek yazma yolunda düşmesi (iki biçim), bellek içi eşitlik kırıcısı. 14 kırmızı. Bir yeşil (V10, aşağıda DÜŞÜK-1). Her biri geri alındı, özetleri eşleşti. Ek yoklama: sekiz bağlantı metni türünde parola ve kullanıcı adı hiçbir çıktıda görünmedi.

Kanıt (CT karalama alanı, depo dışı): `dl10r1-vfy01/` (`sab-results.jsonl`, `logs/`, `probe/out.txt`), işçininki `pvg-r1-sabotage/`.

## Bulgular

- **DÜŞÜK-1:** depo değerinde baştaki boşluk (`" Mongo"`) için test satırı yok. Host'a `TrimStart()` eklenirse bu değer sessizce Mongo'yu seçer ve hiçbir test kırılmaz (V10). Kod bugün doğru (ordinal karşılaştırma). Düzeltme tek `InlineData` satırı. Bu dilimin bir sonraki dokunuşunda kapatılır; takip maddesi olarak yazıldı.
- **DÜŞÜK-2 (bilgi):** gizli değer açılışta eşzamanlı okunuyor (`GetAwaiter().GetResult()`). Bugünkü sağlayıcı bekletmiyor. Gerçekten asenkron bir sağlayıcı gelirse yeniden bakılır.

## Birleştirme

`feature/pvg/wp-0230-dl10-r1` CT dalına `--no-ff` ile birleşti: `27b952a9d`. Çakışma yok. İşçi dalı yerelde kalır, push edilmez (S-125).

## Ölçülmeyenler

- Canlı host, Gateway ve E4 ölçülmedi. DL-10 kabulü canlı yol açılana kadar ertelenir (KK-8).
- Kiracılar arası yanıt bugün 409; 404 AC-6 / DL-4C'yi bekliyor.
- CI ve Linux koşmadı. CaseProcessing, MeddraCoding ve SignalManagement süitlerini işçi koştu (23 / 40 / 43, değişmedi); doğrulayıcı bu yollara dokunulmadığı için koşmadı.
