# Ziyaret Planlama — mockup analizi ve iş sırası

> **CT, 2026-10-06.** Kaynak: `Ziyaret Planlama (standalone).html`; çözülmüş hâli `visit-planning.decoded.html`.
> Dayanaklar: [BRIEF](BRIEF-visit-planning-rep-week.md) · [durum analizi](../../VISIT-PLANNING-current-state-analysis.md) · [mobil not](../../mobile/2026-10-06-visit-planning/MOBILE-NOTE-2026-10-06-visit-planning.md)

## 0. Kullanıcı kararları (2026-10-06)
- K-1 … K-4: bkz. BRIEF (temsilci kendi haftası; sıklığa göre otomatik haftalar; oyun ve segment seçilmez).
- **K-5:** Bölgesi atanmamış temsilci geçiş döneminde **tüm hesapları** görür, üstte "bölge atanmamış" uyarısıyla.
- **K-6:** Atama şimdilik **kullanıcı ↔ bölge** (MOD-0151 kaynak ataması, `resourceType = user`). Pozisyon (İK) üzerinden atama, İK Person kararından sonra.

## 1. Mockup brief'i karşılıyor mu
| Brief | Mockup | Not |
|---|---|---|
| §1 Liste | ✓ Hedefler "122 doktor · 13 eczane", Haftalar "1 onaylı · 3 taslak", boş taslak rozeti + "Boş taslakları sil", "Tamamlandı" (geçmiş dönem), tarih biçimi korunmuş | Temsilci sütunu kalkmış (temsilci görünümü) — uygun |
| §2 Yeni plan | ✓ Ülke otomatik, dönem (aktif + gelecek), hafta (ilk boş; geçmiş seçilemez; "planınız var" → mevcut plana git), temsilci salt okunur; segment ve strateji yok | ⚠ MK-1: form **sağdan açılan panel** olmuş |
| §3 Detay üst | ✓ Durum bazlı eylemler (onayla / yeniden aç / sonraki hafta / bu haftayı üret), haftalık ve dönem kapasite / planlanan + yüzde | — |
| §4 Hedefler | ✓ "Hesaplarım (bölgem)", hesapta "x / y seçili · N bu hafta", hızlı filtreler (bu hafta / hiç görülmeyen / tümü), uzmanlık çoklu seçim, sütunlar sıklık / yapılan–kalan / son ziyaret / durum, izin yok + pasif seçilemez (ipucu), segment rozeti, eczane sekmesi, seçim özeti + saat + kapasite çubuğu, aşım uyarısı, adlı ve hesaba göre gruplu "Seçilenler", "Bölge dışı ekle" | ⚠ MK-2: bölge yoksa boş liste (K-5'e aykırı) |
| §5 Haftalar | ✓ 13 haftalık şerit (durum, ziyaret, doluluk, tatil, uyarı, BUGÜN), kural açıklaması, gün gün doluluk (tatil / yarım gün), kaydırılan / sığmayan hedefler + neden + sonuç, haftadaki doktorlar + dönem noktaları, doktor paneli (sıklık hedefi / yapılan / kalan, dönem boyunca haftalar, "sıradaki içerik: yakında") | Yeni kural: MK-6 |
| §6 Yer bırakma | ✓ "Benim planlarım / Ekip" anahtarı; içerik satırı "yakında" | MK-5 |
| §7 Durumlar | ✓ 9 durum (normal, boş, yükleniyor, hata, aktif dönem yok, kapasite yok, takvim okunamadı, bölge atanmamış, yetkisiz — iskelet yok) | — |
| Rota | ✓ değişmiyor (ekran görüntüsü yer tutucu) | — |

## 2. Mockup'tan çıkan kararlar — **hepsi kullanıcı tarafından kararlaştırıldı (2026-10-06)**; MK-2/3/4/6/7/8 CT önerisiyle kabul
| # | Konu | CT önerisi |
|---|---|---|
| MK-1 | Yeni plan sağ panel (drawer); kullanıcı "form benzer kalsın" demişti | ~~Sayfa formu kalır~~ → **Kullanıcı kararı: mockup'taki gibi sağ panel** (2026-10-06) |
| MK-2 | Bölge yok → boş liste | **K-5 uygulanır:** tüm hesaplar + sarı uyarı bandı; "Bölge dışı ekle" bu durumda gizlenir |
| MK-3 | **Plan = dönem planı**: bir temsilcinin bir dönem için tek planı; içinde haftalar, her haftanın kendi durumu (geçmiş / onaylı / taslak / boş) | **Kabul.** K-2'nin doğal modeli. Backend'de bugün oturum tek durum taşıyor ve tüm haftaları birlikte yazıyor → B-4 |
| MK-4 | Yeniden açma gerekçesi "yöneticinize iletilir" | Yönetici ilişkisi yok. Gerekçe **haftanın geçmişine ve denetim kaydına** yazılır; bildirim yönetici görünümüyle birlikte gelir |
| MK-5 | "Ekip" anahtarı görünüyor | ~~Gizli~~ → **Kullanıcı kararı: mockup'taki gibi görünür**; Ekip, yönetici görünümü gelene kadar etkin değil |
| MK-6 | Kapasite aşılırsa onayda sığmayan hedefler sonraki haftalara kaydırılır | **Kabul**; kaydırma nedeni ve sonucu Haftalar'da listelenir |
| MK-7 | Eczanede "dönemde 2" sabit | Eczane = hesap; sıklık **hesap politikasından** gelir, yoksa doktorlar gibi "dönemde 1 (varsayılan)" |
| MK-8 | "Günde en çok 36 ziyaret" sabit yazı | Günlük üst sınır **dönem kapasitesinden türetilir** (günlük ziyaret dakikası ÷ tipik ziyaret süresi); metin sayıyı veriden alır |
| MK-9 | Yarım gün (28 Ekim) | Çalışma takvimi yarım günü destekliyor; planlayıcı yarım günde kapasiteyi yarıya indirir |

## 3. Sıralı iş listesi (tüm hatalar dahil)
Hata kodları durum analizindendir (A = kararlarla çelişen, B = otomatik haftalar, C = plan kalitesi, D = hata / küçük).

### Faz 0 — veri ve kararlar (hemen, kod değil)
- **0.1** Test temsilcisine bölge ataması (`bestepullukcu` → İstanbul + Kocaeli): kullanıcı Bölge Yönetimi → Kaynak atamaları sayfasından ya da CT script'i (önce deneme, `--apply` kullanıcıda).
- **0.2** AUD-001 kararı (A / B / C) — yeni backend komutları buna takılır.
- **0.3** D8: `asdasdasd` test kaydı — ☑ arşivlendi (2026-10-06).

### Faz 1 — VP-FIX-1 (mockup'tan bağımsız düzeltmeler)
D1 liste hedef sayısı · D2 onaylı plan kilidi (ekran + sunucu) · C2 hafta sonuna ziyaret · C3 çalışma takvimi 400 (tatiller) · A1 strateji seçicisini kaldır · D7 menü (Ziyaret Planlama + Ziyaret Yürütme) · D6 / A5 bugünkü etiketlerin 7 dili (tarih biçimi kalır).

### Faz 2 — backend temel (mobil de bekliyor)
- **B-8** görünen adlar: plan seçimleri + planlanan ziyaretler (D4; mobil D1).
- **B-1** temsilci = oturum + sahiplik: planlar ve planlanan ziyaretler (A2; mobil B01).
- **B-2** hedef evreni = bölge ataması; ataması yoksa tüm hesaplar + uyarı (K-5); "bölge dışı" işareti (A4).
- **B-3** strateji / kampanya / segment sunucuda türetilir, istemci değeri alınmaz; segment dışı doktorun sessizce düşmesi kalkar (A3, A6).

### Faz 3 — backend planlama motoru
- **B-4** dönem planı + hafta durumu (MK-3): haftayı onayla → yalnız o hafta Planlanan Ziyaretler'e yazılır; yeniden aç (gerekçe, MK-4); sonraki hafta otomatik taslak; "bu haftayı üret / yeniden üret". Sıklığı bilinmeyen = dönemde 1 + rozet (B1); dönem içinde eşit dağılım (B2); hafta hafta onay (B3); eczane sıklığı (MK-7).
- **B-5** gün dengeleme: günlük üst sınır kapasiteden (MK-8, C1); hafta sonu / tatil / yarım gün (C2, C3, MK-9); aşımda sonraki haftaya kaydırma (MK-6); kaydırılan / sığmayan listesi ve nedenleri.
- **B-6** "bu hafta görülmesi gerekenler" + doktor başına dönem hedefi / yapılan / kalan / son ziyaret (B4; ziyaret raporlarından).
- **B-7** haftalık ve dönem kapasitesi (C5); ziyaret süresi tipik ziyaret modelinden (C4); seçim özeti için tahmini saat.
- **B-9** segment rozeti alanı.
- **D3** dönem başına tek plan + boş taslak silme; **D5** hedefler için toplu okuma ucu (~100 istek → birkaç).

### Faz 4 — Web arayüzü (mockup'a göre)
- **VP-UI-1** liste + yeni plan paneli (MK-1) + detay üst kısım + durumlar + Ekip anahtarı (MK-5).
- **VP-UI-2** Hedefler sekmesi (bölge uyarısı K-5, bölge dışı ekleme penceresi).
- **VP-UI-3** Haftalar sekmesi + doktor paneli + yeniden açma penceresi.
- Rota'ya dokunulmaz. C6 (çok ziyaretli durakta doktor adları) Rota tasarımını değiştirmeden durak açılınca gösterilir — ayrı küçük iş, kullanıcı onayıyla.

### Faz 5 — mobil
- Faz 2–3 alanları kesinleşince **sözleşme notu** (adlar, sahiplik, sıklık alanları, öneri ucu, hafta durumu); SB-3-MOB ile birlikte.

### Faz 6 — Planlanan Ziyaretler sayfası
- Aynı yöntem: canlı analiz → (gerekirse mockup) → paketler. Bilinen: hedef GUID (D1), sayfa tamamen İngilizce, sahiplik, T1–T3 hedef kuralları, `target_inactive`.

### Faz 7 — sonra
- Yönetici görünümü ("Ekip", MK-5) · "Sıradaki içerik" (SB-3c) · saha temsilcisi rolü + ziyaret raporu yetkileri (F-RBAC) · pozisyon tabanlı atama (K-6 sonrası) · check-in / anti-fraud (MOD-0280).
