# MOD-0230 — bağımsız inceleme isteği 10 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `c236cb87d47f5e821d8a67b168656656d2af55502ac48861ccc26344332c7232` (revizyon 10).
- **Kaynak commit:** `7028e75c4`. Paket değişikliği `770472c0b`, karar kaydı `15e5975d2`. Dal `chore/ct/pvg-ct-01`; yerel, push edilmedi.
- **Risk:** Onay kartının hesapladığı risk LOW. Değişen satırlar muaf satırlardan olmadığı için bağımsız inceleme yine gerekiyor (POLICY-REVIEW-START).
- **Önceki onaylı sürüm:** revizyon 09 (`7dd6a8da…`). İnceleme 09 sonucu UYGUN; onay satırı main'de (#220).
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Bu sürümün nedeni:** Kabul kaydının `required_evidence_source` alanı, onaylı bir paket satırını göstermeli. O satır, gerekli kanıt seviyesini tek başına söylemeli (`.antigravity/rules/status-reporting-and-evidence.md:131`). Pakette böyle bir satır yoktu.
- **Yetki:**
  - CT-Setup'ın cevabı (sahip onayıyla): SOP §23.1 alt sınırları build/test kapısında da geçerli; dilim tablosunda dilim başına tek seviyeli bir sütun olacak.
  - Karar kaydı: `docs/records/decisions/2026-10/pvg-mod0230-kanit-seviyesi-01.md`.
- **İddia:** Değişiklik üç yerde, başka yerde değişiklik yok:
  1. Dilim tablosuna `gerekli kanıt` sütunu eklendi. Değerler:
     - DL-1 E1
     - DL-2 E4
     - DL-3 E3
     - DL-4 E4
     - DL-5 E5
     - DL-6 E5
     - DL-7 E4
     - DL-8 E3
     - DL-9 E5
  2. Tablonun altına bir açıklama cümlesi eklendi.
  3. Paketin başına revizyon 10 notu eklendi.
  
  Ölçütler, kararlar ve kapsam değişmedi.
- **İsteyen:** depo sahibi (rol). **Hazırlayan:** PVG Control Tower oturumu. **Tarih:** 2026-10-26.
- **İnceleyen:** yeni bir Codex oturumu. Oturum kimliği yanıtın başına yazılmalı. İstenen model Codex'in en güçlü modeli, effort high. Gerçekte kullanılan değerler ölçülüp `reviews.tsv` satırına yazılır.
- **ZIP:** `MOD-0230-review-10-7028e75c4.zip` · sha256 `03cf9690bb7c62ad6c5911f81477dbb1d543cccf581394c5276987d1831651af` · 328 778 bayt · 22 dosya + `MANIFEST.sha256`.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-10/`. Git bu klasörü izlemez, depoya girmez; ZIP'i sahip iletir.
  - 17 kaynak dosya `git archive 7028e75c4` ile bayt bayt aynı.
  - E-posta taramasında yalnız test değerleri çıktı.
  - `excerpts/` içinde:
    - revizyon 09'un tam metni;
    - iki sürüm arasındaki diff (`MOD-0230-pack-diff-7dd6a8da-to-c236cb87.diff`);
    - onay kartı;
    - SOP §23.1 ve kabul kuralının alıntısı;
    - CT'nin satır başına seviye ve ayrıştırıcı denetimi.
- **Beyanlar:**
  - Revizyon 10'u `module-pack-author` alt ajanı yazdı. Beyan ettiği effort 15; uygulamanın ölçtüğü oturum effort'u high.
  - CT'nin kendi ölçümü:
    - Her DL satırında tam bir seviye belirteci var.
    - Notta ve açıklama cümlesinde seviye belirteci yok.
    - `slices` DL-1…DL-9'u okuyor.
    - `parse_pack` 33 zorunlu ölçüt buluyor, sorun yok.
  - DL-2, DL-3 ve DL-8'in seviyeleri CT-Setup'ın cevabından geliyor. Öteki altı seviye, CT'nin SOP §23.1'den yaptığı öneri; dayanakları karar kaydındaki tabloda.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-c236cb87.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.
- **Kayıt adımları:** İstek 08 ve 09'daki gibi.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 10 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-10-7028e75c4.zip · sha256 03cf9690bb7c62ad6c5911f81477dbb1d543cccf581394c5276987d1831651af · kaynak commit 7028e75c4 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 c236cb87d47f5e821d8a67b168656656d2af55502ac48861ccc26344332c7232 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (10) yalnız şunu ekliyor: teslim dilimleri tablosunda her dilim satırına tek seviyeli bir "gerekli kanıt" sütunu (DL-1 E1, DL-2 E4, DL-3 E3, DL-4 E4, DL-5 E5, DL-6 E5, DL-7 E4, DL-8 E3, DL-9 E5), tablonun altına bir açıklama cümlesi ve revizyon 10 notu. Amaç: kabul kaydının required_evidence_source alanının, seviyeyi tek başına söyleyen onaylı bir paket satırını gösterebilmesi. Revizyon 09 (7dd6a8da) inceleme 09'da UYGUN aldı ve onaylı.

Önce oku (hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-7dd6a8da-to-c236cb87.diff; excerpts/MOD-0230-pack-rev09-version-7dd6a8da.md; docs/records/decisions/2026-10/pvg-mod0230-kanit-seviyesi-01.md (bu sürümün tek yetkisi); excerpts/control-tower-sop-23.1-and-acceptance-excerpt.txt; .antigravity/rules/status-reporting-and-evidence.md (satır 105-135); scripts/acceptance_record.py (check_record: required_evidence_source, satır 546-597); scripts/pack_approval.py (slices); docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md; inceleme 09 bulguları ve karşılaştırma notu 09; REVIEW-REQUEST-09.md; execution/approvals/*; .antigravity/rules/module-pack-standard.md (satır 283, 520-535); excerpts/ altındaki bütün dosyalar.
Kod koşamazsın; iddiaları ZIP'teki metne ve araç koduna karşı statik olarak kendin ölç.

İnceleme derinliği:
- Değişiklik yalnız iddia edilen üç yerde mi? Künye, ölçütler (AC-1…AC-33), kararlar ve diğer bölümler bayt bayt aynı mı?
- Her DL satırında tam bir \bE[0-5]\b belirteci var mı ve değer karar kaydıyla aynı mı? Not ve açıklama cümlesinde seviye belirteci var mı (varsa acceptance_record'un "kaynak satırı birden çok seviye söylüyor" kuralını bozar mı)? Kabul aracı bu satırı required_evidence_source olarak kullanabilir mi (status ready-for-dev, kimlik eşleşmesi, tek seviye)?
- Seviyelerin dayanağı: DL-2 E4, DL-3 E3, DL-8 E3 CT-Setup cevabından; DL-1 E1, DL-4 E4, DL-5 E5, DL-6 E5, DL-7 E4, DL-9 E5 CT'nin SOP §23.1'den önerisi. Her biri SOP §23.1 alt sınırına uyuyor mu, ölçülemez ya da çelişkili bir seviye var mı (ör. bir dilimin AC'leri o seviyeyle hiç ölçülemiyor mu)?
- Dilim tablosu hâlâ pack_approval.slices kurallarına uyuyor mu (4. sütun, her hücre bölümü paketin başlığı)?
- Gerileme: inceleme 09'un UYGUN sonucunun dayandığı hiçbir metin değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ c236cb87d47f5e821d8a67b168656656d2af55502ac48861ccc26344332c7232
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-10/MOD-0230-review-10-7028e75c4.zip
sha256 03cf9690bb7c62ad6c5911f81477dbb1d543cccf581394c5276987d1831651af
```
