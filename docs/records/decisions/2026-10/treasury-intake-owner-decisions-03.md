---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-03
status: approved — D36–D40 (2026-10-26) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-26
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); soru kutusu cevapları (2026-10-26)
scope: Treasury intake — `treasury-intake-owner-decisions-02.md` (D9–D35) devamı; ayrı dosya, çünkü -02'nin D32–D35 eki PR #235'te incelemede
bound_to: dal `chore/ct/treasury-intake-ct-02`, taban origin/main 04c8fb4cf292797c5ab100180134982e758d6efb
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları 3 (2026-10-26)

Önceki kararlar: `treasury-intake-owner-decisions-01.md` (D0–D8), `-02.md` (D9–D31 main'de; D32–D35 PR #235'te).

## D36 — MOD-0124 D29/1 teyitleri (soru kutusu; hepsi önerilen seçenek)

Sahip (K1) platform-shared-services ve tüketiciler adına teyidi kendisi verir (D29/1'in "diğer sahibin teyidi" koşulu bu üç
madde için kapanır).

1. **O-3 (MOD-0037 kapısı):** "Evet, içe aktarmayla tüketilir" — v1, MOD-0037 uygulanmadan başlar; dış kaynak sağlığı üretmez.
2. **O-17 (kanıt dışa aktarımı):** "DL-4 ayrı komut, yayımı engellemez" — MOD-0031 tekrar (idempotency) ölçümü olmadan açılmaz.
3. **O-14 (yayım olayı):** "v1 yalnız okuma ucu (`current`)" — olay ilk gerçek tüketicinin onayıyla eklenir.

Pack etkisi: MOD-0124 checklist'indeki O-3, O-14, O-17 teyit satırları kapanır; O-7 (MOD-0048 sahibi teyidi, kur kaynağı)
açık kalır. Pack, PR #239 (revizyon turu 02) merge edildikten sonra ayrı bir revizyonla güncellenir (yeni CT-APR-02).

## D37 — MOD-0217 "Document Management" kapısı (soru kutusu; önerilen seçenek)

Kaynak: level-1 kabul `treasury-level1-acceptance-01/MOD-0217-v1.0.md` koşul 3 ("'Document Management' kapısı hiçbir modüle
çözülmüyor"), D19/1. Keşif (`verify_module_id.py --discover "Document Management"`, depo HEAD 04c8fb4cf, 2026-10-26): SoR
haritasında 'Documents' sahibi **MOD-0028**; Blueprint adayları MOD-0029, MOD-0206, MOD-0262.

1. **Seçim: "MOD-0028 + MOD-0262 dosya deposu"** — sözleşme belgesinin kaydı ve sürümleri **MOD-0028 Documentation & Evidence
   Management** (platform-shared-services, pack `approved`) üzerinden; dosyanın kendisi **MOD-0262-FU01 belge dosya deposu**
   (pack `ready-for-dev`) üzerinden. MOD-0029 (kontrollü belgeler) ve MOD-0206 seçilmedi.
2. MOD-0217 bu iki modülün **tüketicisidir**; onlarda değişiklik gerekirse XMC-001 ile o modülün sahibine sorulur, MOD-0217
   pack'inde yazılmaz. Kullanılacak okuma / bağlama uçları MOD-0217 pack'i yazılırken ölçülür.
3. MOD-0217 pack'inin kalan koşulu: MDM domain sahibinin onayı (D19/1). TRM-11 koşulu karşılandı
   (`docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-TRM11-01.md`): kaynakta işlem atomikliği
   doğrulanmış sayılmaz, Recovery kendi testleriyle kanıtlar.

## D38 — MOD-0217 MDM yerleşimi: sahip MDM sahibi yerine karar verir (soru kutusu; önerilen seçenek)

Emsal: D20/1 (MOD-0289 → MDM, sahip MDM sahibi yerine karar verdi). Sahibin sorusu: "bunu main'den kendimiz çözebilir miyiz?"

1. **Yerleşim:** MOD-0217 Contract Lifecycle Management (CLM) → `master-data-management` / `Diten.MdmService` (D19/1 teyit).
   D19/1'in "MDM domain sahibinin onayı" koşulu bu kararla kapanır.
2. **Pack:** taslak pack'i Treasury CT yazar; MDM domain-config satırı ve kimlik kaydı `reserved / planned` satırı pack ile
   önerilir (MOD-0289 satırı da domain-config'te eksik — aynı öneride). Pack önce sahibe ZIP olarak gider, sonra CT-APR-02.
3. **Kod alma yok:** D27/5 yalnız dört Treasury modülünü kapsar; kaynak yalnız okunur. Kod `ready-for-dev` olmadan yazılmaz.
4. MDM sahibine (Ali) bilgi verilir; MOD-0023'te MOD-0217 iş akışı şablonu onun cevabını yine gerektirir.

## D39 — MOD-0217 ile MOD-0144 sınırı (MOD-0217 O-1; soru kutusu, önerilen seçenek)

Ölçüm: MOD-0144 Contracting & Clause Library (procurement, `ready-for-dev`, kodlanmış) Gateway'de `/api/contracts` → 5065'i tutuyor
(`gateway/Diten.ApiGateway/ocelot.json:842-870`); web rotası `/Contracts`, izinler `procurement.contracts.*`.

1. **Satın alma / tedarik sözleşmeleri MOD-0144'te kalır** (onların SoR'u). MOD-0217 **satın alma dışı** hukuki sözleşmeleri tutar:
   satış, hizmet, gizlilik (NDA), kira, lisans, ortaklık, grup içi. MOD-0217'nin sözleşme türü listesinde `PURCHASE` yok.
2. MOD-0144'e dokunulmaz; iki modül arasında başvuru bağlantısı sonraki bir iş (follow-up). MOD-0217 rotası `/api/mdm/contracts`,
   ekranı `/MasterData/Contracts`.
3. Procurement sahibine bilgi verilir (MOD-0144'ü değiştirmeyen sınır; itiraz edebilir).

## D40 — MOD-0217 belge kaydı ve sürümleri (MOD-0217 O-2; D37'nin yorumu; soru kutusu, önerilen seçenek)

Ölçüm: MOD-0028 Documentation & Evidence Management pack'i kontrollü belge yaşam döngüsünü (MOD-0029) ve ikili dosya
depolamayı kapsam dışı sayıyor; iş belgesi kaydı yüzeyi yok. Blueprint MOD-0217 SoR'u: "contracts, contract versions,
approvals/sign-offs, contract metadata".

1. **Sözleşme sürümü MOD-0217'nin kendi kaydıdır** (`ContractVersion`, Blueprint SoR); dosyanın kendisi **MOD-0262-FU01** belge
   deposunda, sürüm onu `ContentId` ile işaret eder. **D37'nin "MOD-0028 kayıt ve sürümler" kısmı bu şekilde yorumlanır:** v1'de
   MOD-0028'e bağımlılık yoktur; MOD-0028 bağı (gerekirse) sonraki bir iş. MOD-0262-FU01 kısmı aynen geçerlidir.
2. MOD-0028'den ek istenmez; MOD-0217 DL-2 bu karar yüzünden beklemez.

## Karar olmayanlar

MOD-0124 O-7 kur kaynağı (MOD-0048 sahibi teyidi); MOD-0217 için MOD-0023 iş akışı şablonu (MOD-0023 sahibi).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
