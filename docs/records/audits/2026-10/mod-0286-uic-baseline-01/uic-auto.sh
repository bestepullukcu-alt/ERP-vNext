#!/bin/sh
# uic-auto.sh - MOD-0286 DL-2 run contract, RC-1..RC-24 in one run (pack revision 06, sha256 7f6868d9).
# Working directory: the repo root. Run through the recorder (evidence flow step 2):
#   python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01 -- sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-auto.sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01
# Each block prints "RC-n", the block's stdout, then "exit=<status of the block's last command>". No `set -e`:
# acceptance is read per RC row from stdout against the pack's expected column; the run's overall exit is recorded, not judged.
# The script writes only under "$OUT": RC-11 navkeys.out; RC-22 resx-tenant.out, resx-platform.out; RC-23 steps/titles/seqdiff/titlediff-<lang>.txt;
# RC-24 keys/kv-<lang>.txt, missing/equal/new-missing/new-equal/closed-missing/closed-equal-<lang>.txt. RC-24 reads the committed baseline "$B" and never writes there.
set -u
OUT="$1"
mkdir -p "$OUT"
G=frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim
J=frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js
R=frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim
T=frontend/Diten.Web/Views/Platform/Tenants
B=docs/records/audits/2026-10/mod-0286-uic-baseline-01/resx-baseline
TAB="$(printf '\t')"
LC_ALL=C; export LC_ALL

echo "RC-1"
grep -L 'Layout = "' $G/Index.cshtml; grep -L 'Layout = "' $T/Index.cshtml $T/Create.cshtml $T/Details.cshtml $T/Security.cshtml
echo "exit=$?"

echo "RC-2"
grep -c 'backbone-skeleton\|skeleton-loader' $G/Index.cshtml; grep -c 'spinner-border' $G/Index.cshtml
echo "exit=$?"

echo "RC-3"
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format report
echo "exit=$?"

echo "RC-4"
grep -c 'createCrudTable(' $J; grep -c 'createList(' $J
echo "exit=$?"

echo "RC-5"
grep -c 'GetAllStrings(true)' $G/_IndexL10n.cshtml; grep -c 'GetAllStrings(true)' frontend/Diten.Web/Views/WorkCenterNext/_L10n.cshtml
echo "exit=$?"

echo "RC-6"
for l in en tr fr es zh ar ru; do test -f $R/GoldenReferenceSlimIndex.$l.resx && echo "$l ok" || echo "$l MISSING"; grep -c 'name="\(GoldenReferenceSlimTitle\|PageDescription\|Code\|Name\|ReferenceType\|Priority\)"' $R/GoldenReferenceSlimIndex.$l.resx; grep -c 'name="AddNewGoldenReferenceSlim"' $R/GoldenReferenceSlimIndex.$l.resx; grep -c 'name="\(Search\|Export\|Import\|Filter\|Apply\|Reset\|ShowAll\|SaveView\|ColumnVisibility\|Status\|Cancel\|AreYouSure\)"' frontend/Diten.Web/Resources/SharedResource.$l.resx; done; test -f $G/GoldenReferenceSlimIndex.cs && echo marker ok; for l in en tr; do test -f frontend/Diten.Web/Resources/Views/Platform/Tenants/TenantsIndex.$l.resx && echo "$l ok"; done
echo "exit=$?"

echo "RC-7"
grep -l 'style="' $G/Index.cshtml $G/_Filter.cshtml $G/_DataTable.cshtml $G/_IndexL10n.cshtml $G/_CreateEditOffcanvas.cshtml $G/_DetailsQuickView.cshtml; grep -c '\.style\.' $J
echo "exit=$?"

echo "RC-8"
grep -n 'alert(\|Swal\.fire\|toastr\.' $J; grep -c 'window.showConfirm' $J; grep -c 'buttonsStyling: false' frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml
echo "exit=$?"

echo "RC-9"
grep -c '"url": "/Platform/Tenants"' frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json
echo "exit=$?"

echo "RC-10"
grep -n 'document.cookie\|Bearer\|access_token' $J
echo "exit=$?"

echo "RC-11"
python3 scripts/check_pack_nav_keys.py > $OUT/navkeys.out 2>&1; echo "rc=$?"; grep -c 'could not read' $OUT/navkeys.out; grep -c 'MOD-0286' $OUT/navkeys.out; git ls-files -- 'execution/domains/*/module-packs/MOD-0286-*.md' | wc -l
echo "exit=$?"

echo "RC-12"
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --data-mode client --api-profile direct-gateway --format gaps
echo "exit=$?"

echo "RC-13"
grep -c 'savedView' $J; grep -c 'api/personalization' $J; grep -c 'dt-save-filter-btn' frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js
echo "exit=$?"

echo "RC-14"
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format gaps
echo "exit=$?"

echo "RC-15"
grep -c 'form-control\|form-select\|form-check-input' $G/_CreateEditOffcanvas.cshtml; grep -n '^form_field_count' execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md
echo "exit=$?"

echo "RC-16"
grep -c 'diten-field' $G/_CreateEditOffcanvas.cshtml; grep -c 'aria-hidden="true"' $G/_CreateEditOffcanvas.cshtml
echo "exit=$?"

echo "RC-17"
grep -c 'id="inlineFilterCollapse"' $G/_Filter.cshtml; grep -c "collapseId: 'inlineFilterCollapse'" $J; sed -n 1046p frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js; grep -c 'aria-label' frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js
echo "exit=$?"

echo "RC-18"
grep -n 'data-dt-primary=' $G/_CreateEditOffcanvas.cshtml $G/_DetailsQuickView.cshtml
echo "exit=$?"

echo "RC-19"
grep -c 'exportColumns' $J; grep -c "mode: 'server'" $J
echo "exit=$?"

echo "RC-20"
grep -c 'colReorder' $J
echo "exit=$?"

echo "RC-21"
for l in en tr fr es zh ar ru; do f=frontend/Diten.Web/Resources/PageHelpResource.$l.resx; grep -c 'name="Help\.USERS\.\(Purpose\|Audience\)"' $f; done
echo "exit=$?"

echo "RC-22"
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant > $OUT/resx-tenant.out 2>&1; echo "rc=$?"; grep -c 'should be Title Case' $OUT/resx-tenant.out; grep -c 'still equal to English' $OUT/resx-tenant.out; grep -c 'Missing' $OUT/resx-tenant.out; python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context platform > $OUT/resx-platform.out 2>&1; echo "rc=$?"; grep -c 'should be Title Case' $OUT/resx-platform.out; grep -c 'still equal to English' $OUT/resx-platform.out; grep -c 'Missing' $OUT/resx-platform.out
echo "exit=$?"

echo "RC-23"
C=frontend/Diten.Web/Services/Help/PageHelpCatalog.cs; grep -c '\["USERS"\] = \[new(' $C; grep '\["USERS"\] = \[new(' $C; CODES=$(grep '\["USERS"\] = \[new(' $C | grep -o 'new("[A-Z0-9_-]*"' | sed 's/new("//;s/"$//'); echo "codes=$(echo $CODES)"; for l in en tr fr es zh ar ru; do f=frontend/Diten.Web/Resources/PageHelpResource.$l.resx; for c in $CODES; do printf "%s %s=" $l $c; grep -c "name=\"Help\.USERS\.Related\.$c\"" $f; done; printf "%s related_total=" $l; grep -c 'name="Help\.USERS\.Related\.' $f; grep -o 'name="Help\.USERS\.Step[0-9]*"' $f | grep -o '[0-9]*' | sort -n -u > $OUT/steps-$l.txt; grep -o 'name="Help\.USERS\.Step[0-9]*Title"' $f | grep -o '[0-9]*' | sort -n -u > $OUT/titles-$l.txt; k=$(wc -l < $OUT/steps-$l.txt | tr -d ' '); printf "%s k=%s range_ok=" $l $k; [ $k -ge 1 ] && [ $k -le 5 ] && echo yes || echo no; printf "%s seq_diff=" $l; seq 1 $k | diff - $OUT/steps-$l.txt > $OUT/seqdiff-$l.txt && echo empty || { echo NONEMPTY; cat $OUT/seqdiff-$l.txt; }; printf "%s title_diff=" $l; diff $OUT/steps-$l.txt $OUT/titles-$l.txt > $OUT/titlediff-$l.txt && echo empty || { echo NONEMPTY; cat $OUT/titlediff-$l.txt; }; done
echo "exit=$?"

echo "RC-24"
for l in en tr fr es zh ar ru; do f=frontend/Diten.Web/Resources/SharedResource.$l.resx; grep -o '<data name="[^"]*"' $f | sed 's/<data name="//;s/"$//' | sort -u > $OUT/keys-$l.txt; awk '/<data name="/{match($0,/name="[^"]*"/); k=substr($0,RSTART+6,RLENGTH-7); if ($0 !~ /<value>/) getline; if (match($0,/<value>.*<\/value>/)) v=substr($0,RSTART+7,RLENGTH-15); else v=""; print k "\t" v}' $f | sort -t "$TAB" -k1,1 > $OUT/kv-$l.txt; done; for l in tr fr es zh ar ru; do comm -23 $OUT/keys-en.txt $OUT/keys-$l.txt > $OUT/missing-$l.txt; join -t "$TAB" -j 1 $OUT/kv-en.txt $OUT/kv-$l.txt | awk -F'\t' '$2==$3 && $2!="" {print $1}' > $OUT/equal-$l.txt; comm -13 $B/missing-$l.txt $OUT/missing-$l.txt > $OUT/new-missing-$l.txt; comm -13 $B/equal-$l.txt $OUT/equal-$l.txt > $OUT/new-equal-$l.txt; comm -23 $B/missing-$l.txt $OUT/missing-$l.txt > $OUT/closed-missing-$l.txt; comm -23 $B/equal-$l.txt $OUT/equal-$l.txt > $OUT/closed-equal-$l.txt; printf "%s missing=%s equal=%s new_missing=%s new_equal=%s closed_missing=%s closed_equal=%s\n" $l $(wc -l < $OUT/missing-$l.txt) $(wc -l < $OUT/equal-$l.txt) $(wc -l < $OUT/new-missing-$l.txt) $(wc -l < $OUT/new-equal-$l.txt) $(wc -l < $OUT/closed-missing-$l.txt) $(wc -l < $OUT/closed-equal-$l.txt); done
echo "exit=$?"
