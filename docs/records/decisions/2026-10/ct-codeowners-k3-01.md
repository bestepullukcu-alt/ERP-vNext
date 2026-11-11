---
decision_id: CT-CODEOWNERS-K3-01
status: decided — yürürlüğe girmesi bu kaydı taşıyan PR'ın bir kod sahibi (CEO ya da kıdemli geliştirici) tarafından onaylanıp merge edilmesine bağlıdır
decided_at_local: 2026-10-27
decided_by: sahip (GMG-ERP, K3); bu kayıt ad yazmaz
decision_source: CT · Setup oturumu (Claude Code), sahibin yanıtı "codeowner-ekle"
scope: `.github/CODEOWNERS` listesine GMG-ERP eklenir; D6 (2026-10-19) kuralı "yalnız CEO ve kıdemli geliştirici" bu ölçüde genişler
bound_to: dal `chore/agents/codeowners-k3-01`, taban origin/main bc687b70187be23427478830f3d16614a8a033c3
recorded_by: CT · Setup; bu kayıt kendi adına karar vermez, onay ya da merge izni değildir
---

# Kod sahibi listesine GMG-ERP (K3) eklenir

## Sorun

`.github/CODEOWNERS` listesinde üç hesap var: `gmg-10` (CEO, K1), `Bestepul` ve `bestepullukcu-alt` (ikisi de K2,
`execution/approvals/approvers.tsv`). Kimse kendi PR'ını onaylayamadığı için K2'nin onay dosyalarına, kontrol araçlarına ya
da CI tanımlarına dokunan PR'ı bugün yalnız K1 onaylayabiliyor; K1 yokken iş duruyor (2026-10-27, sahibe bildirildi).

## Karar

GMG-ERP (K3) listedeki her satıra eklenir. Dört göz değişmez: yazar kendi PR'ını onaylamaz; ekleme yalnız onay
verebilecek kişi sayısını artırır.

## Bilinen risk (ayrı konu, bu kararla çözülmez)

K2'nin listede iki hesabı var (`Bestepul`, `bestepullukcu-alt`). Bir hesapla açılan PR'ın öteki hesapla onaylanması
teknik olarak mümkündür; `approvers.tsv` ikisini aynı kişiye (K2) bağlar ama GitHub bunu bilmez. Önerilen çözüm ayrı bir
karar ister (ör. tek hesap bırakmak).

## İnceleyene not

Bu PR'ı açan hesap GMG-ERP'dir ve PR GMG-ERP'yi listeye ekler. GMG-ERP'nin `approvers.tsv`'deki rolü "geliştirici"dir
(K3), kıdemli değil; bu ekleme rol değişikliği değil, kod sahibi listesinde bir istisnadır. GitHub kod sahibi kuralını
taban daldaki (main) CODEOWNERS'tan okur; bu yüzden bu PR'ın onayı zaten bugünkü kod sahiplerinden (CEO ya da K2) gelmek
zorundadır. GMG-ERP'nin depoda yazma yetkisi var (ölçüldü 2026-10-27, `gh api …/collaborators/GMG-ERP/permission` → write).
