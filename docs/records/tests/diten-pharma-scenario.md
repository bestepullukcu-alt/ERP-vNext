---
title: Diten Pharma test senaryosu — kim kimdir, hangi modül neyi girer
status: living
owner: CT
created: 2026-10-02
---

# Diten Pharma test senaryosu

**Kural (sahip, 2026-09-23; yinelendi 2026-10-02):** modüller boş veriyle değil **bu senaryoyla** kontrol edilir. Bir modülde
girilen veri sonraki modülde aynen kullanılır; bu yüzden sıra bağlayıcıdır. Bir modüle girildi mi, çok büyük iş değilse
bitirilip çıkılır. Bir satır ancak sahip "ok" dedikten sonra ✅ olur; düzeltilen bulgu yeniden test edilmeden ✅ sayılmaz. İşaretleme yeri ve
kimlikli liste: aşağıda "Sahip kontrol listesi".

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
| 1 | Kullanıcılar | Kadrodaki 8 hesap; sıradan kiracı yöneticisi `yonetici@diten.com` (9 Ekim) | Herkes | ✅ BİTTİ — governance-users/2026-09-23.md; yönetici hesabı kontrolü 9 Ekim |
| 2 | Roller | `Çalışan`, `Ekip Lideri`, `Bölüm Yöneticisi`, `İK Yöneticisi`; `deneme` silme denemesi | Rol İzinleri | ✅ BİTTİ — sahip kontrol etti (5 Ekim), kapandı (9 Ekim) |
| 3 | Rol İzinleri | Dört role izinler (görev, zaman çizelgesi, onay) | Kullanıcı Rolleri, her ekran | ✅ BİTTİ — veri girildi, ölçüldü (2 Ekim), kapandı (9 Ekim) |
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

## Sahip kontrol listesi (2026-10-09)

**Kural (sahip, 2026-10-09):** sahibin kontrol edeceği her şey tek yerde durur. İşaretlenen yer, sahibin yan menüsüne
sabitli senaryo sayfasıdır: https://claude.ai/artifact/1H4gCCJWsf1EbFJWPBrFVr. Her satırda "Tamam" / "Sorun var" + not;
işaretler sayfanın veritabanında (`checks/<kimlik>`) tutulur, CT okur. Bu bölüm o listenin depodaki kopyasıdır. CT kilometre
taşlarında işaretleri buraya aktarır. Yeni kontrol önce sayfaya YENİ kimlikle eklenir (kimlik yeniden kullanılmaz), sonra buraya.
Dağınık test listesi verilmez.

Durum anahtarı: `[x]` sahip "Tamam" dedi · `[ ]` bekliyor · ⏳ kod henüz dev'de değil.

### Sahibin ekran dışı işleri
- [x] `is-stok-mesaj` Stok ekibine ve kalite birimine Control Tower'ın hazırladığı mesajı gönderin (stok: yazılı onay + 8 soru; kalite: IU birimi + kalem onaycısı).
- [x] `is-stok-onay` Stok ekibinin yazılı onayı geldi (Global Ürün okumasında stok alanları dönmez; SKU'dan okunur). Gelince Control Tower'a iletin — bu onay olmadan kalem okuma bağlantıları canlıya çıkmaz.
- [x] `is-kalite-cevap` Kalite biriminin cevabı geldi: IU birimi (HIU mı, kendi kodumuz mu) ve kalem etkinleştirmesini onaylayacak pozisyon.
- [ ] `is-push-hat` Ana çalışma dalını GitHub'a gönderin (PR değil): `git -C /Users/alitufanoglu/ERP-vNext/.claude/worktrees/kanban-preview push origin chore/ct-round-2`.
- [ ] `is-stok-cevap-2` Push'tan sonra stok ekibine ikinci cevabı gönderin (kabul edilenler, üç çekince, R-12 beklentisi, bütün MVP'ler cevabı).
- [ ] `is-crm-mesaj` CRM ekibine Marka değişiklikleri mesajını gönderin.
- [ ] `is-qa-pozisyon` Kalem onayından (S2) önce dev'de "QA Müdürü" pozisyonu + en az iki kişi.
- [ ] `is-canli-hesap` Canlı sorusunu cevaplayın: ekibiniz canlıda kiracı ekranlarına platform hesabıyla mı giriyor? (Evetse canlıda da sıradan yönetici hesabı önceden açılmalı.)
- [ ] `is-rol-onerisi` Canlı rol önerisini inceleyin: 13 rol, görev ayrımı kuralları (kendi üretim, fason, satış). Dosya: `docs/records/analysis/roles/2026-10-09-canli-rol-onerisi.md`. Hangi rollerin canlıda açılacağını söyleyin.
- [ ] `is-ik-viewer` Canlı sorusu (güvenlik): canlıda İnsan Sermayesi modülü açık mı, "Viewer" rolü kimlerde? Açıksa Viewer ücret / yan hak ekranlarını görebiliyor (BL-581).
- [ ] `is-mod0290-sorular` MOD-0290 ürün yapısı için 5 soru (tek ürün kaydı, Lokal SKU ekseni, fason ürünler, ülkeye özel ambalaj, canlı onaycı). Harita: `docs/reference/modules/tenant/MOD-0290-urun-kalem-sku-haritasi.md`.
- [ ] `is-marka-plan` MDM ürün ekranları dev'e gelince: Platform → Planlar → ürün ana verisini içeren her plana "Marka" modülünü ekleyin (dev'de siz; canlıda onayınızla).
- [ ] `is-canli-url` Canlıya çıkarken: e-posta bağlantı adresi (AuthService:FrontendBaseUrl) canlı adresle girilir. Girilmezse yeni kiracının ilk yönetici daveti reddedilir.
- [ ] `is-pr` En sonda: tek PR'ı ana dala açın (Control Tower hazır olduğunu söyleyince).

### Yönetici hesabı — ✅ BİTTİ (9 Ekim, sahip)
- [x] `yh-1` Yeni kullanıcı oluştu ve Mailpit'e parola belirleme e-postası geldi mi?
- [x] `yh-2` Bağlantıyla parola belirleyip yonetici@diten.com ile giriş yapabildiniz mi?
- [x] `yh-3` Yeni hesapla Kullanıcılar ve Roller sayfaları açılıyor, bir kullanıcıyı düzenleyip kaydedebiliyor musunuz?

### Kullanıcılar + Roller — ✅ BİTTİ (kontrol 5 Ekim, kapanış 9 Ekim)
- [x] `r-1` Ad alanına yalnız boşluk yazınca Türkçe uyarı çıkıyor ve kullanıcı oluşmuyor mu?
- [x] `r-2` Kendi hesabınızı pasife alma / silme sunulmuyor mu?
- [x] `r-3` Davet aşamasındaki birine "Parolayı Sıfırla" deyince doğru Türkçe cümle çıkıyor mu?
- [x] `r-4` Rol oluşturup silince Denetim Günlüğü'nde iki satır sizin adınızla görünüyor mu?
- [x] `r-5` Viewer rolünden "Dışa aktar" iznini kaldırınca kaydoluyor mu?
- [x] `r-6` Admin (sistem rolü) satırında kilit var, izinleri değiştirilemiyor mu?

### Rol İzinleri — ✅ BİTTİ (veri 2 Ekim, kapanış 9 Ekim)
- [ ] `ri-1` İzni verip kaydedince doğru görünüyor, sayfayı yenileyince kalıyor mu?
- [ ] `ri-2` Yukarıdaki izinlerin hepsi ekranda bulunuyor mu?
- [ ] `ri-3` Sistem rolünün (Admin) izinleri değiştirilemiyor mu?

### Kullanıcı Rolleri — Yönetici hesabından sonra
- [ ] `kr-1` Rolsüz Elif yalnız kendi işini görüyor, yetki isteyen düğmeler çizilmiyor mu?
- [ ] `kr-2` Bir kişiye iki rol verilebiliyor, rol kaldırma çalışıyor mu?
- [ ] `kr-3` Servis hesabı (Entegrasyon Servisi) listede nasıl görünüyor? (notta yazın)
- [ ] `kr-4` Ayşe yeniden giriş yapınca yeni rolünün izinleri geçerli mi?
- [ ] `kr-5` "Parolayı Sıfırla" → Mailpit'e "parola sıfırlama" e-postası geliyor (davet değil) ve bağlantıyla yeni parola konabiliyor mu?
- [ ] `kr-6` Sıfırlanan kişinin başka tarayıcıdaki açık oturumu, sayfa yenilenince düşüyor mu?
- [ ] `kr-7` Pasife alınan kişinin eski bağlantısı hesabı AÇMIYOR ("yönetici tarafından pasifleştirildi"); etkinleştirince aynı bağlantı çalışıyor mu?
- [ ] `kr-8` "Daveti yeniden gönder" → "parola sıfırlama" e-postası geliyor ve eski parola artık giriş yapmıyor mu?

### Bildirim Şablonları — İstediğiniz zaman (5 dk)
- [ ] `bs-1` İkinci sekme "başkası değiştirdi" anlamında bir uyarı veriyor ve birincinin değişikliğini ezmiyor mu?

### Organizasyon — Sırada
- [ ] `o-1` Bir pozisyona iki kişi atanabiliyor mu?
- [ ] `o-2` Koltuğu sonlandırma çalışıyor mu?
- [ ] `o-3` Ağaç görünümü bağlılıkları doğru gösteriyor mu?
- [ ] `o-4` Sistemde olmayan kullanıcının koltuğu nasıl görünüyor? (notta yazın)

### Görev Merkezi — Sırada
- [ ] `g-1` Burak "Onayla"ya basınca sayfa listeye dönüp bildirim gösteriyor mu?
- [ ] `g-2` "Devret" kişi soruyor; listede kendisi ve işi başlatan kişi YOK mu?
- [ ] `g-3` Elif kişi seçicide çıkmıyor mu?
- [ ] `g-4` Ayşe'nin yetkisi olmayan işlemde ekran Türkçe ve anlaşılır bir cümle söylüyor mu?

### Zaman Çizelgesi — Sırada
- [ ] `z-1` Sayaç düğmesi görev detayında tek yerde mi (süre kartında)?
- [ ] `z-2` Onaylanan haftadan sonra görevdeki "harcanan süre" güncellendi mi?
- [ ] `z-3` Reddetme gerekçesi Ayşe'ye görünüyor mu?
- [ ] `z-4` Elif'e ekran anlaşılır bir cümle söylüyor mu?

### Toplantı — Sırada
- [ ] `t-1` Davet Ayşe'ye ulaştı mı (ekranda ve Mailpit'te)?
- [ ] `t-2` Takip görevi Görev Merkezi'nde doğru kişide görünüyor mu?

### Global Ürün — Dev'e gelmedi ⏳
- [ ] `gp-1` Onay Metin'in Görev Merkezi'ne düştü, başlığı anlaşılır mı?
- [ ] `gp-2` Onaydan sonra ürünün durumu değişti mi?
- [ ] `gp-3` Hazırlayan kendi taslağını onaylayamıyor mu?

### GSKU + Lokal SKU — Dev'e gelmedi ⏳
- [ ] `sku-1` GSKU onaydan sonra etkin oluyor mu?
- [ ] `sku-2` Lokal SKU yalnız kapsamı olan şirkette açılabiliyor mu?

### Marka — Dev'e gelmedi ⏳
- [ ] `m-1` Etkin marka bağlanıyor; süresi geçmiş marka seçilemiyor mu?
- [ ] `m-2` Arşivlemeden önce "geri alınamaz" uyarısı çıkıyor mu?

### Bitmiş Ürün — Dev'e gelmedi ⏳
- [ ] `fg-1` Taslak oluşturma ve iptal anlaşılır cümlelerle çalışıyor mu?

### Tüzel Kişi Kapsamı — Dev'e gelmedi ⏳
- [ ] `k-1` Kapsam dışındaki şirket ürünü görmüyor mu?

### Kalem / Malzeme — Ekranı yapılıyor ⏳
- (adımlar kod dev'e gelince eklenecek)

### Profilim + E-posta dili — Yapılıyor ⏳
- (adımlar kod dev'e gelince eklenecek)

## Bulguların yazımı

Modül harfi + sıra: `R1` (Roller), `Rİ1` (Rol İzinleri), `KR1` (Kullanıcı Rolleri), `O1` (Organizasyon), `G1` (Görev Merkezi),
`Z1` (Zaman Çizelgesi), `T1` (Toplantı), `GP1` (Global Product). Her bulgu bir cümle: nerede, ne yaptım, ne oldu, ne bekliyordum.
CT sınıflar: küçük → CT düzeltir; ağır → ayrı sohbete prompt; çok büyük → iş listesi.
