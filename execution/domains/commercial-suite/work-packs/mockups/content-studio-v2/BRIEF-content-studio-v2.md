# MOCKUP BRIEF — İçerik Stüdyosu v2 (set kurgusu · sayfa tasarımcısı · onay ve yayın · saha sunumu)

> Tarih: 2026-09-30 · Hazırlayan: CT · Kullanıcı kararları: tenant sahipliği + ülke katmanı; tasarım serbest ama marka ve mevzuat kuralları kilitli; çıktı = HTML saha sunumu + aynı kaynaktan arşiv PDF'i.
> Mockup yapana not: görsel tasarım dili senin kararın. Aşağıdakiler amaç, içerik ve kurallardır. Mevcut ürünün genel düzenine (sol menü, üst çubuk, kart yapısı, açık/koyu tema) uyman yeterli.

## BAĞLAM
- **Kullanıcılar:** Çok ülkeli bir ilaç şirketinin (merkez Türkiye; Belarus, Özbekistan, Türkmenistan, Gürcistan, Azerbaycan) medikal ve pazarlama ekibi.
- **Hedef:** Saha temsilcilerinin doktor ziyaretinde tablette göstereceği sunumları hazırlamak.
- **Yol:** Sunum bir "anlatı zinciri"ne göre kurulur (örn. ihtiyaç → fayda → klinik kanıt → güvenlilik), sayfa sayfa tasarlanır, medikal / hukuk / ruhsat onayından geçer, yayınlanır.
- **Yayın sonrası:** Sunum sahaya iner; temsilcinin hangi sayfayı ne kadar gösterdiği ölçülür.

## 1) ÜST BİLGİ
- Set adı, kodu, durumu (taslak / pasif / arşiv), son revizyonun durumu.
- Bağlı zincir şablonu (ad + sürüm, sabitlenmiş) ve içerik kapsamı (ürün, pazar / ülke, kitle, dil).
- Eylemler: Uygunluğu kontrol et, Kopyala, Arşivle, Onaya gönder.

## 2) KURGU ALANI (bugün var; iyileştirilebilir)
- Zincirin her adımı bir bölüm. Dallar ayrı anlatı hatlarıdır: her dalın kendi sıralı adımları var; sahadaki sıra dal dal ilerler.
- **Her adıma eklenenler:**
  - bileşen içerikler (Bilgi Bankası'ndan: başlık, dil, tür, yayın durumu);
  - iddialar (kod, metin, onay durumu, ülke sürümü).
- Sıra değiştirme, çıkarma; adımın beklediği en az / en çok öğe ve eksik uyarısı.
- **Yayını engelleyecek durumlar önceden görünür:** onaysız iddia, yayında olmayan içerik, farklı dilde bileşen (bir set tek dilde olmalı).

## 3) SAYFA TASARIMCISI (yeni — sürükle-bırak, broşür / slayt gibi)
- Kurgudaki her adım bir ya da birkaç **sayfaya** dönüşür. Kullanıcı sayfaları serbestçe tasarlar: yerleşim seçer, blokları sürükler, yeniden boyutlandırır, hizalar, sayfa ekler / siler / sıralar.
- **Blok türleri:**
  - başlık, metin, görsel, grafik / tablo, ikon, ayırıcı;
  - **iddia bloğu**, **kaynakça bloğu**, **güvenlilik bilgisi bloğu**, **yasal altbilgi**;
  - etkileşim (sekme, açılır bölüm, alt sayfaya git, pop-up).
- **Hazır sayfa şablonları:** marka ekibinin onayladığı yerleşimler (kapak, mekanizma, çalışma sonucu, grafikli kanıt, güvenlilik, kapanış). Şablonda kilitli bölgeler ve düzenlenebilir bölgeler ayrı görünür.
- **Görünüm modları:** tablet yatay (ana hedef), dikey, telefon önizleme.
- Sayfa küçük resimleri şeridi; her sayfanın hangi zincir adımından ve dalından geldiği görünür.
- **Sürüm:** taslak sürekli kaydedilir; son değişiklikleri geri alma / yineleme.

## 4) KONTROLLER (tasarım serbest, kurallar kilitli)
Yazar tasarımda serbesttir, ama şunları bozamaz. Kurallar hem düzenlerken (anında uyarı) hem onaya gönderirken (engelleyici kontrol listesi) çalışır:
1. **Marka kiti (ürün bazında):**
   - Renkler yalnız ürünün paletinden; serbest renk seçilemez ya da uyarı verir.
   - Logo, yerleşimi ve en küçük boyutu; yazı tipleri; en küçük yazı boyutu.
2. **Onaylı mesaj:**
   - İddia bloğunun metni onaylı iddianın (ülke sürümünün, içerik diline göre) metnidir ve **düzenlenemez**. Niteleyicileri zorunlu olarak yanında görünür.
   - Otomatik dipnot numarası ve kaynakçada karşılığı (kanıt belgesi, sayfa / bölüm).
   - Serbest metin bloğunda iddiaya benzeyen ifade uyarısı (öneri niteliğinde).
3. **Dengeli sunum:** Bir etkinlik iddiası varsa güvenlilik bilgisi bloğu sunumda zorunludur.
4. **Ülkeye göre zorunlu yasal öğeler:** yasal altbilgi, "Reçeteyle satılır", yan etki bildirim satırı, KÜB / KT atfı, ruhsat sahibi (ülkenin şirketinden otomatik).
5. **Onay kodu ve geçerlilik:** Her sayfada onay / revizyon kodu ve geçerlilik tarihi otomatik basılır.
6. **Görseller:** yalnız onaylı varlık kütüphanesinden (Belge Yönetimi). Yeni yükleme onay sürecine girer.
7. **Kilitleme:** Onaylanan revizyonda tasarım kilitlenir; değişiklik yeni revizyon ister.
- **"Uyum kontrolü" paneli:** tüm ihlaller sayfa ve blok bazında listelenir; tıklayınca ilgili bloğa gider; ihlal türü ve nasıl düzeltileceği yazar.

## 5) ONAY VE YAYIN (bugün ekranı yok)
- "Onaya gönder", setin o anki halinin dondurulmuş bir kopyasını (**revizyon**) oluşturur; taslak düzenlenmeye devam edebilir.
- **Revizyon adımları:**
  - **a.** Gönderildi → İncelemede.
  - **b.** İnceleme: medikal / hukuk / ruhsat inceleyicileri sayfaları görür.
    - Sayfa üzerine **not iğnelenir**; iddia bloğuna tıklayınca kanıtı yan panelde açılır.
    - Karar: Onayla / Reddet (gerekçe); karar veren kaydedilir.
  - **c.** Çıktı oluştur (onaylı revizyondan): HTML saha sunumu + arşiv PDF'i. Dosya bilgileri, parmak izi, önizleme / indirme.
  - **d.** Yayınla. Yayınlayan, onaylayandan **farklı** olmalı; ekranda açıkça söylenir. Ön koşul listesi: onaylı, çıktı hazır, tüm iddialar onaylı, tüm bileşenler yayında, uyum kontrolü temiz.
  - **e.** Geri çek (gerekçe).
- **Revizyon geçmişi:** numara, tarih, gönderen, inceleyenler ve kararları, çıktılar, yayınlayan, durum. İki revizyon arasında **sayfa sayfa fark** görünümü.
- **Yayın sonrası "Üretilenler":**
  - Bilgi Bankası içeriği (kod, başlık, bağlantı);
  - Bilgi Yolu (kod, adım sayısı, bağlantı).
  - Geri çekmede "bu yol bir yolculukta kullanılıyor, pasife alınmadı" uyarısı gösterilebilmeli.
- **Hatalar kullanıcı dilinde ve somut:** hangi iddia onaysız, hangi bileşen yayında değil, onaylayan = yayınlayan, çıktı yok, hangi kural ihlal edildi.

## 6) SAHA SUNUMU ÖNİZLEME
- Onaylı revizyonun tablet görünümünde sayfa sayfa oynatılması. Etkileşimler çalışır.
- Kaynakça son sayfada; yasal altbilgi her sayfada.
- "Saha sunumu" ile "arşiv PDF'i"nin aynı onaylı revizyondan geldiği görünür (revizyon no + parmak izi).
- Onaysız revizyon "taslak önizleme" filigranıyla açılır.
- **Sahadan dönen veri (bilgi amaçlı gösterim):** sayfa başına gösterilme sayısı ve ortalama süre (ziyaret raporlarından).

## 7) KAPSAM VE SAHİPLİK (karar)
- Setler, sayfa şablonları, tasarım kütüphanesi: **tenant** (şirket geneli).
- Marka kiti: **ürün bazında**, tenant.
- Zorunlu yasal bloklar ve dil: **ülke bazında**.
- Ruhsat sahibi / yerel şirket bilgisi: ülkenin şirketinden **veri olarak** altbilgiye gelir. Legal entity bir sahiplik ekseni değildir.
- Ülke / kişi bazlı erişim kısıtı şimdilik yok (ileride İK yapısı oturunca).

## 8) ÇOK DİLLİ, TEMA, ERİŞİLEBİLİRLİK
- Arayüz 7 dilde (tr, en, fr, es, zh, ar, ru); Arapça sağdan sola (sayfa tasarımcısı dahil).
- Açık / koyu tema. Klavye ile kullanılabilirlik.

## 9) YETKİ
- İzni olmayan kullanıcıya sayfa iskeleti gösterilmez.
- Ayrı yetkiler: kurgu / tasarım düzenleme, marka kiti ve şablon yönetimi, inceleme / onay, çıktı oluşturma, yayınlama, geri çekme. Düğmeler yetkiye göre görünür.

## 10) ÖRNEK VERİ
- **Ürün:** ALMIBA (levokarnitin, hemodiyaliz hastalarında karnitin eksikliği).
- **Zincir:** "Almiba Hemodiyaliz Karnitin Zinciri".
- **Kitle ve dil:** Nefroloji / Doktor; Türkçe; ülke Türkiye.
- **İddia:** CLM-ALMIBA-02 "Etki mekanizması". Türkiye v1.0 onaylı: "Levokarnitin, uzun zincirli yağ asitlerini mitokondriye taşıyarak enerji metabolizmasını destekler." Niteleyici: "Reçeteyle satılır." Kanıt: GMG-QMS-SOP-0014 "ALMIBA — Etki Mekanizması" s.1.
- **Bileşen:** "ALMIBA — Levokarnitinin etki mekanizması (nefroloji)".
- **Ek örnekler:** onaysız bir iddia, yayında olmayan bir bileşen ve marka dışı renk kullanılmış bir sayfa. Uyum kontrolü ve hata durumları dolu görünsün.

## 11) SENİN ÖNERİLERİN (serbest alan)
- Yukarıdakilerin dışında kullanıcı deneyimini, uyum güvenliğini ya da saha etkisini artıracak iyileştirmeleri eklemekte serbestsin. Örnek: yapay zekâ destekli yerleşim önerisi, iddia arama, sayfa bazlı yorum akışı, erişilebilirlik kontrolü, çok dilli set kopyalama.
- **Önerileri mockup'ta "Öneri" etiketiyle ayrıca işaretle**, istenen kapsamdan ayırt edilebilsin. Her öneri için kısa bir gerekçe yaz.
