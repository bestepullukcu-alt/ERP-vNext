# MOD-0286 PSS-UI-P1-05 (DL-3 yeniden açma, AC-21) kanıt betiği, sürüm 02 — Control Tower yazdı; iş paketi bu dosyayı değiştirmez.
# Sürüm 02 neden (prompt incelemesi v1.0): F1 FU-17 satırı paket 487'deki metinle birebir aranır ve tek seviye CI-3 satırı
# çıkış 2 ister; F2 bozuk sayfa anahtarı doğrudan denenir; F3 iş commit'inden sonra yalnız kayıt ve kuyruk yolları değişebilir.
# Neden: scripts/acceptance_record.py bir AC'yi yalnız TEK kayıtlı koşunun artefaktıyla "geçti" sayar ve ham çıkış 0 ister;
# AC-21 (a)-(g) bu yüzden tek koşuda üretilir. Ölçütler paket rev 15 AC-21 metnindendir; sayılar orada yazılı.
# Kullanım (depo kökünden): sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh <var olan çıktı-dizini> <iş-commit>
# Kayıtlı koşu: python3 scripts/run_record.py run --out <dizin> -- sh <bu betik> <dizin> <iş-commit>
# Yalnız POSIX sh. Depoya yalnız <çıktı-dizini> altına yazar. Sabotajlar depo DIŞINDAKİ geçici bir klonda yapılır (AC-21: "never
# pushed, never merged"); her koşu orada run_record.py ile kayıt altına alınır ve kayıt <çıktı-dizini>'ne taşınır.
# Çıkış 0 yalnız ZORUNLU listedeki her kimlik tam bir kez "geçti" ise; aksi 1; kullanım hatası 2.
OUT="$1"; IS="$2"
if [ -z "$OUT" ] || [ ! -d "$OUT" ] || [ -z "$IS" ]; then echo "kullanım: sh uic-tek-seviye-kanit-02.sh <var olan çıktı-dizini> <iş-commit>"; exit 2; fi
LC_ALL=C; export LC_ALL
PYTHONDONTWRITEBYTECODE=1; export PYTHONDONTWRITEBYTECODE
ROOT=$(pwd)
OUT=$(cd "$OUT" && pwd)
D=docs/records/audits/2026-10/mod-0286-uic-ci-01
SEL=scripts/uic_changed_pages.py
BL=tests/ui-compliance-baseline.tsv
PK=execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
PK_SHA=0c53533e3e2434fbd8abe615f814d037c3c390b06625ee822358871958aed102
BU=$D/uic-tek-seviye-kanit-02.sh
ORA=$D/uic-tek-seviye-oracle.py
BASE=$(git log --diff-filter=A -n 1 --format=%H -- "$BU")   # işin tabanı = bu betiği ekleyen commit (Expected HEAD)
ZORUNLU="CT-2 CT-3 AC-21a AC-21b AC-21c AC-21d AC-21e AC-21f AC-21g REG"
R="$OUT/sonuc.txt"
: > "$R"
ID="-c user.name=uic-kanit -c user.email=uic-kanit@localhost -c commit.gpgsign=false"
WEB=frontend/Diten.Web
GECICI=docs/records/audits/2026-10/uic-tek-gecici

sonuc() { if [ "$2" -eq 0 ] 2>/dev/null; then s=geçti; else s=kaldı; fi; echo "$1 sonuç=$s (zorunlu)"; echo "$1 zorunlu $s" >> "$R"; }
esit() { if [ "$2" = "$3" ]; then echo "  $1 = $2 (beklenen $3)"; return 0; fi; echo "  FARK $1 = $2 (beklenen $3)"; return 1; }
tam() { if grep -F -x -q -- "$2" "$1"; then echo "  satır: $2"; return 0; fi; echo "  EKSİK satır: $2"; return 1; }

echo "BASE=$BASE IS=$IS HEAD=$(git rev-parse HEAD)"

# CT-2 — paket rev 15 değişmedi
echo "CT-2"
e=0
esit "paket sha256" "$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" "$PK")" "$PK_SHA" || e=1
sonuc CT-2 $e

# CT-3 — iş BASE..IS: yalnız izinli yollar; HEAD işi içerir; seçici ve taban HEAD'de iş commit'indekiyle aynı; betikler değişmedi
echo "CT-3"
e=0
if [ -n "$BASE" ] && git merge-base --is-ancestor "$BASE" "$IS" 2>/dev/null && git merge-base --is-ancestor "$IS" HEAD 2>/dev/null; then
  echo "  BASE <= IS <= HEAD"
else
  echo "  FARK BASE, IS ve HEAD sırası tutmuyor"; e=1
fi
git diff --name-only "$BASE" "$IS" > "$OUT/ct3.txt" 2>&1 || e=1
cat "$OUT/ct3.txt"
esit "izinli dışında değişen dosya" "$(grep -v -x -e "$SEL" -e "$BL" "$OUT/ct3.txt" | grep -v "^$D/p1-05-kanit-01/" | grep -v '^docs/records/audits/2026-10/pss-ui-p1-05-rapor-01/' | wc -l | tr -d ' ')" 0 || e=1
esit "seçici işte değişti" "$(grep -c -x "$SEL" "$OUT/ct3.txt")" 1 || e=1
esit "taban işte değişti" "$(grep -c -x "$BL" "$OUT/ct3.txt")" 1 || e=1
esit "işten sonra değişen seçici ve taban" "$(git diff --name-only "$IS" HEAD -- "$SEL" "$BL" | wc -l | tr -d ' ')" 0 || e=1
esit "işten sonra kayıt ve kuyruk dışında değişen dosya" "$(git diff --name-only "$IS" HEAD | grep -v '^docs/records/' | grep -v '^execution/domains/platform-shared-services/work-packs/' | wc -l | tr -d ' ')" 0 || e=1
esit "eklenme sonrası değişen kanıt dosyası" "$(git diff --name-only "$BASE" HEAD -- "$BU" "$ORA" | wc -l | tr -d ' ')" 0 || e=1
esit "kanıt betiğinin silinip eklenmesi" "$(git log --diff-filter=D --format=%H -- "$BU" "$ORA" | wc -l | tr -d ' ')" 0 || e=1
sonuc CT-3 $e

# Geçici klon: BASE ağacı (a'nın öncesi), sonra HEAD
TMPD=$(mktemp -d)
K="$TMPD/klon"
git clone --quiet --shared "$ROOT" "$K" > "$OUT/klon.txt" 2>&1
KG=$?
kbase() { (cd "$K" && git checkout --quiet --detach "$1" && git reset --quiet --hard "$1") >> "$OUT/klon.txt" 2>&1; }
kc() { (cd "$K" && git add -A && git $ID commit --quiet -m "$1") >> "$OUT/klon.txt" 2>&1 && (cd "$K" && git rev-parse HEAD); }
kh() { (cd "$K" && git rev-parse HEAD); }
rr() { # rr <ad> <base> — klonda kayıtlı seçici koşusu; kayıt OUT/<ad>/, çıkış OUT/<ad>.exit
  (cd "$K" && python3 scripts/run_record.py run --out "$GECICI/$1" -- python3 "$SEL" --base "$2") > "$OUT/$1.rr" 2>&1
  (cd "$K" && python3 scripts/run_record.py verify "$GECICI/$1/run-record-v1.json") >> "$OUT/$1.rr" 2>&1
  v=$?
  mv "$K/$GECICI/$1" "$OUT/$1"
  python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['exit'])" "$OUT/$1/run-record-v1.json" > "$OUT/$1.exit" 2>&1
  echo "  koşu $1 --base $(echo "$2" | cut -c1-9): çıkış $(cat "$OUT/$1.exit"), verify $v"
  return $v
}
sayi() { grep -c -E -- "$2" "$OUT/$1/stdout.txt"; }

# AC-21 (a) — tek PR: önce (BASE'in okuyucusu FU-16 satırını reddeder), sonra (--check-baseline 0, satırlar bağımsız ölçümle aynı)
echo "AC-21a"
e=0
[ $KG -eq 0 ] || e=1
kbase "$BASE" || e=1
for satir in "$(printf 'Tasks\tCI-2\tViews/Tasks/Create.cshtml\t1\tdeneme\tFU-16\tdeneme')" "$(printf 'Account\tCI-1\tViews/Account/Login.cshtml:yok:Layout\t-\tdeneme\tFU-16\tdeneme')"; do
  (cd "$K" && printf '%s\n' "$satir" >> "$BL" && python3 "$SEL" --check-baseline) > "$OUT/ac21a-once.txt" 2>&1; g=$?
  cat "$OUT/ac21a-once.txt" | tail -n 1
  esit "önce: bir FU-16 satırıyla --check-baseline" "$g" 2 || e=1
  kbase "$BASE" || e=1
done
python3 "$SEL" --check-baseline > "$OUT/ac21a-check.txt" 2>&1; g=$?
cat "$OUT/ac21a-check.txt"
esit "sonra: --check-baseline" "$g" 0 || e=1
awk -F'\t' '$6 == "FU-16"' "$BL" > "$OUT/ac21a-fu16.tsv"
cut -f1-4 "$OUT/ac21a-fu16.tsv" | sort > "$OUT/ac21a-fu16-anahtar.txt"
python3 "$ORA" satirlar "$IS" | sort > "$OUT/ac21a-bagimsiz.txt" || e=1
echo "  FU-16 satırı $(wc -l < "$OUT/ac21a-fu16.tsv" | tr -d ' '), bağımsız ölçüm $(wc -l < "$OUT/ac21a-bagimsiz.txt" | tr -d ' ') (paket, 2026-10-29: 134)"
if diff "$OUT/ac21a-fu16-anahtar.txt" "$OUT/ac21a-bagimsiz.txt" > "$OUT/ac21a.diff"; then echo "  FU-16 satırları bağımsız ölçümle aynı"; else echo "  FARK FU-16 ile bağımsız ölçüm ($(grep -c '^[<>]' "$OUT/ac21a.diff") satır, ac21a.diff)"; e=1; fi
esit "FU-16 satırlarında taban-01" "$(awk -F'\t' '$6 == "FU-16"' "$BL" | grep -c 'taban-01')" 0 || e=1
python3 "$ORA" sahip "$BL" > "$OUT/ac21a-sahip.txt" 2>&1 || e=1
tail -n 1 "$OUT/ac21a-sahip.txt"
esit "kararla aynı olmayan satır" "$(tail -n 1 "$OUT/ac21a-sahip.txt")" "kararla aynı olmayan FU-16 satırı 0" || e=1
esit "sayfa anahtarı / içermeyen ama FU-16 olmayan satır" "$(tail -n +2 "$BL" | awk -F'\t' '$1 !~ /\// && $6 != "FU-16"' | wc -l | tr -d ' ')" 0 || e=1
esit "sayfa anahtarı / içeren FU-16 satırı" "$(awk -F'\t' '$6 == "FU-16" && $1 ~ /\//' "$BL" | wc -l | tr -d ' ')" 0 || e=1
esit "tabandan silinen satır (yalnız ekleme)" "$(git diff --numstat "$BASE" "$IS" -- "$BL" | cut -f2)" 0 || e=1
python3 "$SEL" --draft-baseline > "$OUT/ac21a-draft.tsv" 2> "$OUT/ac21a-draft.err"; g=$?
esit "--draft-baseline çıkış" "$g" 0 || e=1
awk -F'\t' '$6 == "FU-16"' "$OUT/ac21a-draft.tsv" | cut -f1-4 | sort > "$OUT/ac21a-taslak.txt"
if diff "$OUT/ac21a-fu16-anahtar.txt" "$OUT/ac21a-taslak.txt" > "$OUT/ac21a-taslak.diff"; then echo "  FU-16 satırları taslakla aynı"; else echo "  FARK FU-16 ile taslak"; e=1; fi
esit "taslakta FU-16 satırında taban-01" "$(awk -F'\t' '$6 == "FU-16"' "$OUT/ac21a-draft.tsv" | grep -c 'taban-01')" 0 || e=1
esit "taslakta belirsiz sahipli olmayan FU-16" "$(awk -F'\t' '$6 == "FU-16" && $7 != "belirsiz — sahip belirleyecek"' "$OUT/ac21a-draft.tsv" | wc -l | tr -d ' ')" 0 || e=1
# okuyucu: alt klasör instance'ı ve boş sayfa anahtarı (doğrudan, self-test'ten ayrı)
kbase "$(git rev-parse HEAD)" || e=1
for satir in "$(printf 'Meetings\tCI-2\tViews/Meetings/Report/x.cshtml\t1\tdeneme\tFU-16\tdeneme')" "$(printf 'Meetings\tCI-1\tViews/Meetings/Report/Index.cshtml:yok:Layout\t-\tdeneme\tFU-16\tdeneme')" "$(printf '\tCI-2\tViews/Tasks/Create.cshtml\t1\tdeneme\tFU-16\tdeneme')" "$(printf 'Foo Bar\tCI-2\tViews/Foo Bar/x.cshtml\t1\tdeneme\tFU-16\tdeneme')" "$(printf 'A/B/C\tCI-2\tViews/A/B/C/x.cshtml\t1\tdeneme\tFU-16\tdeneme')" "$(printf 'Meetings\tCI-3\tx exists\t-\tdeneme\tFU-16\tdeneme')"; do
  (cd "$K" && printf '%s\n' "$satir" >> "$BL" && python3 "$SEL" --check-baseline) > "$OUT/ac21a-red.txt" 2>&1; g=$?
  tail -n 1 "$OUT/ac21a-red.txt"
  esit "bozuk satırla --check-baseline" "$g" 2 || e=1
  kbase "$(git rev-parse HEAD)" || e=1
done
sonuc AC-21a $e

# AC-21 (b), (c) — 355 dosya, tam liste, taban tutar
H0=$(git rev-parse HEAD)
echo "AC-21b"
e=0
kbase "$H0" || e=1
(cd "$K" && python3 "$ROOT/$ORA" liste "$H0") > "$OUT/ac21b-liste.txt" || e=1
esit "cshtml dosyası" "$(grep -c '^cshtml' "$OUT/ac21b-liste.txt")" 162 || e=1
esit "js dosyası" "$(grep -c '^js' "$OUT/ac21b-liste.txt")" 87 || e=1
esit "FU-18 dosyası" "$(grep -c '^fu18' "$OUT/ac21b-liste.txt")" 106 || e=1
cut -f2 "$OUT/ac21b-liste.txt" | while IFS= read -r f; do printf '\n' >> "$K/$f"; done
P=$(kh); S=$(kc "uic ac21 b"); [ -n "$S" ] || e=1
rr ac21b "$P" || e=1
esit "tek seviye sayfa" "$(grep -oE '^(PASS|TABAN FU-16) [^/ ]+ CI-2 ' "$OUT/ac21b/stdout.txt" | awk '{print $(NF-1)}' | sort -u | wc -l | tr -d ' ')" 19 || e=1
esit "CI-2 görünüm satırı" "$(sayi ac21b '^(PASS|TABAN FU-16) [^/ ]+ CI-2 Views/')" 162 || e=1
esit "CI-1 görünüm satırı" "$(sayi ac21b '^(PASS|TABAN FU-16) [^/ ]+ CI-1 Views/')" 121 || e=1
esit "CI-4 JS satırı" "$(sayi ac21b '^(PASS|TABAN FU-16) [^/ ]+ CI-4 wwwroot/assets/js/')" 87 || e=1
esit "ölçülmedi Meetings CI-3" "$(sayi ac21b '^ölçülmedi Meetings CI-3 ')" 1 || e=1
tam "$OUT/ac21b/stdout.txt" 'ölçülmedi Meetings CI-3 - doğrulayıcı alansız sayfayı desteklemiyor (FU-17)' || e=1
esit "FU-18 satırı" "$(grep -c "^eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) " "$OUT/ac21b/stdout.txt")" 106 || e=1
esit "paylaşılan dosya satırı" "$(sayi ac21b '^paylaşılan dosya, sayfa kontrolü yok ')" 1 || e=1
tam "$OUT/ac21b/stdout.txt" "paylaşılan dosya, sayfa kontrolü yok $WEB/Views/Platform/_ViewStart.cshtml" || e=1
grep -E '^süre: [0-9]+(\.[0-9]+)? s$' "$OUT/ac21b/stdout.txt" || { echo "  EKSİK süre satırı"; e=1; }
sonuc AC-21b $e
echo "AC-21c"
e=0
esit "(b) koşusu çıkış" "$(cat "$OUT/ac21b.exit")" 0 || e=1
esit "YENİ KIRMIZI" "$(sayi ac21b '^YENİ KIRMIZI ')" 0 || e=1
sonuc AC-21c $e

# AC-21 (d) — sabotaj, dosya kontrolleri
cift() { # cift <ad> <beklenen tam satır>; sabotaj çalışma ağacında
  P=$(kh); S=$(kc "uic ac21 $1"); [ -n "$S" ] || return 1
  rr "ac21$1-sabotaj" "$P" || return 1
  f=0
  esit "$1 sabotaj çıkış" "$(cat "$OUT/ac21$1-sabotaj.exit")" 1 || f=1
  tam "$OUT/ac21$1-sabotaj/stdout.txt" "$2" || f=1
  (cd "$K" && git $ID revert --quiet --no-edit HEAD) >> "$OUT/klon.txt" 2>&1 || f=1
  rr "ac21$1-geri" "$S" || f=1
  esit "$1 geri alma çıkış" "$(cat "$OUT/ac21$1-geri.exit")" 0 || f=1
  return $f
}
echo "AC-21d"
e=0
kbase "$H0" || e=1
esit "Tasks/Create.cshtml spinner-border" "$(git show "$H0:$WEB/Views/Tasks/Create.cshtml" | grep -c 'spinner-border')" 0 || e=1
esit "Recall/cases.js alert(" "$(git show "$H0:$WEB/wwwroot/assets/js/Recall/cases.js" | grep -c 'alert(')" 0 || e=1
printf '\n<span class="spinner-border"></span>\n' >> "$K/$WEB/Views/Tasks/Create.cshtml"
cift d1 'YENİ KIRMIZI Tasks CI-2 Views/Tasks/Create.cshtml 1 +1' || e=1
printf "\nalert('x');\n" >> "$K/$WEB/wwwroot/assets/js/Recall/cases.js"
cift d2 'YENİ KIRMIZI Recall CI-7 wwwroot/assets/js/Recall/cases.js 1 +1' || e=1
sonuc AC-21d $e

# AC-21 (e) — yeni yüzey (CI-11): e1 yeni klasör, e2 yalnız alt klasör sayfası olan klasör
yeni() { # yeni <Klasör>
  kbase "$H0" || return 1
  mkdir -p "$K/$WEB/Views/$1"
  printf '@{\n    Layout = "_LayoutTenantShell";\n}\n' > "$K/$WEB/Views/$1/Index.cshtml"
  printf '@* uic ac21 *@\n' > "$K/$WEB/Views/$1/_IndexL10n.cshtml"
  P=$(kh); S=$(kc "uic ac21 yeni yuzey $1"); [ -n "$S" ] || return 1
  rr "ac21e-$1-1" "$P" || return 1
  f=0
  esit "$1 ilk commit çıkış" "$(cat "$OUT/ac21e-$1-1.exit")" 1 || f=1
  tam "$OUT/ac21e-$1-1/stdout.txt" "YENİ KIRMIZI $1 CI-11 Views/$1/_IndexL10n.cshtml:yok:GetAllStrings" || f=1
  printf '@* uic ac21 *@\n@{ var uicTum = SharedLocalizer.GetAllStrings(true); }\n' > "$K/$WEB/Views/$1/_IndexL10n.cshtml"
  S2=$(kc "uic ac21 yeni yuzey $1 duzeltme"); [ -n "$S2" ] || return 1
  rr "ac21e-$1-2" "$P" || return 1
  esit "$1 ikinci koşu çıkış" "$(cat "$OUT/ac21e-$1-2.exit")" 0 || f=1
  esit "$1 PASS CI-11" "$(sayi "ac21e-$1-2" "^PASS $1 CI-11 ")" 1 || f=1
  return $f
}
echo "AC-21e"
e=0
if git cat-file -e "$H0:$WEB/Views/Foo" 2>/dev/null; then echo "  FARK Views/Foo var"; e=1; fi
esit "Views/HCM altında doğrudan dosya" "$(git ls-tree --name-only "$H0" "$WEB/Views/HCM/" | while IFS= read -r x; do [ "$(git cat-file -t "$H0:$x")" = blob ] && echo "$x"; done | wc -l | tr -d ' ')" 0 || e=1
yeni Foo || e=1
yeni HCM || e=1
sonuc AC-21e $e

# AC-21 (f) — sayfa kaldırıldı; alt klasör sayfaları etkilenmez; geri alma yeni yüzeydir
echo "AC-21f"
e=0
kbase "$H0" || e=1
(cd "$K" && git ls-tree --name-only HEAD "$WEB/Views/Meetings/" | while IFS= read -r x; do [ "$(git cat-file -t "HEAD:$x")" = blob ] && echo "$x"; done) > "$OUT/ac21f-liste.txt"
cat "$OUT/ac21f-liste.txt"
esit "silinecek dosya" "$(wc -l < "$OUT/ac21f-liste.txt" | tr -d ' ')" 9 || e=1
(cd "$K" && while IFS= read -r x; do git rm --quiet "$x"; done < "$OUT/ac21f-liste.txt") >> "$OUT/klon.txt" 2>&1 || e=1
P=$(kh); S=$(kc "uic ac21 f"); [ -n "$S" ] || e=1
rr ac21f "$P" || e=1
esit "silme çıkış" "$(cat "$OUT/ac21f.exit")" 0 || e=1
esit "sayfa kaldırıldı Meetings" "$(sayi ac21f '^sayfa kaldırıldı Meetings$')" 1 || e=1
esit "kapandı satırı = Meetings FU-16 satırı" "$(sayi ac21f '^kapandı - satır silinmeli Meetings ')" "$(awk -F'\t' '$1 == "Meetings" && $6 == "FU-16"' "$BL" | wc -l | tr -d ' ')" || e=1
esit "iki seviyeli Meetings sayfası" "$(grep -c ' Meetings/' "$OUT/ac21f/stdout.txt")" 0 || e=1
(cd "$K" && git $ID revert --quiet --no-edit HEAD) >> "$OUT/klon.txt" 2>&1 || e=1
rr ac21f-geri "$S" || e=1
esit "geri alma çıkış" "$(cat "$OUT/ac21f-geri.exit")" 1 || e=1
esit "geri almada YENİ KIRMIZI" "$(sayi ac21f-geri '^YENİ KIRMIZI ')" 1 || e=1
tam "$OUT/ac21f-geri/stdout.txt" 'YENİ KIRMIZI Meetings CI-11 Views/Meetings/_IndexL10n.cshtml:yok:GetAllStrings' || e=1
sonuc AC-21f $e

# AC-21 (g) — self-test ve adlı durumlar
echo "AC-21g"
e=0
python3 "$SEL" --self-test > "$OUT/ac21g.txt" 2>&1; g=$?
tail -n 3 "$OUT/ac21g.txt"
esit "--self-test çıkış" "$g" 0 || e=1
for n in 01 02 03 04 05 06 07 08 09 10 11 12 13 14; do
  esit "AC-21g$n durumu" "$(grep -c "AC-21g$n" "$OUT/ac21g.txt" | awk '{print ($1 >= 1) ? "var" : "yok"}')" var || e=1
done
sonuc AC-21g $e

# REG — iki seviyeli sayfa bozulmadı; CI komutu tüm sayfa kipine düşmez; değişen sayfa yoksa çıkış 0
echo "REG"
e=0
kbase "$H0" || e=1
printf "\nvar uicSab = \$('#t').DataTable({});\n" >> "$K/$WEB/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js"
P=$(kh); S=$(kc "uic reg"); [ -n "$S" ] || e=1
rr reg-sabotaj "$P" || e=1
esit "iki seviyeli sabotaj çıkış" "$(cat "$OUT/reg-sabotaj.exit")" 1 || e=1
if grep -E -q '^YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-5 wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index\.js( [0-9]+)? \+1$' "$OUT/reg-sabotaj/stdout.txt"; then echo "  iki seviyeli adlı satır var"; else echo "  EKSİK iki seviyeli adlı satır"; e=1; fi
(cd "$K" && git $ID revert --quiet --no-edit HEAD) >> "$OUT/klon.txt" 2>&1 || e=1
rr reg-geri "$S" || e=1
esit "iki seviyeli geri alma çıkış" "$(cat "$OUT/reg-geri.exit")" 0 || e=1
kbase "$H0" || e=1
rr reg-bos "$H0" || e=1
esit "boş fark çıkış" "$(cat "$OUT/reg-bos.exit")" 0 || e=1
esit "boş farkta kontrol satırı" "$(sayi reg-bos ' CI-[0-9]+ ')" 0 || e=1
sonuc REG $e

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
