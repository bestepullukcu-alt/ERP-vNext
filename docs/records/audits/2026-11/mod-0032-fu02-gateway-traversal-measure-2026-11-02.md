# Ağ geçidi yol aşma ölçümü — 2026-11-02 (CT · Yetki)

Kaynak: kule dalı chore/ct/auth-ct-01 (origin/main 4099e3ad1 birleştirilmiş). Var olan test
gateway/Diten.ApiGateway.Tests/Mod0290Fu04ProductMasterTraversalTests.cs'e geçici InlineData eklendi, yalnız o test koşuldu
(`dotnet test gateway/Diten.ApiGateway.Tests --filter FullyQualifiedName~Mod0290Fu04ProductMasterTraversalTests`), sonra satırlar
geri alındı (git status temiz). Gerçek Kestrel + Ocelot, ocelot.json'daki gerçek /api/product-master/{everything} rotası.

| İstek | Ağ geçidi | Servise ulaşan yol |
|---|---|---|
| /api/product-master/%252e%252e/internal/v1/product-master/validate | 200 | /api/internal/v1/product-master/validate |
| /api/product-master/%252e%252e/%252e%252e/internal/x | 200 | /internal/x |
| /api/product-master/skus/%252e%252e/%252e%252e/%252e%252e/internal/x | 200 | /internal/x |
| /api/product-master/%252e%252e%252f%252e%252e%252finternal/x (var olan) | 200 | /api/product-master/..%2f..%2finternal/x (rota içinde) |
| /api/product-master/../../internal/... (var olan) | 404 | hiçbir şey |
| /api/product-master/%2e%2e/%2e%2e/internal/... (var olan) | 404 | hiçbir şey |

Sonuç: çift kodlanmış nokta-nokta kesimi (`%252e%252e/`) ağ geçidinde tek kez çözülüyor, aşağı akış Kestrel'i ikinci kez çözüp
normalleştiriyor; istek rotanın dışına, iç uç yoluna ulaşıyor. Yalnız bir rota (product-master) ölçüldü; ocelot.json'da 163
`{everything}` rotası var (Yönetici ölçümü), aynı davranış beklenir ama ölçülmedi.

Ölçülmeyen: iç uçlar X-Internal-Api-Key istiyor (Yönetici ölçümü), bu yüzden anahtarsız çağrı serviste reddedilir; ağ geçidinin
kiracı/yetki denetimlerinin ham yola bakıp bakmadığı ölçülmedi.
