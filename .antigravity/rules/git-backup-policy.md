# Git Yedekleme ve İsimlendirme Politikası

Bu kural, projedeki her önemli aşamada veya kullanıcı talebi üzerine alınacak yedeklemelerin (Git branch/commit) nasıl isimlendirileceğini belirler.

## İsimlendirme Mantığı
Yedeklemeler (backup) şu formatta isimlendirilmelidir:
`backup/YYYYMMDD-HHmm_OZET_BILGI`

- **YYYYMMDD**: Yıl-Ay-Gün (Örn: 20260302)
- **HHmm**: Saat-Dakika (Örn: 1320)
- **OZET_BILGI**: Yapılan işlemin kısa, teknik ve açıklayıcı adı (lower_snake_case).

**Örnekler:**
- `backup/20260302-1320_datatable_analysis_completed`
- `backup/20260302-1545_legal_entities_ui_fix`

## Uygulama Kuralı
1. Her kritik değişiklikten önce veya sonra (kullanıcı talebiyle) yeni bir yedekleme branch'i oluşturun.
2. Mevcut değişiklikleri bu branch'e "Backup: [OZET_BILGI]" mesajıyla commit edin.
3. İsimlendirme otomatik olarak yukarıdaki formata göre benim tarafımdan (Antigravity) yapılacaktır.
4. Yedekleme bittikten sonra orijinal çalışma branch'ine geri dönün.
