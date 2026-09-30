# WORK PACKAGE — WP-ORG-01 · Di10 tüzel kişiliklerini (32) canlı oluştur — tenant 97c5 (tarayıcı, KOD YOK)

> **CT (SoR).** Claims onay yol haritası §E (`SCMM-claims-approval-evidence-roadmap.md`) — organizasyon kurulumunun 1. adımı. Kaynak: kullanıcının gönderdiği `Legal Entities - Di10.xlsx` (32 satır: **13 tüzel kişilik + 19 temsilcilik**). Sonraki adım (WP-ORG-02): organizasyon birimleri (Medikal/Hukuk/Ruhsat) + pozisyonlar + atamalar. **Kod değişikliği YOK** — canlı veri girişi.
>
> **Veri yazma yetkisi (kullanıcı verdi, yalnız bu liste):** aşağıdaki 32 tüzel kişiliği oluşturma + etkinleştirme (+ ALBANIA RO askıya alma). **Başka hiçbir yazma YOK.**

## Ortam
- URL `http://localhost:5001/LegalEntities` (liste) · oluşturma sihirbazı `/LegalEntities` → Oluştur (Wizard).
- Tenant **97c59330-dbc4-4665-b29c-0c26dbb5cc93** · MDM servis 5059 · koleksiyon `DitenERP_Dev.mdm_legal_entities`.
- **Başlangıç durumu (CT okudu):** 97c5'te 5 demo kayıt var (`LE-001`…`LE-005`: Diten Manufacturing / Holding / Logistics / Energy / Tech Yazılım — ülke ve para birimi boş). **Bunlara DOKUNMA** (Genel Merkez org birimi bunlardan birine bağlı olabilir).

## Alan kodları (MDM lookup — `LegalEntityLookupModels.cs`)
- **Hukuki form:** `CORPORATION` Corporation · `LLC` Limited Liability Company · `PARTNERSHIP` Partnership · `REPOFFICE` Representative Office
- **Organizasyon rolü:** `LEGALENTITY` Legal Entity · `REPOFFICE` Representative Office — **REPOFFICE üst kuruluş ZORUNLU** (validator).
- **Kontrol tipi** (ops.): alt tüzel kişilikler için `SUBSIDIARY`; temsilcilikler için boş.
- **Hukuki durum:** Registered (varsayılan).
- **Kaynak sistem** = `Di10` · **Eski kod** = Code ile aynı.

## Yükleme sırası ve veri (üst → alt; üst oluşmadan alt oluşturulamaz)

**Seviye 0**
| # | Code | Legal Name | Form | Rol | Ülke | Para | Üst |
|---|---|---|---|---|---|---|---|
| 1 | GRANDMEDCALGROUPAG-243 | GRAND MEDICAL GROUP AG | CORPORATION | LEGALENTITY | CH | CHF | — |

**Seviye 1 — üst: GRAND MEDICAL GROUP AG**
| # | Code | Legal Name | Form | Rol | Ülke | Para |
|---|---|---|---|---|---|---|
| 2 | GMGGRANDMEDCALLACLARLTDST-238 | GMG-GRAND MEDICAL ILACLARI LTD STI | LLC | LEGALENTITY | TR | TRY |
| 3 | GRASSEPHARMAGEORGALLC-239 | GRASSE PHARMA GEORGIA LLC | LLC | LEGALENTITY | GE | GEL |
| 4 | BARSNONUZ-276 | BARSINON UZ | LLC | LEGALENTITY | UZ | UZS |
| 5 | MONOMLLC-268 | MONOM LLC | LLC | LEGALENTITY | GE | GEL |
| 6 | SETONDASP-271 | SETONDA SP | LLC | LEGALENTITY | ES | **EUR** ⚠ |
| 7 | ALBANARO-254 | ALBANIA RO | REPOFFICE | REPOFFICE | AL | ALL |
| 8 | AZERBAJANRO-246 | AZERBAIJAN RO | REPOFFICE | REPOFFICE | AZ | AZN |
| 9 | BELARUSRO-245 | BELARUS RO | REPOFFICE | REPOFFICE | BY | BYN |
| 10 | GEORGARO-248 | GEORGIA RO | REPOFFICE | REPOFFICE | GE | GEL |
| 11 | KAZAKHSTANRO-253 | KAZAKHSTAN RO | REPOFFICE | REPOFFICE | KZ | KZT |
| 12 | KOSOVORO-255 | KOSOVO RO | REPOFFICE | REPOFFICE | XK | **EUR** ⚠ |
| 13 | KYRGYZSTANRO-252 | KYRGYZSTAN RO | REPOFFICE | REPOFFICE | KG | KGS |
| 14 | MOLDOVARO-251 | MOLDOVA RO | REPOFFICE | REPOFFICE | MD | MDL |
| 15 | POLANDRO-256 | POLAND RO | REPOFFICE | REPOFFICE | PL | PLN |
| 16 | ROMANARO-266 | ROMANIA RO | REPOFFICE | REPOFFICE | RO | RON |
| 17 | SETONDATMRO-273 | SETONDA TM - RO | REPOFFICE | REPOFFICE | TM | TMT |
| 18 | SPANRO-267 | SPAIN RO | REPOFFICE | REPOFFICE | ES | **EUR** ⚠ |
| 19 | TAJKSTANRO-258 | TAJIKISTAN RO | REPOFFICE | REPOFFICE | TJ | TJS |
| 20 | TURKMENSTANRO-249 | TURKMENISTAN RO | REPOFFICE | REPOFFICE | TM | TMT |
| 21 | UKRANERO-250 | UKRAINE RO | REPOFFICE | REPOFFICE | UA | UAH |
| 22 | UZBEKSTANRO-247 | UZBEKISTAN RO | REPOFFICE | REPOFFICE | UZ | UZS |
| 23 | YUTANLTD-287 | YUTAN LTD | REPOFFICE | REPOFFICE | UA | UAH |

**Seviye 2**
| # | Code | Legal Name | Form | Rol | Ülke | Para | Üst |
|---|---|---|---|---|---|---|---|
| 24 | DTENTEKNOLOJLTD-275 | DITEN TEKNOLOJI LTD | LLC | LEGALENTITY | TR | TRY | GMG-GRAND MEDICAL ILACLARI |
| 25 | GRANDMEDKALMMC-289 | GRAND MEDIKAL MMC | LLC | LEGALENTITY | AZ | AZN | GMG-GRAND MEDICAL ILACLARI |
| 26 | SETONDAKZ-270 | SETONDA KZ | LLC | LEGALENTITY | KZ | KZT | GMG-GRAND MEDICAL ILACLARI |
| 27 | SETONDALTD-290 | SETONDA LTD | LLC | LEGALENTITY | UZ | UZS | GMG-GRAND MEDICAL ILACLARI |
| 28 | SETONDATR-274 | SETONDA TR | LLC | LEGALENTITY | TR | TRY | GMG-GRAND MEDICAL ILACLARI |
| 29 | BARSNONPHARMAKOLLST-257 | BARSINON PHARMA KOLL. STI. | PARTNERSHIP | LEGALENTITY | TR | TRY | GRASSE PHARMA GEORGIA LLC |
| 30 | PHARMAGEORGA-285 | PHARMA GEORGIA | LLC | LEGALENTITY | GE | GEL | BARSINON UZ |

**Seviye 3**
| # | Code | Legal Name | Form | Rol | Ülke | Para | Üst |
|---|---|---|---|---|---|---|---|
| 31 | SETONDAKZRO-272 | SETONDA KZ - RO | REPOFFICE | REPOFFICE | KZ | KZT | SETONDA KZ |
| 32 | SETONDATRRO-269 | SETONDA TR - RO | REPOFFICE | REPOFFICE | TR | TRY | SETONDA TR |

**Durum:** Excel'de 31 kayıt *Active*, **ALBANIA RO** *Suspended*. Hepsi oluşturulduktan sonra etkinleştirilir; ALBANIA RO ardından **askıya alınır**.

## Excel'deki veri sorunları (CT işaretledi — kullanıcı onayladı/düzeltir)
1. **Hırvat Kunası (HRK)** 3 kayıtta (SETONDA SP, SPAIN RO, KOSOVO RO) — İspanya ve Kosova'nın para birimi **EUR**; HRK 2023'te kaldırıldı → **EUR girilir**.
2. Ülke alanı karışık (bazısı ad, bazısı ISO kod) → tabloda **ISO-2**'ye normalize edildi.
3. **YUTAN LTD** adı "LTD" ama hukuki form/rol Temsilcilik — Excel'deki gibi girilir, raporda işaretlenir.
4. Sıra dışı üst kuruluşlar (Excel'deki gibi girilir, raporda işaretlenir): BARSINON PHARMA KOLL. STI. (TR) → üst GRASSE PHARMA GEORGIA · PHARMA GEORGIA (GE) → üst BARSINON UZ.

## KORU / YAPMA / DUR
- **Yazma yalnız bu 32 kaydın oluşturulması + etkinleştirme/askıya alma.** Mevcut `LE-001…LE-005` ve başka tenant kayıtları, org birimleri, pozisyonlar DOKUNMA. Silme/arşivleme YOK. DB'ye doğrudan yazma YOK (okuma serbest).
- Şifre/kimlik bilgisi GİRME; oturum yoksa DUR, kullanıcıdan 97c5'e tarayıcıda giriş yapmasını iste. Kullanıcının oturumlu diğer sekmelerine dokunma, ayrı sekme.
- İlk 2 kaydı **sihirbaz UI'ından** tam gir (alan eşleşmesini doğrula). Kalanlar için sayfanın **kendi same-origin API'si** (sihirbazın gönderdiği aynı istek şekli, ağ kaydından) kullanılabilir — gerçek backend, gerçek doğrulama; her 10 kayıtta listeden kontrol.
- **Idempotency:** yazmadan önce Code ile var mı kontrol et; varsa tekrar oluşturma (raporla).
- **DUR:** ülke listesinde `XK` (Kosova) yoksa → o satırı atla + raporla (kod ekleme YOK) · para birimi listesinde bir kod yoksa → atla + raporla · etkinleştirme onay (approval) istiyor ve bu yetki yoksa → oluşturmayı bitir, etkinleştirmeyi raporla · herhangi bir 5xx → dur, tekrar deneme, raporla.

## Acceptance
- Liste: 97c5'te 32 yeni kayıt (+ 5 eski demo), hiyerarşi doğru (her REPOFFICE'in üstü dolu), 31 Active + ALBANIA RO Suspended.
- DB salt-okuma: `mdm_legal_entities` 97c5 = 37; her yeni kaydın CountryCode/BaseCurrencyCode/LegalFormCode/OrganizationRoleCode dolu; `SourceSystem = Di10`.
- Detay sayfası bir üst ve bir alt kayıtta hiyerarşiyi gösteriyor.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ORG-01 · Di10 tüzel kişiliklerini (32) canlı oluştur — tenant 97c5 (tarayıcı, KOD YOK)
Repository: C:\Users\user\Desktop\ERP-vNext · Branch: test/crm-content-visit-e2e (kod değişikliği YOK, commit YOK)

Amaç: http://localhost:5001/LegalEntities üzerinden Di10 Excel'indeki 32 tüzel kişiliği (13 tüzel kişilik + 19 temsilcilik) tenant 97c5'e canlı oluştur, hiyerarşiyi kur, etkinleştir. Bu, Claims MLR onay akışının organizasyon temelidir.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ORG-01-legal-entities-di10-load.md (32 satır tablo: Code, ad, form/rol kodu, ISO ülke, ISO para, üst; yükleme sırası; veri sorunları) · services/Diten.MdmService/src/Diten.MdmService.Application/Features/Lookups/LegalEntityLookupModels.cs (kod listeleri) · …/LegalEntity/Validators/LegalEntityWriteRequestValidator.cs (REPOFFICE üst zorunlu).

Ön koşul: oturum açık mı (97c5); değilse DUR, kullanıcıdan tarayıcıda giriş yapmasını iste — şifre GİRME.

YAP:
 1) Seviye sırasıyla oluştur: 0 (GRAND MEDICAL GROUP AG) → 1 (22 kayıt) → 2 (7) → 3 (2). Code/Legal Name/Form/Rol/Ülke/Para/Üst WP tablosundaki gibi; Kaynak sistem=Di10, Eski kod=Code; alt tüzel kişiliklerde kontrol tipi SUBSIDIARY (ops.).
 2) İlk 2 kaydı sihirbaz UI'ından tam gir; kalanlar için sayfanın same-origin API'si (sihirbazın gönderdiği istek şekli) kullanılabilir; her 10 kayıtta listeden doğrula.
 3) Yazmadan önce Code ile var mı kontrol et (idempotent).
 4) Hepsi bitince etkinleştir; ALBANIA RO'yu ardından askıya al.
 5) Excel veri sorunları: SETONDA SP / SPAIN RO / KOSOVO RO para birimi EUR (Excel'de HRK yanlış); YUTAN LTD ve sıra dışı üstler Excel'deki gibi, raporda işaretle.
KORU/YAPMA: yazma YALNIZ bu 32 kayıt + etkinleştir/askıya al; LE-001…LE-005 demo kayıtları, org birimleri, pozisyonlar, başka tenant DOKUNMA; silme/arşiv YOK; DB'ye doğrudan yazma YOK (okuma serbest); kod/commit YOK; kullanıcının diğer oturumlu sekmelerine dokunma, ayrı sekme.
Durma: ülke listesinde XK yoksa o satırı atla+raporla; para kodu listede yoksa atla+raporla; etkinleştirme yetki/onay istiyorsa oluşturmayı bitir, etkinleştirmeyi raporla; 5xx → dur, raporla.
RAPOR (§22 TÜRKÇE): oluşturulan kayıtlar tablosu (#, Code, ad, ülke, para, üst, durum, id); atlanan/başarısız satırlar + neden; DB salt-okuma sayımı (97c5 mdm_legal_entities = 37 beklenir; alan doluluk kontrolü); hiyerarşi ekran görüntüsü; konsol/ağ hataları. K13.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (canlı veri)**
- ✅ `DitenERP_Dev.mdm_legal_entities` 97c5 = **37** (5 demo + **32 Di10**): 13 LEGALENTITY + 19 REPOFFICE.
- ✅ Ülke, para birimi, hukuki form ve rol kodu 32 kayıtta da dolu. Üst kuruluşu eksik temsilcilik **0**.
- ✅ Durum: **31 Active + ALBANIA RO Suspended** (`OperationalStatus` 4/5).
- ✅ HRK düzeltmesi: SETONDA SP, SPAIN RO ve KOSOVO RO **EUR**. XK (Kosova) kabul edildi.
- ✅ Sıra dışı üst kuruluşlar Excel'deki gibi girildi: BARSINON PHARMA → GRASSE PHARMA GEORGIA · PHARMA GEORGIA → BARSINON UZ. Kullanıcı onayı bekliyor.
- ℹ Tüzel kişilik sihirbazında ülke listesi isteği 400 `scope_key_required` döndü (WP-ORG-02 raporundaki konsol notu). Muhtemelen global/tenant referans verisi kapsam sorunu. E2E hata listesine eklendi.
