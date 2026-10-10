# Q335 — Recipe (regenerate, do not guess — K7)

Same shape as `../mvp6-q266-testenv-01/RECIPE.md`; differences: port 57335, set `rsq335`, and the scratch copy also
carries the two repository files the Shipments tests read through `RepositoryFile.Locate` (the contract and the
MOD-0183 pack), which Q266's copy did not need.

```bash
# 0. preflight
nc -z localhost 57335 && echo "57335 OPEN — STOP" || echo "57335 free"
df -g / | tail -1                                   # refuse below 2.5 GiB free

# 1. new timestamped folder outside the repo (no rm: a reset is a new folder)
E=~/mvp6-env/q335-$(TZ=Europe/Istanbul date +%Y%m%d-%H%M)       # this run: /Users/natig/mvp6-env/q335-20261003-1839
mkdir "$E" && mkdir -p "$E/db" "$E/log" "$E/src/services" "$E/results"

# 2. mongod: single-node replica set, test commands on
mongod --port 57335 --bind_ip 127.0.0.1 --replSet rsq335 --dbpath "$E/db" --logpath "$E/log/mongod.log" \
  --pidfilepath "$E/mongod.pid" --setParameter enableTestCommands=1 --wiredTigerCacheSizeGB 0.5 --fork
mongosh --quiet --port 57335 --eval 'rs.initiate({_id:"rsq335",members:[{_id:0,host:"127.0.0.1:57335"}]})'
mongosh --quiet --port 57335 --eval 'const s=rs.status(); printjson({set:s.set, members:s.members.length, state:s.members[0].stateStr})'
mongosh --quiet --port 57335 --eval "printjson(db.adminCommand({configureFailPoint:'failCommand', mode:'off'}))"

# 3. scratch copy, same relative layout as the repository
R=/Users/natig/Projects/ERP-vNext-recovery
cd $R/services && rsync -a --exclude bin/ --exclude obj/ --exclude TestResults/ Diten.SupplyChainService Diten.Building.Blocks "$E/src/services/"
mkdir -p "$E/src/docs/analysis/contracts" "$E/src/execution/domains/supply-chain-execution/module-packs"
cp $R/docs/analysis/contracts/shipment-bundle.openapi.yaml "$E/src/docs/analysis/contracts/"
cp $R/execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md "$E/src/execution/domains/supply-chain-execution/module-packs/"
cd "$E/src" && find . -type f | LC_ALL=C sort | xargs shasum -a 256 > "$E/copy-manifest.sha256"
cd $R && shasum -a 256 -c "$E/copy-manifest.sha256" | grep -vc ': OK$'      # 0 = copy equals the repo files

# 4. build once, then one module per run with only that module's variable (evidence/run-modules.sh)
"$E/run-modules.sh"

# 5. tear down (no rm; the data folder stays)
mongosh --quiet --port 57335 --eval 'db.adminCommand({shutdown:1})'
nc -z localhost 57335 && echo "57335 STILL OPEN" || echo "57335 free"
```

Gate rule (unchanged from Q266): green when every module except Claims shows 0 failed, and Claims shows exactly the
restart-mode test failing with "Explicit write/read restart mode required; exclude from ordinary suite."
Run time this time: 315 s for the seven modules, plus a 20 s build.
