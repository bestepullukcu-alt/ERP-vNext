hedef: docs/roadmap/plans/pvg-mod0230-windows-acl-scope-proposal-2026-11-20.md @ 89e373028ecf7acdbe9d12f0de09fa2d06d4839f617bca24fb4d651a491eb758
model: gpt-6.2-astra
effort: high
reviewer_session: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9
kaynak_commit: 06dd930b0a1413f407120ed9f20b4dbac7d34cea

F-4 kapandı. Önerinin 104–126. satırları, otomatik PR koşusunda gerçek checkout’u son PR head’ine Git ebeveyn kayıtları ve tree(M)==tree(H) eşitliğiyle bağlıyor. Bu eşlik kanıtlanamazsa mevcut workflow_dispatch yolunda C==H aranıyor. Her iki yolda da aynı pvg-api işinin aynı koşu denemesine ait TRX, hedef testin geçtiğini ve tam süitte 0 başarısız, 0 atlanan olduğunu göstermeli. Head değişirse kanıt yenileniyor.

F-1, F-2 ve F-3 kapalı. Yeni bulgu veya ENGEL yok. Tek yeni insan onayı üçüncü test dosyasındaki tek metotla sınırlı. Eski kirli ağaç korunuyor. Donmuş WIP’in temiz ağaca aktarımı normal HIGH kapısının 0 sonucuna bağlı. Checkpoint istisnası yok. Altı ayrı RED/GREEN ölçümü korunuyor.

ZIP’in boyutu ve SHA-256 özeti, manifestteki 301 dosyanın tamamı ve hedef belge doğrulandı. Hüküm yalnız kapsam önerisi içindir. Tam .NET referans paketi ZIP’te bulunmuyor. Gerçek ACL ve CI ölçümleri, doğrudan insan onayı, MP onayı ve çalışma izni bekliyor.

sonuç: UYGUN
