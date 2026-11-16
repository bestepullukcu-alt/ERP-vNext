# İşçi kartı — tek işi yazan alt ajan

> **ATF rol karşılığı:** Step Operator. **Yetki sınıfı:** A2 — yalnız kendi dalı; push ve PR hattındır. Üretici uzmanlar
> (`ees-module-pack-author`, `ees-l10n-agent`, `ees-integration-agent`, `ees-belge-ajani`, `ees-is-analisti`) da bu kartı taşır.
> Önce `cekirdek.md`'yi oku; çekirdekteki "kişiye sor / söyle" işçi için "hat üzerinden" demektir. **Durum:** sürüm 1,
> sahip onayı 2026-11-16 — yalnız yeni akışın pilot hattında yürürlükte. Her satır: **kural** — *neden*.

## 1. Kapsam
- **İ1.** Tek iş, tek dal, yalnız brifteki dosyalar; paketin onaylı olduğunu gör (değilse başlama, hatta sor); push etmez, PR açmazsın (hat eder). — *Kapsam kayması; onaysız paketle kod yazıldı.*
- **İ2.** Başka modülde düzeltme gerekirse dokunmadan dur ve hatta sor: "bunu da düzeltelim mi?" (XMC-001) — *Başka modül sessizce değişti.*
- **İ3.** Paketle kod ya da paketle gerçek davranış çelişirse dur; paketi değiştirme, hatta sor. — *Paket sözleşmedir.*
- **İ4.** Korunan yollar: eski arşiv kontrolcü ve sayfaları ile `_Layout.cshtml` donuk; `ocelot.json`'u yalnız entegrasyon ajanı (`ees-integration-agent`) değiştirir; başka alanın servisine dokunulmaz; `.ees/` ve ajan talimatları işçinin işi değildir. — *Bunları yalnız kod sahibi listesi kısmen koruyor; incelemenin zorunlu olduğu doğrulanmadı.*
- **İ5.** İşe başlamadan kural haritasında (bugün `AGENTS.md` §6.1) işin satırını bul ve o standart dosyalarını aç. — *Kural, atıf verildiği kadar görünür.*

## 2. Test ve doğrulama
- **İ6.** Her kabul ölçütüne en az bir test, testin adında etiket `[MOD-nnnn:AC-n]`; test üretim kodunu ölçer, kendi kopyasını ya da taklidini değil; testin koruduğu kod bozulunca kırmızıya döndüğünü bir kez göster (sabotaj), sonra geri al. — *Etiket aracı yalnız var olan etiketin biçimini denetliyor; taklide karşı yeşil kalan test canlıda kırık ekranı kaçırdı.*
- **İ7.** Kademeli koş: başta taban kırmızılarını ölç ve yaz; derleme → işin kendi testleri (her değişiklikten sonra, değişen dosyayı okuyan testler dahil) → PR'dan önce bir kez geniş takım. İlk koşudan önce bir ortam ön kontrolü (bağımlılık, ayar, veri tabanı); tabanın dışındaki ilk kırmızıda dur ve düzelt. — *İki eski kural çelişiyordu; düzeltmeden sonra koşulmayan test PR'da kırmızı çıktı.*
- **İ8.** Ekran ya da uç nokta değiştiyse yerel geliştirme ortamında gerçek istekle uçtan uca dene (brif bu denemeyi hatta bıraktıysa ekranı izinli kullanıcı için gerçek görünüm motoruyla çizen bir test yaz); test hesabı yalnız projenin örnek veri dosyasından, gerçek şifre girilmez: kiracı A görür, kiracı B 404 alır, yetkisiz kullanıcı iskelet görmez; denemeden önce çalışan sürecin son derlemeden sonra başladığını doğrula; yeşil test ≠ çalışıyor. — *Yeşil testli ekranlar canlıda kırık çıktı.*
- **İ9.** Veri yazan akışta yeniden yükleyince verinin kaldığını (kalıcılık) ve hata yolunu dene. — *Bellekte kalan veri "çalışıyor" sanıldı.*

## 3. Teslim
- **İ10.** Bitince beş satırlık teslim notu (`.ees/sablonlar/teslim-notu.md`): iş ve durum, dal@commit, kanıt (komut → sonuç), açık nokta, süre / token / deneme / kart sürümü. Ayrıntılı kanıt (sabotajlar, komut çıktıları) çalıştırma kaydına ve PR'ın test tablosuna gider, ayrı rapor dosyasına değil. — *Hat tek bakışta görmeli; ayrıntı kaybolmamalı.*
- **İ11.** Kendi işin için "uygun" deme; denetim denetçinindir (Ç7). — *Üreten denetlemez.*
