---
status: approved
decided: 2026-10-27
module: MOD-0230
---

# PVG-MOD0230-MIMARI-01 — "Kurumsal mimari" kararlarının sahibi

> **Karar sahibi:** sahip. Kararın sözü "mimari-ben" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Durum

MOD-0230 paketi bazı kararları "Enterprise Architecture" rolüne bırakıyor:
- OD-PVG-2: servisle Gateway arasındaki kiracı ve aktör sözleşmesi.
- OD-PVG-6: runtime bayrağının onay satırına nasıl bağlanacağı.
- OD-PVG-7: port tahsisi.
- Operasyonel runtime yetkisinin bir parçası.

Depoda bu rolün kim olduğu yazmıyor: `execution/approvals/approvers.tsv` ve `execution/registries/module-id-registry.md` bu rolü bir kişiye ya da role bağlamıyor. Geçmişteki port kararlarını sahip vermiş. Ölçüm `origin/main` @ f85bd954b üzerinde yapıldı.

## Karar

1. MOD-0230 için "Enterprise Architecture" kararlarını sahip verir. Paketteki OD-PVG-2, OD-PVG-6 ve OD-PVG-7'nin bu rolden beklenen kısmı sahibe sorulur; dışarıya mesaj gitmez.
2. Her karar ayrı ayrı sorulur. Her biri ayrı bir karar kaydına sahibin kendi sözüyle yazılır. Bu kayıt tek başına hiçbir OD satırını onaylamaz.
3. Ayrıca bilgi olarak: sahip, başka bir Control Tower sohbetinde MOD-0018 / Auth sahibinin kendisi olduğunu söyledi ("auth-ben", 2026-10-27). Bu bilgi PVG kayıtlarına ancak sahip PVG sohbetinde aynı kararı kendi sözüyle verdiğinde girer: OD-PVG-1 ve OD-PVG-2'nin claim adları kısmı.

## Gerileme değerlendirmesi

Kod ya da kuyruk değişmiyor; bu yalnız bir sahiplik kaydı. Kararlar dış bir role beklemeden sahibe sorulabilir hâle geliyor.
