# MOD-0230 — bağımsız inceleme isteği 16 (SOP §20.4 / CT-APR-02)

- **Hedef 1:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `61c7902fb48a5c178de3895d89c68dbbcbbd770868d787d4007da36257850279` (revizyon 14, inceleme 15 düzeltmesiyle).
- **Hedef 2:** `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` @ sha256 `3dfa4c474c013d65e9ce4498372857564a7d26b2ea6bf6d71b15a99786758e37`.
- **Kaynak commit:** `164298436`. Dal `docs/pvg/mod0230-rev14`, yerel, push edilmedi. Önceki commit'ler `9a7a6df30` (inceleme 15), `86de20e25` (inceleme 15 düzeltmesi) ve `164298436` (ön inceleme notları).
- **Risk:** İki kart da HIGH hesaplıyor (paket `35963167…`, DCP-004 `7a1dc197…`). `pack_approval.py check --ref 164298436` sonuçları:
  - paket: "onaydan sonra değişti, yeniden onay gerekli";
  - DCP-004: "onay yok, onay satırı gerekli".
- **Önceki sürüm ve incelemeler:** İnceleme 15 (istek `REVIEW-REQUEST-15.md`, hedefler `985e5755` ve `b8d838f6`) ikisine de KOŞULLU verdi. Bulgular: paket F-1 ve F-2 (ORTA), DCP-004 F-1 (ORTA). Bulgu dosyaları `docs/records/reviews/MOD-0230/2026-10-30-985e5755.md` ve `docs/records/reviews/DCP-004/2026-10-30-b8d838f6.md`. Paket revizyon 13 (`ee175d00…`) `main`'de onaylı.
- **Bu sürümün nedeni:** İnceleme 15'in üç bulgusunu kapatan düzeltme. Revizyon numarası değişmedi; paket notuna düzeltme satırları eklendi. Ön inceleme (S-93, yeni alt ajan, yalnız bu prompt'un ilk satırı değiştirilmiş hâliyle, sha256 `29ab0c2c…` sürümüne) engel vermedi. İki DÜŞÜK notu kapatıldı; yeni commit ve ZIP alındı.
- **Yetki:** sahibin sözleri "Mongo'yu aç (Önerilen)", "Benim onayım yeter", "Hepsini kabul (Önerilen)" (2026-10-29).
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PVG Control Tower · 2026-10-30.
- **İnceleyen:** yeni bir Codex oturumu, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0230-review-16-164298436.zip`, sha256 `68cac5293212bbafc5f362465f05e2b2ee2d139e39035899772717f380fa3c85`, 674 500 bayt. İçinde 67 dosya ve `MANIFEST.sha256` var.
  - İnceleme isteği dosyası ZIP'te değil (S-98).
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-16/` (depoya girmez).
  - Aynı klasördeki `MOD-0230-review-16-86de20e25.zip` kullanılmadı (ön inceleme o sürüme yapıldı).
  - E-posta taraması: DCP-004 başlığındaki sahip adresi (depoda zaten var) ve testlerin `@example.test` / `@example.invalid` adresleri.
- **Bulgu dosyaları:**
  - `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-61c7902f.md`
  - `docs/records/reviews/DCP-004/<YYYY-AA-GG>-3dfa4c47.md`
- **Prompt bloğunun SHA-256'sı:** `77376ce56e2df98d6b44a2f1442064deabda92d035450ca617537b22d2cdfdea` (blok içeriği, son satır sonu dahil).

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack revizyon 14 ve DCP-004 bağımsız incelemesi 16 (SOP §20.4 / CT-APR-02). İnceleyensin; belgeleri yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki iki bulgu bloğudur.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula. Kaynak commit 164298436 (sahip beyanı).
Hedef 1: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 61c7902fb48a5c178de3895d89c68dbbcbbd770868d787d4007da36257850279
Hedef 2: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ sha256 3dfa4c474c013d65e9ce4498372857564a7d26b2ea6bf6d71b15a99786758e37
Biri tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz (istenen effort: high).

Yeniden inceleme. İnceleme 15 (hedefler 985e5755 ve b8d838f6) ikisine de KOŞULLU verdi; bulgu dosyaları ZIP'te (docs/records/reviews/MOD-0230/2026-10-30-985e5755.md, docs/records/reviews/DCP-004/2026-10-30-b8d838f6.md).
Ne değişti (yalnız bunlar; revizyon numarası değişmedi, paket notuna düzeltme satırı eklendi): paket AC-35 (c2) aktör korumaları ve "Build/Test Persistence (Mongo Store)" alanlar maddesi; paket AC-35 (e) commit hatası senaryosu (değiştirme testi, iki testin sırası, fail point'in kapatılması ve commit sayımının başladığı an); DCP-004 §8 madde 5, §10 "Audit trail gate" alt bölümü (MOD-0231/0232/0234 için yapı/test kapısının değişmediği dahil) ve §20 notu. domain-config değişmedi. Farklar: excerpts/*-correction-15.diff; inceleme 15'teki sürümler excerpts/*-reviewed-in-15-*.md.
Kapandı iddiasındaki önceki bulgular: MOD-0230 F-1, F-2; DCP-004 F-1. Her iddiayı kendin ölç; kapanmadıysa yeni F satırı yaz.
Görev: (1) üç bulgunun kapanışı; (2) düzeltmenin yeni bir sorun getirip getirmediği (yeni testlerin ve sabotajların uygulanabilirliği ve yalnız DL-10 izinli yollarına dokunması; DCP-004 §10'un açık denetim kararlarını, MOD-0231/0232/0234 kapsamını ya da canlı kullanım kapısını önceden bağlayıp bağlamadığı; §8.1 dilim tablosunun okunur kalması); (3) inceleme 15'te bulgu almayan bölümler değişmediyse yeniden incelenmez, ama düzeltmeyle çelişirlerse yaz.
Önce oku (hepsi ZIP'te): iki hedef ve domain-config; excerpts/*-correction-15.diff ve *-reviewed-in-15-*.md; inceleme 15 bulgu dosyaları; excerpts/*-diff-*-to-*.diff (revizyon 13'e göre tam fark) ve *-previous-*.md; karar kayıtları kalici-depo-01/02/03; kurallar (module-pack-standard, capability-pack-standard, audit-trail-standard, mongo-indexing, entity-base-template, multi-tenancy, agents-core); PvgService kaynakları ve testleri; CentralAudit kaynakları; scripts/pack_approval.py, scripts/ct_route.py; excerpts/pack-approval-*.txt. excerpts/audit-slice-decision-note-DRAFT-not-normative.md yalnız bağlamdır.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz). İki blok, bu sırayla:
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 61c7902fb48a5c178de3895d89c68dbbcbbd770868d787d4007da36257850279
F-1 | ENGEL|ORTA|DÜŞÜK | <bulgu; satır numarası ve dayanak dosya> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

# DCP-004 — bağımsız inceleme
hedef: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ 3dfa4c474c013d65e9ce4498372857564a7d26b2ea6bf6d71b15a99786758e37
F-1 | … (aynı biçim; numaralama her blokta F-1'den başlar)
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

Kurallar: seviye adını bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin açıklama satırlarında | işareti kullanma; domain-config ile ilgili bulgu ilgili hedefin bloğuna yazılır.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-16/MOD-0230-review-16-164298436.zip
ZIP sha256: 68cac5293212bbafc5f362465f05e2b2ee2d139e39035899772717f380fa3c85
```
