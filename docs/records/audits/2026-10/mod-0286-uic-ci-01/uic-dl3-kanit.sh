# MOD-0286 PSS-UI-P1-04 (DL-3) kanıt betiği, sürüm 01 — Control Tower yazdı; iş paketi bu dosyayı değiştirmez.
# Neden: scripts/acceptance_record.py bir AC'yi yalnız TEK kayıtlı koşunun artefaktıyla "geçti" sayar (:492-494,
# :520-528) ve ham çıkış 0 ister (:632-634). DL-3'ün AC-17 (ara + nihai), AC-18 (13 sabotaj koşusu), AC-19 (taban) ve
# AC-20 (süre) kanıtları bu yüzden tek koşunun içinde üretilir.
# Kullanım (depo kökünden): sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit.sh <çıktı-dizini> <iş-commit> [<ci-koşu-kimliği>]
# Kayıtlı koşu: python3 scripts/run_record.py run --out <dizin> -- sh <bu betik> <dizin> <iş-commit> [<ci-koşu-kimliği>]
# Yalnız POSIX sh. Depoya yalnız <çıktı-dizini> altına yazar; sabotaj commit'leri depo DIŞINDAKİ geçici bir klonda
# yapılır (mktemp -d; run_record.py :137-139 depo dışını görmez, artefakt da saymaz) ve çıktıları <çıktı-dizini>'ne kopyalanır.
# Çıkış 0 yalnız ZORUNLU listedeki her kimlik tam bir kez "geçti" ise; aksi 1; kullanım hatası 2.
# <ci-koşu-kimliği> verilmezse AC-17 (nihai, CI koşusu) "kaldı" olur: workflow (CT-Setup + sahip onayı) gelmeden DL-3
# kabul edilmez (paket AC-17 bölüm 2). İş paketinin kendi koşusu bu yüzden ham çıkış 1 ile biter; bu beklenen durumdur.
OUT="$1"; IS="$2"; CIRUN="$3"
if [ -z "$OUT" ] || [ ! -d "$OUT" ] || [ -z "$IS" ]; then echo "kullanım: sh uic-dl3-kanit.sh <var olan çıktı-dizini> <iş-commit> [<ci-koşu-kimliği>]"; exit 2; fi
LC_ALL=C; export LC_ALL
ROOT=$(pwd)
SEL=scripts/uic_changed_pages.py
BL=tests/ui-compliance-baseline.tsv
PK=execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
PK_SHA=ce134e13b49751c4ba10bf0aefaaf563c5bf431b1697321e1f12212e87e00742
BASE=f12c6a347d5f1e4e3220a775747a173f9ba1cf21
WF=.github/workflows/ui-compliance.yml
ZORUNLU="AC-17a AC-17b AC-18a AC-18b1 AC-18b2 AC-18c AC-18d AC-18e AC-19 AC-20 AC-11 CT-2 CT-3"
R="$OUT/sonuc.txt"
: > "$R"
ID="-c user.name=uic-kanit -c user.email=uic-kanit@localhost -c commit.gpgsign=false"

sonuc() { if [ "$3" -eq 0 ] 2>/dev/null; then s=geçti; else s=kaldı; fi; echo "$1 sonuç=$s ($2)"; echo "$1 $2 $s" >> "$R"; }
esit() { if [ "$2" = "$3" ]; then echo "  $1 = $2 (beklenen $3)"; return 0; fi; echo "  FARK $1 = $2 (beklenen $3)"; return 1; }
icerir() { if grep -F -q -- "$2" "$1"; then return 0; fi; echo "  EKSİK: '$2'"; return 1; }
icermez() { grep -F -q -- "$2" "$1"; g=$?; if [ $g -eq 1 ]; then return 0; fi; if [ $g -eq 0 ]; then echo "  VAR (olmamalı): '$2'"; else echo "  HATA grep $g"; fi; return 1; }
eslesir() { if grep -E -q -- "$2" "$1"; then return 0; fi; echo "  EKSİK desen: $2"; return 1; }

echo "BASE=$BASE IS=$IS HEAD=$(git rev-parse HEAD) CI=${CIRUN:-yok}"

# CT-2 — paket rev 13 (bu işin onaylı paketi) değişmedi
echo "CT-2"
e=0
esit "paket sha256" "$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" "$PK")" "$PK_SHA" || e=1
sonuc CT-2 zorunlu $e

# CT-3 — iş BASE..IS: yalnız izinli yollar; HEAD işi içerir; seçici HEAD'de iş commit'indekiyle aynı
echo "CT-3"
e=0
if git merge-base --is-ancestor "$BASE" "$IS" 2>/dev/null && git merge-base --is-ancestor "$IS" HEAD 2>/dev/null; then
  echo "  BASE <= IS <= HEAD"
else
  echo "  FARK BASE, IS ve HEAD sırası tutmuyor"; e=1
fi
git diff --name-only "$BASE" "$IS" > "$OUT/ct3.txt" 2>&1 || e=1
cat "$OUT/ct3.txt"
n=$(grep -v -x -e "$SEL" -e "$BL" "$OUT/ct3.txt" | grep -v '^docs/records/audits/2026-10/mod-0286-uic-ci-01/' | grep -v '^docs/records/audits/2026-10/pss-ui-p1-04-' | wc -l | tr -d ' ')
esit "izinli dışında değişen dosya" "$n" 0 || e=1
icerir "$OUT/ct3.txt" "$SEL" || e=1
icerir "$OUT/ct3.txt" "$BL" || e=1
git diff --name-only "$IS" HEAD -- "$SEL" > "$OUT/ct3-sonra.txt" 2>&1 || e=1
esit "işten sonra değişen seçici" "$(wc -l < "$OUT/ct3-sonra.txt" | tr -d ' ')" 0 || e=1
sonuc CT-3 zorunlu $e

# AC-11 (paket :672) — P2 başlatılmadı
echo "AC-11"
e=0
for p in frontend/Diten.Web/Views/Platform/UiGovernance services/Diten.Platform/src/Diten.Platform.Application/Features/UiGovernance; do
  if [ -e "$p" ]; then echo "  FARK var: $p"; e=1; else echo "  yok: $p"; fi
done
sonuc AC-11 zorunlu $e

# Seçici var mı
if [ ! -f "$SEL" ] || [ ! -f "$BL" ]; then
  echo "SEÇİCİ YA DA TABAN YOK: $SEL / $BL — kalan bütün kontroller kaldı"
  for k in AC-17a AC-17b AC-18a AC-18b1 AC-18b2 AC-18c AC-18d AC-18e AC-19 AC-20; do sonuc "$k" zorunlu 1; done
else

# AC-19 (paket :680) — taban dosyası okunur, biçim kuralları ve bugünkü ölçümle aynı satır kümesi
echo "AC-19"
e=0
python3 "$SEL" --check-baseline > "$OUT/ac19-check.txt" 2>&1; g=$?
cat "$OUT/ac19-check.txt"; esit "--check-baseline çıkış" "$g" 0 || e=1
esit "başlık" "$(head -n 1 "$BL")" "$(printf 'page\tcheck\tinstance\tcount\treason\tfu\towner')" || e=1
python3 "$SEL" --draft-baseline > "$OUT/ac19-draft.tsv" 2> "$OUT/ac19-draft.err"; g=$?
esit "--draft-baseline çıkış" "$g" 0 || e=1
tail -n +2 "$BL" | grep -v "$(printf '\tbilgi: ')" | cut -f1-4 | sort > "$OUT/ac19-taban-anahtar.txt"
tail -n +2 "$OUT/ac19-draft.tsv" | cut -f1-4 | sort > "$OUT/ac19-taslak-anahtar.txt"
if diff "$OUT/ac19-taban-anahtar.txt" "$OUT/ac19-taslak-anahtar.txt" > "$OUT/ac19.diff"; then
  echo "  taban (page, check, instance, count) bugünkü taslakla aynı: $(wc -l < "$OUT/ac19-taban-anahtar.txt" | tr -d ' ') satır"
else
  echo "  FARK taban ile taslak: $(grep -c '^[<>]' "$OUT/ac19.diff") satır farkı (ac19.diff)"; e=1
fi
esit "bilgi: satırı" "$(grep -c "$(printf '\tbilgi: ')" "$BL")" 2 || e=1
icerir "$BL" "$(printf 'DevEnablement/GoldenReferenceSlim\tbilgi: RC-6 AddNewGoldenReferenceSlim\t-\t-\t')" || e=1
icerir "$BL" "$(printf 'DevEnablement/GoldenReferenceCompact\tbilgi: reference not ready (DEV-0001 status: in-progress)\t-\t-\t')" || e=1
echo "  CI başına satır ve eşleşme toplamı:"
tail -n +2 "$BL" | awk -F'\t' '{r[$2]++; if ($4 ~ /^[0-9]+$/) m[$2]+=$4} END {for (k in r) printf "    %s satır=%d eşleşme=%d\n", k, r[k], m[k]}' | sort
esit "sahip kararı yazmayan satır" "$(tail -n +2 "$BL" | awk -F'\t' '$5 !~ /pss-mod0286-taban-01/' | wc -l | tr -d ' ')" 0 || e=1
sonuc AC-19 zorunlu $e

# Geçici klon (depo dışı): AC-17 ara kaydı ve AC-18 sabotajları
TMPD=$(mktemp -d)
K="$TMPD/klon"
git clone --quiet --shared "$ROOT" "$K" > "$OUT/klon.txt" 2>&1 && (cd "$K" && git checkout --quiet --detach "$(cd "$ROOT" && git rev-parse HEAD)") >> "$OUT/klon.txt" 2>&1
KG=$?
echo "klon çıkış $KG ($(cd "$K" 2>/dev/null && git rev-parse --short HEAD))"
kos() { # kos <ad> <base> — klonda seçiciyi koşar; çıktı ve çıkış OUT'a
  (cd "$K" && python3 "$SEL" --base "$2") > "$OUT/$1.out" 2>&1; echo $? > "$OUT/$1.exit"
  echo "  koşu $1 --base $(echo "$2" | cut -c1-9): çıkış $(cat "$OUT/$1.exit")"
}
kc() { (cd "$K" && git $ID commit --quiet -a -m "$1") >> "$OUT/klon.txt" 2>&1 && (cd "$K" && git rev-parse HEAD); }
kh() { (cd "$K" && git rev-parse HEAD); }

# AC-17 ara kaydı (paket :678 bölüm 1, Test Expectations :724) — self-test + tek sentetik commit
echo "AC-17a"
e=0
[ $KG -eq 0 ] || e=1
python3 "$SEL" --self-test > "$OUT/ac17a-selftest.txt" 2>&1; g=$?
tail -n 5 "$OUT/ac17a-selftest.txt"; esit "--self-test çıkış" "$g" 0 || e=1
P0=$(kh)
(cd "$K" && printf '\n@* uic ara kayit *@\n' >> frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/Index.cshtml \
  && printf '\n@* uic ara kayit *@\n' >> frontend/Diten.Web/Views/Meetings/Report/Index.cshtml \
  && git rm --quiet frontend/Diten.Web/Views/Meetings/Report/_Filter.cshtml \
  && mkdir -p frontend/Diten.Web/Views/Meetings/ReportAra \
  && git mv frontend/Diten.Web/Views/Meetings/Report/_IndexL10n.cshtml frontend/Diten.Web/Views/Meetings/ReportAra/_IndexL10n.cshtml \
  && python3 -c "import json,io;p='frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json';d=json.load(io.open(p,encoding='utf-8'));d['navigation']['Platform Operasyonları'][0]['keywords'].append('uic-ara');io.open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2)+'\n')" \
  && printf '\n// uic ara kayit\n' >> frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js) >> "$OUT/klon.txt" 2>&1 || e=1
kc "uic ara kayit" > /dev/null || e=1
kos ac17a-ara "$P0"
x=$(cat "$OUT/ac17a-ara.exit"); if [ "$x" = 0 ] || [ "$x" = 1 ]; then echo "  çıkış 0|1"; else echo "  FARK çıkış $x"; e=1; fi
eslesir "$OUT/ac17a-ara.out" ' DevEnablement/GoldenReferenceSlim CI-3 ' || e=1
eslesir "$OUT/ac17a-ara.out" ' Meetings/Report CI-' || e=1
eslesir "$OUT/ac17a-ara.out" ' Meetings/ReportAra CI-' || e=1
eslesir "$OUT/ac17a-ara.out" ' Platform/Tenants CI-12 ' || e=1
icerir "$OUT/ac17a-ara.out" 'wwwroot/assets/js/diten-datatable.js' || e=1
icerir "$OUT/ac17a-ara.out" 'paylaşılan dosya, sayfa kontrolü yok' || e=1
eslesir "$OUT/ac17a-ara.out" '^süre: [0-9]+(\.[0-9]+)? s$' || e=1
(cd "$K" && git $ID revert --quiet --no-edit HEAD) >> "$OUT/klon.txt" 2>&1 || e=1
sonuc AC-17a zorunlu $e

# AC-18 (paket :679) — altı çift ve (b2) hazırlık commit'i; her sabotaj --base <ebeveyn> çıkış 1 + adlı satır, her geri alma --base <sabotaj> çıkış 0
cift() { # cift <ad> <beklenen satır ERE>; sabotaj zaten çalışma ağacında
  P=$(kh); S=$(kc "uic sabotaj $1")
  [ -n "$S" ] || return 1
  kos "ac18$1-sabotaj" "$P"; f=0
  esit "  $1 sabotaj çıkış" "$(cat "$OUT/ac18$1-sabotaj.exit")" 1 || f=1
  eslesir "$OUT/ac18$1-sabotaj.out" "$2" || f=1
  (cd "$K" && git $ID revert --quiet --no-edit HEAD) >> "$OUT/klon.txt" 2>&1 || f=1
  kos "ac18$1-geri" "$S"
  esit "  $1 geri alma çıkış" "$(cat "$OUT/ac18$1-geri.exit")" 0 || f=1
  return $f
}
J5=frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js
MR=frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.js
echo "AC-18a"
e=0; [ $KG -eq 0 ] || e=1
(cd "$K" && printf "\nvar uicSab = \$('#t').DataTable({});\n" >> "$J5")
cift a '^YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-5 wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index\.js( [0-9]+)? \+1$' || e=1
sonuc AC-18a zorunlu $e
echo "AC-18b1"
e=0; [ $KG -eq 0 ] || e=1
esit "Meetings/Report CI-5 satırı (count 1)" "$(grep -c "$(printf 'Meetings/Report\tCI-5\twwwroot/assets/js/Meetings/Report/index.js\t1\t')" "$BL")" 1 || e=1
(cd "$K" && python3 -c "import io,re,sys;p=sys.argv[1];L=io.open(p,encoding='utf-8').read().split('\n');i=[k for k,l in enumerate(L) if re.search(r'DataTable\(\s*\{',l)];assert len(i)==1,i;L[i[0]]+=' new DataTable(x, {});';io.open(p,'w',encoding='utf-8').write('\n'.join(L))" "$MR") >> "$OUT/klon.txt" 2>&1 || e=1
cift b1 '^YENİ KIRMIZI Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index\.js( [0-9]+)? \+1$' || e=1
sonuc AC-18b1 zorunlu $e
echo "AC-18b2"
e=0; [ $KG -eq 0 ] || e=1
P=$(kh)
(cd "$K" && python3 -c "import io,re,sys;p=sys.argv[1];L=io.open(p,encoding='utf-8').read().split('\n');i=[k for k,l in enumerate(L) if re.search(r'DataTable\(\s*\{',l)];assert len(i)==1,i;L[i[0]]+=' new DataTable(y,';L.insert(i[0]+1,'options);');io.open(p,'w',encoding='utf-8').write('\n'.join(L))" "$MR") >> "$OUT/klon.txt" 2>&1 || e=1
H=$(kc "uic sabotaj b2 hazirlik")
kos ac18b2-hazirlik "$P"
esit "b2 hazırlık çıkış" "$(cat "$OUT/ac18b2-hazirlik.exit")" 0 || e=1
eslesir "$OUT/ac18b2-hazirlik.out" '^TABAN FU-14 Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index\.js 1$' || e=1
(cd "$K" && python3 -c "import io,sys;p=sys.argv[1];t=io.open(p,encoding='utf-8').read();assert t.count('\noptions);')==1;io.open(p,'w',encoding='utf-8').write(t.replace('\noptions);','\n{});'))" "$MR") >> "$OUT/klon.txt" 2>&1 || e=1
cift b2 '^YENİ KIRMIZI Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index\.js( [0-9]+)? \+1$' || e=1
(cd "$K" && git $ID revert --quiet --no-edit "$H") >> "$OUT/klon.txt" 2>&1 || e=1
sonuc AC-18b2 zorunlu $e
echo "AC-18c"
e=0; [ $KG -eq 0 ] || e=1
(cd "$K" && git rm --quiet frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/_IndexL10n.cshtml) >> "$OUT/klon.txt" 2>&1 || e=1
cift c '^YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-3 _IndexL10n\.cshtml exists' || e=1
eslesir "$OUT/ac18c-sabotaj.out" '^ölçülmedi.* DevEnablement/GoldenReferenceSlim CI-3 ' || e=1
grep 'DevEnablement/GoldenReferenceSlim CI-3' "$OUT/ac18c-sabotaj.out" > "$OUT/ac18c-ci3.txt"
icermez "$OUT/ac18c-ci3.txt" 'kapandı' || e=1
sonuc AC-18c zorunlu $e
TR=frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json
echo "AC-18d"
e=0; [ $KG -eq 0 ] || e=1
(cd "$K" && python3 -c "import json,io,sys;p=sys.argv[1];d=json.load(io.open(p,encoding='utf-8'))
n=0
for top in ('navigation','suggestions'):
  for g in d[top]:
    k=len(d[top][g]); d[top][g]=[i for i in d[top][g] if i.get('url')!='/Platform/Tenants']; n+=k-len(d[top][g])
assert n==2,n
io.open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2)+'\n')" "$TR") >> "$OUT/klon.txt" 2>&1 || e=1
cift d '^YENİ KIRMIZI Platform/Tenants CI-12 platform-search\.tr\.json:yok$' || e=1
sonuc AC-18d zorunlu $e
echo "AC-18e"
e=0; [ $KG -eq 0 ] || e=1
(cd "$K" && python3 -c "import json,io,sys;p=sys.argv[1];d=json.load(io.open(p,encoding='utf-8'))
n=0
for g in d['navigation']:
  k=len(d['navigation'][g]); d['navigation'][g]=[i for i in d['navigation'][g] if i.get('url')!='/Platform/Tenants']; n+=k-len(d['navigation'][g])
assert n==1,n
assert sum(1 for g in d['suggestions'] for i in d['suggestions'][g] if i.get('url')=='/Platform/Tenants')==1
io.open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2)+'\n')" "$TR") >> "$OUT/klon.txt" 2>&1 || e=1
cift e '^YENİ KIRMIZI Platform/Tenants CI-12 platform-search\.tr\.json:yok$' || e=1
sonuc AC-18e zorunlu $e

# AC-20 (paket :681) — süre satırı ara kayıtta (ve nihai CI artefaktında, AC-17b); CI komut satırından tüm sayfa kipi yok
echo "AC-20"
e=0
eslesir "$OUT/ac17a-ara.out" '^süre: [0-9]+(\.[0-9]+)? s$' || e=1
if [ -f "$WF" ]; then
  grep -n 'uic_changed_pages' "$WF" > "$OUT/ac20-wf.txt"; cat "$OUT/ac20-wf.txt"
  esit "workflow'daki seçici çağrısı" "$(wc -l < "$OUT/ac20-wf.txt" | tr -d ' ')" 1 || e=1
  icermez "$OUT/ac20-wf.txt" 'draft-baseline' || e=1
  eslesir "$OUT/ac20-wf.txt" 'uic_changed_pages\.py --base ' || e=1
else
  echo "  workflow dosyası yok ($WF) — CI komut satırı ölçülemedi"; e=1
fi
sonuc AC-20 zorunlu $e

# AC-17 nihai (paket :678 bölüm 2) — kabul edilen commit'te CI işi koşusu ve artefaktı
echo "AC-17b"
e=0
if [ -z "$CIRUN" ]; then
  echo "  CI koşu kimliği verilmedi — workflow (CT-Setup + sahip onayı) ve PR koşusu bekleniyor"; e=1
else
  gh run view "$CIRUN" --json headSha,conclusion,url,workflowName,path > "$OUT/ac17b-run.json" 2>&1 || e=1
  cat "$OUT/ac17b-run.json"
  esit "CI head sha" "$(python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['headSha'])" "$OUT/ac17b-run.json" 2>/dev/null)" "$(git rev-parse HEAD)" || e=1
  esit "CI sonuç" "$(python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['conclusion'])" "$OUT/ac17b-run.json" 2>/dev/null)" success || e=1
  icerir "$OUT/ac17b-run.json" "$WF" || e=1
  mkdir -p "$OUT/ac17b-artefakt" && gh run download "$CIRUN" -D "$OUT/ac17b-artefakt" > "$OUT/ac17b-indir.txt" 2>&1 || e=1
  A=$(find "$OUT/ac17b-artefakt" -type f | head -n 1)
  if [ -n "$A" ]; then
    H=$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" "$A")
    echo "  artefakt $A sha256 $H"
    gh run view "$CIRUN" --log > "$OUT/ac17b-log.txt" 2>&1 || e=1
    icerir "$OUT/ac17b-log.txt" "$H" || e=1
    eslesir "$A" '^süre: [0-9]+(\.[0-9]+)? s$' || e=1
  else
    echo "  FARK artefakt indirilemedi"; e=1
  fi
fi
sonuc AC-17b zorunlu $e

fi

echo "ÖZET"
cat "$R"
gecti=0; toplam=0
for k in $ZORUNLU; do
  toplam=$((toplam + 1))
  n=$(grep -c "^$k zorunlu geçti\$" "$R")
  if [ "$n" = 1 ]; then gecti=$((gecti + 1)); else echo "  zorunlu $k geçmedi"; fi
done
echo "zorunlu geçti $gecti / $toplam"
if [ $gecti -eq $toplam ]; then exit 0; fi
exit 1
