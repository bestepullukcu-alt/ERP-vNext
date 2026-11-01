# TEST-HIZ-01 · yönlendirme portu: §5 sayımının düzeltmesi (2026-10-26)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). `2026-10-26-test-hiz-01-yonlendirme-portu.md` commit'lendi
> ve değiştirilmez; bu kayıt onun §5'indeki sayıyı düzeltir.

**Yanlış sayı:** önceki kayıt "19 dosyada daha" diyordu. Liste `head -20` ile kesilmişti; sayı yanlış.

**Ölçüm** (`test/test-hiz-01-redirect-port` dalında, bu düzeltmeden sonra):

```
git grep -lE "TcpListener\(IPAddress\.(Loopback|Any), 0\)" -- '*.cs' ':!docs/**'
```

- **24 dosya**; origin/main'de 25. Fark, bu işte düzeltilen `AuthInternalClientsRedirectTests.cs`.
- Biri, `frontend/Diten.Web.Tests/Services/PlatformInternalClientsRedirectTests.cs`, bu işteki hatanın aynısını taşıyor: `HttpListener`, `Stop()` ardından `Close()`. Başka modül olduğu için dokunulmadı (XMC-001); ayrı karar.
- Kalanların çoğu mongod portu için boş port bulup bırakıyor. Mongod'un kendisi bağlanıyor; aynı tür pencere var. Ayrı tarama.
