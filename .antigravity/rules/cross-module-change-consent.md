---
description: "XMC-001 — Başka modülde düzeltme onayı: bir iş sırasında işin kapsamı dışındaki mevcut bir modülde düzeltme gerekirse ajan sessizce düzeltmez, sessizce geçmez; durur ve kullanıcıya sorar: 'bunu da düzeltelim mi?'"
---

# Başka Modülde Düzeltme Onayı — XMC-001

> **Neden var.** Sahip 2026-10-07'de istedi: yeni bir modül yazılırken mevcut başka bir modülde düzeltme
> gerekirse ajan kullanıcıya sorsun. Aynı gün iki örnek yaşandı:
> - Bitmiş Ürün işi, Auth izin profilini değiştirmeden manifesti düzeltemeyeceğini ölçtü ve kararı sordu
>   (doğru davranış).
> - Kapsam işi, başka modüllerin koşucularına dokunmadan önce durdu.
>
> İki yanlış davranış var ve ikisi de pahalı:
> - **Sessiz düzeltme.** Başka modülün kabul edilmiş kodu habersiz değişir. O modülün sahibi, testleri ve
>   birleşme sırası bunu bilmez. Paralel dallarla çakışır, ya da kabul edilmiş bir davranışı bozar.
> - **Sessiz geçme.** Görülen hata kimseye söylenmez ve kaybolur.

---

## 1. Kural

Bir iş (WP / prompt) sırasında, işin **beyan edilmiş kapsamı dışındaki** mevcut bir modülde düzeltme gerektiğini
gören ajan:

1. **Düzeltmeye başlamaz.** O modülün dosyasını değiştirmez.
2. **Durur ve sorar.** Kullanıcıya (Control Tower altında çalışıyorsa Control Tower'a) şu biçimde:

   > **Başka modülde düzeltme gerekiyor — bunu da düzeltelim mi?**
   > - **Modül:** `<ad / kod>` · **Dosya:** `<yol:satır>`
   > - **Sorun:** `<tek cümle: şimdi ne oluyor>`
   > - **Neden şimdi:** `<bu işle bağı>`
   > - **Risk:** 🔴 / 🟡 / 🟢 (ertelenirse ne olur — bkz. "Defer = Regression Check")
   > - **Seçenekler:** (a) bu işte düzelt · (b) ayrı iş / backlog · (c) şimdilik bırak, kayda geç
   > - **Önerim:** `<a/b/c>` ve gerekçesi (SAP / Oracle karşılaştırması uygunsa)

3. **Cevaba göre ilerler.** Cevap gelene kadar o modüle dokunmadan kendi kapsamındaki işe devam edebilir. Cevap
   "(a)" ise değişiklik raporda **ayrı bir bölümde** ve adıyla yazılır. "(b)" ise backlog'a kayıt düşer (BL-nnn).
   "(c)" ise raporun açık riskler bölümüne girer.

## 2. Ne "başka modül" sayılır

- İşin promptunda / paketinde adı geçmeyen bir modülün **üretim kodu, deposu, işlemcisi, ekranı, izin profili,
  manifesti ya da yapılandırması**.
- **Paylaşılan** bileşenler (paylaşılan liste bileşeni, `ProductIdentityWriteFaults`, ortak test ana makinesi,
  Building.Blocks …). Birden çok modülü etkilediği için her zaman sorulur.
- Kabul edilmiş ve başka bir dalda paralel değişen dosyalar.

## 3. Sorulmadan yapılabilenler (yine de raporlanır)

- **Derleme / birleşme zorunluluğu:** başka bir modülün arayüzüne eklenen bir üyeyi test çiftlerine eklemek, bir
  yapıcı argümanını güncellemek gibi, davranış değiştirmeyen uzlaştırmalar. Raporda "uzlaştırma" diye adıyla yazılır.
- **Promptun açıkça izin verdiği nokta:** ör. "`GskuRepository`'de yalnız kabul mekanizması". Kapsam o noktayla sınırlıdır.
- **Yalnız ekleme** olan ve başka modülün davranışını değiştirmeyen kayıt / eşleme satırları; prompt izin veriyorsa
  (ör. Platform denetim haritasına yeni ad eklemek).

Şüphede kalınırsa **sorulur**.

## 4. Control Tower altında

CT'nin sürdüğü sohbetlerde soru CT'ye gider (promptların **DUR** koşulları bu kuralın uygulamasıdır). CT kararı
verir ya da ürün kararıysa sahibe taşır. Karar, sohbete ve gün panosuna yazılır.

## 5. Kontrol listesi (rapor)

- [ ] Kapsam dışı değişiklik var mı? Varsa her biri için: soruldu mu, cevap ne, hangi bölümde raporlandı
- [ ] Görülüp düzeltilmeyen sorunlar: backlog kaydı ya da açık risk satırı
