# MOD-0023 — bağımsız inceleme isteği, §17 revizyon 3 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md`, sha256
  `3e802d2f7e2511055eef4e976ebd4950f45a8cdec03fe7e5fbfd1143769a9271` (§17 revizyon 3, yeni bölüm §17.9).
- **Kaynak commit:** `c1aae5487`. Dal `chore/ct/pss-wf-ct-01`, yerel, push edilmedi.
- **Önceki onaylı sürüm:** revizyon 2, sha256 `803fbaa5…` (`execution/approvals/approvals.tsv`, 2026-10-26, yalnız §17). Son
  inceleme `docs/records/reviews/MOD-0023/2026-10-26-803fbaa5.md`: UYGUN, açık engel 0.
- **Risk:** HIGH. Onay kartı (`excerpts/ct-pack-approval-card.txt`) "ilk onay — önceki onaylı sürüm yok" diyor: bu, ana daldaki
  `approvals.tsv` güvenilmez sonucu yüzündendir (`pack_approval.py check` çıkış 1, #241); revizyon 2'nin onay satırı main'de var.
- **Bu sürümün nedeni:** salt okunur kod incelemesi `docs/records/audits/2026-10/mod0023-s17-inspection-01/INSPECTION.md`
  (@f1303a9d8, ölçüm @4c08fc75a) §17'de yanlış ve eksik yerler buldu: DL-1'in izinli yolları `includeDeleted` için yetmiyor;
  DL-2'de hata kodu adı, iptal tekrarı, AC-W8 kaybeden kodu, güvenilir koordinatörler ve Platform içi tüketiciler.
- **Yetki:** sahibin sözleri `rev3-yaz` ve `gecmis-404` (O-S17-2 = (b)), CT · İş akışı sohbeti, 2026-10-29.
- **Yazan:** `module-pack-author` alt ajanı (model opus, effort high); O-S17-2 kararını CT işledi. **Hazırlayan / isteyen:** CT · İş
  akışı oturumu; isteyen depo sahibi (rol).
- **CT ölçümü:** fark yalnız §17 durum satırları, yeni §17.9 ve "Teslim dilimleri" tablosunda
  (`excerpts/MOD-0023-pack-diff-803fbaa5-to-3e802d2f.diff`); §1–§16 ve §17.1–17.8 gövdesi aynı. Dilim tablosu iki dilim okuyor;
  "kapsadığı bölümler" hücreleri başlık adlarıyla aynı. Yazarın kendi eklediği iki kod bilgisi CT tarafından okundu
  (`TrustedWorkflowCancellationCoordinator.cs:362`, `WorkflowDefinitionsController.cs:104-129`). Derleme, test yok.
- **İnceleyen:** yeni bir Codex oturumu. İstenen: en güçlü model, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0023-review-rev3-c1aae5487.zip`, sha256 `34c485cf504ce43766954ac9ba4080984636739f4e6d214370a53a586160e916`,
  189 175 bayt; 27 dosya ve `MANIFEST.sha256`. Yeri (S-29):
  `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3/`. Kaynak dosyalar
  `git archive c1aae5487` ile bayt bayt aynı; e-posta taramasında bulgu yok.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-3e802d2f.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0023 module pack §17 revizyon 3 bağımsız incelemesi (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0023-review-rev3-c1aae5487.zip · sha256 34c485cf504ce43766954ac9ba4080984636739f4e6d214370a53a586160e916 · kaynak commit c1aae5487 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 3e802d2f7e2511055eef4e976ebd4950f45a8cdec03fe7e5fbfd1143769a9271 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (3) §17'ye yalnız §17.9'u ekliyor, §17 durum satırlarını ve "Teslim dilimleri" tablosunu güncelliyor. Kaynağı salt okunur kod incelemesi INSPECTION.md (ölçüm @4c08fc75a). Onaylı revizyon 2 (803fbaa5) excerpts/ içinde; fark excerpts/MOD-0023-pack-diff-803fbaa5-to-3e802d2f.diff.

Önce oku (hepsi ZIP'te): hedef paket §17 (17.1–17.9) ve Teslim dilimleri; fark dosyası; INSPECTION.md; önceki inceleme 2026-10-26-803fbaa5.md; treasury-intake-owner-decisions-02.md (D21, D22, D25, D32); kart (control-tower-operating-card.md) S-85 ve E1–E5 tablosu; services/** altındaki kaynak dosyalar.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç (dosya:satır).

İnceleme derinliği:
- §17.9'daki her kod bilgisi ZIP'teki kaynakla aynı mı (ExecutionFilter, by-objects ve history işleyicileri, WORKFLOW_ACTOR_DENIED / WORKFLOW_ACTOR_MISMATCH ayrımı, iptal tekrarının aktöre bağlı olmaması, AC-W8 kaybeden kodu, güvenilir koordinatörlerin kodları, Timesheet'in 200 beklentisi)?
- DL-1 izinli yolları: O-S17-2 = (b) kararıyla tutarlı mı (history'ye includeDeleted yok; tek yeni silinmişi-içeren okuma by-objects için)? §17.9/2'deki eski seçenek metni (history sorgu kaydı, history eylemi) (b) kararıyla çelişiyor mu, yazara bırakılmış bir belirsizlik var mı? Domain arayüzüne yalnız okuma üyesi, silme üyesi yok, Platform.Common dokunulmaz kuralı korunuyor mu?
- DL-2: AC-W5…W8, W14, W17, W18'in yeni metni ölçülebilir mi, birbirleriyle ve D25/3 ile çelişiyor mu? "Farklı aktör 403 kalır, farklı gövde 409" okuması ve 409'un WORKFLOW_TRANSITION_CONFLICT'ten ayrı bir kod olması önerisi tutarlı mı (§17.7/6 tüketici kuralı)? Güvenilir koordinatörlerin kapsam dışı bırakılması bir boşluk yaratıyor mu?
- AC-W17 tüketici listesi kodla eksiksiz mi (Tasks, Timesheet, WorkCenter dağıtıcısı, Web'in düzenlenebilir anahtarı)?
- Teslim dilimleri: "kapsadığı bölümler" hücreleri başlık adlarıyla birebir aynı mı; E4 seviyesi kartın E1–E5 tablosuna göre gerekçeli mi; bitiş listeleri (AC-W18 dahil) tam mı?
- Değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi? Kapanmamış bir konu kapanmış gösterilmiş mi (O-S17-1 açık kalmalı)?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0023 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ 3e802d2f7e2511055eef4e976ebd4950f45a8cdec03fe7e5fbfd1143769a9271
değerlendirilen olumsuz incelemeler: 470882d1
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa yalnız "Bulgu yok." yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3/MOD-0023-review-rev3-c1aae5487.zip
sha256 34c485cf504ce43766954ac9ba4080984636739f4e6d214370a53a586160e916
```
