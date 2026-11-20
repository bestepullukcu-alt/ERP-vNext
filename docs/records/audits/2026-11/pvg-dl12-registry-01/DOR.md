# WP-0230-DL12-REGISTRY-01 — dağıtım hazırlığı

2026-11-20. Görev sınıfı yönetişim; HIGH, ortak kayıt ve kabul kapısı etkisi. Tek yazıcı `pvg_dl12_readiness`, ayrı temiz managed ağaç `C:/Users/epenc/.codex/worktrees/pvg-dl12-registry/ERP-vNext-git`, dal `feature/pvg/mod-0230-dl12-registry`, ölçülmüş ExpectedHEAD `6902667db14e9ba2f0bd5518fccf228608913260`. Kurulum sonrası yalnız ff-only taban güncellemesi yapıldı; ürün/kayıt yazımı veya test yok.

İnsan XMC kapsam onayı ve açık WP bağı `extra-scope-approval-01.md@a2586b89` içinde. Tek yazma yolu `tests/known-failures.tsv`, yalnız beş fixed rerun_trigger hücresindeki `,*.xml` öğesi. Başka modül/ürün/guard/TSV hücresi değişmez. Ürün sınıfı olmadığı için yeni MP sürümünün onayı bu idari işe aktarılmaz veya önkoşul uydurulmaz; kendi yönetişim kapsam kaydı geçerlidir.

Gerçek keşif `pvg-rev21-discovery-01@9d0df41c5ddc8cfc6f0a9258459501c2c380607b`,31 bulgu/535ref, check-report0 ve kararları kayıtlı. Ortak prefix nedeniyle bu idari satır raporda yok; ledger geçmişi/scope ayrı salt okunur incelendi. Parent veya PREP-WEB BLOCKED satırına bağımlılık konmaz; aksi döngü oluştururdu. Önceki veri koruma eksikleri idari işin kapsamı değildir.

Mevcut Python/Git ve kalıcı Web TRX bu tabanda vardır; SDK/Mongo/npm/uygulama sunucusu gerekmiyor. Gerçek CT kabul üreticisinin kırmızı tabanı BASELINE.md/baseline-red.json ile bağlı. Bağımsız teknik öneri REGISTRY-PROPOSAL-REVIEW.md içinde, XMC insan koşulu artık karşılandı. Bağımsız kesin prompt incelemesi ve normal HIGH dağıtım kapısı henüz BEKLENİYOR. READY bu kapıların geçtiği veya kod yazılabileceği anlamına gelmez. Teslimden sonra gerçek üretim validate/check0, tek-token bellek RED ve bağımsız tam diff/entegrasyon denetimi gerekir. Push/PR yok.
