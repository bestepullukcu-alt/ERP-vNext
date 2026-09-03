#!/usr/bin/env bash
#
# seed-closure-outcomes-dev.sh — DEV-ONLY test data for the work report's "how work ended" chart.
#
# ⚠⚠ THIS IS NOT A PRODUCTION SEED. It is never called from a service start-up, it is not registered
# anywhere, and nothing runs it but a person typing its name. It writes to a LOCAL DEVELOPMENT database and
# it says which one before it touches anything.
#
# WHY IT EXISTS. The report's outcome chart had nothing to draw: MEASURED 2026-09-04, of 178 tasks in the dev
# database, 23 completed and 18 cancelled, exactly ZERO carried a ClosureReasonCode — and neither of the two
# task types had a closure dictionary at all. The feature was correct and invisible, which is the hardest kind
# of thing to review.
#
# WHAT IT DOES
#   1. Gives both existing task types a closure outcome dictionary — five outcomes each, real ones for
#      deviation/incident management rather than invented words.
#   2. Distributes those codes across tasks that ALREADY closed, in a deliberately uneven spread so the chart
#      has a head and a tail rather than five equal slices.
#
# It changes only ClosureOutcomes on task types and ClosureReasonCode on already-closed tasks. It creates no
# task, closes nothing, and touches no lifecycle field.
#
# USAGE
#   scripts/seed-closure-outcomes-dev.sh            seed (asks first)
#   scripts/seed-closure-outcomes-dev.sh --undo     remove everything it wrote
#   scripts/seed-closure-outcomes-dev.sh --status   report only; change nothing
#   scripts/seed-closure-outcomes-dev.sh --yes      skip the confirmation (for a scripted re-run)
#
# IDEMPOTENT. Re-running replaces the dictionaries with the same five outcomes and re-derives the same
# distribution — a task's outcome is chosen from its own id, so the spread is stable across runs rather than
# reshuffling every time and making two screenshots disagree.
#
set -uo pipefail

MONGO_URI="${MONGO_URI:-mongodb://localhost:27017}"
DB_NAME="${DB_NAME:-diten_personalization_dev}"
MODE="seed"
ASSUME_YES="no"

for arg in "$@"; do
  case "$arg" in
    --undo)   MODE="undo" ;;
    --status) MODE="status" ;;
    --yes|-y) ASSUME_YES="yes" ;;
    -h|--help) sed -n '2,32p' "$0"; exit 0 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

command -v mongosh >/dev/null 2>&1 || { echo "mongosh not found on PATH." >&2; exit 1; }

# ── SAY WHERE, BEFORE TOUCHING ANYTHING ──────────────────────────────────────────────────────────────────
# A seed script that does not name its target is one keystroke from being run against the wrong database.
echo
echo "  ⚠ DEV-ONLY TEST DATA"
echo "  target : $MONGO_URI"
echo "  database: $DB_NAME"
echo "  mode   : $MODE"
echo

if [[ "$DB_NAME" != *_dev ]]; then
  # The only hard refusal in this script. A database whose name does not end in _dev is not one this is
  # allowed to write test rows into, whatever the operator typed.
  echo "  REFUSED: '$DB_NAME' does not look like a development database (expected a name ending in _dev)." >&2
  exit 1
fi

if [[ "$MODE" != "status" && "$ASSUME_YES" != "yes" ]]; then
  read -r -p "  Write test data to '$DB_NAME'? [y/N] " reply
  [[ "$reply" == "y" || "$reply" == "Y" ]] || { echo "  cancelled."; exit 0; }
  echo
fi

mongosh --quiet "$MONGO_URI/$DB_NAME" --eval "var MODE='$MODE';" --file /dev/stdin <<'JS'
/*
 * The five outcomes per type. REAL ones for deviation / incident management (the two seeded types are
 * DEV-QMS and DEV-GMP), not invented words: each is a disposition a quality team actually records, and each
 * carries the reason flag that disposition genuinely needs.
 *
 * Tenant-authored outcomes carry LabelText (one language, the administrator's own words) and no resource key
 * — the split TaskFieldDefinition established and TaskClosureOutcome repeats. These are tenant outcomes
 * because they are this organisation's vocabulary, not the product's five system ones.
 */
var OUTCOMES = [
  { Code: 'CORRECTED',        LabelText: 'Düzeltildi',                 Disposition: 'Completed', RequiresReason: false, SortOrder: 10 },
  { Code: 'NOT_RECURRING',    LabelText: 'Tekrar etmedi',              Disposition: 'Completed', RequiresReason: false, SortOrder: 20 },
  { Code: 'CAPA_RAISED',      LabelText: "CAPA'ya devredildi",         Disposition: 'Completed', RequiresReason: true,  SortOrder: 30 },
  { Code: 'OUT_OF_SCOPE',     LabelText: 'Kapsam dışı',                Disposition: 'Cancelled', RequiresReason: true,  SortOrder: 40 },
  { Code: 'DUPLICATE_REPORT', LabelText: 'Mükerrer bildirim',          Disposition: 'Cancelled', RequiresReason: true,  SortOrder: 50 }
];

var COMPLETED = OUTCOMES.filter(function (o) { return o.Disposition === 'Completed'; });
var CANCELLED = OUTCOMES.filter(function (o) { return o.Disposition === 'Cancelled'; });
var ALL_CODES = OUTCOMES.map(function (o) { return o.Code; });

function status() {
  print('  task types                : ' + db.task_types.countDocuments({}));
  print('  types with a dictionary   : ' + db.task_types.countDocuments({ 'ClosureOutcomes.0': { $exists: true } }));
  print('  tasks                     : ' + db.task_items.countDocuments({}));
  print('  completed                 : ' + db.task_items.countDocuments({ CompletedAt: { $ne: null } }));
  print('  cancelled                 : ' + db.task_items.countDocuments({ CancelledAt: { $ne: null } }));
  print('  with a closure outcome    : ' + db.task_items.countDocuments({ ClosureReasonCode: { $ne: null } }));
  print('');
  print('  outcome distribution:');
  db.task_items.aggregate([
    { $match: { ClosureReasonCode: { $ne: null } } },
    { $group: { _id: '$ClosureReasonCode', n: { $sum: 1 } } },
    { $sort: { n: -1 } }
  ]).forEach(function (d) { print('    ' + d._id + ' : ' + d.n); });
}

/*
 * WHICH OUTCOME A TASK GETS — derived from its OWN id, never at random.
 *
 * A random spread would reshuffle on every run, so two screenshots of "the same" data would disagree and
 * nobody could tell a seeding change from a code change. Hashing the id keeps the distribution stable AND
 * uneven: the weights below give the chart a head and a long tail, which is what makes it worth looking at.
 */
function hash(id) {
  var s = String(id), h = 0;
  for (var i = 0; i < s.length; i++) { h = (h * 31 + s.charCodeAt(i)) % 100000; }
  return h;
}

function pickCompleted(id) {
  var r = hash(id) % 100;                    // deliberately skewed: 60 / 30 / 10
  if (r < 60) { return 'CORRECTED'; }
  if (r < 90) { return 'NOT_RECURRING'; }
  return 'CAPA_RAISED';
}

function pickCancelled(id) {
  return (hash(id) % 100) < 70 ? 'OUT_OF_SCOPE' : 'DUPLICATE_REPORT';   // 70 / 30
}

if (MODE === 'status') {
  status();
} else if (MODE === 'undo') {
  var t = db.task_types.updateMany({}, { $set: { ClosureOutcomes: [] } });
  // ⚠ ONLY the codes THIS script writes. A blanket unset would erase outcomes recorded by a real closure.
  var i = db.task_items.updateMany(
    { ClosureReasonCode: { $in: ALL_CODES } },
    { $set: { ClosureReasonCode: null } });

  print('  dictionaries cleared on ' + t.modifiedCount + ' task type(s)');
  print('  outcome cleared on      ' + i.modifiedCount + ' task(s)');
  print('');
  status();
} else {
  db.task_types.find({}, { _id: 1, Code: 1 }).forEach(function (type) {
    db.task_types.updateOne({ _id: type._id }, { $set: { ClosureOutcomes: OUTCOMES } });
    print('  dictionary set on ' + type.Code + ' (' + OUTCOMES.length + ' outcomes)');
  });

  var completedTouched = 0;
  db.task_items.find({ CompletedAt: { $ne: null } }, { _id: 1 }).forEach(function (task) {
    db.task_items.updateOne({ _id: task._id }, { $set: { ClosureReasonCode: pickCompleted(task._id) } });
    completedTouched++;
  });

  var cancelledTouched = 0;
  db.task_items.find({ CancelledAt: { $ne: null } }, { _id: 1 }).forEach(function (task) {
    db.task_items.updateOne({ _id: task._id }, { $set: { ClosureReasonCode: pickCancelled(task._id) } });
    cancelledTouched++;
  });

  print('  outcome written on ' + completedTouched + ' completed and ' + cancelledTouched + ' cancelled task(s)');
  print('');
  status();
}
JS

echo
echo "  undo: scripts/seed-closure-outcomes-dev.sh --undo"
echo
