---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-04
status: approved — D41–D44 (2026-10-26) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-26
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); soru kutusu cevapları (2026-10-26); sahibin sorusu "kararları bana sorabilir misin, süreci uzatmamak için?"
scope: Treasury intake — `-03.md` (D36–D40; PR #242'de) devamı; ayrı dosya, çünkü -03 incelemede
bound_to: dal `docs/ct/treasury-decisions-04`, taban origin/main 195552295
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları 4 (2026-10-26)

Önceki: `treasury-intake-owner-decisions-01.md` (D0–D8), `-02.md` (D9–D35), `-03.md` (D36–D40, PR #242).

## D41 — MOD-0217 açık kararları O-4…O-15 (O-3 hariç; soru kutusu, hepsi önerilen seçenek)

Kaynak: MOD-0217 pack "Açık kararlar" (PR #242, sha256 `20e71816…`). Sahip (K1) MDM sahibi rolünde (D38) karar verir.

1. **O-4 izinler:** pack'teki `mdm.contracts.*` listesi; şirket geneli okuma yalnız açık atamayla. Auth tarafında kaydı MOD-0018
   sahibinin (Ali) işi.
2. **O-5 kayıt kodu:** OTOMATİK, tür öneki **`CTR`**, şirket öneki yöneten şirketin kısa kodu. Sahip, RCS-001 §3'e `CTR` satırının
   eklenmesini (`.antigravity/rules/record-coding-standard.md`) onaylar. Ortak kod servisi BL-565 → D44.
3. **O-6 denetim:** MDM güvenilir niyet izi (`mdm-guvenilir-kaynak-niyeti`, D27/11 deseni); onay sonucu, imza ve fesih K2; borç yok;
   CT-DECISIONS satırı kod aşamasında.
4. **O-7 karşı taraf:** türlü başvuru — `LegalEntity` (süreç içi), `Supplier` (MOD-0140 sözleşmesi ölçülene kadar kapalı), `External`
   (yalnız ad); müşteri v1'de yok.
5. **O-8 imza:** v1'de elle imza kaydı + imzalı kopya belgesi; e-imza sağlayıcısı ve geri çağrı yok.
6. **O-9:** kanıt (MOD-0031), gizlilik düzeyi ve risk derecesi v1 dışı (Follow-up).
7. **O-10 idempotency:** Create'te `Idempotency-Key` zorunlu; anahtar + gövde özeti saklanır.
8. **O-11 kod listeleri ve durum modeli:** pack'teki öneriler (durum tabloları, çekirdek `ContractType` — satış, hizmet, NDA, kira,
   lisans, ortaklık, grup içi; yenileme kararı değerleri); kiracı ek türleri v1'de yok.
9. **O-12 parasal değer:** ayrı izin `mdm.contracts.value.read`; izni olmayana maskeli; okuma ayrıca denetlenmez.
10. **O-13 çağrı kimliği:** kullanıcı token'ı Gateway üzerinden (MOD-0126 O-7 deseni; kullanıcının `platform.*` izinleri de gerekir);
    MOD-0262 `scope` = var olan `Documents`. Ölçüm (DL-2 başı) bunu çürütürse ayrıca sorulur; yeni değer gerekirse XMC-001.
11. **O-14 iç sorumlu:** v1'de oluşturan kullanıcı; Auth kullanıcı araması ölçülünce başka kullanıcı atanabilir.
12. **O-15:** v1'de olay yayını ve ödeme bağı yok; kaynağın "0126 → 0217 → 0124" sırası benimsenmez.

O-3 (MOD-0023 iş akışı şablonu) MOD-0023 sahibinindir (D38/4) — açık. Pack'e işlenmesi: PR #242 merge edildikten sonra ayrı
revizyonla (yeni CT-APR-02).

## D42 — MOD-0126 O-7 ve O-10: MOD-0023 sahibi teyidi yerine sahip teyidi (soru kutusu, önerilen seçenek)

D29/1'in "MOD-0023 sahibi teyidi açık" koşulu kapanır: O-7 (v1'de kararlar yalnız MOD-0126 ucundan, kullanıcı token'ıyla genel
MOD-0023 uçları; güvenilir tüketici yolu kullanılmaz; Görev Merkezi'nden karara karşı R11) ve O-10 (onaylayıcı adayları MOD-0023
şablonunun atama kuralından; R1–R4 dışlaması; ayrı dizin istemcisi yok). O-8 (şablon kodları, adım sayıları) MOD-0023 sahibinde
(Ali) açık kalır.

## D43 — MOD-0124 O-7: MOD-0048 sahibi teyidi yerine sahip teyidi (soru kutusu, önerilen seçenek)

v1'de kur çevrimi yok; pozisyonlar kendi para biriminde gösterilir; kur kaynağı gelecekte ayrı bir iş. D29/1'in "MOD-0048 sahibi teyidi
açık" koşulu kapanır.

## D44 — Ortak kayıt kodu servisi (BL-565) sahibi (soru kutusu)

**Beste.** Treasury dört modülü, MOD-0289 ve MOD-0217'nin Create komutları bu servisi bekler; istek Beste'ye gider (çağrı sözleşmesi
ve zaman).

## Karar olmayanlar

MOD-0217 O-3 (MOD-0023 şablonu — Ali); MOD-0126 O-8 / MOD-0119 O-11 / MOD-0124 O-20 şablon kodları (Ali); DCP numarası, SEC-002 yolu,
`AuditCategory` `Finance`, MOD-0048 kümeleri, MOD-0149 eki (Beste dönecek).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
