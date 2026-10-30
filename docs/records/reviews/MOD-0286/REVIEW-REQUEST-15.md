# MOD-0286 — bağımsız inceleme isteği 15 (SOP §20.4; D-9 / FU-1 "kural tek yerden" adım 2)

- **Hedef:** `01-degisiklik.diff` @ sha256 `51ddb5b2accd8a8a344091e374f3177c885f9143a58b799a6b1d22975cce377e` (`.antigravity/**` ajan, skill ve kural değişikliği; paket gövdesi değişmez)
- **Kaynak commit:** taban `origin/main` d27f0272d · dal `chore/agents/ui-kural-tek-adim2-01` (yerel; değişiklik commit edilmedi, push edilmedi)
- **Risk:** korunan yol (`.antigravity/**`, AGENTS.md §4) — sahip onayı ve bağımsız inceleme gerekli
- **Önceki sürüm ve incelemeler:** ilk Codex incelemesi. S-93 ön incelemesi 3 tur koştu (kayıt değildir); son tur ENGEL'siz.
- **Yetki:** sahibin sözleri "1. grubu at", "2. grubu onayla", "3. grubu onayla", "Yerinde kalsın" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PSS-UI Control Tower oturumu · 2026-10-29
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `mod0286-kural-tek-adim2-04.zip` · sha256 `74847355ece012a7e5bd31b65a427650c7a6bd5b62d85003c94ad37845573e8b` · 910 483 bayt · 63 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/mod0286-kural-tek-adim2-04.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286/2026-10-29-51ddb5b2.md`
- **Prompt bloğunun SHA-256'sı:** `c0ec41cb5b606922ea9ef06ef7b18a776667866350c2839294ed4973e69f8f23`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 "kural tek yerden" adım 2 değişikliğinin bağımsız incelemesi 15 (SOP §20.4). İnceleyensin; değişikliği yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP mod0286-kural-tek-adim2-04.zip · sha256 74847355ece012a7e5bd31b65a427650c7a6bd5b62d85003c94ad37845573e8b · önce açtığın klasörde `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: 01-degisiklik.diff @ sha256 51ddb5b2accd8a8a344091e374f3177c885f9143a58b799a6b1d22975cce377e — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: 00-SAHIP-KARARLARI.md sahibin 18 kararını ve sınırlarını verir; 01 diff'i, 02 yeni ve 03 eski dosyaları, 04 bağlam dosyalarıdır (taban origin/main d27f0272d). Şunları ölç:
1. Her karar diff'te doğru uygulanmış mı? Eksik kalan, kararın ötesine geçen ya da karar listesinde olmayan değişiklik var mı?
2. Kural dosyalarına taşınan her cümle, bağlamdaki kodla ve kurallarla doğru mu? Kuraldan fazlasını ya da eksiğini söyleyen, ölçülmemiş bir şeyi kesin söyleyen cümle var mı?
3. Değişiklik yeni bir kural-kural ya da kural-ajan çelişkisi yarattı mı?
4. Silinen ya da numarası, adı değişen bir maddeye başka bir dosyadan (kod, betik, kural, ajan) atıf var mı; atıflar hedefini buluyor mu?
5. Üretilmiş kopyalar (`.claude/`, `.codex/`, `.agents/`) kaynaklarıyla ve `.antigravity/adapters.json` ile tutarlı mı?
6. Sahibin sınırları (korunan yollar, başka modülün paketi, dışarıda bırakılan çelişkiler A/B/C) korunmuş mu?
Çıktı: ilk satır `hedef: 01-degisiklik.diff @ 51ddb5b2`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, yol:satır> | açık | <karar #> | 2026-10-29`; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Yanıtı Türkçe ver.
```
