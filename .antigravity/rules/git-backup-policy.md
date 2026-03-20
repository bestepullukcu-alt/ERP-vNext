---
description: "GIT-001 — Diten ERP vNext Git Yedekleme, Branch İsimlendirme ve Versiyon Kontrol Politikası"
---

# Git Yedekleme ve İsimlendirme Politikası

Bu politika, projedeki her kritik aşamada veya kullanıcı talebi üzerine alınacak yedeklemelerin standartlarını belirler. Amaç, hatasız bir geçmiş (history) yönetimi ve güvenli geri dönüş noktaları oluşturmaktır.

## 🕰️ Yedek Türleri

### A) Varsayılan Güvenli Artefact Yedeği
Kullanıcı yalnızca "git yedeği al" veya benzeri genel bir talep verirse, **öncelikli yöntem** çalışma ağacını bozmayan artefact yedeğidir:

- `.git-backups/<repo>-YYYYMMDD-HHmmss.bundle`
- `.git-backups/<repo>-YYYYMMDD-HHmmss-working-tree.patch`
- `.git-backups/<repo>-YYYYMMDD-HHmmss-untracked.tar.gz`

Bu yöntem:
- mevcut branch'i değiştirmez,
- commit zorunluluğu yaratmaz,
- tracked + untracked değişiklikleri ayrı ayrı geri döndürülebilir formatta saklar.

### B) Branch/Commit Yedeği
Kullanıcı açıkça branch açılmasını, commit atılmasını veya "backup branch" oluşturulmasını isterse branch tabanlı yöntem uygulanır.

## 🕰️ İsimlendirme Mantığı (Naming Convention)

Branch tabanlı yedekler her zaman aşağıdaki formatta isimlendirilmelidir:
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

### Varsayılan Artefact Yedeği Protokolü
1. **İzleme:** Mevcut değişiklikleri `git status` ile kontrol et.
2. **Bundle:** Tüm ref/history için `git bundle create ... --all` üret.
3. **Tracked Diff:** Commitlenmemiş tracked değişiklikler için `git diff --binary > ...working-tree.patch` üret.
4. **Untracked Archive:** Untracked dosyaları `.tar.gz` olarak ayrıca arşivle.
5. **Doğrulama:** Oluşan dosyaların mevcut olduğunu ve boyutlarının makul olduğunu kontrol et.

### Branch/Commit Yedeği Protokolü
1. **İzleme:** Mevcut değişiklikleri `git status` ile kontrol et.
2. **Branch Oluşturma:** Yukarıdaki formata uygun isimlendirme ile yeni bir yedekleme branch'i aç (`git checkout -b backup/...`).
3. **Commit:** Değişiklikleri "Backup: [OZET_BILGI]" mesajıyla bu branch'e işle.
4. **Güvenli Dönüş:** Yedekleme bittikten sonra orijinal çalışma branch'ine geri dön.



---

## 🚨 Ne Zaman Yedek Alınmalı?

- **Önemli Refactor Öncesi:** Bir servisin çekirdek mantığı (örn: CQRS Handler yapısı) değişmeden hemen önce.
- **UI "Altın Referans" Güncellemeleri:** `LegalEntities` gibi projenin standartlarını belirleyen sayfalarda yapılan büyük değişikliklerden sonra.
- **Hata Ayıklama (Debugging) Öncesi:** Karmaşık bir hatayı çözmek için kodun birçok noktasında geçici değişiklikler yapılmadan önce.
- **Kullanıcı Talebi:** Kullanıcı "Şu anki halini yedekle" veya "git yedeği al" dediğinde.

---

## ✅ Kontrol Listesi
- [ ] Kullanıcı branch/commit mi istedi, yoksa güvenli artefact yedeği mi yeterli?
- [ ] Artefact yedeğinde `.bundle + working-tree.patch + untracked.tar.gz` üçlüsü üretildi mi?
- [ ] Branch yedeğinde isim `backup/` ön ekiyle ve `YYYYMMDD-HHmm` formatıyla doğru mu?
- [ ] Branch yedeğinde özet bilgi `snake_case` ve açıklayıcı mı?

> **Mühür:** Bu kural, Antigravity orkestrasının "Hafıza Yönetimi" kuralıdır. Hiçbir emek kaybolmamalı, her geri dönüş yolu açık tutulmalıdır.
