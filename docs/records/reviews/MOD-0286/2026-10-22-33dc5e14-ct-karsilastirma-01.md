# MOD-0286 — Control Tower karşılaştırması 01 (Codex inceleme 01, SOP §20.4-4)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ 79f87c1bf (sha256 `33dc5e14…`)
- İnceleme: Codex, sonuç **UYGUN DEĞİL**, 3 ENGEL + 5 ORTA; ham metin `2026-10-22-33dc5e14-codex-01-raw.txt` (aynı klasör, commit'siz taslak).
- Beyan eksiği: Codex "tam model kimliği, etkin effort ve oturum kimliği bu bağlamda sunulmuyor" dedi. `review-row` üç değeri ister; sahip Codex oturumundan okuyup verir ya da satır "beyan edilmedi" ile yazılır (sahip kararı).
- Yöntem: her bulgu ZIP'teki aynı sürüm dosyalarıyla CT tarafından yeniden ölçüldü (komut ve satır aşağıda). Taslak; bulgu dosyası biçimine çevirme ve commit sahibin onayıyla.

## Bulgu bulgu

| Bulgu | CT kararı | Ölçüm |
|---|---|---|
| F-1 ENGEL · otomatik komutlar `run_record.py` ile koşulamaz | **Kabul** | `scripts/run_record.py:131` `subprocess.run(argv, cwd=ROOT)`; kabuk yok, pipe ve glob çözülmez. Paket :138 glob (`[A-Z]*.cshtml`), :139 dosya argümanı yok, :150/:151/:157 bare `index.js` ve `Views/…` göreli yol, :154 ikinci JSON'un dizini yok, :144 `ls … \| wc -l`. |
| F-2 ENGEL · bazı "auto" maddeler şartı ölçmüyor | **Kabul** | `verify_datatable_page.py`'de `form_field_count` ve `GetAllStrings` geçmiyor (grep 0). Golden Slim `_IndexL10n.cshtml:9-13` açık anahtar listesi (`Localizer["…"]`), doğrulayıcı köprü kontrolleri yine PASS. UIC-7 dosya sayısı dil kümesini kanıtlamaz. |
| F-3 ENGEL · UIC-24 zorunluyu isteğe bağlı yapıyor | **Kabul** (ColReorder dahil) | `frontend-datatable-template.md:24` "Save View CTA (ZORUNLU)"; `:107-129` dışa aktarma = ekran (sahip kararı 2026-09-24); `:192` ve `frontend-js-standard.md:74-75` colReorder "her zaman eklenir". |
| F-4 ORTA · DL-2 zorunlu kuralları eksik kapsıyor | **Kabul** | `frontend-form-template.md:21-31` (her alan `.diten-field` + ikon, tarih ikonu açar); `frontend-standards.md:165-166` (her kontrole bas); `frontend-datatable-template.md:630-631` (icon-only title+aria-label, filtre aria-controls/aria-expanded); `page-help-and-keyboard.md:16, :105-118` (geri alınamaz işlem yalnız Ctrl/⌘+Enter, `data-dt-primary="chord"`). |
| F-5 ORTA · çatışma kaydı kararı belirlemiyor | **Kabul** | premium-modal §2 ve FG-003 aynı katman (`.antigravity/rules`); "kural kazanır" seçmez. `frontend-js-standard.md:53` 7.1 "Tercih edilen yol: createCrudTable" ↔ `:981` anti-pattern tablosu ve BL-440 kararı (createList). Kural dosyası eskimiş; kural sahibi kararı (D-8). |
| F-6 ORTA · 15 ayrı koşu dizini akışı çalışmaz | **Kabul** | `run_record.py:79-85` `status_paths()` `--untracked-files=all`; `:121-123` kirli ağaçta reddeder; `:137-139` koşu dışı değişikliği reddeder. İlk koşunun izlenmeyen kaydı ikinci koşuyu durdurur. |
| F-7 ORTA · AC-13 sabotajı araçla uyuşmuyor | **Kabul** | `verify_datatable_page.py:937-949` kökü alıp `frontend/Diten.Web/…` yollarını kendisi kurar; `:535-536` `createList` çağrısı yoksa `resolve_list_factory` boş döner, `[factory]` sapması üretilmez; baseline zaten çıkış 1 (`excerpts/datatable-verifier-golden-slim.txt`). |
| F-8 ORTA · AC-8 küresel çıkış 0 başka paketlere bağlı | **Kabul** | `excerpts/nav-keys.txt`: 2 yeni off-form anahtar MOD-0176:645 ve MOD-0177:915 (SCE paketleri, P1 dışı, XMC-001). |

## İtirazlar
- Yok. Önceki yanıtımdaki olası itiraz (ColReorder'ın zorunluluğu) ölçümle düştü: `frontend-js-standard.md:74-75`.

## İncelemenin kaçırdığı, CT ekliyor
- **C-1:** UIC-17 (Ctrl+K kaydı) platform maddesidir; AC-5'in fixture'ı Golden Slim (kiracı kabuğu) bu maddeyi ölçemez; platform fixture'ı adlandırılmalı (aday `Platform/Tenants`, `platform-search.en.json` kaydı var mı ölçülecek).
- **C-2:** "elle: inceleme kaydı" maddelerinde imzalayan rol yazılı değil (CT mi, bağımsız inceleyici mi); `manual.md`'de UIC başına rol + tarih satırı tanımlanmalı.
- **C-3:** Codex beyanı (model/effort/oturum) eksik; `reviews.tsv` satırı için sahip kararı gerekir (yukarıda).

## Sonraki adım (CT)
Revizyon 02'yi `module-pack-author` yazar (F-1…F-8 + C-1/C-2), aynı dosya, commit yok; sonra bulgu dosyası + `review-row` (sahip onayı) ve REVIEW-REQUEST-02.
