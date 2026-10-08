---
title: Diten Pharma test senaryosu — kim kimdir, hangi modül neyi girer
status: living
owner: CT
created: 2026-10-02
---

# Diten Pharma test senaryosu

**Kural (sahip, 2026-09-23; yinelendi 2026-10-02):** modüller boş veriyle değil **bu senaryoyla** kontrol edilir. Bir modülde
girilen veri sonraki modülde aynen kullanılır; bu yüzden sıra bağlayıcıdır. Bir modüle girildi mi, çok büyük iş değilse
bitirilip çıkılır. Bir satır ancak sahip "ok" dedikten sonra ✅ olur; düzeltilen bulgu yeniden test edilmeden ✅ sayılmaz.

Kiracı: dev'de **Platform Admin Tenant** (`00000000-…-0001`); "Diten Pharma" bu kiracının içindeki tüzel kişi (DP-CH).
İlk tur kaydı: [governance-users/2026-09-23.md](governance-users/2026-09-23.md).

## Kadro

| Kişi | E-posta | Rol (Roller → Kullanıcı Rolleri) | Pozisyon (Organizasyon) | Bağlı olduğu | Senaryodaki işi |
|---|---|---|---|---|---|
| Diten Admin | admin@diten.com | SuperAdmin | CEO | — | Kurulumu yapar; en üst onaycı |
| Metin Aydın | metin.aydin@ditenpharma.test | Bölüm Yöneticisi | CTO | CEO | Burak'ın yöneticisi; Global Product onaycısı |
| Burak Şen | burak.sen@ditenpharma.test | Ekip Lideri | DEV-LEAD | CTO | Ayşe ve Deniz'e görev atar, haftalarını onaylar |
| Ayşe Korkmaz | ayse.korkmaz@ditenpharma.test | Çalışan | DEV-ENG | DEV-LEAD | Görev yapar, süre girer, haftasını onaya gönderir |
| Deniz Yalçın | deniz.yalcin@ditenpharma.test | Çalışan | DEV-ENG (ikinci koltuk) | DEV-LEAD | İkinci çalışan; devir ve yeniden atama hedefi |
| Cem Duran | cem.duran@ditenpharma.test | İK Yöneticisi | HR-MGR | CEO | Zaman çizelgesi ayarları ve kategorileri |
| Elif Çetin | elif.cetin@ditenpharma.test | Çalışan | **pozisyon yok** | — | Olumsuz durum: atanamaz, onaycısı bulunamaz |
| Fatih Demir | fatih.demir@ditenpharma.test | — | — | — | Silinmiş hesap (yeniden ekleme bulgusu) |
| Entegrasyon Servisi | svc-kargo@ditenpharma.test | — | — | — | Servis hesabı: kişi seçicilerde çıkmamalı (BL-433) |

Bugünkü ölçüm (2026-10-02, salt okunur): yalnız admin ve Ayşe aktif; Ayşe'nin rolü `deneme`; koltuklar admin=CEO, Ayşe=DEV-ENG,
DEV-LEAD koltuğu Auth'ta olmayan bir kullanıcıda. Roller: Admin, GQD, QADocumentation, ReadOnly, SuperAdmin, Task-Manager,
Task-User, Viewer, deneme.

## Sıra ve her modülün senaryoya kattığı

| # | Modül | Bu modülde girilen | Sonraki modülün kullandığı | Kayıt |
|---|---|---|---|---|
| 1 | Kullanıcılar | Kadrodaki 8 hesap | Herkes | governance-users/2026-09-23.md ✅ |
| 2 | Roller | `Çalışan`, `Ekip Lideri`, `Bölüm Yöneticisi`, `İK Yöneticisi`; `deneme` silme denemesi | Rol İzinleri | — |
| 3 | Rol İzinleri | Dört role izinler (görev, zaman çizelgesi, onay) | Kullanıcı Rolleri, her ekran | — |
| 4 | Kullanıcı Rolleri | Kadro tablosundaki rol atamaları; pasif hesapların aktifleşmesi | Organizasyon, Görev Merkezi | — |
| 5 | Organizasyon | Pozisyon bağlılıkları (kim kime bağlı) ve koltuklar | Görev atama kapsamı, haftanın onaycısı | — |
| 6 | Görev Merkezi | Burak → Ayşe / Deniz görevleri; onaylı görev; Devret | Zaman çizelgesi (süre görevden gelir) | task-center/ |
| 7 | Zaman Çizelgesi | Ayşe'nin haftası: sayaç + elle giriş, onaya gönder, Burak onaylar / reddeder | Görevdeki harcanan süre | — |
| 8 | Toplantı | Ekip toplantısı, tutanak, takip görevi | Görev Merkezi | — |
| 9 | Global Product | Ürün taslağı → onaya gönder → Metin onaylar | GSKU ve sonrası | — |

## Adım adım: modül başına girilecek veri ve bakılacaklar

### 2 · Roller (`/Roles`)
Dört rol: `Çalışan`, `Ekip Lideri`, `Bölüm Yöneticisi`, `İK Yöneticisi`. Denemeler: aynı ad ikinci kez; açıklama değiştir; `deneme`
rolünü sil (Ayşe'ye atanmış); sistem rolünü düzenle / sil; arama, sıralama, sütun gizleme, dışa aktarma; dil değiştir.
**2026-10-02 tur 1:** R1 aynı ad reddi İngilizce · R3 dışa aktarmanın ayrı izni yok · rol olayları Denetim Günlüğü'nde yok → WP-ROLES-CLOSE-01.

### 3 · Rol İzinleri (`/RoleAssignments`)
| Rol | İzinler |
|---|---|
| Çalışan | `platform.tasks.read / create / update / claim / complete` · `time-entry.timesheets.read / update` · `platform.meetings.read` |
| Ekip Lideri | Çalışan + `platform.tasks.assign / cancel / work-report.read` · `time-entry.approvals.read` · `platform.meetings.create / update / minutes-write` |
| Bölüm Yöneticisi | Ekip Lideri + `platform.tasks.read-all` · `time-entry.weeks.reopen` |
| İK Yöneticisi | `time-entry.timesheets.read` · `time-entry.categories.manage` · `time-entry.settings.manage` · `time-entry.weeks.reopen` |

Bakılacak: kaydedince ve sayfa yenilenince kalıyor mu; tabloda olup ekranda bulunmayan izin (bulgu); izin adları anlaşılır mı;
sistem rolünün izinleri değiştirilebiliyor mu; verilen / alınan izin Denetim Günlüğü'nde görünüyor mu.

### 4 · Kullanıcı Rolleri (`/UserRoleAssignments`) — önce Kullanıcılar'da hesapları açın
1. Kullanıcılar'da Metin, Burak, Cem, Deniz, Elif'i aktif edin; giriş yapacak olanlara (en az Burak ve Ayşe) "Parolayı Sıfırla" ile
   bağlantı üretin (dev'de bağlantı ekranda ve Mailpit'te).
   **Rol atamadan ÖNCE** (BL-410 kontrolü): Elif'le giriş yapın — rolsüz hesap Görev Merkezi'ni görür (tasarım gereği); yalnız kendi işini mi
   görüyor, "+ Yeni" gibi yetki isteyen düğmeler çizilmiyor mu?
2. Kadro tablosundaki rolleri atayın: Metin → Bölüm Yöneticisi · Burak → Ekip Lideri · Ayşe, Deniz, Elif → Çalışan · Cem → İK Yöneticisi.
3. Ayşe'den `deneme` rolünü kaldırın; sonra Roller'e dönüp `deneme` rolünü silin.
Bakılacak: bir kişiye iki rol; rol kaldırma; servis hesabı (svc-kargo) listede nasıl görünüyor; atama Denetim Günlüğü'nde, yapan
kişinin adıyla görünüyor mu; Ayşe yeniden giriş yapınca yeni rolünün izinleri geçerli mi (izinler oturum açılırken yüklenir).

### 5 · Organizasyon (`/OrganizationUnits`, `/Positions`, `/PositionAssignments`)
1. Bağlılıklar: DEV-ENG → DEV-LEAD → CTO → CEO; HR-MGR → CEO.
2. Koltuklar: Metin = CTO · Burak = DEV-LEAD (bugün bu koltukta sistemde olmayan bir kullanıcı var: önce onu sonlandırın) ·
   Deniz = DEV-ENG (ikinci koltuk) · Cem = HR-MGR · Ayşe = DEV-ENG (var) · Elif = koltuk YOK (bilerek).
Bakılacak: bir pozisyona iki kişi; koltuğu sonlandırma; ağaç görünümü; silinmiş / sistemde olmayan kullanıcının koltuğu nasıl
görünüyor; değişiklikler Denetim Günlüğü'nde.

### 6 · Görev Merkezi (`/WorkCenterNext`) — iki oturum: Burak ve Ayşe
1. Burak: Ayşe'ye bir görev atar (son tarihli), Deniz'e bir görev atar, Elif'e atamayı dener (pozisyonu yok → seçicide çıkmamalı).
2. Ayşe: görevi kabul eder, başlatır, tamamlar; bir görevi Deniz'e devretmeyi dener (yetkisi yok → ne diyor?).
3. Onaylı görev: Burak onay gerektiren bir görev açar; Ayşe tamamlar; onay Burak'a düşer → Onayla (listeye dönüp bildirim göstermeli),
   bir başkasında **Devret** (kişi soruyor mu; kendisi ve başlatan listede yok mu).

### 7 · Zaman Çizelgesi (`/TimeEntry`) — Cem, Ayşe, Burak
1. Cem (`/TimeEntry/Settings`, `/TimeEntry/Categories`): kategorileri kurar; Diten Pharma şirketi için sayacı gerekçe yazarak açar.
2. Ayşe: görevde sayacı başlatır / durdurur (süre kartı; düğme tek yerde mi), elle süre girer, kategoriyle süre girer, haftayı gönderir;
   gönderdikten sonra geri çekmeyi dener.
3. Burak (`/TimeEntry/Approvals`): Ayşe'nin haftasını görür, bir haftayı onaylar, birini gerekçeyle reddeder.
4. Ayşe: reddedilen haftayı düzeltip yeniden gönderir; onaylanan haftada görevin "harcanan süre"si güncellendi mi.
5. Elif: haftasını göndermeyi dener (onaycısı yok → ne diyor?).

## Bulguların yazımı

Modül harfi + sıra: `R1` (Roller), `Rİ1` (Rol İzinleri), `KR1` (Kullanıcı Rolleri), `O1` (Organizasyon), `G1` (Görev Merkezi),
`Z1` (Zaman Çizelgesi), `T1` (Toplantı), `GP1` (Global Product). Her bulgu bir cümle: nerede, ne yaptım, ne oldu, ne bekliyordum.
CT sınıflar: küçük → CT düzeltir; ağır → ayrı sohbete prompt; çok büyük → iş listesi.
