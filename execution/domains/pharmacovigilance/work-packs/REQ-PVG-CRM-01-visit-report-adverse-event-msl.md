# TALEP — REQ-PVG-CRM-01 · CRM ziyaret raporundan PV'ye advers olay ve endikasyon dışı soru

> **Kimden:** Commercial Suite / CRM ziyaret raporu ekibi (sahip tarafından iletildi, 2026-10-21).
> **Kime:** PVG Control Tower.
> **Tarih:** 2026-10-21.
> **Durum:** AÇIK — **ertelendi** (sahip: "zamanı gelince yapılabilir"). Bu kayıt karar, kapsam onayı ya da iş paketi
> değildir; dört soruyu sınıflandırır, PVG'nin bugünkü olgularını ve Control Tower itirazlarını yazar. Kuyruk satırı
> açılmadı.
> **CRM tarafındaki karşılığı:** `execution/domains/commercial-suite/work-packs/ROADMAP-visit-workspace.md` satır 57
> (K-W8 "Advers olay → hangi PV ekranı / kaydı; endikasyon dışı → MSL kim", W5) ve satır 84 (W5 bildirimler: "advers olay
> → PV (24 sa), endikasyon dışı → MSL") @ 0ff7cf430; rapor modeli `WP-VW-W4-report-v2.md` satır 167 @ 536bea2db:
> `Safety`: `AdverseEvent` bool + `AdverseEventNote?` · `OffLabelRequest` bool.
> **Kural notu:** CRM kodu ve belgeleri commercial-suite'indir; PVG onlara dokunmaz (XMC-001). Mevzuat ifadeleri
> (ICH E2D, EU GVP Modül VI) **genel bilgidir, depoda kaynağı yoktur**; QPPV (farmakovijilanstan sorumlu nitelikli kişi)
> doğrulamadan karar sayılmaz.

## Özet sınıflandırma

| # | Soru | Sınıf | Sahip | Ne zaman yapılabilir | Gelecek riski |
|---|---|---|---|---|---|
| 1 | Advers olay PV'de hangi kayda düşer, uç / olay var mı | **Var olan modülün takibi (FU adayı):** MOD-0230 Case Intake & Triage — sistemden sisteme alım sözleşmesi | PVG (MOD-0230) + CRM (MOD-0155 Field Sales / Visit Planning) | MOD-0230 build-gate açıkları (OD-PVG-2 kimlik bağlamı) kapanınca sözleşme; canlı alım yalnız operasyonel runtime kapısı açılınca | 🔴 CRM geçici bir e-posta yolu kurarsa denetimsiz ikinci bir PV kanalı oluşur |
| 2 | 24 saat kuralı: saat ne zaman başlar, hangi zaman damgası | **Var olan modül alanı + süreç kararı:** MOD-0230 alanı (`ReceivedAtUtc` dışında "farkına varma anı" yok); yasal süreler MOD-0233 Reporting & Submissions (W-4) | QPPV (süreç) + PVG (alan) | Hemen karar verilebilir (alan + SOP); kod MOD-0230 revizyonunda | 🔴 Saat yanlış yerden başlarsa geç bildirim, denetim bulgusu |
| 3 | Asgari alanlar; CRM'de hasta kimliği yok, takip nasıl | **Var olan modül alanı:** MOD-0230 alan sözleşmesi + MOD-0231 Case Processing (takip) | PVG + QPPV | MOD-0230 bir sonraki paket revizyonunda | 🟡 Eksik alanla gelen bildirim geçerli vaka olmaz, takip yükü artar |
| 4 | Endikasyon dışı soru → MSL; atama nerede, kim yönetir | **PVG kapsamı dışı — başka sahip / aday:** Blueprint 8.8'de MSL / Medical Information modülü yok | Medikal (Medical Affairs) sahibi; kimlik için önce Master'da Module Change Request | PVG'nin işi değil; Master-first kararıyla | 🟡 Endikasyon dışı kullanım + advers olay birlikteyse PV'ye de gitmezse kaçak |

## 1. Advers olay — PV'de hangi kayıt, uç mu e-posta mı

**Ölçülen olgular (PVG, `73cce019a`):**

- Doğru hedef kayıt MOD-0230 "Case Intake & Triage" taslağıdır (`SafetyCaseIntake`); paket alanları `IntakeChannel`,
  `SourceType`, `SourceReference`, `ReporterType` ile kaynağı ayırır
  (`execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, "Entity Fields").
- Bir uç var (`POST /api/pv-case-intake-triage`) ama bugün **kullanılamaz**: servis JWT doğrulamıyor, kiracı ve aktörü
  istek başlığından alıyor, izin kapısı her isteği reddediyor, depo bellek içi (paket "Build-Gate Gaps" G-1, G-2, G-10).
  Operasyonel runtime kapısı kapalı; üretim kullanımı onaylı değil.
- Sistemden sisteme alım için sözleşme yok: paketin rol matrisinde "PVG System Integration — Approved intake contract
  only" satırı var, sözleşmenin kendisi yazılmamış. Olay (outbox / mesaj kuyruğu) tabanlı alım da tanımlı değil.
- Denetim kaydı: dört yazma komutu GxP sınıfı (AUD-001 K2: denetim kaydı yoksa işlem yok); PVG'de denetim yolu yok
  (OD-PVG-9).

**Control Tower itirazları (körü körüne kabul edilmez):**

- Sorunun "uç mu e-posta mı" diye iki seçenek sunması eksik: bugün **ikisi de PV sistemi değildir**. Uç kapalı; e-posta
  ise advers olay anlatısını (PHI) denetim izi olmayan bir kanala taşır. Geçici yol bir şirket SOP'si kararıdır (QPPV),
  yazılım kararı değil. CRM, QPPV onayı olmadan e-posta ile "PV'ye bildirildi" saymamalı.
- CRM raporunda advers olay rapora bağlı; rapor gecikirse bildirim de gecikir (CRM W5 "rapor süresi doldu" zamanlayıcısı
  raporların geç kapanabildiğini gösteriyor). Advers olay işaretlendiği anda rapordan **bağımsız** iletilmelidir.
- CRM'in advers olay notunu kendi veritabanında süresiz tutması ikinci bir PHI deposu yaratır; saklama, maskeleme ve
  denetim sorumluluğu CRM'e geçer. Öneri: CRM yalnız "iletildi + PV referansı" tutar, anlatıyı PV'ye aktarır.

**Önerilen yanıt (öneri — onay değil):** hedef kayıt MOD-0230 alım taslağıdır. Sistem yolu: CRM → PV olay ya da
servis kimliğiyle API çağrısı; `IntakeChannel` = saha ziyareti, `SourceType` = CRM, `SourceReference` = CRM rapor
kimliği. Bu sözleşme MOD-0230'un takibi olarak yazılır; canlıya alınması operasyonel runtime onayına bağlıdır. O güne
kadar iletim QPPV'nin onayladığı SOP kanalıyla yapılır.

## 2. 24 saat kuralı

**Ölçülen olgular:** paket yalnız `ReceivedAtUtc` alanını tanımlar ve "istemci zaman damgasına güven politikası hâlâ
onay ister" der ("Entity Fields" satırı); "farkına varma anı" (day zero) diye ayrı bir alan yoktur. Yasal raporlama
süreleri Blueprint'te MOD-0233 Reporting & Submissions'ındır (W-4; pakette yok).

**Control Tower itirazı:** sorunun öncülü ("saat PV tarafında hangi andan sayılıyor") yanlış olabilir. Genel mevzuat
bilgisine göre (EU GVP Modül VI) yasal saat, şirketin **herhangi bir çalışanı** asgari bilgiyi ilk öğrendiğinde başlar —
yani temsilcinin ziyarette öğrendiği an, PV'nin aldığı an değil. "24 saat içinde PV'ye ulaşsın" kuralı bir şirket iç
SOP'sidir ve bu ilk andan sayılır. QPPV doğrulamalı.

**Önerilen yanıt (öneri — onay değil):** CRM iki zaman damgası gönderir: (a) temsilcinin olayı öğrendiği an (ziyaret
anı, UTC), (b) temsilcinin işaretleyip gönderdiği an (UTC). PV alındığı anı kendisi yazar. MOD-0230'a "farkına varma
anı" alanı bir sonraki paket revizyonunda eklenir; hangi damganın güvenilir sayılacağı QPPV kararıdır.

## 3. Asgari alanlar; hasta kimliği

**Ölçülen olgular:** MOD-0230'da zorunlu: `IntakeChannel`, `SourceType`, `ReceivedAtUtc`, `ReporterType`,
`AdverseEventNarrative`; **isteğe bağlı:** `SuspectProductText`, `PatientSubjectCode`, `ReporterContactSummary`,
`EventOnsetDate`. CRM raporunda `AdverseEventNote` isteğe bağlı ve güvenlik bölümünde ürün bağlantısı yok.

**Control Tower itirazları:**

- Genel mevzuat bilgisine göre (ICH E2D) geçerli bir vaka için dört asgari ölçüt gerekir: tanımlanabilir bildiren,
  tanımlanabilir hasta, şüpheli ürün, advers olay. Pakette şüpheli ürün isteğe bağlı; CRM'de ürün bağlantısı yok ve not
  isteğe bağlı. İkisi de eksik — PVG paketinde bir bulgu (bir sonraki revizyonda düzeltilmeli), CRM'de bir gereksinim.
- "Hasta kimliği CRM'de yok" bir engel değildir: tanımlanabilir hasta için kimlik gerekmez; baş harf, yaş, cinsiyet ya
  da "bir hasta" yeterlidir. Takip, hekim (bildiren) üzerinden yapılır; CRM'in tuttuğu hekim kimliği ve iletişim bilgisi
  tam olarak PV'nin takip için istediği şeydir (`ReporterContactSummary`, PII).

**Önerilen yanıt (öneri — onay değil):** asgari: ürün (Global Ürün kimliği), olay açıklaması (zorunlu), öğrenme anı,
bildiren hekim (CRM kimliği + iletişim), hastaya dair bilinen en az bir tanımlayıcı (yaş / cinsiyet / baş harf, yoksa
"bilinmiyor"), CRM rapor kimliği. Takibi PV hekim üzerinden yapar (MOD-0231 Case Processing).

## 4. Endikasyon dışı soru → MSL

**Ölçülen olgular:** Blueprint 8.8 `Blueprint_Data`'da "MSL", "Medical Science Liaison", "Medical Information",
"off-label", "Medical Affairs" geçen modül yok (tüm hücrelerde arama, 2026-10-21). CRM'de `crm-visit-request-type`
seçeneklerinde `msl_meeting` var (`WP-VW-W4-report-v2.md` satır 81 @ 536bea2db). MSL atamasını PV yönetmez.

**Control Tower itirazları:**

- Bu soru PVG kapsamı dışıdır; PVG yanıt vermemeli, sahip göstermelidir: medikal birim (Medical Affairs). Blueprint'te
  modül olmadığı için atama verisinin yeri ancak Master'da bir Module Change Request ile açılır (Master-first, Blueprint
  8.7); PVG kimlik uydurmaz.
- İstisna: endikasyon dışı kullanımla **birlikte** bir advers olay bildirilirse bu PV'ye de gider (endikasyon dışı
  kullanım özel bir durum olarak kaydedilir). CRM'de `OffLabelRequest` ve `AdverseEvent` ayrı işaretler olduğu için ikisi
  birlikte işaretlendiğinde 1. maddedeki yol da çalışmalıdır.

**Önerilen yanıt (öneri — onay değil):** MSL ataması PV'nin değil medikal birimin konusudur; ürün / bölge / ülke
bazında nerede tutulacağı Master'da bir modül kararıdır. CRM, karar verilene kadar MSL yönlendirmesini PV'ye bağlamamalı.

## Karşılaştırma (genel bilgi, depoda kaynağı yok)

- **Oracle (Argus Safety):** CRM ya da çağrı merkezinden gelen bildirimler alım (intake) ekranına ya da E2B(R3) /
  form entegrasyonuyla düşer; "awareness date" alanı ayrı tutulur ve yasal saat ondan sayılır.
- **SAP:** yerleşik bir PV vaka modülü yoktur; satış/CRM tarafı (SAP Sales Cloud) bildirimleri ayrı bir güvenlik
  veritabanına arayüzle iletir. Diten'in yaklaşımı (CRM → PVG MOD-0230) Oracle desenine yakındır.

## Açık sahip kararları (TBD dahil)

1. Geçici iletim kanalı (MOD-0230 canlı olana kadar) — QPPV / sahip. **Bekliyor.**
2. "Farkına varma anı" alanının MOD-0230'a eklenmesi ve güvenilir zaman damgası — QPPV + PVG paket sahibi. **Bekliyor.**
3. `SuspectProductText`'in zorunlu yapılması ve asgari ölçütlerin MOD-0230'a yazılması — PVG paket sahibi + QPPV.
   **Bekliyor.**
4. MSL ataması ve Medical Information modülü için Module Change Request — medikal birim sahibi + Enterprise
   Architecture. **Bekliyor; PVG'nin kararı değil.**
5. CRM'e yanıtın kimden gideceği (sahip mi, PVG CT mi) — sahip. **Bekliyor.**

## Sonraki adım (her biri ayrıca sorulur)

- Kuyruk: madde 1–3 için `PVG-M1-*` ya da MVP satırı olarak DISCOVERED kayıt (keşif + DoR ile); madde 4 için kuyruk
  satırı açılmaz, yalnız sahibine yönlendirme.
- CRM'e yanıt taslağı (`ct_route.py` ile hedef; gönderilmez).
