#!/usr/bin/env bash
# TEST-HIZ-01 — the local fast run of ONE test project: [Trait("Category", "Mongo")] and [Trait("Category", "Slow")]
# are left out; CI (service-tests.yml) still runs every test. Guide: docs/guides/operations/local-fast-tests.md.
#
# Every Mongo path the tests resolve is pointed at scripts/test-env/refuse-mongod/mongod, which exits 97, and a given
# lane URI is removed — so a test that needs Mongo but carries no Mongo trait goes RED here ("exited with code 97")
# instead of quietly starting a mongod. Measured 2026-10-23 on the pilot project: 0 mongod processes started.
#
#   scripts/test-env/fast-local-tests.sh <test .csproj> [extra filter] [extra dotnet test arguments...]
#   scripts/test-env/fast-local-tests.sh services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj
#   scripts/test-env/fast-local-tests.sh services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj "FullyQualifiedName~.Tasks."
set -euo pipefail

# Only projects whose Mongo tests are tagged (the pilot). Any other project would run its untagged Mongo tests against
# the refusing mongod and be red for the wrong reason.
TAGGED_PROJECTS=(
  services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj
  services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj
)

if [ $# -lt 1 ]; then
  sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'
  exit 2
fi

repo="$(cd "$(dirname "$0")/../.." && pwd)"
project="$1"
shift
known=""
for tagged in "${TAGGED_PROJECTS[@]}"; do
  if [ "$project" = "$tagged" ]; then known=1; fi
done
if [ -z "$known" ]; then
  echo "fast-local-tests: $project has no Mongo / Slow tags yet (TEST-HIZ-01 pilot). Tagged: ${TAGGED_PROJECTS[*]}" >&2
  exit 2
fi

filter="Category!=Mongo&Category!=Slow"
if [ $# -gt 0 ] && [ "${1#-}" = "$1" ]; then
  filter="($filter)&($1)"
  shift
fi

refuse="$repo/scripts/test-env/refuse-mongod"
set +e
env -u DITEN_PLATFORM_TEST_MONGO_URI \
  DITEN_TEST_MONGOD="$refuse/mongod" \
  DITEN_FU02_MONGOD_PATH="$refuse/mongod" \
  DITEN_ITEST_MONGOD_BIN_DIR="$refuse" \
  dotnet test "$repo/$project" --filter "$filter" "$@"
code=$?
set -e
if [ $code -ne 0 ]; then
  echo "fast-local-tests: red. A test that failed with 'exited with code 97' needs Mongo: tag its class" \
    '[Trait("Category", "Mongo")].' >&2
fi
exit $code
