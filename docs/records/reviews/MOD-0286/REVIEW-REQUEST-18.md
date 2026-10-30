# MOD-0286 — bağımsız inceleme isteği 18 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `0c53533e3e2434fbd8abe615f814d037c3c390b06625ee822358871958aed102` (revizyon 15)
- **Kaynak commit:** `9aba6fb95` (paket) + `d90cbe0d9` (inceleme 16 kaydı) · dal `chore/pss/mod0286-rev15-kor-nokta-01` (yerel; push edilmedi) · taban `origin/main` 7ac15454e
- **Risk:** paket onaydan sonra değişti (`pack_approval.py check`: yeniden onay gerekli); DL-3 CI kapsamını genişletir — inceleme gerekli
- **Önceki sürüm ve incelemeler:** rev 14 `87f46dcf…` inceleme 14 UYGUN, main'de onaylı; rev 15 `5f678d2a…` inceleme 16 KOŞULLU, 2 ORTA, ikisi de ZIP eksiği (`2026-10-29-5f678d2a.md`). İstek 17 (`REVIEW-REQUEST-17.md`, aynı hedef) Codex'e verilmeden bu istekle değiştirildi: kart ve şablon main'de güncellendi (tek kopya teslim, ZIP satırları prompt'un son iki satırı; ön inceleme bulguları prompt'a girmez). İstek 17'nin ZIP'i (`7328e1ff…`) kullanılmaz. Rev 15 için S-93 ön incelemesi 2 tur (ikisi de ENGEL'siz, kayıt değildir); bulguları yazar işledi.
- **Bu sürümün nedeni:** inceleme 16'nın iki ZIP eksiği kapatıldı (`06-icerik/`, `07-olcum/`, `04-baglam/` kural metinleri); paket 5f678d2a → 0c53533e (fark ZIP'te).
- **Yetki:** sahibin sözü "kör nokta" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PSS-UI Control Tower oturumu · 2026-10-30. Paketi `module-pack-author` yazdı (beyan "orta-yüksek", doğrulanamadı; uygulama oturumu high).
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-review-18-9aba6fb95.zip` · sha256 `79cc842c91c86973eddb4aa4e854a4ada65cac76b6c32d29bd1d3eb75c1cc762` · 6 513 862 bayt · 2564 dosya + `MANIFEST.sha256`; kart ve şablon `origin/main` 63acdf2ec sürümü, geri kalan bağlam 7ac15454e
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-18/MOD-0286-review-18-9aba6fb95.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-0c53533e.md`; yeni bulgular F-40'tan
- **Prompt bloğunun SHA-256'sı:** `5e3737bad192ad1c3a787ffb7082830f904f28b39141dbce0eef1a91a2a9d8c5`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 18 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra açtığın klasörde `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 0c53533e3e2434fbd8abe615f814d037c3c390b06625ee822358871958aed102 (revizyon 15) — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Yeniden inceleme. Önceki inceleme 16'nın hedefi 5f678d2a idi (KOŞULLU; kayıt ve CT yanıtı `04-baglam/docs/records/reviews/MOD-0286/2026-10-29-5f678d2a.md`). Ne değişti — paket 5f678d2a → 0c53533e (fark `03-fark/MOD-0286-5f678d2a-to-0c53533e.diff`): alan klasöründe duran 9 JS dosyası FU-18 satırına alındı (bölüm 1 (2b), (3); 97 → 106); satır kontrolü tabanı "52 satır (48 dosya)"; tek seviye taban satırının instance'ı klasörde doğrudan duran dosya; FU-16 satırlarının gerekçesi yeni sahip kararını anar; klasör içi `_ViewStart` ayrıca paylaşılan satırı alır; Failure Path satır biçimi; taban yeniden ölçüm notu; AC-18 değişenler listesinde. ZIP'e `06-icerik/` (7ac15454e: `Views/**` 1740 ve `wwwroot/assets/js/**` 796 dosya, tamamı), `07-olcum/` (betikler ve çıktıları; BENIOKU.txt'teki gibi koşulabilir) ve `04-baglam/` altında SOP §20.4 ile kural metinleri eklendi. Kapandı iddiasındaki önceki bulgular — inceleme 16 F-25 (görünüm/JS içerikleri yok) ve F-26 (SOP §20.4 / CT-APR-02 metni yok). Her iddiayı kendin ölç.
Görev: paketin bu revizyonunu SOP §20.4'e göre incele: (1) yeni eşleme `04-baglam/scripts/uic_changed_pages.py`, 05 listeleri ve 06 içerikleriyle ölçülebilir ve doğru mu (sayılar, klasörler, dışarıda kalanlar, taban satırları, eşleşmeler, FU-18 dosyaları, paylaşılan JS, `pages/**` başvuru grafiği, AC-21 başlangıç koşulları); (2) AC-21 ve FU-16/17/18 ölçülebilir, sıralı ve paketin geri kalanıyla (AC-17…AC-20, CI tablosu, taban kuralları, korunan yollar) çelişkisiz mi; (3) revizyon başka bir şeyi (künye, onay alanları, dilim tablosu, desen bloğu, UIC/RC) değiştirdi mi; (4) kod ya da korunan yol değişikliği gerektiren her şey sahip onayı ve ayrı iş olarak mı yazılmış; (5) inceleme 16'nın F-25 ve F-26'sı kapandı mı.
Çıktı: ilk satır `hedef: <paket yolu> @ 0c53533e`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, paket satırı ve dayanak dosya:satır> | açık | - | <tarih>`; yeni bulgular F-40'tan başlar (paketteki daha küçük numaralar başka turlarındır); kapanan önceki bulgu için `F-25 | kapandı | <dayanak>` biçiminde satır; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Yanıtı Türkçe ver.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-18/MOD-0286-review-18-9aba6fb95.zip
ZIP sha256: 79cc842c91c86973eddb4aa4e854a4ada65cac76b6c32d29bd1d3eb75c1cc762
```
