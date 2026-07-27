# Görev Merkezi — Test Sırası (CAND-CAP-0006 / MOD-0024)

**Hazırlayan:** CONTROL TOWER · **Tarih:** 2026-07-26 · **Branch:** `feature/pss/candcap0006-wc1-work-item-projection`

Bu sıra, MOD-0024 Faz 1-3 + WC-1/WC-1b'nin **canlı** doğrulaması içindir. Veriler gerçektir (Mongo'da, API üzerinden oluşturuldu) — showcase fixture'ı KAPALI.

---

## Ön koşullar

| Kontrol | Beklenen |
|---|---|
| Servisler | 5000 gateway · 5001 web · 5056 auth · 5057 platform · 5059 mdm · 5060 hcm |
| Giriş | `admin@diten.com` → **`/WorkCenterNext`**'e düşmeli |
| Sekme sayaçları | Gelen Kutusu **2** · İşlerim **13** · Havuz **2** · Geçmiş **4** |
| Kullanıcı | Diten Admin, pozisyonu **CFO** (Finans birimi) |

Sayaçlar tutmuyorsa önce onu söyle — sonraki adımlar bu veriye dayanıyor.

**Nasıl not al:** her kusur için → hangi ekran · ne yaptın · ne bekledin · ne oldu. Ekran görüntüsü varsa daha iyi. "Şurası çirkin" de geçerli bir nottur; UX turu bu.

---

## Oturum 1 — Gelen Kutusu / kabul akışı  (~10 dk)

Yeni gelen kişisel iş buraya düşer; **kabul edilmeden İşlerim'e geçmez.**

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 1.1 | Gelen Kutusu'nu aç | 2 kalem: *Q3 nakit akış projeksiyonunu onayla*, *Yeni tedarikçi ödeme koşullarını değerlendir* | Boş liste · fazladan kalem |
| 1.2 | Tip çiplerine bak | **Kabul Bekleyen 2**, diğerleri 0 | Yanlış sayaç |
| 1.3 | Bir kaleme tıkla | Detay açılır; başlık, son tarih, atayan görünür | Boş alan · ham anahtar (`Xyz_Abc`) · GUID |
| 1.4 | **Kabul et** | Kalem Gelen Kutusu'ndan çıkar → İşlerim'e geçer; sayaçlar 2→1 ve 13→14 | Sayaç güncellenmez · kalem iki yerde birden |
| 1.5 | Sayfayı **yenile** | Yeni durum korunur | Eski hale döner ⇒ ekran yalan söylüyor 🔴 |
| 1.6 | Kalan kalemi kabul et | Gelen Kutusu boşalır, "Her şey tamam" görünür | Boş-durum mesajı yok/ham anahtar |

---

## Oturum 2 — İşlerim / yaşam döngüsü  (~15 dk)

Aks yasası: **sekme = sahiplik · segment = durum · çip = tip+sinyal.**

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 2.1 | Segmentlere bak | **Aktif 11 · Bekleyen 1 · Planlı 1** | Üçten fazla segment |
| 2.2 | Tarihleri kontrol et | *Temmuz kapanış* → **4g gecikmiş** (kırmızı) · *Tedarikçi sözleşme* → **Bugün dolacak** · *Ay sonu kapanış* → **4g kaldı** | Yanlış gün sayısı ⇒ saat yine dondu 🔴 |
| 2.3 | *Ay sonu kapanış* (Beklemede) → **Başlat** | "Devam ediyor" olur, aksiyon **Tamamla**'ya döner | Buton değişmez · durum yanlış |
| 2.4 | Yenile | Durum korunur | Geri döner 🔴 |
| 2.5 | Bir kalemi **ertele** (snooze) | Kalem **Bekleyen** segmentine geçer, **sekme değişmez** | Sekme değişirse ⇒ aks yasası ihlali 🔴 |
| 2.6 | **SLA riski** çipine tıkla | Yalnız riskli kalemler süzülür, sayaç tutar | Sayaç ≠ liste |
| 2.7 | Arama kutusuna `kapanış` yaz | Eşleşenler kalır | Türkçe karakter eşleşmiyor |
| 2.8 | Liste ↔ tablo görünümü | Aynı kalemler, aynı sayı | Tabloda kalem kaybı |

---

## Oturum 3 — Checklist ve alt görev  (~15 dk)

**Kural:** checklist tamamlamayı **bloklar**, alt görev **bloklamaz**.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 3.1 | *Ay sonu kapanış kontrol listesi*'ni aç | 3 madde: *Banka ekstrelerini indir* (zorunlu) · *Cari mutabakat farklarını listele* (**bloklayıcı**) · *Yönetici özetini hazırla* (isteğe bağlı) | Madde eksik · zorunluluk türü görünmüyor |
| 3.2 | Bloklayıcı madde **işaretsizken** Tamamla dene | **Engellenir** + sebep görünür | İzin verirse 🔴 (bloklayıcı anlamsız) |
| 3.3 | Maddeleri işaretle | Sayaç (X/3) artar; yenilemede korunur | Yenilemede sıfırlanır 🔴 |
| 3.4 | Yeni checklist maddesi ekle | Listeye girer, kaydedilir | Kaydolmaz |
| 3.5 | *ERP faz 2 devreye alma*'yı aç | **3 alt görev**: Veri göçü doğrulaması (başlatılmış) · Anahtar kullanıcı eğitimi · Kesin geçiş provası | Alt görev listesi boş |
| 3.6 | Bir alt görev **başlığına tıkla** | Alt görev kendi tam detayı ile açılır (normal görev gibi) | Tıklanamıyor · yarım detay |
| 3.7 | Alt görevi başlat/tamamla | Kendi yaşam döngüsü çalışır; **üst görevi bloklamaz** | Üst görev kilitlenirse 🔴 |

---

## Oturum 4 — Onay kapısı (Faz 3 · en kritik)  (~10 dk)

MOD-0024 onayı **raporlar ve devreder**, asla karar vermez. Karar MOD-0023'ün.

| # | Adım | Beklenen | Kusur sayılır |
|---|---|---|---|
| 4.1 | İşlerim → **Bekleyen** segmenti | *Yeni maliyet merkezi açılış talebi* burada, durumu **Bekliyor** | Aktif'te görünürse |
| 4.2 | Sunulan aksiyona bak | **Planla** var; **Başlat YOK/kapalı** | Başlat aktifse 🔴 (onaysız iş başlar) |
| 4.3 | Başlat'ı zorla (varsa) | Mesaj: *"Görev, onaylayan kişinin kararını bekliyor."* | "Sunucu hatası" · "başka biri değiştirdi" ⇒ 🔴 |
| 4.4 | Yenile | Görev hâlâ başlamamış | Başlamışsa 🔴 |

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
| 7.1 | Geçmiş'i aç | 4 kalem (tamamlanan + iptal edilen), **salt okunur** | Aksiyon butonu varsa |
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

## Bilinen açıklar — tekrar bildirmene gerek yok

| Konu | Durum |
|---|---|
| **Öncelik** kolonu/çipi yok | Kasıtlı — sözleşmede tanımlı değil, **BL-032** |
| Dil değişince URL durumu silinir | Kabul edildi, test turuyla birlikte düzeltilecek |
| Vekâlet ("X adına") yok | Gerçek veri yok; kapsam yalnız "Kendim" |
| Kanban / Takvim görünümü | **BL-015**, en sona bırakıldı |
| Bildirim / çan | **WC-4 + BL-025**, yapılmadı |
| Görev bağımlılıkları | **BL-028**, şema var, çalışma zamanı yok |
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
