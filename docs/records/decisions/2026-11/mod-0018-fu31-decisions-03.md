# MOD-0018-FU31 — kararlar 03

Bu bir ek kayıttır. 01 ve 02 numaralı kayıtlar değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Kişi adı yazılmaz.

| # | Karar | Söz | Kaynak | Nerede | Zaman |
|---|---|---|---|---|---|
| 10 | K-18 kapısı kapandı. MOD-0178 sahibi, K-1'in 4. şartı için önerilen denetim düzeyini kabul etti. Kabulde 4. şart, aşağıdaki (a)–(c) ile ölçülür. | Aşağıdaki alıntı | MOD-0178 sahibi (depo ekibi) | MOD-0018 sahibi cevabı CT · Destek sohbetine yapıştırdı; Destek bu oturuma aynen iletti | 2026-11-11 |

Alıntı. Selamlama satırı kişi adı içerdiği için çıkarıldı; metnin geri kalanı aynen:

> şart için önerdiğiniz düzey uygun: kurulum ve izin değişikliği Auth'un kendi denetim kaydına kesin olarak yazılsın, merkezi
> günlüğe "en iyi çaba" ile iletilsin. Garantili iletimi şimdi istemiyoruz; merkezi denetimin garantili iletimini BL-501
> kapsamında, MVP'lerden sonra ayrıca ele alacağız (bugünkü rol ekranıyla aynı düzey). Kabulde 4. şartı şöyle ölçeceğiz:
> (a) her rol kurulumunda ve izin değişikliğinde Auth'ta yerel denetim kaydı yazılıyor; (b) merkezi iletim çağrılıyor;
> (c) merkezi günlük erişilemezken ya da iletim yapılandırılmamışken kurulum yine tamamlanıyor, yerel kayıt duruyor ve hata
> günlüğe düşüyor. (c) için pakete bir hata yolu testi eklenirse iyi olur. Aynı adlı rolde "atlandı" kaydı da bu düzeyde
> yazılabilir. 36 saatlik tahmin bizim için uygun. Paketi onaylayıp kodu planlayabilirsiniz. Teşekkürler.

## CT notları

- **Yerel kayıt "kesin olarak" yazılır.** Bu söz, K-18 (a)'daki "yerel satır en iyi çaba" ifadesinden daha sıkıdır. Paket bir sonraki sürümde yerel yazımı kesin yapar; merkezi iletim en iyi çaba kalır. Yöntem pakette tanımlanır.
- **Süre farkı.** MOD-0178 sahibi 36 saati kabul etti. FU31'in güncel tahmini yaklaşık 40 saattir. Farkı Destek bildirir.
- **Onay ve izin durumu.**
  - "Paketi onaylayıp kodu planlayabilirsiniz" cümlesi MOD-0178 sahibinin görüşüdür. FU31'in paket onayı değildir. Onay, MOD-0018 sahibinin CT · Auth sohbetindeki «onayla» sözüyle verilir.
  - K-19 (Platform sahibinin etiket izni) açıktır.

Bu kayıt commit'ten sonra değiştirilmez.
