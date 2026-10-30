# MOD-0286 — bağımsız inceleme isteği 20 (SOP §20.4; D-9 / FU-1 "kural tek yerden" adım 3, ek değişiklik)

- **Hedef:** `01-degisiklik.diff` @ sha256 `d8bb03efde556b7a8b8590967fe8a59731e65558f557ff835ee2c35c519607c1` (`.antigravity/**` fark; 8 ajan kopyası atfa, 1 kural satırı düzeltmesi; paket gövdesi değişmez)
- **Kaynak commit:** uç `02b645f1e` · taban `163106bac` (inceleme 19'un hedefi) · dal `chore/agents/ui-kural-tek-adim3-01` (yerel; push edilmedi)
- **Risk:** korunan yol (`.antigravity/**`, AGENTS.md §4) — sahip onayı ve bağımsız inceleme gerekli
- **Önceki inceleme:** 19, KOŞULLU (`2026-10-30-dc15c44c.md`). S-93 ön incelemesi 1 tur koştu (kayıt değildir); ENGEL'siz, durdu.
- **Yetki:** sahibin sözleri "Kuralı da düzelt" ve "Evet, 8'ini bağla" (2026-10-30)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PSS-UI Control Tower oturumu · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-review-20-02b645f1e.zip` · sha256 `48cfb0bf05268a62e6822890ab5e06e1bebddaaced45bfd3fc04efe3eb789664` · 1 077 019 bayt · 117 dosya + `MANIFEST.sha256`. Paket değil fark hedefli olduğu için inceleme 19'daki biçimle kuruldu: `review_zip.py` yalnız paket farkı üretir.
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-20/MOD-0286-review-20-02b645f1e.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-d8bb03ef.md`; yeni bulgular F-52'den
- **Prompt bloğunun SHA-256'sı:** `07dd882ccd389350fb8e572b39f38ba0101b7fd09235d612efd65100ecc4e9c0`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 "kural tek yerden" adım 3 ek değişikliğinin bağımsız incelemesi 20 (SOP §20.4). İnceleyensin; değişikliği yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra açtığın klasörde `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: 01-degisiklik.diff @ sha256 d8bb03efde556b7a8b8590967fe8a59731e65558f557ff835ee2c35c519607c1 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: 00-SAHIP-KARARLARI.md sahibin kararını, sınırları ve bilinen anlam farklarını verir; 01 diff'i, 02 yeni ve 03 eski dosyaları, 04 bağlam dosyaları, 05 depo geneli ölçüm çıktısıdır. Fark 9 satır: 8 ajan kopyası atfa çevrildi, 1 kural satırı düzeltildi. Şunları metni okuyarak ölç:
1. Diff'te silinen her gereklilik, yerine yazılan atfın gösterdiği kural bölümünde aynı anlamda duruyor mu? 00'da yazılı anlam farkları dışında kaybolan ya da farklı söylenen var mı?
2. Her atıf hedefini buluyor mu (dosya, bölüm, madde, başlık) ve içeriği gerçekten taşıyan yere mi gidiyor?
3. `rebuildDependentSelect` düzeltmesi F-50'nin iki durumunu kapatıyor mu, yeni bir hata getiriyor mu?
4. `frontend-ui-ux 24–25` diye atıf veren `verify_datatable_page.py` ve `diten-datatable.js` için madde numaraları ve anlamı hâlâ tutuyor mu?
5. Değişiklik sahibin kararının dışına çıkıyor mu, kararın istediği bir yer eksik mi?
6. Değişiklik gelecekte bir ajanın daha kötü sayfa ya da paket üretmesine yol açar mı?
F-51 kapsamındaki dosyalar bu ZIP'te var: inceleme 19'da ölçemediğin ters bağımlılıkları da bak. ZIP'te hâlâ eksik girdi varsa ayrı bir bulgu olarak yaz.
Çıktı: ilk satır `hedef: 01-degisiklik.diff @ d8bb03efde556b7a8b8590967fe8a59731e65558f557ff835ee2c35c519607c1` (64 hanenin tamamı); her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, yol:satır> | açık | K3 | 2026-11-06`, numaralar F-52'den başlar, metnin içinde `|` kullanma; bulgu yoksa bulgu satırı yazma; kapanan eski bulguları düz metinle `Kapananlar (inceleme 19): F-…` satırında yaz, o satırda `|` kullanma; son satırda `Sonuç: UYGUN` ya da `Sonuç: KOŞULLU` ya da `Sonuç: UYGUN DEĞİL`. Yanıtı Türkçe ver.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-20/MOD-0286-review-20-02b645f1e.zip
ZIP sha256: 48cfb0bf05268a62e6822890ab5e06e1bebddaaced45bfd3fc04efe3eb789664
```
