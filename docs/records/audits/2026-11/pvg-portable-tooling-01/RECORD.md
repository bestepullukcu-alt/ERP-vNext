# PVG — portable build/test araç hazırlığı

Tarih 2026-11-20. Kullanıcının onayladığı rev20 build/test hazırlığı kapsamında ayrı devops/test-environment ajanı çalıştı. Repo/global ayar değişikliği yok.

Kök: C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120.
.NET SDK8.0.428/runtime8.0.34 ve Node22.23.5/npm10.9.9, resmi üretici metadata/zip/checksum kaynaklarından indirildi. .NET SHA512 ve Node SHA256 doğrulamaları gerçek arşiv baytlarıyla eşleşti.

Manifest: tools-manifest.json; SHA256 5553836a3c21b794ef9d008047d554023ea0eecc4e182d7fcde7afc7935322d4. CT bu manifest hash'ini ayrıca doğruladı. Tam URL/hash/executable ve process-env değerleri manifesttedir.

Smokelar: dotnet --info/--list-sdks, node --version, node npm-cli.js --version; tümü çıkış0. Repo global.json/.nvmrc/.node-version pini yok, CI8.0.x/22 ile uyumlu. Global install/registry/profile/PATH değişmedi. Worker yalnız process DOTNET_ROOT/PATH/CLI-home/cache kullanır. Başlangıçta yaklaşık179GB boş disk vardı.

Build, restore, npm-ci, ürün/mimari test, Mongo veya CI bu ortam işiyle çalıştırılmadı. Ortamın hazır olması ürün dispatch kapısının yerine geçmez.
Ajan /root/pvg_dl12_readiness, devralınan model/effort görünmüyor; farklı bir model kullanıldığı iddia edilmez.
