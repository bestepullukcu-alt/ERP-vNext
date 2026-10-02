# DEC-SCMM-05 — Marka kiti + onaylı görsel kütüphanesi: yerleşim (CAND-CAP-0011 kapsam genişlemesi)

> **Format:** §14 Decision Brief. **Durum:** ✅ `DECIDED` — kullanıcı (owner) 2026-10-02: "evet katılıyorum".
> **Bağlam:** Bilgi Yolu Stüdyosu `DESIGN-KP-STUDIO-knowledge-path-studio.md` §2.4 / §8 — KP-5b (marka kiti + onaylı görsel kütüphanesi), sayfa tasarımcısı KP-UI-3'ün ön koşulu. Önceki karar: DEC-SCMM-01 (Seçenek H — CAND-CAP-0011 rezervasyonu).

---

**Decision ID:** DEC-SCMM-05

**Soru:** Marka kiti ve onaylı tanıtım görselleri CRM'e mi, Marketing'e mi, başka bir yere mi ait?

**Ölçülen kanıt (Blueprint `docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx`, `Blueprint_Data` + `Module Pages`):**
- Blueprint'te **dijital varlık / marka kiti / tanıtım materyali modülü yok**; pazarlama içeriğini yöneten modül de yok.
- Commercial Suite (CRM + O2C) **Marketing** grubu: MOD-0164 Consent, MOD-0165 Campaign, MOD-0166 Journeys & Automation, MOD-0167 Segmentation — varlık / marka yok.
- **CAND-CAP-0011** "Structured Content & Messaging Management (Content Studio)" — `execution/registries/module-id-registry.md`: "candidate / pending-EA … no Blueprint module match; **Marketing capability-group gap**". Bilgi Yolu Stüdyosu, iddialar, MLR onayı bu adayın altında (DEC-SCMM-01).
- **Dosya ve belge:** MOD-0262 Internal Document Repository (Platform; SoR "Document binaries"), MOD-0028 Documentation & Evidence Management (Platform Content Service; SoR "Documents, templates, versions, document metadata"; sayfalar Document Library, Template Manager, Version Compare).
- **Marka / ürün kaydı:** MOD-0290 Product / Item / SKU Master (MDM); Brand MDM'de (MOD-0290-FU02).

**Karar:**

| Parça | Sahip | Not |
|---|---|---|
| Görsel dosyası, sürümleri, önizleme / türev boyutları | **Platform Belge Yönetimi** (MOD-0262 dosya, MOD-0028 belge + sürüm + metadata) | CRM / İçerik Stüdyosu ayrı dosya deposu **kurmaz**; belgeye referans verir |
| "Onaylı tanıtım görseli" yönetişimi: ürün / ülke kısıtı, kullanım hakkı + bitiş tarihi, zorunlu alternatif metin, onay (Regülasyon / MLR) | **İçerik Stüdyosu — CAND-CAP-0011 (Marketing)** | Kapsam genişlemesi; dosyayı kopyalamaz |
| Marka kimliği (logo, renk paleti, yazı tipleri, kullanım kuralları) | **İçerik Stüdyosu — CAND-CAP-0011**, MDM markasına / ürününe bağlı | MDM marka + ürün kaydının sahibi; görsel kimlik pazarlama verisi |
| Menü yeri | **"İçerik Stüdyosu / Pazarlama"** grubu (saha CRM ekranları değil) | Kullanıcı medikal + pazarlama ekibi |

**Sonuçlar:**
- CAND-CAP-0011 kapsamına eklenir: marka kiti + onaylı varlık yönetişimi. Registry satırı EA modül kimliği verilene kadar "candidate" kalır (DCP-002).
- KP-5a ekranları (Güvenlilik Metinleri, Ülke Yasal Profilleri) menüde CRM altında → aynı gerekçeyle İçerik Stüdyosu grubuna taşınır (küçük menü işi, ayrı WP).
- KP-5b önce **mockup** (kullanıcı 2026-10-02): brief `execution/domains/commercial-suite/work-packs/mockups/kp-5b-brand-assets/BRIEF-kp-5b-brand-kit-asset-library.md`.

**Required record changes (CT uygular):** `module-id-registry.md` CAND-CAP-0011 notuna "DEC-SCMM-05: + brand kit / approved asset governance (binaries stay MOD-0262/0028)" eki.
