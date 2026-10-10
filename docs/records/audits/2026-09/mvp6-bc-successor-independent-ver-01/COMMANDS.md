# MVP6-BC-SUCCESSOR-INDEPENDENT-VER-01 — commands

The verification used only the durable writer archive extracted under
`/private/tmp/mvp6-bc-successor-ver-01/source`.

```text
sha256sum BC-SOURCE.tar.gz SOURCE-MANIFEST.tsv CHANGED-PRESERVED.tsv MANIFEST.tsv RAW-EVIDENCE.tar.gz
tar -xzf BC-SOURCE.tar.gz -C /private/tmp/mvp6-bc-successor-ver-01/source
python3 <422-entry SHA-256 verifier>

/Users/natig/.dotnet/dotnet restore \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  --disable-parallel

/Users/natig/.dotnet/dotnet build \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  -c Debug --no-restore -m:1 /nr:false

/opt/homebrew/bin/mongod --port 57292 \
  --dbpath /private/tmp/mvp6-bc-successor-ver-01/mongo \
  --bind_ip 127.0.0.1 --replSet rsmod192 \
  --setParameter enableTestCommands=1

mongosh --quiet --port 57292 --eval \
  'rs.initiate({_id:"rsmod192",members:[{_id:0,host:"127.0.0.1:57292"}]})'

/Users/natig/.dotnet/dotnet test \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  -c Debug --no-build --no-restore \
  --filter FullyQualifiedName~CapacityPlans \
  --logger 'trx;LogFileName=capacity-ver.trx'

python3 /private/tmp/mvp6-bc-successor-ver-01/private-http_restart_probe.py
```

The Capacity tests contain a fixed `57192` seed. A verifier-owned TCP forward
listened on that otherwise empty compatibility port and forwarded only to the
verifier-owned replica set on `57292`. No writer Mongo process, writer API
process or writer database was reused.

The first no-restore build failed because the archive intentionally carried no
`obj/project.assets.json`; it is preserved in `raw/build.log`. Restore followed
by the native .NET 8 build passed. The first HTTP evidence run used a malformed
mongosh database URI and queried `test`; it failed its persistence assertion and
is preserved as `raw/http-probe-first-failed.*`. Only the corrected run is
controlling.
