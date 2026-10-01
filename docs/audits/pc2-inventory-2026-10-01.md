# PC2 worktree, dal ve stash envanteri — 2026-10-01

Bu rapor bağımsız denetim öncesi yerel Git envanteridir; merge, rebase, reset, stash pop/drop, force push veya ürün geliştirmesi yapılmadı. `no behaviour change` checkpoint mesajı, bu turda yeni geliştirme yapılmadığını anlatır; checkpoint içindeki eski çalışma değişikliklerinin davranış etkisi bağımsız denetim konusudur.

## Sonuç ve güvenlik sınırı

- `git worktree list --porcelain`: **42 worktree** (ana checkout dahil).
- `git for-each-ref refs/heads`: **46 yerel dal**; `git stash list --date=iso`: **0 stash**.
- HTTPS/Git Credential Manager üzerinden `archive/pc2/*` başlangıçta boş doğrulandı. **40 yeni archive ref** atomik push ile oluşturuldu ve remote read-back ile 40/40 doğrulandı; hiçbir mevcut origin dalı güncellenmedi.
- Dört worktree, mevcut kaynak/çıktı altında gerçek Development secret/config literal’leri nedeniyle checkpoint’ten ve o worktree’nin archive push’undan çıkarıldı. **305 exact dosya yolu** aşağıda listelenmiştir; değerleri rapora alınmadı.
- `git status --ignored --porcelain=v1 --untracked-files=all` 42 worktree üzerinde tarandı: belirtilen uzantılarda ve hariç tutulan dizinler dışında **0 ignored kaynak/test**.
- `.local/`, `.testoutput/`, `bin/`, `obj/`, `node_modules/` ve tüm gizli değer dosyaları commit dışında tutuldu. Ayrıca `.work/` altındaki generated/runtime çıktıları güvenli ürün kaynağı sayılmadı; secret tespit edilen worktree’lerde işlemler durduruldu.

## Worktree envanteri

Sayılar snapshot anındaki `git status --porcelain=v1 --untracked-files=all` giriş sayılarıdır. `M` değiştirilmiş/staged tracked, `U` untracked dosya sayısıdır; `.local` ve `.testoutput` untracked sayısına dahildir ama commit’e alınmaz. `C:/dev/ERP-vNext/.worktrees/` öneki tabloda `WT/` olarak kısaltılmıştır; `ROOT` ana checkout’tur.

| Yol | Dal | HEAD | M | U |
|---|---|---|---:|---:|
| `ROOT` | `feature/mdm/mod-0290-product-item-sku-master` | `9b7f0e61a1fdc1f697cda1acf3a803188aa1d74f` | 3 | 26072 |
| `WT/baseline-5cacc15c` | `DETACHED` | `5cacc15c191c95c1e3b95b3a16946e4e5f53079d` | 0 | 0 |
| `WT/baseline-dcb6509f` | `DETACHED` | `dcb6509ff69adaad3e135f5d6cfb88151f3075c3` | 0 | 0 |
| `WT/brd-mongo-profile` | `feature/pss/pss-012-brd-mongo-profile` | `6e81da661d8c9489a2bf4b3c513e372489aae796` | 0 | 0 |
| `WT/dcp-006-product-regulatory-pv-readiness` | `feature/mdm/dcp-006-product-regulatory-pv-readiness` | `33792201e86a6208e0c09e10e1a320c2b52ac6ba` | 0 | 0 |
| `WT/fg-writer-dependency-review-20260913` | `codex/fg-writer-dependency-review-20260913` | `2f2cf7cd0ce243abe11b36ee3e38892cc2604414` | 0 | 0 |
| `WT/fu22-baseline-verify` | `DETACHED` | `417ec2560c3b21a837815fabcced19d7d104f1e7` | 0 | 0 |
| `WT/mod-0018-fu16-brand-product-registration-mapping` | `feature/pss/mod-0018-fu16-brand-product-registration-mapping` | `8cc92a4101525054fdec528c9cf4c7ab177726bf` | 0 | 0 |
| `WT/mod-0018-fu21-trusted-legal-entity-scope` | `feature/pss/mod-0018-fu21-trusted-legal-entity-scope` | `417ec2560c3b21a837815fabcced19d7d104f1e7` | 0 | 0 |
| `WT/mod-0018-fu23-product-identity-lifecycle-permissions` | `feature/pss/mod-0018-fu23-product-identity-lifecycle-permissions` | `de3187437927dfc50857e10d0b07c8abe298d163` | 0 | 0 |
| `WT/mod-0018-fu24-fu26-governance` | `feature/pss/mod-0018-fu24-fu26-governance-reconciliation` | `14730a06b3dd6ae6bfd8d45dcc8a88384cf2898e` | 0 | 0 |
| `WT/mod-0021-fu01-r1-integration` | `feature/pss/mod-0021-fu01-r1-integration` | `096034ae4a9156c9463881370214c228598b7316` | 0 | 0 |
| `WT/mod-0021-fu01-trusted-durable-source-audit-intent-ingestion` | `feature/pss/mod-0021-fu01-trusted-durable-source-audit-intent-ingestion` | `dc77a468d26907fc7257f3b69c6c155fb866631a` | 0 | 0 |
| `WT/mod-0021-fu02-audit-outbox-temporal-storage-hardening` | `feature/pss/mod-0021-fu02-audit-outbox-temporal-storage-hardening` | `635ef04f27ec0b5c93390271d6cebd2884fb7ce3` | 0 | 0 |
| `WT/mod-0021-fu02-inventory` | `feature/pss/mod-0021-fu02-audit-temporal-inventory-drift` | `a3d822b93b0a8d5eb6b46b49972c94975506a66f` | 0 | 0 |
| `WT/mod-0023-fu02-start-authorization` | `feature/pss/mod-0023-fu02-start-authorization` | `28047a49ed6580c4fe9bf2aeb3501cbd0e54c6c5` | 0 | 0 |
| `WT/mod-0023-fu02-terminal-evidence-hardening` | `feature/pss/mod-0023-fu02-terminal-evidence-hardening` | `1af053ae8984cde78bd886180616612f923be36b` | 0 | 0 |
| `WT/mod-0023-fu03-closure-remediation` | `feature/pss/mod-0023-fu03-closure-projection-reconciliation` | `782b19f75b80cd54990a12589a0978cd871ade11` | 0 | 0 |
| `WT/mod-0023-fu04-port` | `feature/pss/mod-0023-fu04-port` | `0bf54703cc13a49d0551a991ebdfe405071e9c9d` | 0 | 0 |
| `WT/mod-0033-fu02-service-identity-token-issuance-foundation` | `feature/pss/mod-0033-fu02-service-identity-token-issuance-foundation` | `8998103bf3cda83a8742f433021c167bc3179cbf` | 0 | 0 |
| `WT/mod-0033-fu02-workflow-audience` | `feature/pss/mod-0033-fu02-workflow-audience` | `87dd852fbe05280b664aea0ea53f64dbb80ce936` | 0 | 0 |
| `WT/mod-0290-audit-consumer-delivery` | `feature/mdm/mod-0290-audit-consumer-delivery` | `e97f647cc15bb46acfffe790050e116315a2096b` | 0 | 0 |
| `WT/mod-0290-delegated-maker-port` | `feature/mdm/mod-0290-delegated-maker-gateway-compatibility` | `9f3254390cae3b9b76b435776b4ae9061fa17aef` | 0 | 0 |
| `WT/mod-0290-final-integration` | `feature/mdm/mod-0290-product-identity-final-integration` | `bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25` | 1 | 15261 |
| `WT/mod-0290-first-gsku-lifecycle` | `feature/mdm/mod-0290-first-gsku-lifecycle` | `d2d655fd70b82ba9395e32b5b5c1a201ce74e189` | 0 | 0 |
| `WT/mod-0290-fu01-abb-workcenter` | `feature/mdm/mod-0290-fu01-abb-workcenter` | `7dd8b91bc34851ffb8258308f1cfbfc7508f6459` | 0 | 442 |
| `WT/mod-0290-fu03-delivery` | `feature/mdm/mod-0290-fu03-delivery` | `b64b3dc8d5e83f6d8cc5e9a39fab7e8a91cfe0ac` | 0 | 0 |
| `WT/mod-0290-fu03-integration` | `feature/mdm/product-identity-lifecycle-integration` | `af0432c62c8c695b9f0b2e2b641a8559136c74aa` | 0 | 2842 |
| `WT/mod-0290-global-product-lifecycle` | `feature/mdm/mod-0290-global-product-lifecycle` | `c8e2b6b79b56c79b68e67cf4815a5a994888d3ce` | 0 | 0 |
| `WT/mod-0290-global-product-lifecycle-api` | `feature/mdm/mod-0290-global-product-lifecycle-api` | `615cb9d89854a901e399390026ff60450adceb28` | 0 | 0 |
| `WT/mod-0290-global-product-workflow-consumer` | `feature/mdm/mod-0290-global-product-workflow-consumer` | `1daf65d18621be300473c68c27fa51b4fb73aff6` | 0 | 0 |
| `WT/mod-0290-gsku-child-admission-retirement` | `feature/mdm/mod-0290-gsku-child-admission-retirement` | `a9d8cd8fbc34b1254c7035cfed55a9811025c387` | 0 | 0 |
| `WT/mod-0290-gsku-lifecycle-api` | `feature/mdm/mod-0290-gsku-lifecycle-api` | `8affaa00a4071f2d3ad67102426694890eff5979` | 0 | 0 |
| `WT/mod-0290-lsku-lifecycle` | `feature/mdm/mod-0290-lsku-lifecycle` | `69896a5f22df68a74a8c417f0f32168be09ae002` | 0 | 0 |
| `WT/mod-0290-lsku-replica-ping-remediation` | `feature/mdm/mod-0290-lsku-replica-ping-remediation` | `9783b53c9b8f03d7c5a4048bedd6ebe6375321a9` | 0 | 0 |
| `WT/mod-0290-product-identity-lifecycle-workcenter-plan` | `feature/mdm/mod-0290-product-identity-lifecycle-workcenter-plan` | `ace6d1937c938bea720195a3a011b7c672ce6bef` | 0 | 0 |
| `WT/mod-0290-product-lifecycle-ui` | `feature/mdm/mod-0290-product-lifecycle-ui` | `8f669de1fcf99c050e0ee92e8166485cec427790` | 0 | 0 |
| `WT/mod-0290-product-scope-integration` | `feature/mdm/mod-0290-product-scope-integration` | `9f6b999b45b9c45fa307ba7bddecca11f4df1b61` | 0 | 0 |
| `WT/product-five-release-readiness-20260913` | `codex/product-five-release-readiness-20260913` | `bdc39c86f4f9622ba713551a524ad97b17e8720e` | 0 | 303 |
| `WT/product-pv-delivery-integration-20260907` | `codex/product-pv-delivery-integration-20260907` | `e365118254d141e169daa0d614878577c70947bd` | 0 | 13378 |
| `C:/Users/AliT/.codex/worktrees/p5-entitlement-command/ERP-vNext` | `codex/p5-entitlement-command-20260929` | `10a268a2e1c42e48753cd1081c2ad8f15a469ba9` | 0 | 715 |
| `C:/Users/AliT/.codex/worktrees/p5-selected-audit/ERP-vNext` | `codex/p5-selected-audit-20260929` | `2e9ad4d1e75a7e114d464f1a5435ac8588c5e52f` | 0 | 1774 |

## Checkpoint ve durdurulan worktree’ler

- `WT/product-pv-delivery-integration-20260907`: 76 exact kaynak/test/pack/audit yolu commitlendi; `e365118254d141e169daa0d614878577c70947bd`. `docs/audits/product-five-cross-branch-delivery-audit-2026-09-29.md` commit içindedir. `.local/**` ve `.testoutput/**` dışarıda.
- `WT/fg-writer-dependency-review-20260913`: üç planning pack yolu commitlendi; `2f2cf7cd0ce243abe11b36ee3e38892cc2604414`.
- `C:/Users/AliT/.codex/worktrees/p5-entitlement-command/ERP-vNext`: 21 exact kaynak/test yolu commitlendi; `10a268a2e1c42e48753cd1081c2ad8f15a469ba9`; `.testoutput/**` dışarıda.
- `C:/Users/AliT/.codex/worktrees/p5-selected-audit/ERP-vNext`: 14 exact kaynak/test yolu commitlendi; `2e9ad4d1e75a7e114d464f1a5435ac8588c5e52f`; `.testoutput/**` dışarıda.
- `ROOT`, `WT/mod-0290-final-integration`, `WT/mod-0290-fu01-abb-workcenter`, `WT/mod-0290-fu03-integration`: secret/config dosyaları görüldüğü anda o worktree’de stage/commit/push durduruldu. ROOT’un 3 tracked dokümanı ve 3 yeni kaynak/rapor dosyası; final-integration’ın değiştirilmiş Development config’i; `.work/**` scratch çıktıları yerelde kaldı.
- Üç detached baseline worktree temizdi; yeni dal açma gereği yoktu. `WT/product-five-release-readiness-20260913` yalnız `.testoutput/**` içeriyordu; kaynak checkpoint’i yoktu.

## Yerel dal ve archive ref envanteri

`origin/<dal>` yoksa tüm yerel geçmiş aday sayıldı; varsa `origin/<dal>..<dal>` kullanıldı. Modül sütunu `origin/main...dal` değişen yol adlarından çıkarılmış kaba sınıflamadır; paylaşılan pack birden çok modüle dolaylı dokunabilir ve bu bir içerik denetimi değildir. `—` beş modül için path düzeyinde doğrudan eşleşme bulunmadığını belirtir.

| Yerel dal | SHA | Aday commit | Archive ref / durum | Yol bazlı beş-modül izi |
|---|---|---:|---|---|
| `backup/20260829-1253_fu23_pre_rebase` | `eb5195be04f19a799e23812db67affb45ba494b9` | 633 | PUSHED `archive/pc2/backup/20260829-1253_fu23_pre_rebase` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `codex/fg-writer-dependency-review-20260913` | `2f2cf7cd0ce243abe11b36ee3e38892cc2604414` | 870 | PUSHED `archive/pc2/codex/fg-writer-dependency-review-20260913` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `codex/p5-entitlement-command-20260929` | `10a268a2e1c42e48753cd1081c2ad8f15a469ba9` | 880 | PUSHED `archive/pc2/codex/p5-entitlement-command-20260929` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `codex/p5-selected-audit-20260929` | `2e9ad4d1e75a7e114d464f1a5435ac8588c5e52f` | 880 | PUSHED `archive/pc2/codex/p5-selected-audit-20260929` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `codex/product-five-release-readiness-20260913` | `bdc39c86f4f9622ba713551a524ad97b17e8720e` | 870 | PUSHED `archive/pc2/codex/product-five-release-readiness-20260913` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `codex/product-pv-delivery-integration-20260907` | `e365118254d141e169daa0d614878577c70947bd` | 880 | Bu rapor commit’inden sonra yeni archive ref olarak push edilecek | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/dcp-006-product-regulatory-pv-readiness` | `33792201e86a6208e0c09e10e1a320c2b52ac6ba` | 804 | PUSHED `archive/pc2/feature/mdm/dcp-006-product-regulatory-pv-readiness` | — |
| `feature/mdm/mod-0290-audit-consumer-delivery` | `e97f647cc15bb46acfffe790050e116315a2096b` | 624 | PUSHED `archive/pc2/feature/mdm/mod-0290-audit-consumer-delivery` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-dcp-backlog` | `f68e5c6d120629ea2958b77cfc140ee52c04e0eb` | 312 | PUSHED `archive/pc2/feature/mdm/mod-0290-dcp-backlog` | — |
| `feature/mdm/mod-0290-delegated-maker-gateway-compatibility` | `9f3254390cae3b9b76b435776b4ae9061fa17aef` | 831 | PUSHED `archive/pc2/feature/mdm/mod-0290-delegated-maker-gateway-compatibility` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-first-gsku-lifecycle` | `d2d655fd70b82ba9395e32b5b5c1a201ce74e189` | 661 | PUSHED `archive/pc2/feature/mdm/mod-0290-first-gsku-lifecycle` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-fu01-abb-workcenter` | `7dd8b91bc34851ffb8258308f1cfbfc7508f6459` | 627 | SECRET STOP — push yok | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-fu01-product-abbreviation-register` | `f68e5c6d120629ea2958b77cfc140ee52c04e0eb` | 312 | PUSHED `archive/pc2/feature/mdm/mod-0290-fu01-product-abbreviation-register` | — |
| `feature/mdm/mod-0290-fu03-delivery` | `b64b3dc8d5e83f6d8cc5e9a39fab7e8a91cfe0ac` | 623 | PUSHED `archive/pc2/feature/mdm/mod-0290-fu03-delivery` | — |
| `feature/mdm/mod-0290-fu03-integration` | `d296b43516e917f315368fdf5e950f088a36f218` | 645 | PUSHED `archive/pc2/feature/mdm/mod-0290-fu03-integration` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-global-product-lifecycle` | `c8e2b6b79b56c79b68e67cf4815a5a994888d3ce` | 654 | PUSHED `archive/pc2/feature/mdm/mod-0290-global-product-lifecycle` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-global-product-lifecycle-api` | `615cb9d89854a901e399390026ff60450adceb28` | 660 | PUSHED `archive/pc2/feature/mdm/mod-0290-global-product-lifecycle-api` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-global-product-workflow-consumer` | `1daf65d18621be300473c68c27fa51b4fb73aff6` | 658 | PUSHED `archive/pc2/feature/mdm/mod-0290-global-product-workflow-consumer` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-gsku-child-admission-retirement` | `a9d8cd8fbc34b1254c7035cfed55a9811025c387` | 662 | PUSHED `archive/pc2/feature/mdm/mod-0290-gsku-child-admission-retirement` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-gsku-lifecycle-api` | `8affaa00a4071f2d3ad67102426694890eff5979` | 663 | PUSHED `archive/pc2/feature/mdm/mod-0290-gsku-lifecycle-api` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-lsku-lifecycle` | `69896a5f22df68a74a8c417f0f32168be09ae002` | 675 | PUSHED `archive/pc2/feature/mdm/mod-0290-lsku-lifecycle` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-lsku-replica-ping-remediation` | `9783b53c9b8f03d7c5a4048bedd6ebe6375321a9` | 851 | PUSHED `archive/pc2/feature/mdm/mod-0290-lsku-replica-ping-remediation` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-product-identity-final-integration` | `bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25` | 855 | SECRET STOP — push yok | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-product-identity-lifecycle-workcenter-plan` | `ace6d1937c938bea720195a3a011b7c672ce6bef` | 628 | PUSHED `archive/pc2/feature/mdm/mod-0290-product-identity-lifecycle-workcenter-plan` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-product-item-sku-master` | `9b7f0e61a1fdc1f697cda1acf3a803188aa1d74f` | 314 | SECRET STOP — push yok | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-product-lifecycle-ui` | `8f669de1fcf99c050e0ee92e8166485cec427790` | 664 | PUSHED `archive/pc2/feature/mdm/mod-0290-product-lifecycle-ui` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/mdm/mod-0290-product-scope-integration` | `9f6b999b45b9c45fa307ba7bddecca11f4df1b61` | 642 | PUSHED `archive/pc2/feature/mdm/mod-0290-product-scope-integration` | Product Legal Entity Scope |
| `feature/mdm/product-identity-lifecycle-integration` | `af0432c62c8c695b9f0b2e2b641a8559136c74aa` | 688 | SECRET STOP — push yok | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0018-fu16-brand-product-registration-mapping` | `8cc92a4101525054fdec528c9cf4c7ab177726bf` | 827 | PUSHED `archive/pc2/feature/pss/mod-0018-fu16-brand-product-registration-mapping` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0018-fu21-trusted-legal-entity-scope` | `417ec2560c3b21a837815fabcced19d7d104f1e7` | 640 | PUSHED `archive/pc2/feature/pss/mod-0018-fu21-trusted-legal-entity-scope` | Product Legal Entity Scope |
| `feature/pss/mod-0018-fu23-product-identity-lifecycle-permissions` | `de3187437927dfc50857e10d0b07c8abe298d163` | 653 | PUSHED `archive/pc2/feature/pss/mod-0018-fu23-product-identity-lifecycle-permissions` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0018-fu24-fu26-governance-reconciliation` | `14730a06b3dd6ae6bfd8d45dcc8a88384cf2898e` | 836 | PUSHED `archive/pc2/feature/pss/mod-0018-fu24-fu26-governance-reconciliation` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0021-fu01-r1-integration` | `096034ae4a9156c9463881370214c228598b7316` | 624 | PUSHED `archive/pc2/feature/pss/mod-0021-fu01-r1-integration` | — |
| `feature/pss/mod-0021-fu01-trusted-durable-source-audit-intent-ingestion` | `dc77a468d26907fc7257f3b69c6c155fb866631a` | 587 | PUSHED `archive/pc2/feature/pss/mod-0021-fu01-trusted-durable-source-audit-intent-ingestion` | — |
| `feature/pss/mod-0021-fu02-audit-outbox-temporal-storage-hardening` | `635ef04f27ec0b5c93390271d6cebd2884fb7ce3` | 585 | PUSHED `archive/pc2/feature/pss/mod-0021-fu02-audit-outbox-temporal-storage-hardening` | — |
| `feature/pss/mod-0021-fu02-audit-temporal-inventory-drift` | `a3d822b93b0a8d5eb6b46b49972c94975506a66f` | 850 | PUSHED `archive/pc2/feature/pss/mod-0021-fu02-audit-temporal-inventory-drift` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0023-fu02-start-authorization` | `28047a49ed6580c4fe9bf2aeb3501cbd0e54c6c5` | 657 | PUSHED `archive/pc2/feature/pss/mod-0023-fu02-start-authorization` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0023-fu02-terminal-evidence-hardening` | `1af053ae8984cde78bd886180616612f923be36b` | 655 | PUSHED `archive/pc2/feature/pss/mod-0023-fu02-terminal-evidence-hardening` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0023-fu03-closure-projection-reconciliation` | `782b19f75b80cd54990a12589a0978cd871ade11` | 851 | PUSHED `archive/pc2/feature/pss/mod-0023-fu03-closure-projection-reconciliation` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0023-fu04-port` | `0bf54703cc13a49d0551a991ebdfe405071e9c9d` | 827 | PUSHED `archive/pc2/feature/pss/mod-0023-fu04-port` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0033-fu02-service-identity-token-issuance-foundation` | `8998103bf3cda83a8742f433021c167bc3179cbf` | 613 | PUSHED `archive/pc2/feature/pss/mod-0033-fu02-service-identity-token-issuance-foundation` | — |
| `feature/pss/mod-0033-fu02-workflow-audience` | `87dd852fbe05280b664aea0ea53f64dbb80ce936` | 656 | PUSHED `archive/pc2/feature/pss/mod-0033-fu02-workflow-audience` | Global Product, GSKU, LSKU, ABB, Product Legal Entity Scope |
| `feature/pss/mod-0048-fu01-reference-data-provider` | `f68e5c6d120629ea2958b77cfc140ee52c04e0eb` | 312 | PUSHED `archive/pc2/feature/pss/mod-0048-fu01-reference-data-provider` | — |
| `feature/pss/pss-012-brd-mongo-profile` | `6e81da661d8c9489a2bf4b3c513e372489aae796` | 583 | PUSHED `archive/pc2/feature/pss/pss-012-brd-mongo-profile` | — |
| `feature/pv/kurulum-dogrulama` | `f68e5c6d120629ea2958b77cfc140ee52c04e0eb` | 312 | PUSHED `archive/pc2/feature/pv/kurulum-dogrulama` | — |
| `main` | `f68e5c6d120629ea2958b77cfc140ee52c04e0eb` | 0 | origin/main karşılaştırmasında ahead=0; push yok | — |

## Secret/config nedeniyle atlanan exact yollar

Aşağıdaki dosyalarda Development config anahtarlarına atanmış boş olmayan literal değerler saptandı. İçerik ve değerler bu rapora alınmadı. `.work/` altındaki kopyalar generated/runtime çıktılarıdır; bu dosyalar commit/push edilmedi. Bu tarama `.local`/`.testoutput` gibi zaten yasaklı çıktı dizinlerinin secret içeriğini yayımlamayı amaçlamaz.

### `C:/dev/ERP-vNext` — 171 dosya

```text
.work/fu02e-cli-build-final3/appsettings.Development.json
.work/fu02e-cli-build/appsettings.Development.json
.work/fu02e-pipe-hardening-final/appsettings.Development.json
.work/fu02e-pipe-hardening/appsettings.Development.json
.work/fu02e-token-cap-final/appsettings.Development.json
.work/fu03-consumer-fix/build/appsettings.Development.json
.work/fu03-consumer-fix/tests/appsettings.Development.json
.work/fu03-g-review/frontend-release/appsettings.Development.json
.work/fu21-build/appsettings.Development.json
.work/fu21-closure-build/appsettings.Development.json
.work/fu21-closure-focused/Release/net8.0/appsettings.Development.json
.work/fu21-closure-focused2/Release/net8.0/appsettings.Development.json
.work/fu21-closure-full/Release/net8.0/appsettings.Development.json
.work/fu21-closure-release/appsettings.Development.json
.work/fu21-final-build/appsettings.Development.json
.work/fu21-final-focused/Release/net8.0/appsettings.Development.json
.work/fu21-final-full/Release/net8.0/appsettings.Development.json
.work/fu21-final-release/appsettings.Development.json
.work/fu21-focused/Release/net8.0/appsettings.Development.json
.work/fu21-full/Release/net8.0/appsettings.Development.json
.work/fu21-release/out/appsettings.Development.json
.work/fu21-test-build/appsettings.Development.json
.work/g2-final-build/out/appsettings.Development.json
.work/g2-frontend-release-final/appsettings.Development.json
.work/g2-frontend-release-root/appsettings.Development.json
.work/g2-lifecycle-frontend-build/appsettings.Development.json
.work/g4-atomic-independent-review-out/appsettings.Development.json
.work/g4-coordinator-race/appsettings.Development.json
.work/g4-coordinator-race2/appsettings.Development.json
.work/g4-final-4/appsettings.Development.json
.work/g4-final-5/appsettings.Development.json
.work/g4-final-6/appsettings.Development.json
.work/g4-identity-final/appsettings.Development.json
.work/g4-main-api/appsettings.Development.json
.work/g4-main-api2/appsettings.Development.json
.work/g4-main-api3/appsettings.Development.json
.work/g4-main-api4/appsettings.Development.json
.work/g4-main-final-2/appsettings.Development.json
.work/g4-main-final-3/appsettings.Development.json
.work/g4-main-final-focused/appsettings.Development.json
.work/g4-main-final-full/appsettings.Development.json
.work/g4-main-final-release/appsettings.Development.json
.work/g4-main-full-early/appsettings.Development.json
.work/g4-main-full/appsettings.Development.json
.work/g4-main-tests-1/appsettings.Development.json
.work/g4-main-tests-2/appsettings.Development.json
.work/g4-main-tests-3/appsettings.Development.json
.work/g4-main-tests-4/appsettings.Development.json
.work/g4-main-tests-5/appsettings.Development.json
.work/g4-main-tests-final/appsettings.Development.json
.work/g4-main-tests/appsettings.Development.json
.work/g4-main-tests2/appsettings.Development.json
.work/g4-main-tests3/appsettings.Development.json
.work/g4-main-tests4/appsettings.Development.json
.work/g4-main-tests5/appsettings.Development.json
.work/g4-temporal-tests-10/appsettings.Development.json
.work/g4-temporal-tests-11/appsettings.Development.json
.work/g4-temporal-tests-12/appsettings.Development.json
.work/g4-temporal-tests-13/appsettings.Development.json
.work/g4-temporal-tests-2/appsettings.Development.json
.work/g4-temporal-tests-3/appsettings.Development.json
.work/g4-temporal-tests-4/appsettings.Development.json
.work/g4-temporal-tests-6/appsettings.Development.json
.work/g4-temporal-tests-7/appsettings.Development.json
.work/g4-temporal-tests-8/appsettings.Development.json
.work/g4-temporal-tests-9/appsettings.Development.json
.work/g4-temporal-tests/appsettings.Development.json
.work/g4-worker-mongo/appsettings.Development.json
.work/g4-worker-scope/appsettings.Development.json
.work/gsku-all-final/appsettings.Development.json
.work/gsku-all-final2/appsettings.Development.json
.work/gsku-d-api/appsettings.Development.json
.work/gsku-d-e-api/appsettings.Development.json
.work/gsku-d-e-core/appsettings.Development.json
.work/gsku-d-e-manifest/appsettings.Development.json
.work/gsku-d-e-manifest2/appsettings.Development.json
.work/gsku-d-e-tests/appsettings.Development.json
.work/gsku-d-platform-tests/appsettings.Development.json
.work/gsku-d-regression/appsettings.Development.json
.work/gsku-d-tests2/appsettings.Development.json
.work/gsku-d-tests3/appsettings.Development.json
.work/gsku-d-tests5/appsettings.Development.json
.work/gsku-d-tests6/appsettings.Development.json
.work/gsku-d-tests7/appsettings.Development.json
.work/gsku-d-tests8/appsettings.Development.json
.work/gsku-e-build/appsettings.Development.json
.work/gsku-e-tests/appsettings.Development.json
.work/gsku-g-build/frontend/appsettings.Development.json
.work/gsku-gateway-build/appsettings.Development.json
.work/gsku-h-runtime/auth/appsettings.Development.json
.work/gsku-h-runtime/mdm/appsettings.Development.json
.work/gsku-h-runtime/platform/appsettings.Development.json
.work/gsku-h-tests/auth/appsettings.Development.json
.work/gsku-h-tests/mdm/appsettings.Development.json
.work/gsku-h-tests/platform2/appsettings.Development.json
.work/gsku-retirement-frontend/appsettings.Development.json
.work/h1b-coordinator/appsettings.Development.json
.work/h1b-coordinator2/appsettings.Development.json
.work/h1b-crash1/appsettings.Development.json
.work/h1b-current/appsettings.Development.json
.work/h1b-deep1/appsettings.Development.json
.work/h1b-deep2/appsettings.Development.json
.work/h1b-deep3/appsettings.Development.json
.work/h1b-deep4/appsettings.Development.json
.work/h1b-deep5/appsettings.Development.json
.work/h1b-defect-fixed/appsettings.Development.json
.work/h1b-defect/appsettings.Development.json
.work/h1b-focused-2/appsettings.Development.json
.work/h1b-focused-final/appsettings.Development.json
.work/h1b-focused/appsettings.Development.json
.work/h1b-full-final/appsettings.Development.json
.work/h1b-header/appsettings.Development.json
.work/h1b-header2/appsettings.Development.json
.work/h1b-header3/appsettings.Development.json
.work/h1b-header4/appsettings.Development.json
.work/h1b-mongo/appsettings.Development.json
.work/h1b-runner1/appsettings.Development.json
.work/h1b-runner2/appsettings.Development.json
.work/h1b-runner3/appsettings.Development.json
.work/h1b-size-defect/appsettings.Development.json
.work/integrated-review/auth-focused/appsettings.Development.json
.work/integrated-review/gateway/appsettings.Development.json
.work/integrated-review/lsku-contract-final/appsettings.Development.json
.work/integrated-review/manifest/appsettings.Development.json
.work/integrated-review/mdm-focused-2/appsettings.Development.json
.work/integrated-review/mdm-focused/appsettings.Development.json
.work/l2-build/appsettings.Development.json
.work/l2-focused/appsettings.Development.json
.work/l2-full/appsettings.Development.json
.work/l2-release-final/appsettings.Development.json
.work/l2-release/appsettings.Development.json
.work/l2-test/appsettings.Development.json
.work/lsku-auth-final-build/appsettings.Development.json
.work/lsku-auth-final-test-bin/Release/net8.0/appsettings.Development.json
.work/lsku-retirement-final-build4/appsettings.Development.json
.work/lsku-retirement-final-test-bin/Release/net8.0/appsettings.Development.json
.work/market-cli-run/appsettings.Development.json
.work/market-cli-tests/appsettings.Development.json
.work/market-operational-build/appsettings.Development.json
.work/market-operational-release/appsettings.Development.json
.work/market-replay-fix-tests/appsettings.Development.json
.work/mod-0290-final-runtime-20260903/frontend-build/appsettings.Development.json
.work/mod-0290-final-runtime-20260903/platform-build/Release/net8.0/appsettings.Development.json
.work/mod-0290-g1-actor-build/appsettings.Development.json
.work/mod-0290-g1-final-review-build2/appsettings.Development.json
.work/mod-0290-g1-final-review-build3/appsettings.Development.json
.work/mod-0290-g1-frontend-build-final/appsettings.Development.json
.work/mod-0290-g1-frontend-build/appsettings.Development.json
.work/mod-0290-maker-replay-regression/bin2/Release/net8.0/appsettings.Development.json
.work/mod-0290-maker-replay-regression/bin3/Release/net8.0/appsettings.Development.json
.work/mod-0290-maker-replay-regression/bin4/Release/net8.0/appsettings.Development.json
.work/mod0033-fu02-rereview/appsettings.Development.json
.work/mod0033-fu02-review/appsettings.Development.json
.work/nav-frontend-build/out/appsettings.Development.json
.work/nav-l10n-test2/out/appsettings.Development.json
.work/nav-manifest-test2/out/appsettings.Development.json
.work/navigation-enable/build/appsettings.Development.json
.work/navigation-enable/tests/appsettings.Development.json
.work/personalization-save-view-build/appsettings.Development.json
.work/personalization-save-view-build2/appsettings.Development.json
.work/verify-platform-wiring/api-bin/Release/net8.0/appsettings.Development.json
.work/verify-platform-wiring/test-bin/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-api/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-api5/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-tests3/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-tests4/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/fu03-f-tests5/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/fu03-f-tests4/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/fu03-f-tests5/Release/net8.0/appsettings.Development.json
```

### `C:/dev/ERP-vNext/.worktrees/mod-0290-final-integration` — 103 dosya

```text
.work/abb-review-auth/appsettings.Development.json
.work/abb-review-frontend/appsettings.Development.json
.work/abb-review-mdm/appsettings.Development.json
.work/abb-review-platform-2/appsettings.Development.json
.work/auth-l5-build/Release/net8.0/appsettings.Development.json
.work/auth-l5-full/Release/net8.0/appsettings.Development.json
.work/auth-l5-output/Release/net8.0/appsettings.Development.json
.work/gateway-put-route/build-bin/Release/net8.0/appsettings.Development.json
.work/gateway-put-route/contract-bin/Release/net8.0/appsettings.Development.json
.work/gsku-audit-map-bin/appsettings.Development.json
.work/gsku-b-finalbin/appsettings.Development.json
.work/gsku-b-mongobin/appsettings.Development.json
.work/gsku-b-proofbin/appsettings.Development.json
.work/gsku-b-proofbin2/appsettings.Development.json
.work/gsku-b-regbin/appsettings.Development.json
.work/gsku-b-testbin/appsettings.Development.json
.work/gsku-b-testbin2/appsettings.Development.json
.work/gsku-correction-build2/appsettings.Development.json
.work/gsku-correction-final-build/appsettings.Development.json
.work/gsku-correction-frontend-final/appsettings.Development.json
.work/gsku-correction-frontend/appsettings.Development.json
.work/gsku-correction-mdm/appsettings.Development.json
.work/gsku-correction-platform/appsettings.Development.json
.work/gsku-correction-tests3/appsettings.Development.json
.work/gsku-correction-tests4/appsettings.Development.json
.work/gsku-correction-tests5/appsettings.Development.json
.work/gsku-correction-tests6/appsettings.Development.json
.work/gsku-correction-tests7/appsettings.Development.json
.work/gsku-frontend-bin-final/appsettings.Development.json
.work/gsku-frontend-bin/appsettings.Development.json
.work/l3-api-build/appsettings.Development.json
.work/l3-final-build/appsettings.Development.json
.work/l3-focused-build/appsettings.Development.json
.work/l3-regression-build/appsettings.Development.json
.work/l3-release-build/appsettings.Development.json
.work/l3-review-build/appsettings.Development.json
.work/l3-review-final/appsettings.Development.json
.work/l3-review-release/appsettings.Development.json
.work/l3-test-build/appsettings.Development.json
.work/l34-fix-build-final/appsettings.Development.json
.work/l34-fix-combined/appsettings.Development.json
.work/l34-fix-compile/appsettings.Development.json
.work/l34-fix-final/appsettings.Development.json
.work/l34-fix-final2/appsettings.Development.json
.work/l34-fix-focused/appsettings.Development.json
.work/l4-focused-reason/appsettings.Development.json
.work/l4-focused/appsettings.Development.json
.work/l4-unit/appsettings.Development.json
.work/l6-frontend-build-final/appsettings.Development.json
.work/l6-frontend-build/appsettings.Development.json
.work/lsku-final-tests/appsettings.Development.json
.work/lsku-final-tests2/appsettings.Development.json
.work/lsku-frontend-build2/appsettings.Development.json
.work/lsku-retirement-build/appsettings.Development.json
.work/lsku-retirement-final-build/appsettings.Development.json
.work/lsku-retirement-final-build3/appsettings.Development.json
.work/lsku-slice-build/appsettings.Development.json
.work/lsku-test-bin/Release/net8.0/appsettings.Development.json
.work/lsku-test-bin2/Release/net8.0/appsettings.Development.json
.work/lsku-tests/appsettings.Development.json
.work/lsku-tests2/appsettings.Development.json
.work/lsku-tests3/appsettings.Development.json
.work/platform-audit-test-bin/Release/net8.0/appsettings.Development.json
frontend/Diten.Web/.work/l6-review-fix-build/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-build2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-mongo/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-mongo2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-mongo3/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-audit-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-complete-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-complete-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-build/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-output/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-output2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-output3/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-output4/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/abb-wc-output5/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/lsku-final-build/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/lsku-withdraw-build/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/lsku-withdraw-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/lsku-withdraw-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/src/Diten.MdmService.Api/.work/lsku-withdraw-tests3/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-audit-mongo/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-audit-mongo2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-audit-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-complete-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-complete-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-wc-output/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-wc-output2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-wc-output3/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-wc-output4/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/abb-wc-output5/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/lsku-withdraw-tests/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/lsku-withdraw-tests2/Release/net8.0/appsettings.Development.json
services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/.work/lsku-withdraw-tests3/Release/net8.0/appsettings.Development.json
services/Diten.Platform/src/Diten.Platform.API/.work/abb-audit-output/Release/net8.0/appsettings.Development.json
services/Diten.Platform/src/Diten.Platform.API/.work/abb-platform-tests/Release/net8.0/appsettings.Development.json
services/Diten.Platform/src/Diten.Platform.API/.work/lsku-audit-tests/Release/net8.0/appsettings.Development.json
services/Diten.Platform/tests/Diten.Platform.Application.Tests/.work/abb-audit-output/Release/net8.0/appsettings.Development.json
services/Diten.Platform/tests/Diten.Platform.Application.Tests/.work/abb-platform-tests/Release/net8.0/appsettings.Development.json
services/Diten.Platform/tests/Diten.Platform.Application.Tests/.work/lsku-audit-tests/Release/net8.0/appsettings.Development.json
services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json
```

### `C:/dev/ERP-vNext/.worktrees/mod-0290-fu01-abb-workcenter` — 3 dosya

```text
.work/abb-focused-replay/appsettings.Development.json
.work/abb-full-replay/appsettings.Development.json
.work/abb-replay-tests/appsettings.Development.json
```

### `C:/dev/ERP-vNext/.worktrees/mod-0290-fu03-integration` — 28 dosya

```text
.work/auth-fu24-live-current/appsettings.Development.json
.work/auth-fu24-operational/appsettings.Development.json
.work/auth-fu25-live/appsettings.Development.json
.work/auth-lifecycle-current/appsettings.Development.json
.work/di-fix/build/appsettings.Development.json
.work/di-fix/test/appsettings.Development.json
.work/h-acceptance-build/auth-final/appsettings.Development.json
.work/h-acceptance-build/frontend-final/appsettings.Development.json
.work/h-acceptance-build/gateway-final/appsettings.Development.json
.work/h-acceptance-build/mdm-final/appsettings.Development.json
.work/h-acceptance-build/platform-final/appsettings.Development.json
.work/live-accept-current/auth/appsettings.Development.json
.work/live-accept-current/frontend/appsettings.Development.json
.work/live-accept-current/gateway/appsettings.Development.json
.work/live-accept-current/mdm/appsettings.Development.json
.work/live-accept-current/platform/appsettings.Development.json
.work/live-accept-final/auth-fu25-v2/appsettings.Development.json
.work/live-accept-final/auth-fu25-v3/appsettings.Development.json
.work/live-accept-final/auth-fu25-v4/appsettings.Development.json
.work/live-accept-final/auth-fu25/appsettings.Development.json
.work/live-accept-final/auth/appsettings.Development.json
.work/live-accept-final/mdm-v2/appsettings.Development.json
.work/live-accept-final/mdm-v3/appsettings.Development.json
.work/live-accept-final/mdm/appsettings.Development.json
.work/live-accept-final/platform/appsettings.Development.json
.work/live-final/frontend/appsettings.Development.json
.work/live-final/mdm/appsettings.Development.json
services/Diten.AuthService/src/Diten.AuthService.Api/.work/fu24-build/Release/net8.0/appsettings.Development.json
```

## Açık kalanlar

- `codex/product-pv-delivery-integration-20260907` için bu raporun commit’i ve yeni `archive/pc2/codex/product-pv-delivery-integration-20260907` ref’i, rapor snapshot’ından sonra tamamlanıp son kullanıcı yanıtında SHA ile doğrulanmalıdır; commit kendi SHA’sını içeremez.
- Secret stop verilen dört worktree’nin mevcut kaynakları/çıktıları ve archive ref’leri bu turda aktarılmadı; güvenli ayıklama ve ayrı onay gerekir. Burada kalan iş bağımsız denetimde yapılmış sayılmamalıdır.
- Ürün işinde bağımsız denetim bitene kadar yeni geliştirme yapılmamalıdır.

## Ek tur — kalan dört worktree'nin güvenli arşivlenmesi (2026-10-01)

Bu bölüm önceki snapshot'taki dört secret-stop dalın aktarılmamış olması durumunu günceller. Kullanıcının yeni, açık config-hariç checkpoint/push yetkisiyle dört dalın güvenli kaynak ve commit geçmişi GitHub'a aktarıldı. Secret config ve üretilmiş çıktılar aktarılmadı; çalışma ağacında korundu. Yeni geliştirme, test/build, servis/veri işlemi, merge, rebase, force push, silme, reset veya stash işlemi yapılmadı. Mevcut worktree dalları değiştirilmedi.

### Başlangıç envanteri

| Worktree | Yerel dal | Başlangıç HEAD | Değişmiş tracked | Untracked | Bu tur checkpoint dosyası |
|---|---|---|---:|---:|---:|
| `C:/dev/ERP-vNext` | `feature/mdm/mod-0290-product-item-sku-master` | `9b7f0e61a1fdc1f697cda1acf3a803188aa1d74f` | 3 | 26072 | 5 |
| `C:/dev/ERP-vNext/.worktrees/mod-0290-final-integration` | `feature/mdm/mod-0290-product-identity-final-integration` | `bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25` | 1 | 15261 | 0 |
| `C:/dev/ERP-vNext/.worktrees/mod-0290-fu01-abb-workcenter` | `feature/mdm/mod-0290-fu01-abb-workcenter` | `7dd8b91bc34851ffb8258308f1cfbfc7508f6459` | 0 | 442 | 0 |
| `C:/dev/ERP-vNext/.worktrees/mod-0290-fu03-integration` | `feature/mdm/product-identity-lifecycle-integration` | `af0432c62c8c695b9f0b2e2b641a8559136c74aa` | 0 | 2842 | 0 |

Untracked üst-klasör dağılımı (checkpoint öncesi, dosya sayımı):

| Worktree | Üst klasör → dosya sayısı |
|---|---|
| Ana checkout | `.work` 24625; `services` 855; `work` 542; `.worktrees` 39; `docs` 8; `.pnpm-store` 3 |
| final-integration | `.work` 8815; `services` 5531; `.testoutput` 880; `frontend` 35 |
| ABB WorkCenter | `.work` 442 |
| FU03 integration | `.work` 2713; `services` 129 |

### Checkpoint ve GitHub read-back

Ana checkout checkpoint'i: `23c0f21839ed8b6df77625e6c4f40fc11b1cb194`.

Mesaj: `wip: checkpoint before independent audit — ERP-vNext (config with secrets excluded)`.

Exact beş kaynak-belge yolu:

- `docs/product-backlog.md`
- `execution/domains/platform-shared-services/module-packs/CAND-CAP-0002-FU05-tenant-module-entitlements.md`
- `execution/domains/platform-shared-services/module-packs/PSS-012-business-reference-data-stewardship.md`
- `docs/audits/product-five-cross-branch-delivery-audit-2026-09-29.md`
- `docs/audits/product-five-cross-branch-inventory-2026-09-29.json`

Diğer üç worktree'de üretilmiş/secret içerik dışında checkpoint edilecek yeni kaynak/test/belge bulunmadı; boş commit oluşturulmadı. Mevcut commit geçmişleri yine push edildi.

| Yerel dal | Origin archive ref | Origin'den doğrulanan SHA | Ürün kapsamı |
|---|---|---|---|
| `feature/mdm/mod-0290-product-item-sku-master` | `archive/pc2/feature/mdm/mod-0290-product-item-sku-master` | `23c0f21839ed8b6df77625e6c4f40fc11b1cb194` | Beş ürünün çapraz-dal teslim envanteri; bu checkpoint runtime değişikliği içermez |
| `feature/mdm/mod-0290-product-identity-final-integration` | `archive/pc2/feature/mdm/mod-0290-product-identity-final-integration` | `bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25` | GP/GSKU/LSKU ve ortak ürün lifecycle entegrasyon geçmişi |
| `feature/mdm/mod-0290-fu01-abb-workcenter` | `archive/pc2/feature/mdm/mod-0290-fu01-abb-workcenter` | `7dd8b91bc34851ffb8258308f1cfbfc7508f6459` | ABB / WorkCenter |
| `feature/mdm/product-identity-lifecycle-integration` | `archive/pc2/feature/mdm/product-identity-lifecycle-integration` | `af0432c62c8c695b9f0b2e2b641a8559136c74aa` | GP/GSKU/LSKU/ABB/Scope entegrasyon geçmişi |

Dört yeni ref atomik, normal push ile oluşturuldu; `git ls-remote origin "refs/heads/archive/pc2/*"` read-back'i dört tip SHA'sını birebir doğruladı. Önceki 41 ref ile toplam 45 archive ref gözlendi. Bu raporun entegrasyon-dalı commit'i ayrıca mevcut `archive/pc2/codex/product-pv-delivery-integration-20260907` ref'ine normal fast-forward push edilir; kendi commit SHA'sı son kullanıcı yanıtında bildirilir.

### Commit geçmişi / secret kapısı

Origin'deki güncel `main` SHA'sı `90fde0846ef5c5eb630439c52ba13a6053ffad55` olarak doğrulandı ve yalnız remote-tracking `origin/main` güncellendi; yerel main/checkout değiştirilmedi. Önceki eski baseline'ın yerine bu güncel baseline ile dört dalın belirlenen config yolları yeniden karşılaştırıldı.

| Worktree | `origin/main` → dal config yol farkı | Main dışında config-yolu commit sayısı | Sonuç |
|---|---:|---:|---|
| Ana checkout | 0 | 0 | Push güvenlik kapısı geçti |
| final-integration | 1 | 0 | Yalnız `services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json`; secret leaf değer farkı 0, yalnız non-secret şekil farkı; geçti |
| ABB WorkCenter | 0 | 0 | Geçti |
| FU03 integration | 0 | 0 | Geçti |

Final-integration config karşılaştırması JSON içinde, değerler çıktıya alınmadan yapıldı. Secret alanlarının path/değer eşitliği korundu. Dalın main'de bulunmayan yeni secret config commit'i saptanmadı. Bu tur push edilemeyen dal yoktur. Bu kontrol, main'de zaten bulunan secret'ları yeni secret saymaz veya main'in genel secret güvenliğine sertifika vermez.

### Dışarıda kalan dosyalar ve korunma

305 secret-config dosyasının exact ADLARI önceki dört-worktree ekindeki listelerde yer alır; hiçbir değer raporlanmaz. Bu tur statü sınıflaması:

- Ana checkout: önceki `C:/dev/ERP-vNext` listesindeki 171 yolun tamamı **untracked**, commit dışında.
- final-integration: önceki 103 yoldan `services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json` **dirty tracked**; kalan 102 yol **untracked**, commit dışında.
- ABB WorkCenter: önceki 3 yolun tamamı **untracked**, commit dışında.
- FU03 integration: önceki 28 yolun tamamı **untracked**, commit dışında.

Toplam: 304 untracked secret config + 1 dirty tracked secret config. Dirty config değiştirilmedi/stage edilmedi. Ordinal `relative path + SHA-256` envanterlerinin checkpoint/push öncesi ve sonrası eşitliği:

| Worktree | Dosya | Önce = sonra SHA-256 |
|---|---:|---|
| Ana checkout | 171 | `C5E4134642AF3C26958A6442C549BFB3203167B0A6B43071C752F7FDD419C078` |
| final-integration | 103 | `0ADC983E14183C8E0ACD3A269ECC0ADDFD55EBFF01C217F9625B59D7C5357717` |
| ABB WorkCenter | 3 | `3C8A6FD75A3AFE7A0F48BFE02399A16508F62F3D8A5FB0BDB12F130B03AD738F` |
| FU03 integration | 28 | `BB5515008E738FD50F9761DEDE37A232FD6F9BBE639D7AD5CB91869B28D72F34` |

Üretilmiş çıktılar, dependency/cache ve kopya runtime/build ağaçları (`bin/`, `obj/`, `.local/`, `.testoutput/`, `.work/`, `node_modules/`, `.pnpm-store/`, `TestResults/`, `work/`, nested checkout çıktıları ve geçici çıktılar) kapsam dışında kaldı. Yukarıdaki untracked dağılımlar bunları içerir; tamamı korunmuştur.

Ek özel dışlamalar:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/LskuIdentityWorkflowOptions.cs`: uzantısı `.cs` olsa da içerik gerçek kaynak değil, yakalanmış dosya-okuma hata transcript'idir; generated error output olarak dışlandı ve korundu.
- `docs/ES-ek-doc/Decomposition_Work_Structuring_Engine_Spec_v2_0_Audited.docx`
- `docs/ES-ek-doc/Integrated_Product_Development_CTD_Canonical_Architecture_v1.3.docx`
- `docs/ES-ek-doc/Management_Governance_Domain_Structure_v2_1_Audited.docx`
- `docs/ES-ek-doc/Project ongoing status report 26 July 2026 Integrated RnD-RA Lifecycle - v4.xlsx`
- `docs/ES-ek-doc/Project ongoing status report 26 July 2026 Integrated RnD-RA Lifecycle - v5.xlsx`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx`

Son altı belge, bu talebin açık kaynak/test/belge uzantı allow-list'inde `.docx/.xlsx` bulunmadığı için commit edilmedi; yerinde korunur.

### Özellikle istenen commit nesneleri

| İstenen nesne | Tam commit SHA | GitHub archive üzerinden erişim |
|---|---|---|
| `af0432c62` | `af0432c62c8c695b9f0b2e2b641a8559136c74aa` | `archive/pc2/feature/mdm/product-identity-lifecycle-integration` tip'i |
| `bb9ce94d0` | `bb9ce94d0ca3d4520f3d1555d284cbe3f8219f25` | `archive/pc2/feature/mdm/mod-0290-product-identity-final-integration` tip'i |
| `9b7f0e61a` | `9b7f0e61a1fdc1f697cda1acf3a803188aa1d74f` | Ana checkout archive tip'inin atası |
| `48154c4f1` | `48154c4f18c2070d0b912bd86a5a62cbb6805158` | FU03 lifecycle-integration archive tip'inin atası |

Dört nesne commit olarak doğrulandı; belirtilen dal tipleri için dört `git merge-base --is-ancestor` kontrolü exit 0 verdi. Origin tip SHA read-back'i ile birlikte nesnelerin push edilen geçmişten ulaşılabilir olduğu doğrulandı.

### Son sınır

Kayıt ve arşivleme, modüllerin tamamlandığı veya kabul/merge-ready olduğu anlamına gelmez. Secret config ve kapsam dışı binary belgeler bilinçli biçimde yalnız yerel kalır. Bağımsız denetim tamamlanana kadar bu PC'de ürün geliştirmesi yapılmayacaktır.
