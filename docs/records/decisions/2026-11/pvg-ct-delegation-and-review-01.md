---
module: MOD-0230
work_package: WP-0230-DL12
status: decided
decision_date: 2026-11-20
authorization_scope: scope-approval-conditional-on-separate-chat-review-and-ct-coordination
code_started: false
publication_started: false
---

# PVG — bağımsız sohbet incelemesi ve CT çalışma düzeni

Kullanıcı bu sohbette onayladığını belirtti; önce hazırlanan paketin bağımsız başka bir sohbette kontrol edilmesini şart koştu. CT yalnız paketleme, iş dağıtımı ve yönetimi yapacak; ürün geliştirmesi ayrı işçilere verilecek. Güvenli olduğunda paralel çalışılacak, doğrulanan işler CT dalında birleştirilecek ve sonuç tek PR ile sunulacak. Kullanıcı biten worktree'lerin temizlenmesini de istedi.

Bu kararın kapsam onayı mevcut revizyon 17 taslağı içindir (paket SHA-256 `ecd02b5e1af5173d003d32bf496ad9988507b26b76770e81005f3c6b0eb342e1`). Yeni bağımsız inceleme sonucu ve sürüme bağlı zorunlu kapılar tamamlanmadan DL-12 başlatılmaz. Paket baytları değişirse önceki onay yeni baytlara taşınmaz.

## Yürütme

- Bağımsız ayrı sohbet: `01a1f849-c6bd-7ba3-b788-6ebf7a7551e9`; istek: `docs/records/reviews/MOD-0230/REVIEW-REQUEST-21.md`.
- CT ürün/test kodu yazmaz. Paket düzeltmesini yazar rolüne, geliştirmeyi kapsamı sınırlandırılmış işçiye verir.
- Paralel yürütmede en çok iki etkin işçi, ayrı worktree ve çakışmayan dosya sahipliği gerekir. Paylaşılan dosyalar tek yazarlı yürütülür.
- İşçi sonucu bağımsız doğrulanır; yalnız doğrulanan commit'ler CT dalına merge commit ile katılır. İşçi dalları uzağa itilmez; tek modül/CT dalı ve tek sonuç PR'ı hedeflenir.
- Bu söz gelecekteki belirsiz SHA için push/PR kapısını geçmiş saydırmaz; son sürüm hazır olduğunda somut dal ve SHA ile son PR kapısı uygulanır.
- Biten worktree'ler ancak değişiklikler korunmuş, ilgili PR main'e birleşmiş, commit'lerin main'den erişilebilirliği ve temiz/itilmemiş iş bulunmadığı ölçülmüşse kaldırılır. Açık veya birleştirilmemiş iş silinmez. Yönetilen worktree için uygulamanın arşivleme/kaldırma aracı kullanılır.
- Bu tur ürün geliştirmesi, ürün testi, push, PR veya worktree silme başlatılmadı.
