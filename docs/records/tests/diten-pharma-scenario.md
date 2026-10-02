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

## Bulguların yazımı

Modül harfi + sıra: `R1` (Roller), `Rİ1` (Rol İzinleri), `KR1` (Kullanıcı Rolleri), `O1` (Organizasyon), `G1` (Görev Merkezi),
`Z1` (Zaman Çizelgesi), `T1` (Toplantı), `GP1` (Global Product). Her bulgu bir cümle: nerede, ne yaptım, ne oldu, ne bekliyordum.
CT sınıflar: küçük → CT düzeltir; ağır → ayrı sohbete prompt; çok büyük → iş listesi.
