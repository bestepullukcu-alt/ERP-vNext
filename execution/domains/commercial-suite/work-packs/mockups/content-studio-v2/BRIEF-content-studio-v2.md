# MOCKUP BRIEF — Bilgi Yolu Stüdyosu (kurgu · sayfa tasarımcısı · MLR onayı · yayın · saha sunumu)

> Tarih: 2026-09-30 (v2 — karar §8 sonrası) · Hazırlayan: CT.
> **Kararlar:**
> - Ayrı bir "İçerik Seti" YOK: kurgu, iddialar, onay ve yayın doğrudan **Bilgi Yolu**nda.
> - **Her yol** bir zincir şablonuna bağlı ve MLR (Medikal → Hukuk → Ruhsat) onayından geçer.
> - Tenant sahipliği + ülke katmanı.
> - Tasarım serbest, marka ve mevzuat kuralları kilitli.
> - Çıktı: HTML saha sunumu + aynı kaynaktan arşiv PDF'i.
>
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Aşağıdakiler amaç, içerik ve kurallardır. Mevcut ürünün genel düzenine (sol menü, üst çubuk, kart yapısı, açık/koyu tema) uyman yeterli.

## BAĞLAM
- **Kullanıcılar:** Çok ülkeli bir ilaç şirketinin (merkez Türkiye; Belarus, Özbekistan, Türkmenistan, Gürcistan, Azerbaycan) medikal ve pazarlama ekibi.
- **Ne üretir:** Saha temsilcilerinin doktor ziyaretinde tablette göstereceği anlatıyı **Bilgi Yolu** olarak hazırlar.
- **Yolun kurgusu:** Yol bir "anlatı zinciri"ne göre kurulur (örn. ihtiyaç → mekanizma → klinik kanıt → güvenlilik). Zincirin her adımına içerikler ve iddialar yerleşir. Sayfalar tasarlanır.
- **Onay ve yayın:** Yol MLR onayından geçer, yayınlanır. Yayındaki yol, etkileşim yolculuğunun bir aşaması olarak ziyarete gider.
- **Sahadan dönüş:** Temsilcinin hangi sayfayı ne kadar gösterdiği ölçülür.

**Bugün var olan (iyileştirilecek):** Bilgi Yolları sayfası.
- **Liste:** kod, ad, sürüm, durum, adım sayısı.
- **Oluştur / Düzenle — kimlik, sınıflandırma, zamanlama:** konu, başlık, kitle, dil, geçerlilik.
- **Oluştur / Düzenle — adımlar:** içerik seç, sırala; adım tipi, zorunlu, ön koşul adımı, kavram düğümü, tahmini süre, dal koşulları.
- **Yayın ve sürüm:** ayrı "Yayınla", adım setinin dondurulması, yeni sürüm.

Mockup bu sayfayı aşağıdaki kapsamla yeniden kurgulamalı.

## 1) YOL LİSTESİ
- **Sütunlar:** kod, ad, ürün, ülke, dil, zincir şablonu, sürüm, durum (taslak / incelemede / onaylı / yayında / geri çekildi / arşiv), son revizyonun onay durumu, sahada kullanıldığı yolculuk sayısı.
- **Filtreler:** ürün, ülke, dil, durum, "onaysız eski yol".
- **Onaysız eski yollar:** Bu kurgudan önce oluşturulmuş, zincire bağlı olmayan ve MLR'den geçmemiş yollar ayrıca işaretlenir ("Onaysız — sahada kullanılamaz"). Tek tıkla "zincire bağla ve onaya gönder" akışına girer.

## 2) ÜST BİLGİ (yol çalışma alanı)
- Yol adı, kodu, sürümü, durumu, son revizyonun durumu.
- **Bağlam:**
  - zincir şablonu (ad + sürüm, sabitlenmiş);
  - **ülke** (tek seçim) ve **dil** (o ülkenin dillerinden);
  - **ürün** ve **kitle** zincirden otomatik gelir, salt okunur.
  - Ülke ve dil yalnız taslakta değişir.
- **Eylemler:** Uygunluğu kontrol et, Kopyala, Arşivle, Onaya gönder, Yeni sürüm.

## 3) KURGU ALANI
- **Adımlar ve dallar:** Zincirin her adımı bir bölüm. Dallar ayrı anlatı hatlarıdır; her dalın kendi sıralı adımları vardır. Sahadaki sıra dal dal ilerler: A dalının tüm adımları, sonra B dalı.
- **Her adıma eklenenler:**
  - içerikler (Bilgi Bankası'ndan: başlık, dil, tür, yayın durumu);
  - **iddialar** (kod, metin, onay durumu, hangi ülke sürümü).
- **Adım ayarları:** sıra değiştirme, çıkarma; zorunlu / isteğe bağlı; tahmini süre; ön koşul adımı; adımın beklediği en az / en çok öğe ve eksik uyarısı.
- **Tek dil kuralı:** Yolun dilinden farklı bir içerik eklenemez; neden yazılır.
- **Yayını engelleyecek durumlar önceden görünür:** onaysız iddia, yayında olmayan içerik, ülkede onaylı sürümü olmayan iddia.

## 4) SAYFA TASARIMCISI (sürükle-bırak, broşür / slayt gibi)
- Her adım bir ya da birkaç **sayfaya** dönüşür. Kullanıcı sayfaları serbestçe tasarlar: yerleşim seçer, blokları sürükler, boyutlandırır, hizalar; sayfa ekler, siler, sıralar.
- **Blok türleri:**
  - başlık, metin, görsel, grafik / tablo, ikon, ayırıcı;
  - **iddia bloğu**, **kaynakça bloğu**, **güvenlilik bilgisi bloğu**, **yasal altbilgi**;
  - etkileşim (sekme, açılır bölüm, alt sayfaya git, pop-up).
- **Hazır sayfa şablonları:** marka ekibinin onayladığı yerleşimler (kapak, mekanizma, çalışma sonucu, grafikli kanıt, güvenlilik, kapanış). Kilitli bölgeler ve düzenlenebilir bölgeler ayrı görünür.
- **Görünüm:** tablet yatay (ana hedef), dikey, telefon önizleme.
- **Sayfa şeridi:** küçük resimler; her sayfanın hangi adımdan ve daldan geldiği görünür. Taslak sürekli kaydedilir; geri al / yinele.

## 5) KONTROLLER (tasarım serbest, kurallar kilitli)
Kurallar hem düzenlerken (anında uyarı) hem onaya gönderirken (engelleyici kontrol listesi) çalışır:
1. **Marka kiti (ürün bazında):**
   - renkler yalnız ürün paletinden;
   - logo ve en küçük boyutu, yazı tipleri, en küçük yazı boyutu.
2. **Onaylı mesaj:**
   - İddia bloğunun metni, onaylı iddianın **o ülkedeki, yolun dilindeki** metnidir ve düzenlenemez.
   - Niteleyicileri zorunlu olarak yanında görünür.
   - Dipnot ve kaynakça (kanıt belgesi, sayfa / bölüm) otomatik.
   - Serbest metinde iddiaya benzeyen ifade uyarısı (öneri niteliğinde).
3. **Dengeli sunum:** Etkinlik iddiası varsa güvenlilik bilgisi bloğu zorunlu.
4. **Ülkeye göre zorunlu yasal öğeler:** yasal altbilgi, "Reçeteyle satılır", yan etki bildirim satırı, KÜB / KT atfı, ruhsat sahibi (ülkenin şirketinden otomatik).
5. **Onay kodu ve geçerlilik:** her sayfada otomatik.
6. **Görseller:** yalnız onaylı varlık kütüphanesinden (Belge Yönetimi).
7. **Kilitleme:** Onaylanan revizyonda tasarım kilitlenir; değişiklik yeni sürüm ister.
- **"Uyum kontrolü" paneli:** tüm ihlaller sayfa ve blok bazında; tıklayınca bloğa gider; ihlal ve düzeltme yolu yazar.

## 6) MLR ONAYI VE YAYIN
- "Onaya gönder" yolun o anki halinin dondurulmuş bir **revizyonunu** oluşturur ve MLR akışını başlatır. Taslak düzenlenmeye devam edebilir.
- **Onay akışı:** Medikal → Hukuk → Ruhsat, sıralı. İş öğeleri Görev Merkezi'ne düşer; ekranda akışın hangi adımda olduğu ve kimin beklediği görünür.
- **İnceleyici görünümü:**
  - sayfalar;
  - sayfaya **iğnelenmiş notlar**;
  - iddia bloğuna tıklayınca yan panelde **kanıt belgesi** (sabitlenmiş sürüm, alıntı);
  - Onayla / Reddet (gerekçe; ret için zorunlu), not.
- **Gönderen kendi yolunu onaylayamaz.**
- **Çıktı oluştur** (onaylı revizyondan): HTML saha sunumu + arşiv PDF'i; dosya bilgileri, parmak izi, önizleme / indirme.
- **Yayınla:**
  - Ön koşul listesi: onaylı, çıktı hazır, tüm iddialar onaylı, tüm içerikler yayında, uyum kontrolü temiz.
  - Yayındaki yol, yolculuk aşamalarında seçilebilir hale gelir.
  - Yeni sürüm yayınlanınca eskisi "yerini aldı" olur.
  - Eski sürümü kullanan yolculuk aşaması varsa uyarı gösterilir.
- **Geri çek** (gerekçe).
- **Revizyon geçmişi:** numara, tarih, gönderen, MLR adımları (kim, karar, not, tarih), çıktılar, yayınlayan, durum. İki revizyon arasında **sayfa sayfa fark**.
- **Hatalar kullanıcı dilinde ve somut:** hangi iddia onaysız, hangi içerik yayında değil, hangi kural ihlal edildi, çıktı yok.

## 7) SAHA SUNUMU ÖNİZLEME VE KULLANIM
- Onaylı revizyonun tablet görünümünde sayfa sayfa oynatılması; etkileşimler çalışır; kaynakça son sayfada, yasal altbilgi her sayfada.
- "Saha sunumu" ile "arşiv PDF'i"nin aynı onaylı revizyondan geldiği görünür (revizyon no + parmak izi). Onaysız revizyon "taslak önizleme" filigranıyla açılır.
- **Nerede kullanılıyor:** bu yolu kullanan etkileşim yolculukları ve aşamaları, bağlı strateji şablonları.
- **Sahadan dönen veri:** sayfa başına gösterim sayısı ve ortalama süre.

## 8) KAPSAM VE SAHİPLİK (karar)
- Yollar, sayfa şablonları, tasarım kütüphanesi: **tenant**.
- Marka kiti: **ürün bazında**.
- Zorunlu yasal bloklar ve dil: **ülke bazında**.
- Ruhsat sahibi / yerel şirket: ülkenin şirketinden veri olarak gelir. Legal entity bir sahiplik ekseni değil.
- Ülke / kişi bazlı erişim kısıtı şimdilik yok.

## 9) ÇOK DİLLİ, TEMA, ERİŞİLEBİLİRLİK
- Arayüz 7 dilde (tr, en, fr, es, zh, ar, ru); Arapça sağdan sola (sayfa tasarımcısı dahil).
- Açık / koyu tema; klavye ile kullanılabilirlik.

## 10) YETKİ
- İzni olmayan kullanıcıya sayfa iskeleti gösterilmez.
- Ayrı yetkiler: yol düzenleme / tasarım, marka kiti ve şablon yönetimi, çıktı oluşturma, yayınlama, geri çekme. İnceleme / onay Görev Merkezi'nde pozisyona göre.
- Düğmeler yetkiye göre görünür.

## 11) ÖRNEK VERİ
- **Ürün:** ALMIBA (levokarnitin, hemodiyaliz hastalarında karnitin eksikliği).
- **Zincir:** "Almiba Hemodiyaliz Karnitin Zinciri".
- **Kitle, ülke, dil:** Nefroloji / Doktor; Türkiye; Türkçe.
- **Yol:** "ALMIBA — HD Karnitin Detay Yolu".
- **İddia:** CLM-ALMIBA-02 "Etki mekanizması". Türkiye v1.0 onaylı: "Levokarnitin, uzun zincirli yağ asitlerini mitokondriye taşıyarak enerji metabolizmasını destekler." Niteleyici: "Reçeteyle satılır." Kanıt: GMG-QMS-SOP-0014 "ALMIBA — Etki Mekanizması" s.1.
- **İçerik:** "ALMIBA — Levokarnitinin etki mekanizması (nefroloji)".
- **MLR örneği:** Medikal onayladı (not: "mekanizma cümlesi uygun"), Hukuk bekliyor.
- **Ek örnekler:** onaysız bir iddia, yayında olmayan bir içerik, marka dışı renk kullanılmış bir sayfa ve bir "onaysız eski yol" satırı. Uyum kontrolü ve hata durumları dolu görünsün.

## 12) SENİN ÖNERİLERİN (serbest alan)
- Yukarıdakilerin dışında kullanıcı deneyimini, uyum güvenliğini ya da saha etkisini artıracak iyileştirmeleri eklemekte serbestsin. Örnek: yapay zekâ destekli yerleşim önerisi, iddia arama, sayfa bazlı yorum akışı, erişilebilirlik kontrolü, bir yolu başka ülke / dile uyarlama akışı.
- **Önerileri mockup'ta "Öneri" etiketiyle ayrıca işaretle**, istenen kapsamdan ayırt edilebilsin. Her öneri için kısa bir gerekçe yaz.
