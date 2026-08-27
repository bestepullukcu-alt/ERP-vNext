# Management & Governance Module Packs

Henüz module pack yoktur. Bu klasörün varlığı implementation authority vermez.

İlk olası, birbirinden ayrı pack'ler:

- `MOD-0354` — DWS Wave 1 Structural Core
- `MOD-0355` — BPM Process Model/Version Core

Her pack `draft` ile başlar. İnsan onayıyla `approved` / `ready-for-dev` olmadan production code veya
`Diten.ManagementGovernanceService` scaffold'u oluşturulamaz.

Pack'ler:

- DCP-006 Gate 2'nin dört korunan tehlikesini ve pure-structural-dependency ayrımını korur;
- MOD-0024 task, MOD-0023 workflow/approval, MOD-0031 evidence ve MOD-0018 effective-permission
  sahipliklerini yeniden uygulamaz;
- `Dws` ve `ProcessModeling` için ayrı architecture tests tanımlar;
- isolation testleri mekanik güvence veremezse mandatory split fallback'i bloklayıcı olarak uygular.
