---
description: "GIT-001 — Diten ERP vNext Git Yedekleme, Branch İsimlendirme ve Versiyon Kontrol Politikası"
---

# Git Yedekleme ve İsimlendirme Politikası

Bu politika, projedeki her kritik aşamada veya kullanıcı talebi üzerine alınacak yedeklemelerin (Branch/Commit) standartlarını belirler. Amaç, hatasız bir geçmiş (history) yönetimi ve güvenli geri dönüş noktaları oluşturmaktır.

## 🕰️ İsimlendirme Mantığı (Naming Convention)

Yedeklemeler (backup) her zaman aşağıdaki formatta isimlendirilmelidir:
`backup/YYYYMMDD-HHmm_ozet_bilgi`

- **YYYYMMDD:** Yıl-Ay-Gün (Örn: 20260309)
- **HHmm:** Saat-Dakika (Örn: 1545)
- **ozet_bilgi:** Yapılan işlemin kısa, teknik ve açıklayıcı adı (küçük harf ve snake_case).

**Standart Örnekler:**
- `backup/20260309-1000_mdm_tenant_id_refactor`
- `backup/20260309-1320_datatable_layout_v2_sync`
- `backup/20260309-1545_legal_entities_ui_final_golden`

---

## 🏗️ Uygulama Protokolü

Ajan (Antigravity), bir yedekleme talebi aldığında veya kritik bir sürece girmeden önce şu adımları izler:

1. **İzleme:** Mevcut değişiklikleri `git status` ile kontrol et.
2. **Branch Oluşturma:** Yukarıdaki formata uygun isimlendirme ile yeni bir yedekleme branch'i aç (`git checkout -b backup/...`).
3. **Commit:** Değişiklikleri "Backup: [OZET_BILGI]" mesajıyla bu branch'e işle.
4. **Güvenli Dönüş:** Yedekleme bittikten sonra orijinal çalışma branch'ine (`main` veya `develop`) geri dön.



---

## 🚨 Ne Zaman Yedek Alınmalı?

- **Önemli Refactor Öncesi:** Bir servisin çekirdek mantığı (örn: CQRS Handler yapısı) değişmeden hemen önce.
- **UI "Altın Referans" Güncellemeleri:** `LegalEntities` gibi projenin standartlarını belirleyen sayfalarda yapılan büyük değişikliklerden sonra.
- **Hata Ayıklama (Debugging) Öncesi:** Karmaşık bir hatayı çözmek için kodun birçok noktasında geçici değişiklikler yapılmadan önce.
- **Kullanıcı Talebi:** Kullanıcı "Şu anki halini yedekle" dediğinde.

---

## ✅ Kontrol Listesi
- [ ] Branch ismi `backup/` ön ekiyle başlıyor mu?
- [ ] Tarih ve saat formatı (`YYYYMMDD-HHmm`) doğru mu?
- [ ] Özet bilgi `snake_case` formatında ve açıklayıcı mı?
- [ ] Yedekleme sonrası ana branch'e geri dönüldü mü?

> **Mühür:** Bu kural, Antigravity orkestrasının "Hafıza Yönetimi" kuralıdır. Hiçbir emek kaybolmamalı, her geri dönüş yolu açık tutulmalıdır.