# MOD-0023 — bağımsız inceleme isteği, §17 revizyon 3, tur 2 (son sürüm; SOP §20.4 / CT-APR-02)

> Bu istek `REVIEW-REQUEST-rev3-02.md`'nin yerine geçer: o istekteki ZIP (`f20655c5…`, paket `07253001…`) geçersizdir; paket o tarihten
> sonra sekiz ara kontrol turuyla değişti.

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md`, sha256
  `5512cbca0a3683cb24fc9675d9c9c8ccd3e289353ed3a123472d80a8c2bf67f5`.
- **Kaynak commit:** `7b7c384d2`. Dal `chore/ct/pss-wf-ct-01`, yerel, push edilmedi.
- **Önceki resmî tur:** `docs/records/reviews/MOD-0023/2026-10-29-3e802d2f.md` — UYGUN DEĞİL (F-1 ENGEL, F-2 ORTA, F-3 ORTA);
  reviews.tsv satırı yerel.
- **Ara kontroller (resmî değil; reviews.tsv'ye satır yok):** `docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-01…08.md`
  — taze bağlamlı alt ajan (model sonnet; yazar opus), Codex tur 2 metni. Sonuçlar: 02 UYGUN DEĞİL; 03…08 KOŞULLU; 07 ve 08 yalnız
  düşük bulgu. 08'in dört düşük bulgusu son yazar turunda düzeltildi; **düzeltmeden sonra ara kontrol koşulmadı.**
- **Açık sahip kararı:** O-S17-3 (yalnız Approve/Reject geç ikiz: (i) ret öncesi yeniden okuma — öneri; (ii) tüketici kuralı). Açık
  olması bulgu değildir; DL-2 prompt'unun kapısıdır. O-S17-1 (hukuk görüşü) açık; O-S17-2 = (b) kararlı (S17-2).
- **Yetki:** sahibin sözleri `rev3-yaz`, `gecmis-404`, `duzelt` (2026-10-29); sahip yokken ara kontrol yinelemesi Yönetici
  talimatıyla.
- **Risk:** HIGH. `pack_approval.py check` ana dalda onay dosyasını güvenilmez buluyor (#241; düzeltme #288, merge bekliyor) —
  `excerpts/ct-pack-approval-check.txt`.
- **İnceleyen:** yeni bir Codex oturumu; en güçlü model, effort high.
- **ZIP:** `MOD-0023-review-rev3-final-7b7c384d2.zip`, sha256
  `69187b2adda7ea13c5ce04758b8cd153236c999b31134a981ce631251f999635`, 588 234 bayt; 133 dosya ve `MANIFEST.sha256`. Yeri:
  `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3-final/`. Kaynaklar commit'lerden
  `git archive` ile bayt bayt alındı; e-posta taramasında yalnız test kurgusu `starter@diten.local`.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-5512cbca.md`.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0023 module pack §17 revizyon 3 bağımsız incelemesi, tur 2 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0023-review-rev3-final-7b7c384d2.zip · sha256 69187b2adda7ea13c5ce04758b8cd153236c999b31134a981ce631251f999635 · kaynak commit 7b7c384d2 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 5512cbca0a3683cb24fc9675d9c9c8ccd3e289353ed3a123472d80a8c2bf67f5 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önceki tur (docs/records/reviews/MOD-0023/2026-10-29-3e802d2f.md, ZIP'te) UYGUN DEĞİL dedi: F-1 AC-W16 by-objects 404 beklentisi koda aykırı; F-2 AC-W6/W7/W8 yarış yanıtları zamanlamadan bağımsız yazılmış; F-3 ZIP'te dayanak dosyalar eksik. O turdan sonra §17.9 düzeltildi ve sekiz ara kontrol (alt ajan, resmî değil; docs/records/audits/2026-10/mod0023-s17-inspection-01/ARA-KONTROL-01…08.md, ZIP'te) bulgularıyla genişletildi; dayanak dosyalar eklendi. Farklar: excerpts/MOD-0023-pack-diff-3e802d2f-to-5512cbca.diff (önceki resmî turdan), excerpts/MOD-0023-pack-diff-803fbaa5-to-5512cbca.diff (onaylı revizyon 2'den). Paket açık bir sahip kararı içerir (O-S17-3); açık olması tek başına bulgu değildir, ama seçeneklerin metni ve ölçütlerin ona bağlanışı tutarlı olmalıdır.

Önce oku (hepsi ZIP'te): hedef paket §17 (17.1–17.9) ve Teslim dilimleri; iki fark dosyası; önceki resmî tur bulguları; INSPECTION.md ve ara kontroller; REVIEW-REQUEST-rev3-01.md; execution/approvals/approvals.tsv; treasury-intake-owner-decisions-02.md (D21, D22, D25, D32); mod0023-s17-owner-decisions-01.md; kart (control-tower-operating-card.md) S-85 ve E1–E5 tablosu; services/**, frontend/** kaynakları ve excerpts/.
Kod koşamazsın; iddiaları ZIP'teki kaynaklara karşı statik olarak kendin ölç (dosya:satır).

İnceleme derinliği:
- F-1, F-2, F-3 kapandı mı? Her biri için kapanış ya da neden açık kaldığı, dosya:satır ile.
- §17.9'daki kontrol sırası ve yarış durumları (Approve, Reject, RequestInfo, Delegate; son adım ve ara adım; aynı ve farklı aktör; aynı ve farklı gövde; sıralı ve eşzamanlı yol; zorlanan dallarda satır sayısı) koda uyuyor mu; RequestInfo yinelenen anahtar eşlemesi uygulanabilir mi; mevcut testlerin kapsamı doğru anlatılmış mı; O-S17-3 seçenekleri kodla ve §17.7/6 ile tutarlı mı; O-S17-3'e bağlı ölçüt listesi her yerde aynı mı; izinli yollar doğru mu?
- AC-W2, AC-W11, AC-W14 ve AC-W16 birbirleriyle ve kodla tutarlı mı?
- DL-1 izinli yolları O-S17-2 = (b) ile tutarlı mı?
- Kişi alanı listesi ve sınırı entity dosyalarıyla eksiksiz mi?
- AC-W3, AC-W17 ve AC-W18 listeleri ZIP'teki dosyalarla eksiksiz mi?
- Durum paragrafı, kapanış ve bitiş listeleri tutarlı mı; Teslim dilimleri hücreleri başlık adlarıyla birebir aynı mı ve E4 alıntısı kartla birebir mi; değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter; durum paragrafı hariç) değişmiş mi; O-S17-1 açık mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0023 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ 5512cbca0a3683cb24fc9675d9c9c8ccd3e289353ed3a123472d80a8c2bf67f5
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
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3-final/MOD-0023-review-rev3-final-7b7c384d2.zip
sha256 69187b2adda7ea13c5ce04758b8cd153236c999b31134a981ce631251f999635
```
