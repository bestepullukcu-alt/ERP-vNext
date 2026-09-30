# WORK PACKAGE — WP-ORG-02 · MLR departmanları + pozisyonlar (Medikal / Hukuk / Ruhsat) — tenant 97c5 (tarayıcı, KOD YOK)

> **CT (SoR).** Claims onay yol haritası §E'nin 2. adımı. **WP-ORG-01'den SONRA** çalışır; GMG-GRAND MEDICAL ILACLARI tüzel kişiliği var olmalı.
>
> **Kullanıcı kararları (2026-09-28):**
> - **Genel merkez = GMG-GRAND MEDICAL ILACLARI LTD STI (TR)**.
> - Medikal, Hukuk ve Ruhsat departmanları **yalnız TR'de** var ve **tüm ülkelere hizmet ediyor**.
> - Bir kişi birden çok fonksiyonu üstlenebilir.
>
> **Kod değişikliği YOK.**

## Başlangıç durumu (CT okudu, 2026-09-28)
- **Org birimleri:** `diten_personalization_dev.organization_units`
  - `HQ` "Genel Merkez". Tüzel kişiliği **LE-004 "Diten Energy A.Ş."** (demo kayıt, `e4eb36f8-…`).
  - "ewrferfer" ve "zxzxzx" adlı iki anlamsız test birimi. **Dokunma.**
- **Pozisyonlar:** 22 kayıt, hepsi ticari ya da teknik (CEO, CTO, medical-representative, product-manager…). "Genel Merkez" altında. Birim bağı kopuk 5 yinelenen kayıt ve test kayıtları var. **Dokunma.**
- **Kullanıcılar (97c5):** 2 kişi.
  - Admin User (`bestepullukcu@gmail.com`)
  - sema pullukcu (`b.pullukcu@grandmedical.eu`)
- **Atama:** 1 tane (Admin User).

## Hedef yapı
```
LE  GMG-GRAND MEDICAL ILACLARI LTD STI (TR)
OU  HQ "Genel Merkez"  (tip HQ)   ← tüzel kişiliği LE-004 demo → GMG'ye çevrilir (tek alan güncellemesi)
 ├─ OU MED "Medikal"  (tip GroupFunction, LE GMG)   yönetici: POS-MED-DIR
 │     ├─ POS-MED-DIR  Medikal Direktör
 │     └─ POS-MED-SPC  Medikal Uzman            → bağlı: POS-MED-DIR
 ├─ OU LEG "Hukuk"    (tip GroupFunction, LE GMG)   yönetici: POS-LEG-CNS
 │     ├─ POS-LEG-CNS  Hukuk Müşaviri
 │     └─ POS-LEG-SPC  Hukuk Uzmanı             → bağlı: POS-LEG-CNS
 └─ OU REG "Ruhsat"   (tip GroupFunction, LE GMG)   yönetici: POS-REG-MGR
       ├─ POS-REG-MGR  Ruhsat Müdürü
       └─ POS-REG-SPC  Ruhsat Uzmanı            → bağlı: POS-REG-MGR
```

- **Birim tipi GroupFunction:** Merkezdeki birimler tüm ülkelere hizmet veriyor, yani grup fonksiyonu. İleride bir ülkede yerel ekip kurulursa o ülkenin tüzel kişiliği altına `Department` tipinde birim açılır.
- **Pozisyonlar:** tip Permanent, **durum Active** (varsayılan Draft; etkinleştirilmezse workflow adayı olamaz), FTE 1.
- **Workflow'daki karşılığı (A2'de kullanılacak):** Medikal adımının adayları POS-MED-DIR ve POS-MED-SPC; Hukuk adımınınkiler POS-LEG-CNS ve POS-LEG-SPC; Ruhsat adımınınkiler POS-REG-MGR ve POS-REG-SPC. Adımı bu pozisyonlardan herhangi biri onaylar. Tüm ülkeler bu şablonu kullanır.

## Atamalar (test için)
Tenant'ta 2 kullanıcı var. Workflow testinde **gönderen onaylayamaz** kuralı geçerli, bu yüzden:
- **Admin User** iddiaları hazırlar ve gönderir. **MLR pozisyonuna atanmaz.**
- **sema pullukcu:** POS-MED-DIR (**Primary**), POS-LEG-CNS (**Secondary**), POS-REG-MGR (**Secondary**). Tek kişi üç fonksiyonu üstlenir; kullanıcı kararıyla bu izinli.
- Uzman pozisyonları (`*-SPC`) boş kalır.
- Başlangıç tarihi bugün, bitiş boş, neden: Hire / Transfer.

> ⚠ Atamalar gerçek bir kişiyi etkiler. Kullanıcı farklı bir dağılım isterse bu bölüm değişir.

## KORU / YAPMA / DUR
- **İzin verilen yazmalar yalnız şunlar:**
  - "Genel Merkez"in tüzel kişilik alanının GMG'ye çevrilmesi (başka alanı değişmez);
  - 3 birim;
  - 6 pozisyon (+ etkinleştirme);
  - 3 birimin yönetici pozisyonunun ayarlanması;
  - sema pullukcu'nun 3 ataması.
- **Dokunulmayacaklar:** Mevcut pozisyonlar, test birimleri ve kayıtları, Admin User'ın mevcut ataması, tüzel kişilikler. Silme ve arşivleme yok. DB'ye doğrudan yazma yok (okuma serbest).
- Şifre ya da kimlik bilgisi girilmez. Oturum yoksa DUR. Kullanıcının oturum açık diğer sekmelerine dokunulmaz, ayrı sekme kullanılır.
- Her kayıt oluşturulmadan önce aynı kodla var mı diye bakılır (idempotency).
- **DUR:**
  - GMG tüzel kişiliği yoksa, yani WP-ORG-01 bitmemişse.
  - Birim formunda GroupFunction tipi yoksa: `Department` tipiyle devam et ve raporla.
  - "Genel Merkez"in tüzel kişiliği bağlı pozisyonlar yüzünden değiştirilemiyorsa: departmanları yine GMG altında oluştur, üst birimi "Genel Merkez" yap ve raporla.
  - Pozisyon etkinleştirme onay ya da yetki istiyorsa: oluşturmayı bitir, etkinleştirmeyi raporla.
  - 5xx hata alınırsa: dur ve raporla.

## Acceptance
- `/OrganizationUnits`: "Genel Merkez" (LE GMG) altında Medikal, Hukuk, Ruhsat var ve her birinin yöneticisi dolu.
- `/Positions`: 6 yeni pozisyon Active, bağlılıkları doğru.
- `/PositionAssignments`: sema pullukcu'nun 3 ataması aktif (1 Primary, 2 Secondary).
- DB salt-okuma sayımları:
  - `organization_units`: +3;
  - `positions`: +6;
  - `position_assignments`: +3.
- Workflow şablon ekranında (`/Workflow`, varsa) pozisyon seçicide 6 pozisyon görünüyor. Yoksa raporla; A2 adımında kullanılacak.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ORG-01 bittikten SONRA)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ORG-02 · MLR departmanları + pozisyonlar (Medikal / Hukuk / Ruhsat) — tenant 97c5 (tarayıcı, KOD YOK)
Repository: C:\Users\user\Desktop\ERP-vNext · Branch: test/crm-content-visit-e2e (kod değişikliği YOK, commit YOK)

Amaç: Claims MLR onay akışı için organizasyonu kur. Genel merkez = GMG-GRAND MEDICAL ILACLARI LTD STI (TR); Medikal/Hukuk/Ruhsat yalnız TR'de ve tüm ülkelere hizmet ediyor. Ön koşul: WP-ORG-01 bitti (GMG tüzel kişiliği var) — yoksa DUR.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ORG-02-mlr-org-units-positions.md (hedef yapı, kodlar, atamalar, izinli yazmalar) · execution/domains/commercial-suite/work-packs/SCMM-claims-approval-evidence-roadmap.md §E.

Ön koşul: oturum açık mı (97c5); değilse DUR, kullanıcıdan tarayıcıda giriş yapmasını iste — şifre GİRME.

YAP (sırayla; her kayıttan önce kodla var mı kontrol et):
 1) /OrganizationUnits: "Genel Merkez" (HQ) tüzel kişiliğini LE-004 demo → GMG-GRAND MEDICAL ILACLARI olarak güncelle (yalnız bu alan).
 2) 3 birim: MED "Medikal", LEG "Hukuk", REG "Ruhsat" — tip GroupFunction, tüzel kişilik GMG, üst birim Genel Merkez.
 3) /Positions: POS-MED-DIR Medikal Direktör, POS-MED-SPC Medikal Uzman (→POS-MED-DIR), POS-LEG-CNS Hukuk Müşaviri, POS-LEG-SPC Hukuk Uzmanı (→POS-LEG-CNS), POS-REG-MGR Ruhsat Müdürü, POS-REG-SPC Ruhsat Uzmanı (→POS-REG-MGR); tip Permanent, FTE 1, birimleri kendi departmanı; hepsini Active yap.
 4) Birimlerin yönetici pozisyonu: MED→POS-MED-DIR, LEG→POS-LEG-CNS, REG→POS-REG-MGR.
 5) /PositionAssignments: sema pullukcu (b.pullukcu@grandmedical.eu) → POS-MED-DIR Primary, POS-LEG-CNS Secondary, POS-REG-MGR Secondary; başlangıç bugün, bitiş boş. Admin User'a MLR ataması YAPMA (gönderen onaylayamaz kuralı).
KORU/YAPMA: yazma YALNIZ yukarıdaki 5 madde; mevcut 22 pozisyon, test birimleri (ewrferfer/zxzxzx), Admin'in mevcut ataması, tüzel kişilikler DOKUNMA; silme/arşiv YOK; DB'ye doğrudan yazma YOK (okuma serbest); kod/commit YOK; kullanıcının diğer oturumlu sekmelerine dokunma, ayrı sekme.
Durma: GMG yoksa DUR; GroupFunction tipi yoksa Department ile devam+raporla; Genel Merkez'in tüzel kişiliği değiştirilemiyorsa departmanları yine GMG altında oluştur+raporla; pozisyon etkinleştirme yetki/onay istiyorsa oluşturmayı bitir, raporla; 5xx → dur, raporla.
RAPOR (§22 TÜRKÇE): oluşturulan/güncellenen kayıtlar (kod, ad, id, durum, bağlılık); atamalar; DB salt-okuma sayımları (organization_units +3, positions +6, position_assignments +3); workflow ekranında pozisyon seçicide 6 pozisyon görünüyor mu (ekran varsa); ekran görüntüleri; konsol/ağ hataları. K13.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (canlı veri)**
- ✅ HQ "Genel Merkez" artık **GMG** (`1754b820-…`) altında. MED, LEG ve REG: tip **GroupFunction (5)**, tüzel kişilik GMG, üst birim HQ, yöneticileri dolu.
- ✅ 6 pozisyon **Active (1)**; birimleri ve bağlılıkları (`*-SPC → *-DIR/CNS/MGR`) doğru.
- ✅ Atamalar: `d27fa4a6-…` = **b.pullukcu@grandmedical.eu** → POS-MED-DIR Primary, POS-LEG-CNS Secondary, POS-REG-MGR Secondary. Üçü aktif, iptal yok. Admin'in (`c5769c62`) CEO ataması aynı kaldı.
- ✅ Test birimleri ve mevcut pozisyonlar değişmedi. Workflow pozisyon seçicisi 6 pozisyonu döndürüyor.
- ℹ HQ'nun tipi **Department** kaldı (WP yalnız tüzel kişilik alanına izin veriyordu). İsteğe bağlı olarak **HQ** tipine çevrilebilir; kullanıcı kararı.
