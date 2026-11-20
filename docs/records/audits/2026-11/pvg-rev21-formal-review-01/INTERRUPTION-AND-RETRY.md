# Kesilen kontrol ve dosyaya bağlı yeniden çalışma

2026-11-20. Araç oturumu kesilince önceki card65364,row49527 ve dispatch38697 süreç tanıtıcıları okunamadı. Normal dispatch capture/çıktı dosyası yoktu; dar süreç denetiminde bu kontrollerin Python süreci bulunmadı. Sonuç yokluğu PASS veya kullanıcı onayının geri alınması sayılmadı. Deftere HIGH satırı o denemelerde eklenmemişti.

Aynı kaynakref a53656cdeb8dff40839e5e86ed708d812ad00f19 ve aynı onay kapsamıyla kontroller yeniden çalıştı. Korunan araç dosyası değiştirilmedi. Dış visualization dizinindeki kayıtlı hash'li yardımcı, gerçek `pack_approval.main` card ve row girişlerini aynı süreçte çağırır; yalnız üretim aracının normal okuma cache'i paylaşılır. Normal prompt_gate CLI ayrı süreçte orijinal check/queue/review argümanlarıyla çalışır. Sonuçlar her adım tamamlandığında ayrı capture dosyasına yazılır; yardımcı onay defterine yazmaz ve işçi başlatmaz.

Gerçek card0 ve row0 tamamlandı: packea4ff430, cardc959bb4b7ba2bbad17762de3828aa4f5c3ccc75419ea8f8d6929f150331202c8. Kaynak/body çıktı hashleri capture dosyalarıyla doğrulandı; yalnız aracın ürettiği satır approvals.tsv sonuna eklendi. Güncel GitHub CLI hesabı tekrar ElikPenco ölçüldü. K5 yazar ile K2 HIGH onaylayanın ayrılığı önceki insan beyanı sınırı ve deferred main insan adımıyla korunur; authenticated Beste iddiası yoktur.

Eski ilk review-row denemelerinde hesap adı author alanına verilmişti; araç K-kişi anahtarı istediği için çıkış1 verdi ve satır üretilmedi. AuthorK5/requested_byElikPenco ile gerçek üretici tekrar çalıştı, iki geçerli UYGUN satırı0 ile üretildi. Kart/onay row author alanı ise CLIlogin kullanır; bu iki şema karıştırılmaz. Guard/şema gevşetilmedi.

Kalan normal dağıtım denetimi bitmeden kayıt işçisi yazmaz. Kesin pack check ve Windows replacement işçisinin kendi normal kapıları ayrıca gereklidir. Üç kontrolün birinin sonucu diğerinin yerine geçmez.
