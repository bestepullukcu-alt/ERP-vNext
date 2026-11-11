# MOD-0230 — inceleme 12 Control Tower karşılaştırma notu

- **İnceleme:** `docs/records/reviews/MOD-0230/2026-10-27-a51f2349.md` (Codex, GPT-6; kimlik `/root`, UUID doğrulanamadı).
- **Hedef:** revizyon 12, `a51f2349a83ee99c0a47c0528ca429a83ba48bc2844f22a488b13a9fdada7966`.
- **Sonuç:** KOŞULLU. Tek bulgu F-1 (ORTA). ENGEL yok.

## F-1 — CT değerlendirmesi: doğru

- **Bulgunun söylediği:** AC-20(a)(b) vekilin `start`/`length` → `pageNumber`/`pageSize` eşlemesini ve 100 sınırını yalnız vitest dosyasına bağlıyor. vitest, C# denetleyicisini çalıştırmaz. Metin okuyan bir kontrol, eşleme bozulduğunda testi kırmaz.
- **Ölçtüğüm:** bulgu doğru. Ev örneğinde (`GlobalProductsController.cs:85–104`) eşleme C# tarafında yapılıyor. Bugünkü PVG denetleyicisi (`:59`) `pageNumber`/`pageSize`'ı doğrudan alıyor.
- **Bu sürümde nasıl karşılanır:** WP-0230-DL7A prompt'u, vekil eşlemesini ve sınırı adı verilmiş bir C# testiyle ölçer. Test, `frontend/Diten.Web.Tests` altında denetleyiciyi çalıştırır; DL7A v1.0 taslağında bu test W1–W6 olarak zaten var. Dört vitest adı aynı kalır: tarayıcı ölçümleri vitest'te, vekil ölçümü C#'ta. Paket metni bir sonraki revizyonda bu ikiye ayrımı açıkça yazar.
- **Etkisi:** Paketin ölçtüğü davranış doğru. Eksik olan, kanıtın hangi test katmanında alınacağının belirtilmesi. Bu boşluk dağıtılan prompt'ta kapatılır.

## Sonraki adım (sahibin kararı)

1. **Bu sürümü onaylamak.** F-1 açık ve ORTA olarak kalır. `pack_approval.py` yalnız UYGUN DEĞİL ya da açık ENGEL durumunda durur. Onay kartı riski HIGH olduğu için onay satırı da HIGH yazılır. Onay, zorunlu PR'ın Beste tarafından birleştirilmesiyle geçerli olur. Bekleyen dalga 4 işleri ve kayıtlar aynı PR'a girer.
2. **Revizyon 13.** Yalnız AC-20'nin test katmanı yazılır ve yeni bir inceleme turu açılır.

CT önerisi: 1.
