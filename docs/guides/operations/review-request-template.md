# Bağımsız inceleme isteği — şablon (S-94)

Her CT, Codex'e (ve S-93 ön incelemesine) giden her paket incelemesini bu biçimde hazırlar. Kayıt:
`docs/records/reviews/<MOD>/REVIEW-REQUEST-<NN>.md`. Örnek kayıtlar: `docs/records/reviews/MOD-0230/REVIEW-REQUEST-12.md`,
`docs/records/reviews/MOD-0286/REVIEW-REQUEST-07b.md`.

## 1. Dosya

````markdown
# <MOD> — bağımsız inceleme isteği <NN> (SOP §20.4 / CT-APR-02)

- **Hedef:** `<paket yolu>` @ sha256 `<64 hex>` (revizyon <n>)
- **Kaynak commit:** `<sha>` · dal `<dal>` (yerel / push edildi)
- **Risk:** <kartın hesapladığı risk> — neden inceleme gerekli
- **Önceki sürüm ve incelemeler:** <yoksa "ilk inceleme">
- **Bu sürümün nedeni:** <yeniden incelemede: ne değişti; ilk incelemede yazılmaz>
- **Yetki:** sahibin sözü "<kelime>" (<tarih>)
- **İsteyen / hazırlayan / tarih:** <rol> · <CT> · <YYYY-AA-GG>
- **İnceleyen:** yeni bir Codex oturumu · istenen model ve effort · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `<ad>.zip` · sha256 `<64 hex>` · <bayt> · <dosya sayısı> + `MANIFEST.sha256`
  - Yeri: `<mutlak yol>` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/<MOD>/<YYYY-AA-GG>-<hedef sha ilk 8>.md`
- **Prompt bloğunun SHA-256'sı:** `<64 hex>`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — <MOD> module pack bağımsız incelemesi <NN> (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP <ad> · sha256 <…> · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: <paket yolu> @ sha256 <…> — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
[Yeniden incelemede: Ne değişti — <liste>. Kapandı iddiasındaki önceki bulgular — <F-n listesi>. Her iddiayı kendin ölç.]
Görev: <ne incelenecek>
Çıktı: ilk satır `hedef: <paket yolu> @ <sha>`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`;
son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL.
```
````

## 2. Sohbette sahibe (dört blok, bu sırayla)

1. Tek cümle: "Codex'te **yeni** bir oturum açın, aşağıdaki metni ve ZIP'i verin."
2. Prompt'un tamamı, tek ```text bloğunda (dosyadaki blokla aynı; özeti blok altında yazılır).
3. ZIP'in mutlak yolu, kendi kod bloğunda.
4. ZIP'in SHA-256'sı, kendi kod bloğunda.

Son satır: "Codex'in cevabını olduğu gibi bu sohbete yapıştırın."

## 3. Yapılmaz

- Prompt yerine yalnız dosya yolu vermek ("dosyadan kopyalayın").
- Prompt'u kod bloğu dışında, markdown olarak yazmak (başlıklar ve `|` satırları bozulur).
- İlk incelemede CT'nin görüşünü, beklenen sonucu ya da "şuraya bakma" yönlendirmesini yazmak.
- Ön inceleme (S-93) bulgularını Codex prompt'una eklemek.
