# Koşullu bölümler — kısa paket standardı eki

> Yalnız paket bir koşullu bölüm açtığında okunur. Ana kural: `.ees/standartlar/paket-standardi.md` (KP6).
> **Açılan** bölümler künyede `conditional:` listesindedir (kısa ad, aşağıdaki başlıktaki) ve pakette bölüm 7'den sonra
> `## K-<ad>. <Ad>` başlığıyla, listedeki sırayla yazılır. **Açılmayan bölüm yazılmaz.**

## Her koşullu bölümün biçimi
- Tablo: `soru · cevap · ilgili AC · dilim`; en çok ~15 satır. Dilim sütunu `DL-n` yazar (boyut ölçümü o hücreye bakar).
- Her soru cevaplanır ya da `TBD` yazılır; `TBD`, bölüm 7'de bir açık karar satırıdır (KP7).
- `AC` hücresi ölçüt numarası ya da `—` (ölçüt gerekmiyorsa); `dilim` hücresi her zaman `DL-n`. Dilim tablosunun
  "kapsadığı bölümler" hücresine başlığın tamamı yazılır (`K-zaman. Zaman`).
- Cevabın doğrulanabilir kısmı bir kabul ölçütüdür (`AC-n`, bölüm 5): bölüm yalnız soruyu cevaplar, ölçüt bölüm 5'te yazılır.
- Denetçi (pilot hattında) "açılmamış ama gereken bölüm" arar.

## Bölümler

### `para` — Para
**Ne zaman:** tutar, fiyat, kur ya da vergi hesaplıyor ya da saklıyor. **En az:** ondalık tip; para birimi kaynağı; hassasiyet;
yuvarlama kuralı ve yeri; saat dilimi; kur hangi tarihin kuru.

### `zaman` — Zaman
**Ne zaman:** tarih, gün, hafta, süre ya da zamanlanmış iş sonucu kullanıcıya görünüyor. **En az:** hangi saat dilimi (kiracı mı kişi
mi); gün sınırı ve gece yarısı; hafta tanımı (ISO hafta-yılı); yaz saati geçişi; tatil ve iş günü kaynağı; süre birimi ve yuvarlama.

### `veri-degisikligi` — Veri değişikliği
**Ne zaman:** şema, referans veri ya da var olan kayıt değişiyor. **En az:** göç; geri doldurma; tohum veri; geri alma;
temizlik; saklama süresi; uyum, finans, denetim ve son durum kayıtlarının silinmez/değişmez olduğu açıkça.

### `sozlesme-degisikligi` — Sözleşme değişikliği
**Ne zaman:** başka modülün tükettiği uç nokta, olay ya da veri biçimi değişiyor. **En az:** tüketiciler kim; geriye uyum; iki
sürümün birlikte çalıştığı süre; eski sürümün kaldırılma tarihi; iki yerde yazılan bilgiyi bağlayan test.

### `yuksek-hacim` — Yüksek hacim
**Ne zaman:** liste ya da iş binlerce kayıtla çalışıyor. **En az:** p95 yanıt süresi; üst sınır; toplu iş boyutu; sayfalama.

### `disa-aktarma` — Dışa aktarma
**Ne zaman:** dosya, rapor ya da başka sisteme veri çıkıyor. **En az:** biçim; izin; değişmezlik; üst veri; hassas veri kuralı.

### `gozlem` — Gözlemlenebilirlik
**Ne zaman:** arka planda koşan iş, olay tüketicisi ya da servisler arası çağrı var. **En az:** ilişki kimliği; log; metrik; iz;
hata ve denetim olayı; maskeleme.

### `hassas-veri` — Hassas veri
**Ne zaman:** kişisel, sağlık, finans ya da gizli veri işleniyor. **En az:** veri sınıfı; maskeleme; sunucu tarafı yetki; kiracı
ayrımı; sır yönetimi.

### `ekran` — Ekran
**Ne zaman:** kullanıcı arayüzü var. **En az:** ekran kalıbı ve gerekçesi (sihirbaz varsayılan değildir; sıklık ve etkileşime
göre); altın referans slim / compact ve `data_mode`; klavye, odak, etiket, bütün durumlar (boş, yükleniyor, hata, yetkisiz);
sayfa yardımı ve tur; seçim listesi kaynağı (platform lookup); menü anahtarı biçimi (`check_pack_nav_keys.py` ölçer).

### `durum-akisi` — Durum değiştiren akış
**Ne zaman:** kayıt durumu değişiyor, iki işlem aynı kaydı değiştirebilir ya da iş yarıda kalabilir. **En az:** eşzamanlılık
(sürüm ya da koşullu yazma); tekrar güvenliği (anahtar); yarıda kalan işin telafisi; yeniden deneme sınırı; işlem (transaction)
gerekiyor mu ve tekli Mongo'da ne olur.

### `rota` — Rota
**Ne zaman:** yeni ağ geçidi rotası gerekiyor. **En az:** var olan rota yeter mi; gerekiyorsa `integration-agent` işi (paket
`ocelot.json` yazmaz).

### `dis-sistem` — Dış sistem
**Ne zaman:** başka bir sistemle (banka, e-posta, takvim) konuşuyor. **En az:** kesinti; zaman aşımı; bırakma; yeniden deneme;
sahte ortam.

### `mevzuat` — Mevzuat
**Ne zaman:** yasal bir kurala dayanıyor. **En az:** kuralın kaynağı; yorumu kimin; uzman görüşü gerekiyor mu (ajan yorum yapmaz;
gerekiyorsa açık karar satırı).

## Örnek (kısaltılmış)
```markdown
## K-zaman. Zaman
| Soru | Cevap | AC | Dilim |
|---|---|---|---|
| Hangi saat dilimi? | kiracının saat dilimi | 12 | DL-2 |
| Hafta tanımı? | ISO hafta-yılı, pazartesi başlar | 13 | DL-2 |
| Tatil kaynağı? | TBD (bölüm 7, karar 2) | — | DL-3 |
```
