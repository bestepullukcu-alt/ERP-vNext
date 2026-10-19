# Ziyaret akışı — Ziyaret Planlama, Planlanan Ziyaretler, Ziyaret Yürütme, ziyaret raporu

> **CT, 2026-10-08.** Kullanıcı: "Bunları unutma, bir daha anlatmanı isteyeceğim." Kod ve iş paketleriyle doğrulanmış özet; Faz 6 / SB-3c öncesi durum.

Üç sayfa bir zincirin halkalarıdır. Kodda **"Ziyaret Raporları" adında ayrı bir sayfa yok**: raporlar Ziyaret Yürütme içinde yazılır ve düzeltilir.

```
Ziyaret Planlama ──onay──▶ Planlanan Ziyaretler ──günü gelince──▶ Ziyaret Yürütme (+ rapor)
   (haftanın planı)            (tek tek ziyaret kaydı)                (yapıldı mı, ne sunuldu)
                                                                             │
                     doktorun "yapılan / kalan", "son ziyaret", "yapıldı" rozeti ◀─┘
```

## 1. Ziyaret Planlama (`/CRM/VisitPlanning`): "Bu hafta kimi göreceğim?"
- Temsilci, dönem başına **tek plan** tutar.
- Hedef kurum / doktor seçer, doktora ürün seçer.
- Sistem ziyaretleri **sıklığa göre** haftalara, **coğrafyaya ve gün bütçesine göre** günlere dağıtır.
- Temsilci gerekirse bir ziyareti ya da kurumu başka güne sabitler.
- Hafta **onaylanınca** o haftanın ziyaretleri Planlanan Ziyaretler'e yazılır; sonraki haftalar onaylanana kadar yalnız **taslaktır** (kayıt değildir).
- **Yeniden açma** gerekçe ister; raporsuz ziyaretler iptal edilir (`week_reopened`).
- Sekmeler: Hedefler · Haftalar · Rota. (Faz 1–4, 4J)

## 2. Planlanan Ziyaretler (`/CRM/PlannedVisits`): "Kime, ne zaman, ne sunacağım?"
- **Amacı:** her ziyaretin resmi kaydı. Bir ziyaret = hedef (doktor ya da kurum) + tarih / saat + süre + sunulacak içerik.
- **Kayıtların kaynağı:**
  - **planlamadan** (`source = route-plan`): hafta onayında yazılır. Ürün listesi (`contentItems`), yolculuk / aşama ve süre o an **dondurulur**.
  - **elle** (`source = manual`): "Yeni" ile plan dışı tek ziyaret. Hedef, tarih, saat, süre, amaç, tür, not ve isteğe bağlı yolculuk / aşama girilir.
- **İşlemler:**
  - listele / süz;
  - ayrıntı;
  - düzenle;
  - **onayla** (kesinleşti);
  - **iptal** (gerekçe zorunlu);
  - **arşivle**.
- **Yaşam döngüsü:** planlandı → onaylandı → iptal / arşiv.
- **Arka planda tutulanlar:** ziyaretin neden seçildiği, yani köken bilgisi:
  - sıklık kuralı;
  - izin (KVKK);
  - içeriğin hangi oyundan geldiği (temsilci görmez, ARCH GATE);
  - doktorun uygunluğu.
- **Durum: eski tasarım → Faz 6.**
  - Sayfa İngilizce.
  - Ad yerine GUID görünüyordu (backend adları artık gönderiyor).
  - Elle oluşturma kuralları T1–T3 netleşmedi: hedef kişi mi işyeri mi; pasif hedef (`target_inactive`); düzenlemede hedef değişir mi.
  - Ek konular: numune, amaç, ortak ziyaret.

## 3. Ziyaret Yürütme (`/CRM/VisitExecution`): "Bugün ne yapacağım, ne yaptım?"
- **Amacı:** temsilcinin günlük saha ekranı. Liste değil, **gün / hafta takvimi**.
- Her ziyarette **"Ne sunacağım"**: dondurulmuş ürünler, yolculuk / aşama, içerik adımları (E2E-FIX-1).
- **Hızlı işaretleme:** yapıldı / yapılamadı / ertelendi (yeni tarih + not).
- **Rapor** (yan panel), içinde:
  - sonuç;
  - **gerçekte sunulan** aşama (varsayılan planlanan, değiştirilebilir);
  - numuneler;
  - geri bildirim;
  - neden kodu.
- **Kurallar:**
  - ileri tarihli ziyaret kapatılamaz / raporlanamaz (`409 visit_not_yet_due`); ertelemek serbest;
  - **rapor değişmez**, yalnız **düzeltme (amendment)** eklenir (denetim kuralı);
  - yetkiler ayrı: `crm.visit-report.record` ve `crm.visit-report.amend`.
- **Sonra (SB-3c):** ziyaret başlat / tamamla akışı; sunulanın doktorun yolculuk ilerlemesine yazılması.

## 4. Ziyaret raporu (ayrı sayfa değil, bir kayıt)
- Ziyaret Yürütme'den yazılan **değişmez** kayıt, planlanan ziyarete bağlı (`visit_reports.PlannedVisitId`).
- Sistemin geri kalanı ziyaretin yapıldığını buradan anlar:
  - Ziyaret Planlama'da doktorun **yapılan / kalan**, **son ziyaret**, **bu hafta görülmeli** bilgisi raporlardan hesaplanır;
  - Haftalar'daki yeşil **"yapıldı"** (`reportStatus`) buradan gelir;
  - hafta yeniden açılınca **raporlu ziyaret iptal edilmez**.
- **İleride (SB-3c):** rapordaki "gerçekte sunulan" → yolculuk ilerlemesi → sıradaki ziyaretin aşaması.

## Özet tablo
| Sayfa | Soru | Kim | Durum (2026-10-08) |
|---|---|---|---|
| Ziyaret Planlama | Bu hafta kimi göreceğim? | temsilci | yeni tasarım (Faz 1–4) + 4J |
| Planlanan Ziyaretler | Ziyaretlerim neler, durumları ne? | temsilci, yönetici | eski tasarım → **Faz 6** |
| Ziyaret Yürütme | Bugün ne sunacağım, ne yaptım? | temsilci | çalışıyor; başlat / tamamla + ilerleme **SB-3c** |
