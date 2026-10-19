# CT-APR-01 — sürüme bağlı paket onayının başlangıç kaydı

Bu dosyanın main'e girdiği commit, kuralın başladığı yerdir (sahip kararı D5, 2026-10-19). Tarih değil commit sırası
ölçülür.

- **Kapsam:** bu commit'ten sonra değişen bir Module Pack, dağıtımdan önce (`scripts/prompt_gate.py check`)
  `execution/approvals/approvals.tsv`'de geçerli bir onay satırı ister (`scripts/pack_approval.py check`).
- **Eski onaylar:** bu commit'ten beri değişmemiş ve yeni kurala göre onay satırı almamış paketlerin onayı geçerli kalır;
  geriye dönük düşürülmez.
- **Onay dosyası:** yalnız satır eklenir; iptal `action: revoke` satırıyla yapılır.
- **Onaylayıcılar:** `execution/approvals/approvers.tsv`'de: hesap, kişi anahtarı (`K1`…; aynı kişinin hesapları aynı
  anahtarı taşır), rol, yüksek risk yetkisi. Depo herkese açık olduğu için gerçek ad yazılmaz. Yüksek riskli işi, onay
  satırı main'e girmeden **önceki** listede `high: evet` olan ve paketi yazan kişi olmayan biri onaylar; junior
  onaylayamaz. Listeye aynı PR'da eklenen hesap sayılmaz.
- **Güven sınırı (ilk sürüm):** araç beyanların tutarlılığını denetler; kimin yazdığını doğrulamaz. İnsan adımı main
  çizgisinden git ile türetilir: yüksek riskli onay satırı main'e, satırı ekleyenden farklı birinin merge ettiği bir
  merge commit'iyle gelir. Bu yüzden PR'lar "merge commit" ile birleştirilmelidir (squash ya da fast-forward'da insan
  adımı görülmez ve yüksek riskli onay geçmez).
- **GitHub katmanı (D6):** `.github/CODEOWNERS` 2026-10-19'da main'e girdi (PR #153). Onay dosyaları, onaylayıcı
  listesi, kontrol araçları, CI tanımları ve CODEOWNERS'ın kendisi yalnız CEO ya da kıdemli geliştirici incelemesiyle
  değişir; bu, depo ayarlarında "Require review from Code Owners" açıkken zorunludur.
- **Bu dosya silinemez:** eklendikten sonra yokluğu kuralı kapatmaz; onay geçmez.

Sözleşme: `docs/roadmap/plans/ct-dev-readiness-approval-01/CONTRACT.md` §3.
