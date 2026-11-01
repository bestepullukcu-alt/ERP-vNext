# MOD-0023 — bağımsız inceleme isteği, §17 revizyon 3, tur 2 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md`, sha256
  `0725300120ae8dfbbedd9e4b10a77eb5b954526ea7fdbaea3d2821057f3a6324`.
- **Kaynak commit:** `b8822a9cf`. Dal `chore/ct/pss-wf-ct-01`, yerel, push edilmedi.
- **Önceki tur:** `docs/records/reviews/MOD-0023/2026-10-29-3e802d2f.md` — UYGUN DEĞİL (F-1 ENGEL, F-2 ORTA, F-3 ORTA); satır
  `execution/approvals/reviews.tsv` (yerel). CT üç bulguyu da kodla karşılaştırdı ve kabul etti.
- **Bu turun değişiklikleri** (`excerpts/MOD-0023-pack-diff-3e802d2f-to-07253001.diff`): F-1 → §17.9 yeni madde 15, AC-W16
  yeniden yazıldı (by-objects: başka kiracının nesnesi boş liste, 200; history ve `instances/{id}`: 404); F-2 → §17.9 madde 9–10,
  AC-W6/W7/W8 zorlanan durumlara ayrıldı (ilk okumada tekrar, geç gelen ± expectedVersion, bariyerle örtüşme); CT notu → madde 2
  O-S17-2 = (b) ile tutarlı hale getirildi. F-3 → bu ZIP: kişi alanları için entity dosyaları, mevcut Workflow testleri,
  Tasks servisleri, Web kaynakları ve tüketici istemcileri ile testleri eklendi.
- **Yazarın kendi kararı (CT doğruladı):** bariyer testinde kaybeden 409 ya da 200 Replayed kabul edilir; gerekçe
  `WorkflowTerminalTransitionWriter.cs:348-354` açıklaması (gerçek Mongo'da yazma çakışması yeniden denemelerinden sonra
  "işlem kullanılamaz" yoluna düşülebiliyor; 2026-10-02 ölçümü, yeniden ölçülmedi).
- **Yetki:** sahibin sözü `duzelt` (CT · İş akışı sohbeti, 2026-10-29).
- **Yazan:** `module-pack-author` alt ajanı (model opus, effort high). **İsteyen:** depo sahibi (rol).
- **Risk:** HIGH (onay kartı `excerpts/ct-pack-approval-card.txt`; "önceki onaylı sürüm yok" satırı ana daldaki
  `approvals.tsv` güvenilmez sonucundandır, #241).
- **İnceleyen:** yeni bir Codex oturumu; en güçlü model, effort high.
- **ZIP:** `MOD-0023-review-rev3-r2-b8822a9cf.zip`, sha256 `f20655c5ef5c1092d367877edbebc0e55a6a0f759f2f588a5b62b915ab9a36e8`,
  455 476 bayt; 104 dosya ve `MANIFEST.sha256`. Yeri (S-29):
  `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3-r2/`. Kaynak dosyalar
  `git archive b8822a9cf` ile bayt bayt aynı. E-posta taraması: yalnız test kurgusu `starter@diten.local`
  (`WorkflowInstanceStartTests.cs:380`), gerçek kişi değil.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-07253001.md`.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0023 module pack §17 revizyon 3 bağımsız incelemesi, tur 2 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0023-review-rev3-r2-b8822a9cf.zip · sha256 f20655c5ef5c1092d367877edbebc0e55a6a0f759f2f588a5b62b915ab9a36e8 · kaynak commit b8822a9cf (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 0725300120ae8dfbbedd9e4b10a77eb5b954526ea7fdbaea3d2821057f3a6324 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önceki tur (docs/records/reviews/MOD-0023/2026-10-29-3e802d2f.md, ZIP'te) UYGUN DEĞİL dedi: F-1 AC-W16 by-objects 404 beklentisi koda aykırı; F-2 AC-W6/W7/W8 yarış yanıtları zamanlamadan bağımsız yazılmış; F-3 ZIP'te dayanak dosyalar eksik. Bu tur: §17.9 madde 15 (F-1), madde 9–10 (F-2), madde 2 O-S17-2 = (b) ile hizalandı; ZIP'e entity dosyaları, mevcut Workflow testleri, Tasks servisleri, Web kaynakları, Inventory / CRM / MDM istemcileri ve testleri eklendi (F-3). Farklar: excerpts/MOD-0023-pack-diff-3e802d2f-to-07253001.diff (bu tur), excerpts/MOD-0023-pack-diff-803fbaa5-to-07253001.diff (onaylı revizyon 2'den).

Önce oku (hepsi ZIP'te): hedef paket §17 (17.1–17.9) ve Teslim dilimleri; iki fark dosyası; önceki tur bulguları; INSPECTION.md; treasury-intake-owner-decisions-02.md (D21, D22, D25, D32); kart S-85 ve E1–E5 tablosu; services/** ve frontend/** kaynakları.
Kod koşamazsın; iddiaları ZIP'teki kaynaklara karşı statik olarak kendin ölç (dosya:satır).

İnceleme derinliği:
- F-1, F-2, F-3 kapandı mı? Her biri için kapanış ya da neden açık kaldığı, dosya:satır ile.
- §17.9 madde 9–10'daki kontrol sırası (ilk okuma tekrarı, görev okuma, expectedVersion, karar verilebilirlik, işlem içi yarış) WorkflowTaskTransitionSupport.cs ve WorkflowTerminalTransitionWriter.cs ile aynı mı; her durum için yazılan test zorlama yöntemi uygulanabilir mi (mevcut WorkflowCandidateDecisionMongoTests* ve WorkflowStartRecoveryMongoTests bariyer kalıpları)? Bariyer testinde 409 ya da 200 Replayed kabulü kodla tutarlı mı?
- Madde 2: DL-1 izinli yolları O-S17-2 = (b) ile tutarlı mı; history için includeDeleted kalıntısı kaldı mı; IWorkflowInstanceRepository dışındaki arayüzlere üye eklenmiyor mu?
- Kişi alanı listesi (§17.8/4 + §17.9) entity dosyalarıyla eksiksiz mi?
- AC-W3 ve AC-W17 tüketici listeleri ZIP'teki istemci ve test dosyalarıyla eksiksiz mi?
- Değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi; Teslim dilimleri hücreleri başlık adlarıyla aynı mı; O-S17-1 açık mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0023 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ 0725300120ae8dfbbedd9e4b10a77eb5b954526ea7fdbaea3d2821057f3a6324
değerlendirilen olumsuz incelemeler: 470882d1, 3e802d2f
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar (inceleme 2026-10-29-3e802d2f): <düz metin; satır başında F- kullanma>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa yalnız "Bulgu yok." yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3-r2/MOD-0023-review-rev3-r2-b8822a9cf.zip
sha256 f20655c5ef5c1092d367877edbebc0e55a6a0f759f2f588a5b62b915ab9a36e8
```
