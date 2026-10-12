# Kapanış sonrası program — sıra, İK sayfa haritası ve bekleyenler

**Durum:** PLAN · **Tarih:** 2026-10-12 · **Sahibi:** Control Tower · **Onay:** sahip (2026-10-12, "zamanı gelince buradan bakıp ona göre çalıştırırız")
**Çalışma şekli:** tek sohbet, sahibin yönlendirmesiyle. CT her adım için prompt'u, hangi sohbeti (yeni ya da mevcut), modeli ve effort'u verir. Sayfa sayfa gidilir; her sayfada eksikleri ve o sayfanın backlog kayıtlarını CT çıkarıp sahibe sorar.

> Bu belge ileriye dönük bir plandır. Kapanış bittikçe ve sayfalar ölçüldükçe güncellenir.

---

## 1. Sıra

1. **Kapanış:** açık işlerin bitmesi, hat doğrulaması, tek PR. Bu belge yazılırken sürüyor.
2. **Stok ekibine "hazır"** (2–3 gün; başka bir ekip bekliyor). Bkz. §2.
3. **Yöneticinin programı:** Görev Merkezi, doküman yönetimi, toplantı, görev, toplantı raporu (tutanak), zaman çizelgesi ve bunların doğru çalışması için gereken İK modülleri.
   - Önce **1 günlük durum ölçümü**: her alanda ne var, ne eksik, ne bozuk; İK'dan tam olarak ne gerekiyor. Sonunda büyüklük ve sırayla tek plan çıkar; sahip yöneticisiyle onaylar.
   - Beklenen sıra:
     1. İK temeli (§3);
     2. görev ve Görev Merkezi;
     3. toplantı ve tutanak;
     4. zaman çizelgesi;
     5. doküman yönetimi.
4. **Paralel (ayrı worktree, ayrı sohbet):** oturum kapatma güvenlik işi (§4.1).
5. **Sahiple konuşulacaklar:**
   - MOD-0290'ın sırası (P6 GTIN, P9 ambalaj, İTS / barkod modülü BL-616);
   - iş ortağı modeli (BL-620);
   - Driver.js sayfa turları;
   - backlog önceliklendirmesi.

## 2. Stok ekibine "hazır" için kalanlar

Kaynak: `docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md` §4a (stok ekibiyle uzlaşma, S0–S7).

| Kalan | Büyüklük | Not |
|---|---|---|
| S2 kalem etkinleştirme onayı: hatla birleşim | orta | kapanışın içinde |
| S6 birim çevrimi kayıtlar arası tutarlılığı (çelişen oranlar, aynı boyut, hassasiyet) | küçük–orta (1–2 prompt) | tek kayıt kuralları S3'te var |
| MDM yetki manifesti adımı (`mdm.product-master.read` + stok anahtarları; Platform listesi 38 → 41) | küçük (1 prompt) | BL-592 |
| S7 dev örnek verisi + stok ekibine "hazır" bildirimi | küçük | veriyi sahip girer, adımları CT verir |
| BL-585 MDM'i stok varlığı ucuna bağlama | küçük–orta (1–2 prompt) | Stok ekibi ucu verdi (2026-10-12): `GET http://127.0.0.1:5067/api/internal/inventory/stock-presence?skuId=…&skuLevel=Item|Gsku|Lsku|FinishedGood`; başlıklar `X-Tenant-Id` + `X-Internal-Api-Key: ditenmdmservice:<sır>`; `Authorization` GÖNDERİLMEZ (kullanıcı belirteçli istek reddedilir). Sır sahibin parola yöneticisinde; sahip MDM user-secrets'a kendisi girer, CT sırsız komut verir; hiçbir dosyada / günlükte / mesajda geçmez. 5067 servis port bandının (5011–5060) dışında → port listesine eklenir. |

Yapılmış olanlar: S0 sözleşme, S1 kalem çekirdeği, S3 stok davranışı alanları, S4 okuma sözleşmesi, S5 Kalemler sayfası + GSKU / LSKU / Bitmiş Ürün "Stok davranışı" panelleri, stok izin anahtarları (BL-573).

## 3. İK sayfa haritası (Görev Merkezi / toplantı / zaman çizelgesi için)

Ölçüm: Blueprint master 8.1 (`docs/reference/blueprint/…master 8.1.xlsx`, Module Pages + Blueprint_Data) ve `origin/main` (2026-10-12).

| Sayfa | Neden gerekli (örnek) | Blueprint | origin/main'de |
|---|---|---|---|
| Çalışan kaydı | Göreve atanan, toplantıya katılan kişi buradan gelir | MOD-0251 | `HCM/Employees` (gmg-10, 2026-07) |
| Organizasyon birimi | ekip / departman; "Ekibim" görünümü | MOD-0288 | `Organization/OrganizationUnits` (CT, 2026-09-21) |
| Pozisyon | onaycıyı pozisyonla bulmak ("Kalite Müdürü onaylar") | MOD-0288 | `Organization/Positions` (CT) |
| Pozisyon ataması / yönetici hiyerarşisi | kim kime bağlı; yönetici ekibinin görevlerini görür | MOD-0299 | ⚠ **İki tane:** `Organization/PositionAssignments` (CT) + `HumanCapital/PositionAssignments` (Cihan, 2026-09-14). Hangisinin kalacağı kararı gerekli. |
| Vekâlet | izindeki yöneticinin onayları vekile | MOD-0288 Delegation & Assignment Matrix | sayfa görünmüyor; ölçülecek |
| Çalışma takvimi + resmi tatiller | bitiş tarihi iş günüyle; tatile toplantı konmaz; beklenen saat | Platform çalışma takvimi | `Platform/WorkingCalendars`, `WorkingCalendarImports`, `WorkingCalendar/Overrides` (CT, 2026-08-28) |
| İzin / devamsızlık | izindekine görev atanırken uyarı / vekâlet; toplantıda "izinli"; zaman çizelgesinde izin günü | MOD-0280 + MOD-0315 | kısmen: `HumanCapital/TimeAttendanceLeave` (Cihan, 7 dosya); ne kadarı çalışıyor, ölçülecek |
| İzin bakiyesi + izin talebi | talep onaya gider (Görev Merkezi) | MOD-0315 Leave Balance View | görünmüyor |
| Vardiya / çalışma planı | vardiyalı çalışanın beklenen saati, toplantı zamanı | MOD-0315 Timesheet / Roster | **yok** |
| Çalışan / yönetici self servis | Görev Merkezi ile çakışır (Approval Inbox) | MOD-0319 | yok |
| İşe alıştırma, çalışan projeksiyonları | dolaylı | MOD-0303 | `HumanCapital/EmployeeOnboarding`, `EmployeeProjections` (Cihan) |

**Sahibin notu:** main'deki İK sayfalarının bir kısmı başka geliştiricinin ve %100 bitmiş olmayabilir. Ölçümde her sayfa açılıp şunlar kontrol edilir:
- gerçek mi taklit mi veri;
- kaydetme;
- yetki (UAS-001);
- 7 dil.

**İK temeli sırası:**
1. çalışan;
2. organizasyon / pozisyon;
3. pozisyon ataması (yinelenme kararıyla);
4. çalışma takvimi;
5. izin;
6. vardiya.

## 4. Bekleyen backlog (kapanıştan sonra, sırayla)

### 4.1 Paralel worktree: oturum kapatma (büyük, 4+ prompt)
- BL-587: pasife alınan ya da parolası sıfırlanan kişinin açık oturumu 2 saate kadar çalışıyor.
- BL-532, BL-463: yetki değişikliği geç yansıyor.
- Kullanıcılar + Roller kapanışının madde 1'i: 401'de isteği yenileyen ortak katman (~80 dosya) ve kısa belirteç (`min(15, SessionTimeoutMinutes)`).

### 4.2 Tek sohbet, sırayla: Kullanıcılar / Roller / Rol İzinleri

| Sayfa | Ne görülür (düzelmediyse) | Kayıt | Büyüklük |
|---|---|---|---|
| Kullanıcılar | "admin@diten.com" her ortamda kodda sabit parola özetiyle oluşturuluyor | BL-556 / BL-534 | orta (canlı kararı sahibin) |
| Kullanıcılar | yönetici olmayan birini silerken de "son yönetici silinemez" | BL-558 | küçük |
| Kullanıcılar | e-postası olmayan kullanıcıya parola bağlantısı ulaşmıyor | BL-618 | küçük |
| Kullanıcılar | silme / pasife alma reddi ham kod; "Parolayı sıfırla" eski parolayı geçersiz kılmıyor; liste hatasında İngilizce uyarı | BL-450 / BL-529 / BL-515 | düzeltilmiş görünüyor, sahip kontrolüyle kapatılacak |
| Roller | admin olmayan yöneticiye onay izni verilemiyor | BL-506 | küçük (takeover dalından taşıma) |
| Roller | kalite / onaycı rolü kiracı ekranından kurulamıyor (Doküman Yönetimi izinleri platform kapsamlı, bazı izinler katalogda yok) | BL-582 | orta |
| Roller | firma yöneticisinin rol işlemleri platform hesabının yetkisini daraltabiliyor | BL-550 | orta |
| Rol İzinleri | yetkisiz kişi sayfa iskeletini görüyor | BL-617 | küçük |
| Rol İzinleri | grup başlıkları "mod0251 / person / lookups" kod olarak | BL-507 / BL-342 | küçük |
| Rol İzinleri | İngilizce kalan satır / düğme ("Read All") | BL-508 | küçük (7 dil) |
| Rol İzinleri | "Görevi Güncelle" tek izni 14 işlemi açıyor | BL-341 | orta: önce çipe "kapsadıkları" ipucu, sonra 3–4 izne bölme (düzenle / yürüt / incele / yapı) |
| Rol İzinleri | katalogdan silinen modülün izinleri listede kalıyor | BL-339 | küçük |

### 4.3 Diğer
- BL-619: eski Görev Merkezi (`/WorkCenter`, taklit) kaldırılır; aynı denetimde kullanılmayan / yarım sayfalar listelenir.
- BL-620: iş ortağı kaydı yok (kimlik ve izinli kiracılar elle GUID). Karar: modeli tamamla ya da rolü kaldır.

Büyüklük ölçüsü:
- küçük: 1 prompt, yaklaşık yarım gün;
- orta: 2–3 prompt, 1–2 gün;
- büyük: 4 ve üzeri prompt, 3–5 gün.
