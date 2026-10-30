# MOD-0286 — bağımsız inceleme isteği 19 (SOP §20.4; D-9 / FU-1 "kural tek yerden" adım 3)

- **Hedef:** `01-degisiklik.diff` @ sha256 `dc15c44c13b25313834ea5a435824b8f92172b93d81853fd9acc77379c1af27d` (`.antigravity/**` ajan ve skill değişikliği; kural dosyası ve paket gövdesi değişmez)
- **Kaynak commit:** `163106bac` · taban `origin/main` bf3b0cd57 · dal `chore/agents/ui-kural-tek-adim3-01` (yerel; push edilmedi)
- **Risk:** korunan yol (`.antigravity/**`, AGENTS.md §4) — sahip onayı ve bağımsız inceleme gerekli
- **Önceki sürüm ve incelemeler:** bu fark için ilk Codex incelemesi. S-93 ön incelemesi 2 tur koştu (kayıt değildir); son tur ENGEL'siz, durdu.
- **Yetki:** sahibin sözü "Evet" (2026-10-30)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PSS-UI Control Tower oturumu · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-review-19-163106bac.zip` · sha256 `bcca4a1db39437aec81ced09e2ac39cae8e4accd9d9bb86d0ac68225ca001e65` · 664 176 bayt · 92 dosya + `MANIFEST.sha256`. Paket değil fark hedefli olduğu için adım 2'deki biçimle kuruldu: `review_zip.py` yalnız paket farkı üretir.
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-19/MOD-0286-review-19-163106bac.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-dc15c44c.md`; yeni bulgular F-50'den
- **Prompt bloğunun SHA-256'sı:** `6048e45f34fb5734b1d7a0b8e11b58a4c9506a46df5f34e2f4f1c10b17a16267`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 "kural tek yerden" adım 3 değişikliğinin bağımsız incelemesi 19 (SOP §20.4). İnceleyensin; değişikliği yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra açtığın klasörde `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: 01-degisiklik.diff @ sha256 dc15c44c13b25313834ea5a435824b8f92172b93d81853fd9acc77379c1af27d — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: 00-SAHIP-KARARLARI.md sahibin kararını ve sınırları verir; 01 diff'i, 02 yeni ve 03 eski dosyaları, 04 bağlam dosyalarıdır (taban bf3b0cd57). Şunları metni okuyarak ölç:
1. Diff'te silinen her gereklilik, yerine yazılan satırın gösterdiği kural bölümünde aynı anlamda duruyor mu? Artık hiçbir yerde durmayan ya da kuralda farklı söylenen var mı?
2. Her atıf hedefini buluyor mu (dosya, bölüm, madde, başlık) ve içeriği gerçekten taşıyan yere mi gidiyor?
3. Değişiklik sahibin kararının dışına çıkıyor mu, kararın istediği bir yer eksik mi?
4. Ajan dosyalarına yeni eklenen cümleler doğru ve yapılabilir mi?
5. Silinen ya da numarası, adı değişen bir maddeye başka bir dosyadan (kod, betik, kural, ajan) bağlılık var mı?
6. Üretilmiş kopyalar (`.claude/`, `.codex/`) kaynaklarıyla ve `.antigravity/adapters.json` ile tutarlı mı?
7. Değişiklik gelecekte bir ajanın daha kötü sayfa ya da paket üretmesine yol açar mı?
ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.
Çıktı: ilk satır `hedef: 01-degisiklik.diff @ dc15c44c13b25313834ea5a435824b8f92172b93d81853fd9acc77379c1af27d` (64 hanenin tamamı); her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, yol:satır> | açık | K3 | 2026-11-06`, numaralar F-50'den başlar, metnin içinde `|` kullanma; bulgu yoksa bulgu satırı yazma; son satırda `Sonuç: UYGUN` ya da `Sonuç: KOŞULLU` ya da `Sonuç: UYGUN DEĞİL`. Yanıtı Türkçe ver.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-19/MOD-0286-review-19-163106bac.zip
ZIP sha256: bcca4a1db39437aec81ced09e2ac39cae8e4accd9d9bb86d0ac68225ca001e65
```
