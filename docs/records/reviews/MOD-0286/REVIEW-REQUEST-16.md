# MOD-0286 — bağımsız inceleme isteği 16 (SOP §20.4 / CT-APR-02; rev 15, DL-3 tek seviye sayfalar)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `5f678d2a377809133d078a97222041ca20fc5a21c7c5b6bcb82a29e0c0d94494` (revizyon 15)
- **Kaynak commit:** `fcd5b7f63` · dal `chore/pss/mod0286-rev15-kor-nokta-01` (yerel; push edilmedi) · taban `origin/main` 7ac15454e
- **Risk:** paket onaydan sonra değişti (`pack_approval.py check`: 175 satır, yeniden onay gerekli); DL-3 CI kapsamını genişletir — inceleme gerekli
- **Önceki sürüm ve incelemeler:** rev 14 `87f46dcf…` — inceleme 14 UYGUN (`2026-10-27-87f46dcf.md`), main'de onaylı (PR #266). Rev 15 için S-93 ön incelemesi 1 tur (ENGEL'siz, kayıt değildir); bulguları yazar düzeltti.
- **Bu sürümün nedeni:** DL-3 seçicisi yalnız `Views/{Area}/{Module}/` sayfalarını görüyor; tek seviye `Views/{Module}/` altındaki 162 görünüm ve 87 JS dosyası hiçbir CI kontrolüne girmiyor (CT ölçümü 2026-10-29, origin/main 7ac15454e). Paket bunu yanlış anlatıyordu.
- **Yetki:** sahibin sözü "kör nokta" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PSS-UI Control Tower oturumu · 2026-10-29. Paketi `module-pack-author` yazdı (beyan effort 15, doğrulanamadı; uygulama oturumu high).
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-review-16-fcd5b7f63.zip` · sha256 `ba119a99116b50b608a8a731071a52da71d8f81b4ce1bc41342a55e04d178f9d` · 270 325 bayt · 16 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-16/MOD-0286-review-16-fcd5b7f63.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-5f678d2a.md`; yeni bulgular F-25'ten
- **Prompt bloğunun SHA-256'sı:** `bd573dffc28a5dc40bc2e271f8c34aa4327be86a08361bef7841f3133a8f4c89`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 16 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0286-review-16-fcd5b7f63.zip · sha256 ba119a99116b50b608a8a731071a52da71d8f81b4ce1bc41342a55e04d178f9d · önce açtığın klasörde `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 5f678d2a377809133d078a97222041ca20fc5a21c7c5b6bcb82a29e0c0d94494 (revizyon 15) — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Yeniden inceleme. Ne değişti (rev 14 → rev 15; rev 14 main'de onaylı, inceleme 14 kaydı 04-baglam altında): DL-3 eşlemesine tek seviye sayfa `Views/{Module}/` eklendi; CI tablosunun kapsam hücreleri, DL-3 dışarıda bırakılanlar cümlesi, yeni "DL-3 tek seviye sayfalar (rev 15)" bölümü, AC-19 istisnası, yeni AC-21, FU-14 tanımı, FU-16/17/18, Validation Rules, Failure Path, Repo Scope, Test Expectations, Ready-for-dev ve Implementation Notes "Rev 15" değişti. Kapandı iddiasındaki önceki bulgu yok. Her iddiayı kendin ölç.
Görev: paketin bu revizyonunu SOP §20.4'e göre incele: (1) yeni eşleme `04-baglam/scripts/uic_changed_pages.py` ile ve 05 listeleriyle ölçülebilir ve doğru mu (sayılar, klasörler, dışarıda kalanlar); (2) AC-21 ve FU-16/17/18 ölçülebilir, sıralı ve paketin geri kalanıyla (AC-17…AC-20, CI tablosu, taban kuralları, korunan yollar) çelişkisiz mi; (3) revizyon başka bir şeyi (künye, onay alanları, dilim tablosu, desen bloğu, UIC/RC) değiştirdi mi; (4) kod ya da korunan yol değişikliği gerektiren her şey sahip onayı ve ayrı iş olarak mı yazılmış.
Çıktı: ilk satır `hedef: <paket yolu> @ 5f678d2a`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, paket satırı ve dayanak dosya:satır> | açık | - | 2026-10-29`; bulgular F-25'ten başlar; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Yanıtı Türkçe ver.
```
