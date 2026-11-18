# MVP-2 / MVP-3 devri — 2026-11-17

Devreden: Cihan · Ölçüm: origin/main `76466f413`. Tarihli kayıttır; ana dalda düzeltilmez.

| Dosya | İçerik |
|---|---|
| [MVP-2-DEVIR.md](MVP-2-DEVIR.md) | Procurement modülleri, MOD-0140 DL-3'ün durumu, CT kuyruğu, sözler, kararlar |
| [MVP-3-DEVIR.md](MVP-3-DEVIR.md) | Manufacturing modülleri, CT kuyruğu, feragatler |

## 1. İşin nerede olduğu

Ekim–Kasım işleri bir bulut oturumunda yapıldı. Commit yazarı `Claude`; push ve PR'lar `bestepullukcu-alt` hesabıyla.
Bu yüzden yazarı "Cihan" olan son commit main'de 2026-09-23 tarihli (`780f8368a`) görünüyor ve `cihancalli` adına
açık PR yok. İş aynı depoda, aşağıdaki dallardadır.

| Dal | Uç | PR | Karar |
|---|---|---|---|
| `docs/mod0140/rev9-3` | `361623752` | yok — **Cihan açacak** (S-115 F-5: PR'ı satırların yazarı açar) | sakla |
| `feature/mvp2/supplier-dl3-commands` | `d2476927c` | yok — Rev 9.3 main'de `check`'ten geçtikten sonra açılır | sakla |
| `fix/mod-0143-invoice-error-l10n` | `a8fab59ae` | #278, açık, inceleme bekliyor | sakla |
| `docs/mvp2-mvp3/handoff-2026-11-17` | bu kayıt | bu kaydın PR'ı | sakla |
| `hr-future` | `d2ad629d8` (yazar Cihan, 2026-09-14) | yok | **silme** (§3) |

## 2. Oturumun yerel durumu (2026-11-17)

| Komut | Sonuç |
|---|---|
| `git fetch origin --prune` | hata yok |
| `git log --branches --not --remotes --oneline` | **boş**: push edilmemiş commit yok |
| `git stash list` | **boş** |
| her worktree'de `git status --short` | `docs/mod0140/rev9-3`, DL-3, #278 ve ana klasör **temiz**; ayrık (detached) bir yardımcı worktree'de yalnız `scripts/__pycache__/` var |

Yerel dallar: `docs/mod0140/rev9-3`, `feature/mvp2/supplier-dl3-commands` ve `fix/mod-0143-invoice-error-l10n`
uzaktakiyle aynı uçta. Oturumun çalışma dalı uzaktaki geçmişte zaten var; yerel `main` geride. Hiçbirinde saklanacak
ek iş yok, yerel kopyalar **sil**.

Depo dışında kalanlar: oturumun ürettiği durum raporu ve diğer ekiplere yazılan yanıt taslakları. İçerikleri bu iki
nota aktarıldı; ayrıca saklanacak dosya yok.

## 3. `hr-future` — silinmemeli

Mesajdaki "içeriği zaten main'de" tespiti ölçümle doğrulanmadı:

- `git rev-list --count origin/main..origin/hr-future` → **68** commit main'de yok.
- `git cherry origin/main origin/hr-future` → 68 commit'in **hiçbirinin** main'de eşdeğeri yok.
- Ortak atadan bu yana dalın değiştirdiği dosyalar main ile karşılaştırıldı:
  - 1.118 dosya yalnız bu dalda var (en çok `services/Diten.Platform`, `frontend/Diten.Web`, `services/Diten.DataKnowledgeService`);
  - 663 dosya main'dekinden farklı.
- Commit'ler arasında mobil (Android) uygulamanın ayrı depoya çıkarılması, M1 mobil dalgaları ve TalentEcosystem
  LE kapsamı gibi işler var.

Dal bu oturumdan önce (2026-09-14) yazıldı; içeriğinin hangi parçasının hâlâ gerekli olduğuna sahibi karar vermeli.
O karara kadar silinmemesi önerilir.
