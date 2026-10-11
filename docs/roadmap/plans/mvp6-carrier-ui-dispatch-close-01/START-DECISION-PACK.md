# Exact start-decision package

## Current authority result

No applicable approval exists for pack application, BC successor transfer, combined shared integration application, or UI DEV. The SCOPE-01 owner text and BC owner text are drafts, and the backend-only MOD-0184 readiness does not grant UI work. Therefore nothing was applied.

## Decision A — UI pack and Phase 1.5

Copyable text:

> MOD-0184 Carrier UI için SCOPE-01 proposed pack patch SHA256 `93ee76da36fd100069878b0e3b0621f6e60884d312d0e142c2e7fcb24267777d` dosyasının pack preimage `2df9363b7c870672fab13b68a87e7fb849ed7a3f213c5197e87f9c2147de817e` üzerine uygulanarak target `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f` üretmesini ve `PHASE15-ACCEPTANCE.tsv` içindeki tasarım eşlemesini onaylıyorum. Bu karar mevcut bounded backend kabulünü korur; UI DEV, BC kaynak aktarımı veya shared entegrasyon uygulama yetkisi vermez.

## Decision B — separate final BC + integration handoff

This must be a distinct owner action after selecting the exact registered target checkout.

Copyable text:

> Seçilen kayıtlı integration checkout için, ayrı BC successor kararındaki exact B preimage/C patch koşulları sağlanarak 422-row target manifest `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634` materialize edilmesini onaylıyorum. Ardından tek integration owner, `BASELINE-PATCH-TARGET.tsv` içindeki 18 preimage’in tamamı eşleşirse combined Carrier integration patch SHA256 `3fdc9188f635a2428e3ffc5c4ef07c9c4b747b2f74a4196d8e6207cd85ccf1d5` dosyasını bir kez uygulayabilir. Alt patch’ler Gateway `0ecc88eb0783088e008d5f8cddbda50482e4e9dfd57277f302a3a5a7621ebee4`, module registration `18001827f4156942028851a55f912b81f7a2cdc2b887fce8d434310bda5168cc` ve navigation localization `e752b46e904d934bc3dc71c50fe0006a4febf603b5cdf656821ac678639aa8bf` olarak sabittir. Preimage uyuşmazlığı overwrite yetkisi değildir. Bu karar 21 UI-owned dosyanın geliştirmesini, yeni backend endpoint/permission, layout değişikliği, rollout, E5/G5, commit/push/stash yetkisini kapsamaz.

## UI dispatch release after A+B evidence

Only after both decisions are executed and their writer-complete hashes exist may CT release `UI-DEV-v2.0-HELD.md` as an active prompt. Independent `UI-VER-v2.0-HELD.md` remains blocked until the UI writer completes.
