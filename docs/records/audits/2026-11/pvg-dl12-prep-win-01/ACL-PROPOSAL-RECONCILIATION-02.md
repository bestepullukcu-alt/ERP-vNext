# Windows ACL kapsam önerisi — ikinci inceleme karşılığı

2026-11-20. CT kararı; ürün veya paket değişikliği değildir.

Ham inceleme: `docs/records/reviews/MOD-0230/2026-11-20-windows-acl-scope-review-02.md`, hedef öneri SHA-256 `cde6effd06f482a1e5a3027d842d41856fea7679dfe34109603e5409eb0a5d47`, sonuç KOŞULLU; ENGEL yok. Önceki F-1/F-2/F-3 kapanmış; F-4 orta bulgu kabul edildi.

Paket yazarı yalnız önerinin CI kaynak bağını netleştirdi. Yeni öneri SHA-256 `89e373028ecf7acdbe9d12f0de09fa2d06d4839f617bca24fb4d651a491eb758`.
Olağan PR sentetik merge checkout'u ancak gerçek commit ebeveynleri, olay head/base'i ve tree(M)==tree(H) ile kaynak eşliğini kanıtlarsa yeterlidir. Bu bağ kurulamazsa mevcut workflow_dispatch final dalda gerçek checkout C==H ve aynı pvg-api işi/TRX ile kullanılabilir. Yeni YAML değişikliği veya ikinci gereksiz matrix koşusu önerilmez. Final H değişirse eski kanıt kapanış sayılmaz.

Bu koşu ve kaynak bağı henüz ölçülmedi. Tek öneri üçüncü test dosyasında tek metot gövdesidir; insan ek kapsam onayı henüz istenmedi/alınmadı. Mevcut pack/domain SHA'ları değişmedi; eski dirty WIN ağacı korunuyor. Checkpoint istisnası istenmiyor. Son öneri yeniden bağımsız incelemeye sunulacaktır.
