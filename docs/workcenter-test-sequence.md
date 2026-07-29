# Görev Merkezi — Test Sırası (CAND-CAP-0006 / MOD-0024)

**Hazırlayan:** CONTROL TOWER · **Tarih:** 2026-07-26 · **Branch:** `feature/pss/candcap0006-wc1-work-item-projection`

Bu sıra, MOD-0024 Faz 1-3 + WC-1/WC-1b'nin **canlı** doğrulaması içindir. Veriler gerçektir (Mongo'da, API üzerinden oluşturuldu) — showcase fixture'ı KAPALI.

---

## Ön koşullar

| Kontrol | Beklenen |
|---|---|
| Servisler | 5000 gateway · 5001 web · 5056 auth · 5057 platform · 5059 mdm · 5060 hcm |
| Giriş | `admin@diten.com` → **`/WorkCenterNext`**'e düşmeli |
| Sekme sayaçları | **Başlangıçta ne görüyorsan onu buraya yaz** — sonraki adımlar mutlak sayıya değil, bu başlangıca göre **değişime** bakar |
| Kullanıcı | Diten Admin, pozisyonu **CFO** (Finans birimi) |
| Havuz | **2** olmalı (`Yatırımcı sunumu…`, `Banka kredi başvurusu…`) — burası doğrulama turlarından etkilenmedi |

**Önce temizlik:** elle denerken bıraktığın `sasasa` · `asda` · `asdasd` başlıklı kalemleri sil (satır menüsü → Sil). Bunlar Gelen Kutusu sayısını şişiriyor.

Sayaçları mutlak sayı olarak sabitlemiyoruz: doğrulama turları veriyi ilerletti (bazı kalemler artık Planlı/Devam Ediyor). Kusur ölçütü **tutarlılık**: bir aksiyondan sonra sayaç doğru yönde değişmeli, kalem iki sekmede birden görünmemeli, yenilemede durum korunmalı.

**Nasıl not al:** her kusur için → hangi ekran · ne yaptın · ne bekledin · ne oldu. Ekran görüntüsü varsa daha iyi. "Şurası çirkin" de geçerli bir nottur; UX turu bu.

---

## Oturum 1 — Gelen Kutusu / kabul akışı  (~10 dk)

Yeni gelen kişisel iş buraya düşer; **kabul edilmeden İşlerim'e geçmez.**

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 1.1 | Gelen Kutusu'nu aç | Sana **atanmış ama henüz kabul etmediğin** kalemler (ör. *Yeni tedarikçi ödeme koşullarını değerlendir*) | Boş liste · kabul etmiş olduğun kalem hâlâ burada |
| 1.2 | Tip çiplerine bak | Çip sayılarının toplamı listedeki kalem sayısına eşit | Toplam tutmuyor |
| 1.3 | Bir kaleme tıkla | Detay açılır; başlık, son tarih, atayan görünür | Boş alan · ham anahtar (`Xyz_Abc`) · GUID |
| 1.4 | **Kabul et** | Kalem Gelen Kutusu'ndan çıkar → İşlerim'e geçer; Gelen −1, İşlerim +1 | Sayaç güncellenmez · kalem iki yerde birden |
| 1.5 | Sayfayı **yenile** | Yeni durum korunur | Eski hale döner ⇒ ekran yalan söylüyor 🔴 |
| 1.6 | Kalan kalemi kabul et | Gelen Kutusu boşalır, "Her şey tamam" görünür | Boş-durum mesajı yok/ham anahtar |

---

## Oturum 2 — İşlerim / yaşam döngüsü  (~15 dk)

Aks yasası: **sekme = sahiplik · segment = durum · çip = tip+sinyal.**

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 2.1 | Segmentlere bak | Üç segment: **Aktif · Bekleyen · Planlı**; üçünün toplamı sekme sayacına eşit | Üçten fazla segment · toplam sekme sayacını tutmuyor |
| 2.2 | Tarihleri kontrol et | *Temmuz kapanış* → **4g gecikmiş** (kırmızı) · *Tedarikçi sözleşme* → **Bugün dolacak** · *Ay sonu kapanış* → **4g kaldı** | Yanlış gün sayısı ⇒ saat yine dondu 🔴 |
| 2.3 | *Ay sonu kapanış* (Beklemede) → **Başlat** | "Devam ediyor" olur, aksiyon **Tamamla**'ya döner | Buton değişmez · durum yanlış |
| 2.4 | Yenile | Durum korunur | Geri döner 🔴 |
| 2.5 | Bir kalemi **ertele** (snooze) | Kalem **Bekleyen** segmentine geçer, **sekme değişmez** | Sekme değişirse ⇒ aks yasası ihlali 🔴 |
| 2.5a | Aktif bir görevde **Bilgi bekle** → gerekçe yazmadan onayla | Reddedilir, gerekçe zorunlu | Boş gerekçeyle kaydederse |
| 2.5b | Gerekçe yazıp onayla | Görev **Bekleyen**'e geçer, **sekme değişmez** | Sekme değişirse 🔴 |
| 2.5c | Bekleyen'deki o göreve bak | Satırda **yazdığın gerekçe cümlesi** çip olarak görünür | Boş çip · ham anahtar · cümle yok |
| 2.5d | Aynı görevde **Devam et** | Görev *Devam ediyor*'a döner, gerekçe temizlenir | Gerekçe kalırsa · durum değişmezse |
| 2.5e | Yenile | Yeni durum korunur | Geri dönerse 🔴 |
| 2.6 | **SLA riski** çipine tıkla | Yalnız riskli kalemler süzülür, sayaç tutar | Sayaç ≠ liste |
| 2.7 | Arama kutusuna `kapanış` yaz | Eşleşenler kalır | Türkçe karakter eşleşmiyor |
| 2.8 | Liste ↔ tablo görünümü | Aynı kalemler, aynı sayı | Tabloda kalem kaybı |

---

## Oturum 3 — Checklist ve alt görev  (~15 dk)

**İki ayrı kavram, karıştırma:**
- **Checklist** = tek görevin *içinde* işaret kutuları. Sahibi/tarihi/yaşam döngüsü **yok**. *"Bu adımları yaptım mı?"*
- **Alt görev** = kendi atananı, tarihi, yaşam döngüsü ve **kendi detay sayfası** olan gerçek bir görev. *"Bu parçayı kim yapacak?"*

**Kural (sahip kararı 2026-07-28):** ikisi de tamamlamayı **bloklar**. Bloklayıcı checklist maddesi açıkken tamamlanamaz; **açık alt görev varken de** tamamlanamaz. **İptal edilen alt görev saymaz** — yoksa gereksizleşen bir alt görev üst görevi sonsuza kadar kilitler.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 3.1 | *Ay sonu kapanış kontrol listesi*'ni aç | 3 madde: *Banka ekstrelerini indir* (zorunlu) · *Cari mutabakat farklarını listele* (**bloklayıcı**) · *Yönetici özetini hazırla* (isteğe bağlı) | Madde eksik · zorunluluk türü görünmüyor |
| 3.2 | Bloklayıcı madde **işaretsizken** Tamamla dene | **Engellenir** + sebep görünür | İzin verirse 🔴 (bloklayıcı anlamsız) |
| 3.3 | Maddeleri işaretle | Sayaç (X/3) artar; yenilemede korunur | Yenilemede sıfırlanır 🔴 |
| 3.4 | Yeni checklist maddesi ekle | Listeye girer, kaydedilir | Kaydolmaz |
| 3.5 | *ERP faz 2 devreye alma*'yı aç | **3 alt görev**: Veri göçü doğrulaması (başlatılmış) · Anahtar kullanıcı eğitimi · Kesin geçiş provası | Alt görev listesi boş |
| 3.6 | Bir alt görev **başlığına tıkla** | Alt görev kendi tam detayı ile açılır (normal görev gibi) | Tıklanamıyor · yarım detay |
| 3.7 | Alt görevi başlat/tamamla | Kendi yaşam döngüsü çalışır | Alt görev kendi başına ilerleyemiyorsa |
| 3.8 | Açık alt görev varken üst görevi tamamla | **Tamamla görünür ama kapalı**, sebebi yazıyor: *"… alt görevi kapanmadan tamamlanamaz"*; zorlasan da görev tamamlanmaz | Tamamlanırsa 🔴 · buton gizlenmişse (kural: gizleme, kapat) · sebep yoksa |
| 3.8a | Açık alt görev varken üst görevi **başlat** | Başlar — alt görev yalnız tamamlamayı engeller | Başlatma da engelleniyorsa (yön ayrımı bozulmuş) |
| 3.8b | Alt görevi **iptal et** → üst görevi tamamla | Tamamlanır; iptal edilen alt görev engel saymaz | Hâlâ engelliyse 🔴 (kalıcı kilit) |
| 3.9 | Alt görevi **başkasına ata** → sonra tamamlamayı dene | Sen tamamlayamazsın (atanan değilsin), **iptal edebilirsin** (oluşturansın) | Tamamlayabiliyorsan 🔴 yetki hatası |

---

## Oturum 4 — Onay kapısı (Faz 3 · en kritik)  (~10 dk)

MOD-0024 onayı **raporlar ve devreder**, asla karar vermez. Karar MOD-0023'ün.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 4.1 | İşlerim → **Bekleyen** segmenti | *Yeni maliyet merkezi açılış talebi* burada, durumu **Bekliyor** | Aktif'te görünürse |
| 4.2 | Birincil aksiyona bak | **"Devam et" görünür ama KAPALI**; yanında **"Onay bekleniyor…"** çipi | **"Görevi iptal et" birincilse 🔴** — yıkıcı aksiyon terfi etmiş |
| 4.3 | Kapalı butonun üzerine gel | Sebep okunur (tooltip veya çip) | Sebep hiçbir yerde yoksa 🟠 |
| 4.4 | Başlat'ı zorla (üç-nokta menüsünden) | Mesaj: *"Görev, onaylayan kişinin kararını bekliyor."* | "Sunucu hatası" · "başka biri değiştirdi" ⇒ 🔴 |
| 4.5 | Yenile | Görev hâlâ başlamamış | Başlamışsa 🔴 |
| 4.6 | Onay bekleyen görevde **Bilgi bekle** | İzinlidir (bekleme ilerleme değil) | Engellenirse 🟠 |

> Onay **verme** akışı (onaylayan kişinin ekranı) bu turda test edilmiyor — onaylayan *Agent Sub*, ayrı kullanıcı.

---

## Oturum 5 — Havuz / üstlenme  (~10 dk)

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 5.1 | Havuz'u aç | 2 kalem: *Yatırımcı sunumu için finansal özet hazırla*, *Banka kredi başvurusu dosyasını gözden geçir* | Boş ⇒ pozisyon bağı koptu |
| 5.2 | Grup adına bak | **Uydurma kuyruk adı OLMAMALI** (eskiden "Operasyon Kuyruğu" yazıyordu) | Var olmayan takım adı 🔴 |
| 5.3 | **Üzerime al** | Kalem Havuz'dan çıkar → İşlerim'e geçer, sahibi sen olursun | Havuzda kalırsa · iki yerde birden |
| 5.4 | Yenile | Korunur | Geri dönerse 🔴 |
| 5.5 | Aynı kalemi **bırak** (release) | Havuza geri döner, sahipsizleşir | Dönmezse |

---

## Oturum 6 — Görev oluşturma (+ Yeni)  (~15 dk)

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 6.1 | **+ Yeni → Görev oluştur** | Form açılır (offcanvas veya tam sayfa) | Hiç açılmaz 🔴 |
| 6.2 | Boş kaydet | Alan bazlı hata; **son tarih zorunlu** | Genel "hata oluştu" · sessiz başarısızlık |
| 6.3 | Kendine görev oluştur | İşlerim'de anında görünür | Görünmezse 🔴 (kaybolan görev) |
| 6.4 | **Kişiye ata** → *Agent Sub* | Kaydedilir; senin listende **görünmez** (onun kutusuna gider) | Sende kalırsa |
| 6.5 | **Havuza ata** → CFO | Havuz'a düşer, senin de üstlenebileceğin şekilde | Düşmezse |
| 6.6 | Atama seçicisini aç | Yalnız gerçek kişiler: *Agent Sub — Muhasebe Md*, *Diten Admin — CFO* | GUID görünürse 🔴 · hayalet isim |
| 6.7 | Onay gerektir + yönetici seç | Kaydedilir; görev **Bekleyen**'e düşer, Başlat kapalı | Başlat açıksa 🔴 |
| 6.8 | Uzun başlık / özel karakter (`<`, `&`, emoji) | Düzgün kaydedilir ve gösterilir | Bozuk gösterim · kırık sayfa |

---

## Oturum 7 — Geçmiş ve eşzamanlılık  (~10 dk)

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 7.1 | Geçmiş'i aç | Yalnızca tamamlanan + iptal edilen kalemler, **salt okunur** | Aksiyon butonu varsa · açık bir kalem burada görünüyorsa |
| 7.2 | Bir görevi tamamla → Geçmiş | Anında görünür, sayaç artar | Görünmezse |
| 7.3 | **İki sekmede aynı görevi aç**; birinde tamamla, diğerinde başlat | İkinci sekme: *"Bu görevi başka biri sizden önce değiştirdi. Ekran güncel durumla yenilendi."* | Sessizce üzerine yazarsa 🔴 (veri kaybı) |

---

## Oturum 8 — 7 dil ve responsive  (~15 dk)

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 8.1 | Sırayla `en · fr · es · zh · ar · ru · tr` | **Hiçbir ham anahtar** görünmez (`Xyz_Abc`, `errorAbc`) | Ham anahtar 🔴 |
| 8.2 | Modül çipi | Dile göre değişir: Görevler / Tasks / Tâches / المهام | Hep Türkçe kalırsa |
| 8.3 | Tarihler | Dile göre: "4g gecikmiş" / "Overdue 4d" | Çevrilmemiş |
| 8.4 | **Arapça** | Sağdan sola yerleşim düzgün | Bozuk hizalama |
| 8.5 | Dil değiştirince | ⚠️ **Bilinen kusur:** sekme/kalem/panel durumu sıfırlanır (`?culture=` query'yi siler) — bunu tekrar bildirmene gerek yok | — |
| 8.6 | Tarayıcıyı daralt (mobil) | Yatay kaydırma yok, aksiyonlar erişilebilir | Taşma · gizlenen buton |
| 8.7 | Karanlık tema | Okunabilir kontrast | Görünmez metin |

---

## Oturum 9 — Öncelik ve bağımlılıklar  (~10 dk)

İkisi de CT tarafından HTTP seviyesinde doğrulandı; burada aradığımız **ekranın doğruyu söyleyip söylemediği**.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 9.1 | Yeni görev oluştururken **Öncelik** seç | Üç seçenek: Düşük · Orta · Yüksek; varsayılan Orta | Dördüncü seçenek · ham değer (`Medium`) |
| 9.2 | Listeye dön | Öncelik çipi/işareti kalemde görünür | Görünmezse · seçtiğinden farklıysa |
| 9.3 | Detayda önceliği değiştir → yenile | Yeni değer korunur | Eski değere dönerse 🔴 |

**Bağımlılık için not:** Görev Merkezi'nde bağımlılık **ekleme arayüzü YOK ve olmayacak** — pack §12 Y3: *"Task Center bağımlılıkları salt-okunur render eder; aggregator içinde bağımlılık editörü olmaz."* Kenarları kurmak MOD-0024'ün kendi yüzeyinin işi. Aşağıdaki kenarları **ben kuracağım**, sen ekranı sınayacaksın; "ekle butonu yok" bir kusur değildir.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 9.4 | A görevini aç (CT bir FS bağımlılığı kurmuş olacak) | Bağımlılık **salt-okunur** listede, öncülün adı ve tip çipi (FS) ile | Liste yoksa · GUID gösteriyorsa · **ekle/sil butonu varsa** 🔴 |
| 9.5 | A'da **Başlat**'a bak | Buton **görünür ama kapalı**, sebebi yazıyor: "FS: B kapanmadan başlanamaz" | Buton gizlenmişse (kural: gizleme, kapat) · sebep yoksa |
| 9.6 | Yine de zorla (butona bas) | Hiçbir şey olmaz; görev **Açık** kalır | Görev başlarsa 🔴 (kural sunumda kalmış demektir) |
| 9.7 | B'yi tamamla → A'ya dön | Engel banner'ı kalkar, **Başlat** açılır ve çalışır | Banner kalırsa · buton hâlâ kapalıysa |
| 9.8 | FF bağımlılığı olan C'yi aç | **Başlat** açık, **Tamamla** kapalı | İkisi de kapanırsa (yön ayrımı bozulmuş) |
| 9.9 | Öncülü iptal edilmiş D'yi aç | Bloklanmamış | Bloklu kalırsa 🔴 (kalıcı kilit) |
| 9.10 | 7 dilde 9.5'teki engel cümlesi | Her dilde çevrili | Ham anahtar 🔴 |

---

## Bilinen açıklar — tekrar bildirmene gerek yok

| Konu | Durum |
|---|---|
| ~~**Öncelik** yok~~ | **Yapıldı** (BL-032) — artık test kapsamında, bkz. Oturum 9 |
| Dil değişince URL durumu silinir | Kabul edildi, test turuyla birlikte düzeltilecek |
| Vekâlet ("X adına") yok | Gerçek veri yok; kapsam yalnız "Kendim" |
| Bekleyen kalemde **kimi** beklediğin yazmıyor | Sebep ve tarih var, onaylayanın adı yok — tipli kimlik alanı boş geliyor |
| **İade et** ve **Başkasına ata** yok | Sıradaki dilim (**BL-034** madde 1 ve 5) |
| `Bekleyen`'e giren tek yol **Bilgi bekle** | `pause` bilinçli olarak yapılmadı — kavram `Waiting` + erteleme ile çakışıyor |
| Kanban / Takvim görünümü | **BL-015**, en sona bırakıldı |
| Bildirim / çan | **WC-4 + BL-025**, yapılmadı |
| ~~Görev bağımlılıkları~~ | **Yapıldı** (BL-028) — kurma/kaldırma + bloklama çalışıyor, bkz. Oturum 9 |
| Tekrarlayan görevler | Faz 4, yapılmadı |
| Yapılandırılabilir alanlar (Faz/İş Türü/Pazar) | Faz 5, yapılmadı |
| İnceleme (review) iş türü | Faz 3b, yapılmadı |
| `/WorkCenter` (eski sayfa) | Sökülmesi **BL-029** |

---

## Öncelik: hangi kusur beklemez

- 🔴 **Veri kaybı / ekran yalanı** — yenilemede geri dönen değişiklik, kaybolan görev, sessizce üzerine yazma
- 🔴 **Yetki/kapı ihlali** — onaysız işin başlayabilmesi, başkasının işini görmek
- 🟠 **Yanlış bilgi** — hatalı tarih, uydurma isim/etiket, ham anahtar
- 🟢 **Görsel** — hizalama, boşluk, renk, ikon

İlk ikisini bulduğun an dur ve bana getir; diğerlerini turun sonunda topluca ver.
