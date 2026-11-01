# MOD-0230 — bağımsız inceleme isteği 11 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, sha256 `918dd66020a266ce02de6b1d690ec0efa251acf27747f2925ec7bdbfe4d3081f` (revizyon 11).
- **Kaynak commit:** `22b92aff3`. Dal `chore/ct/pvg-ct-01`; yerel, push edilmedi.
- **Risk:** Onay kartı riski HIGH hesaplıyor. Nedeni, "Tenant Isolation Rules" ile "Authorization Convention" bölümlerindeki değişiklikler ve bir korunan yol anması. Bu yüzden bağımsız inceleme gerekiyor.
- **Önceki onaylı sürüm:** revizyon 10 (`c236cb87…`). İnceleme 10 sonucu UYGUN; onay satırı `main`'de (#223).
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Bu sürümün nedeni:** Sahip 2026-10-27'de dört açık kararın bekleyen kısmını onayladı. Paket hâlâ bu kararları "bekliyor" diye gösteriyordu.
- **Yetki:** sahibin karar kayıtları (hepsi ZIP'te):
  - `pvg-mod0230-mimari-sahibi-01.md` — Enterprise Architecture kararlarını sahip verir ("mimari-ben").
  - `pvg-mod0230-od-pvg-1-izin-01.md` — OD-PVG-1 ("izin-evet"; sahip MOD-0018 / Auth sahibi sıfatıyla; PKS-001 §4'e `pvg.*` satırı eklendi).
  - `pvg-mod0230-od-pvg-2-onay-01.md` — OD-PVG-2 ("kimlik-evet").
  - `pvg-mod0230-od-pvg-6-bayrak-01.md` — OD-PVG-6 ("bayrak-evet").
  - `pvg-mod0230-od-pvg-7-port-01.md` ve `-port-02.md` — OD-PVG-7 ("port-evet"). §3 tablo satırı, AGENTS.md boyut testi yüzünden bekliyor.
- **İddia:**
  - Yalnız ön koşul ve karar durumu metinleri değişti. Değişen yerler: Open Decisions tablosu, AC-2, AC-4, AC-5 ve AC-10'un ön koşul ifadeleri, DL-3, DL-4 ve DL-6'nın ön koşulları, G-3 ile G-13'e eklenen "karar onaylandı, uygulama DL-x'te" notları, Authorization Convention, port ve kiracı metinleri ile revizyon 11 notu.
  - Hiçbir ölçütün neyi ölçtüğü değişmedi. Dilim, yol ve kapsam aynı. Hiçbir build-gate açığı kapatılmadı. AC sayısı 33; hepsi tek satırlı ve zorunlu.
- **CT ölçümü** (`excerpts/ct-parser-and-slice-check.txt`):
  - `parse_pack`: iki sürümde de 33 zorunlu ölçüt, sorun yok.
  - Metni değişen AC satırları yalnız AC-2, AC-4, AC-5 ve AC-10.
  - `slices` DL-1…DL-9; `gerekli kanıt` sütunu revizyon 10 ile aynı.
- **İsteyen:** depo sahibi (rol). **Hazırlayan:** PVG Control Tower oturumu. Revizyon 11'i `module-pack-author` alt ajanı yazdı; uygulamanın ölçtüğü effort high. **Tarih:** 2026-10-27.
- **İnceleyen:** yeni bir Codex oturumu. Yanıtın başına oturum kimliği, model ve effort yazılır. İstenen: Codex'in en güçlü modeli, effort high.
- **ZIP:** `MOD-0230-review-11-22b92aff3.zip`, sha256 `13fb78fe39fb6b23e3a980e920d38242ac5961287594af8a7123c13b10cad8d2`, 374 861 bayt. İçinde 31 dosya ve `MANIFEST.sha256`.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-11/`. Git bu klasörü izlemez; ZIP'i sahip iletir.
  - Kaynak dosyalar `git archive 22b92aff3` ile bayt bayt aynı.
  - E-posta taramasında sonuç çıkmadı.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-918dd660.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 11 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-11-22b92aff3.zip · sha256 13fb78fe39fb6b23e3a980e920d38242ac5961287594af8a7123c13b10cad8d2 · kaynak commit 22b92aff3 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 918dd66020a266ce02de6b1d690ec0efa251acf27747f2925ec7bdbfe4d3081f — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (11) sahibin 2026-10-27'de onayladığı dört açık kararı pakete işliyor: OD-PVG-1 (izin anahtarı ailesi pvg.case-intake-triage.* ve pvg.* ad alanı), OD-PVG-2 (servis JWT'yi doğrular; kiracı X-Tenant-Id = token TenantId; X-Diten-* yok sayılır), OD-PVG-6 (bayrak açıkken Pvg:Runtime:ApprovalReference zorunlu; CI referansın depodaki onay kayıtlarında olduğunu ölçer), OD-PVG-7 (5011 PvgService'in; web-verify 5002; AGENTS.md §3 satırı boyut testi yüzünden bekliyor). Sahip Enterprise Architecture kararlarını kendisi veriyor (MIMARI-01) ve MOD-0018 / Auth sahibi. İddia: yalnız ön koşul ve karar durumu metinleri değişti; hiçbir AC'nin ölçtüğü şey, dilim, yol, kapsam ya da gerekli kanıt değişmedi; hiçbir build-gate açığı kapatılmadı. Revizyon 10 (c236cb87) inceleme 10'da UYGUN aldı ve onaylı.

Önce oku (hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-c236cb87-to-918dd660.diff; excerpts/MOD-0230-pack-rev10-version-c236cb87.md; docs/records/decisions/2026-10/ altındaki altı karar kaydı (mimari-sahibi-01, od-pvg-1-izin-01, od-pvg-2-onay-01, od-pvg-6-bayrak-01, od-pvg-7-port-01, od-pvg-7-port-02) ve pvg-mod0230-open-decisions-01.md; .antigravity/rules/permission-key-standard.md (§4 tablosu, pvg.* satırı), security-jwt.md, multi-tenancy.md, ports.md, module-pack-standard.md; AGENTS.md §3 ve §4; .claude/launch.json; gateway/Diten.ApiGateway/Program.cs (CORS); scripts/acceptance_record.py (parse_pack), scripts/pack_approval.py (slices, card); excerpts/pack-approval-card.txt; excerpts/ct-parser-and-slice-check.txt; inceleme 10 bulguları, karşılaştırma notu 10 ve REVIEW-REQUEST-10.md; execution/approvals/*.
Kod koşamazsın; iddiaları ZIP'teki metne ve araç koduna karşı statik olarak kendin ölç.

İnceleme derinliği:
- Diff'teki her değişiklik bir karar kaydına dayanıyor mu? Kayıtta olmayan bir karar ya da kayıttan geniş bir ifade pakete girmiş mi (ör. OD-PVG-6'nın bağ biçimi, OD-PVG-7'nin tablo satırı "yazıldı" gibi gösterilmiş mi)?
- AC-1…AC-33: AC-2, AC-4, AC-5, AC-10 dışında hiçbir AC satırı bayt bayt değişmiş mi? Bu dört satırda yalnız ön koşul mu değişti, ölçülen şey aynı mı? Her satır hâlâ `- [ ] AC-<n> (zorunlu) — …` tek satır biçiminde mi?
- Dilim tablosu ve `gerekli kanıt` sütunu revizyon 10 ile aynı mı; DL-3/DL-4/DL-6 ön koşul metinleri kararlara uygun mu; DL-3'ün yolları AC-10'un CI denetimini taşıyabiliyor mu (ölçüt ile yol tutarsızlığı var mı)?
- Kapanmamış konular yanlışlıkla kapanmış gösterilmiş mi (OD-PVG-3, OD-PVG-9, QPPV, MOD-0290/0150/0288/0048 sözleşmeleri, rol matrisi, Required Approval Gates)?
- Paketteki port, kiracı ve yetki metinleri AGENTS.md, PKS-001, security-jwt ve multi-tenancy ile çelişiyor mu?
- Gerileme: inceleme 10'un UYGUN sonucunun dayandığı bir metin değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 918dd66020a266ce02de6b1d690ec0efa251acf27747f2925ec7bdbfe4d3081f
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-11/MOD-0230-review-11-22b92aff3.zip
sha256 13fb78fe39fb6b23e3a980e920d38242ac5961287594af8a7123c13b10cad8d2
```
