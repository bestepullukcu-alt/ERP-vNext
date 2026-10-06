# Ziyaret Planlama (`/CRM/VisitPlanning`) — durum analizi

> **CT, 2026-10-06.** Canlı gezinti (97c5, kullanıcı girişi, ayrı sekme, yalnız okuma — kayıt / üretim / uygulama yapılmadı) + kod okuması.
> Örnek plan: `#0848afed` (Türkiye 2026 Q4, 41. hafta, onaylandı, 122 doktor + 13 eczane + 36 hesap → 176 ziyaret).

## 0. Kullanıcı kararları (2026-10-06)
- **K-1:** Ziyaret Planlama'yı **temsilci kendi haftası için** kullanır. İleride yönetici görünümü ayrı eklenebilir (c) — tasarım buna kapı bırakır.
- **K-2:** Bir hafta planlandıktan sonra **sonraki haftalar doktorun / kurumun ziyaret sıklığına göre otomatik** planlanır.
- **K-3:** Temsilci **strateji şablonu ("oyun") seçemez** (mimari kural: oyun ve kampanya sunucuda türetilir, temsilci görmez).

## 1. Sayfa bugün ne yapıyor
| Ekran | İçerik |
|---|---|
| Liste "Taslak planlarım" | Plan (#GUID kısa), dönem, hafta, temsilci (e-posta), durum, hedefler, güncellendi; işlemler: Görüntüle, Rota oluştur, Detaylar |
| Yeni plan | Ülke, dönem, hafta, **temsilci (kullanıcı listesinden seçilir)**, segment (çoklu ama yalnız ilki uygulanır), **strateji şablonu** |
| Detay — özet | Dönem, temsilci, hafta, durum, tarih aralığı, çalışma saatleri; eylemler: "Bu haftanın planı olarak kaydet", Route oluştur, Re-plan |
| Detay — kartlar | Arz / Talep / Planlanan / Planlanmayan |
| Detay — Hedefler | Hesap ekle (klinik / hastane) → seçili hesabın doktorları (uzmanlık filtresi) + bağlı eczaneler; seçim özeti |
| Detay — Rota | Gün sekmeleri (Pzt–Cum), hafta seçici, günün durakları (saat, kurum, doktor, ziyaret türü, yürüme / yol süresi, öğle arası), harita, sürüklenebilir durak sırası |
| Uygula | Planlanan Ziyaretler'e yazar (`source = route-plan`) |

Backend'de **sıklıkla genişletme var** (`FrequencyExtendPlanner`, MOD-0155 FU05): 1. haftadaki her hedef için MOD-0165 sıklık politikası çözülür, sonraki haftalara aynı oturumda eklenir.

## 2. Bulgular
### 2.1 Kararlarla çelişen (K-1 / K-3)
| # | Bulgu | Etki |
|---|---|---|
| A1 | Formda **strateji şablonu seçici** var | K-3 ihlali; ayrıca seçici zaten boş: API `templateId / templateName` dönüyor, `form.js` `strategyTemplateId / name` okuyor |
| A2 | **Temsilci** kullanıcı listesinden seçiliyor (e-posta) | Temsilci başkası adına plan yapabilir; K-1'de temsilci = oturumdaki kişi olmalı (mobil B01 / `resources/me` ile aynı konu) |
| A3 | **Segment** seçici temsilciye açık ("pick stays manual") | Segment yönetici / pazarlama kavramı; temsilcinin evreni = kendi bölgesi / atanmış doktorları |
| A4 | Hesap ekleme 43K hesabın tamamından ("bölge dışı uyarılır, gizlenmez") | Temsilcinin evreni bölge ataması olmalı; bölge dışı istisna |
| A5 | Liste ve detayda yönetici kavramları / İngilizce: "Turn a rep's selection…", "Cycle period", "Route oluştur", "Re-plan", "New session" | Temsilci diliyle yeniden yazılmalı |

### 2.2 Otomatik haftalar (K-2)
| # | Bulgu |
|---|---|
| B1 | Sıklık **109 / 177 ziyarette `unknown`** → bu hedefler yalnız 1. haftada; sonraki haftalara hiç taşınmadı. Sıklık politikası olmayan doktor için varsayılan yok. |
| B2 | Genişletme 41. haftadan **44. haftaya** atladı (42–43 boş); kadansın dönem içine dağılımı görünür değil, temsilci "dönem boyunca kimi kaç kez göreceğim" bilgisini göremiyor. |
| B3 | Sonraki haftalar ayrı bir "plan" olarak görünmüyor; aynı oturumun içinde. Hafta hafta onay / düzeltme akışı yok. |
| B4 | Doktor başına **dönem hedefi / yapılan / kalan** (frekans uyumu) gösterilmiyor. |

### 2.3 Planın kalitesi
| # | Bulgu |
|---|---|
| C1 | **Günlük yük dengesiz:** Pzt 57, Sal 51, Çar 27, Per 7, **Cum 0**. Günlük üst sınır / dengeleme yok. 20 doktor tek kurumda art arda (10:18–12:53). |
| C2 | **Pazar günü ziyaret** üretilmiş: `VP-0848afed-0176` → 2026-10-25 (Pazar), `source = route-plan`. |
| C3 | **Çalışma takvimi okunmuyor:** `api/working-calendar` → 400 (`'X-Tenant-Id' is not allowed on admin endpoints`) — tatiller (ör. 29 Ekim) planlamaya girmiyor. |
| C4 | Tüm ziyaretler **3 dakika**. Plan CAP-MODEL'den (tipik ziyaret süresi) önce üretildi; yeni üretimde doğrulanmalı. |
| C5 | **Arz 6543** (tüm dönemin kapasitesi) ile **Talep 176** (bu oturum) aynı satırda — birimler farklı, karşılaştırma yanıltıcı. Haftalık kapasite gösterilmeli. |
| C6 | Rotada çok ziyaretli duraklarda doktor adları görünmüyor ("7 ziyaret"); ne sunulacağı (içerik / SB-3) rotada yok. |

### 2.4 Hatalar / küçükler
| # | Bulgu |
|---|---|
| D1 | Liste **"Hedefler" sütunu hep 0** (API `selectedContactCount` 122 dönerken) — alan adı uyuşmazlığı. |
| D2 | Onaylanmış planda "Bu haftanın planı olarak kaydet", "Hedefleri kaydet", "Düzenle" hâlâ açık. |
| D3 | 17 planın 10'u **boş taslak** (0 hedef) — yarım kalan oturumlar birikiyor; temizleme / tek aktif taslak kuralı yok. |
| D4 | Hedefler sekmesi "Seçilenler" listesi **ham GUID**; doktor satırında "BAĞLANTI" sütunu GUID (mobil D1 ile aynı konu). |
| D5 | Detay açılışında ~100 istek (hesap başına `contacts` + `related-accounts`, bazıları iki kez). |
| D6 | Dil / biçim: İngilizce etiketler (Accounts, Doctors, Specialty, Out-of-territory…), uzmanlık adları İngilizce, kurum türü kodu büyük harfle (HOSPİTAL, CLİNİC), tarih "5 Oct, 26", ülke adları İngilizce. |
| D7 | **Ziyaret Planlama ve Ziyaret Yürütme menüde yok** (menüde yalnız Planlanan Ziyaretler, Dönemler, Dönem Kapasitesi, Sıklık Politikaları). |
| D8 | Kayıtlarda manuel test kalıntısı: `asdasdasd` (2026-09-14, taslak). |

## 3. Eksik olan (temsilci akışı için)
1. **Benim haftam** girişi: oturumdaki temsilci, dönem ve hafta otomatik; başka kullanıcı / segment / oyun seçimi yok.
2. **Hedef evreni = bölge ataması** (MOD-0151); doktor kartında segment / oyun yerine yalnız sonuç: önerilen sıklık, dönem hedefi / yapılan / kalan, son ziyaret.
3. **Otomatik hafta önerisi:** 1. hafta onaylanınca sonraki haftalar sıklığa göre taslak gelir; temsilci hafta hafta görür ve düzeltir. Sıklığı bilinmeyen hedef için varsayılan kural (ör. dönemde 1) ya da açık uyarı.
4. **Gün dengeleme + kısıtlar:** günlük ziyaret üst sınırı, hafta sonu / tatil (çalışma takvimi), izin / bölge dışı gün.
5. **"Ne sunacağım"** her ziyarette (SB-3 içerik çözücüsü) — rotada ve ziyaret detayında.
6. **Haftalık kapasite** göstergesi (dönem kapasitesinin haftaya düşen payı) — CAP-MODEL ile.
7. Yönetici görünümü (ileride, c): ekibin haftaları, uyum, onay.

## 4. Mobil talepleriyle kesişim
- **B01 sahiplik** ↔ A2 (temsilci = oturum kullanıcısı; sunucu sahiplik filtresi).
- **D1 görünen adlar** ↔ D4 + Planlanan Ziyaretler listesi / detayı + Ziyaret Yürütme takvimi.
- **Kaynak modeli (`resources/me`)** ↔ A2 / A4 (temsilci ↔ kaynak ↔ bölge).
- **T1–T3 hedef kuralları** ↔ Hedefler sekmesi (pasif hedef, düzenlemede hedef değişimi).

## 5. Öneri — sıra
1. **Mockup brief** (temsilci "Benim haftam" + otomatik haftalar + gün dengeleme + ne sunacağım; yönetici görünümüne kapı) → kullanıcı mockup yaptırır.
2. Mockup'tan bağımsız hemen yapılabilecek düzeltmeler (küçük paket): D1, D2, C2, C3, A1 (seçiciyi kaldır), D6 / A5 dil, D7 menü.
3. Backend: temsilci = oturum (A2/B01), bölge evreni (A4), sıklığı bilinmeyen hedef kuralı (B1), gün dengeleme + takvim (C1–C3).
4. Ardından Planlanan Ziyaretler (D1 adları, sahiplik, T1–T3) aynı yöntemle.
