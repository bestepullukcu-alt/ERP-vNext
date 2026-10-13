#!/bin/bash
# Q366 suites: Platform, Auth, MDM, SupplyChain on <tree> (stack = fixed, prefix = pre-fix). One Mongo per module.
E=$(cd "$(dirname "$0")" && pwd); TREE=$1; O=$E/results/suites-$TREE; mkdir -p $O; M=/opt/homebrew/bin/mongod
t(){ local name=$1 sln=$2; shift 2; local s=$(date +%s); (cd $E/$TREE && env "$@" perl -e 'alarm 2400; exec @ARGV' dotnet test $sln -nodeReuse:false -p:UseSharedCompilation=false > $O/$name.log 2>&1); echo "== $TREE $name rc=$? $(( $(date +%s)-s ))s"; grep -E '^(Passed!|Failed!)' $O/$name.log | sed 's/ - .*(net8.0)//' ; grep -E '^\s+Failed [A-Za-z]' $O/$name.log | sed 's/^ *//' | cut -c1-200 | head -15; }
t platform services/Diten.Platform/Diten.Platform.sln DITEN_TEST_MONGOD=$M DITEN_PLATFORM_TEST_MONGO_URI='mongodb://127.0.0.1:57371/?replicaSet=rsq366p' PLATFORM_COMPOSITION_GUARD_MONGO_URI='mongodb://127.0.0.1:57366/?replicaSet=rsq366g'
t auth services/Diten.AuthService/Diten.AuthService.sln DITEN_ITEST_MONGOD_BIN_DIR=/opt/homebrew/bin AUTH_COMPOSITION_GUARD_MONGO_URI='mongodb://127.0.0.1:57369/?replicaSet=rsq366a'
t mdm services/Diten.MdmService/Diten.MdmService.sln MDM_TEST_MONGO='mongodb://127.0.0.1:57368/?replicaSet=rsq366m' MONGO_TEST_URI='mongodb://127.0.0.1:57368/?replicaSet=rsq366m' MDM_COMPOSITION_GUARD_MONGO_URI='mongodb://127.0.0.1:57368/?replicaSet=rsq366m'
echo "== $TREE done $(date +%T)"
