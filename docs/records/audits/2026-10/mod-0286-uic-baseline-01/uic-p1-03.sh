# MOD-0286 PSS-UI-P1-03 kanıt betiği — Control Tower yazdı; iş paketi bu dosyayı değiştirmez.
# Kullanım (depo kökünden): sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-p1-03.sh <çıktı-dizini>
# Kayıtlı koşu: python3 scripts/run_record.py run --out <B>/auto-02 -- sh <B>/uic-p1-03.sh <B>/auto-02
# Yalnız POSIX sh (bash'e özgü <(...) yok); yazdığı her dosya <çıktı-dizini> altındadır.
# 1) uic-auto.sh'yi olduğu gibi koşar (RC-1…RC-24; toplam çıkışı yazılır, paket AC-5 gereği yargılanmaz).
# 2) Paket AC-1…AC-16 (AC-14 hariç: elle) ve üç CT kontrolünü ölçer; her blok "<kimlik> sonuç=geçti|kaldı (<tür>)" yazar.
# 3) Çıkış 0 yalnız 17 zorunlu kimliğin hepsi tam bir kez "geçti" yazdıysa; aksi 1. Kullanım hatası 2.
OUT="$1"
if [ -z "$OUT" ] || [ ! -d "$OUT" ]; then echo "kullanım: sh uic-p1-03.sh <var olan çıktı-dizini>"; exit 2; fi
LC_ALL=C; export LC_ALL
B=docs/records/audits/2026-10/mod-0286-uic-baseline-01
D=docs/reference/modules/platform/ui-design-system-page-pattern-governance
SP=$D/spec.md
CL=$D/compliance-checklist.md
PK=execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
PK_SHA=89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44
ZORUNLU="AC-1 AC-2 AC-3 AC-4 AC-5 AC-6 AC-7 AC-8 AC-9 AC-10 AC-11 AC-12 AC-15 AC-16 CT-1 CT-2 CT-3"
R="$OUT/sonuc.txt"
: > "$R"

sonuc() { # $1 kimlik, $2 zorunlu|isteğe-bağlı, $3 hata sayısı (0 = geçti)
  if [ "$3" -eq 0 ] 2>/dev/null; then s=geçti; else s=kaldı; fi
  echo "$1 sonuç=$s ($2)"
  echo "$1 $2 $s" >> "$R"
}
dosya() { # dosya yoksa hata yazar ve 1 döner
  if [ -f "$1" ]; then return 0; fi
  echo "  HATA dosya yok: $1"; return 1
}
say() { # say <desen> <dosya> [grep seçenekleri] — eşleşen satır sayısı; grep hatasında (çıkış 2) "HATA"
  n=$(grep -c $3 -- "$1" "$2"); rc=$?
  if [ $rc -gt 1 ]; then echo HATA; else echo "$n"; fi
}
esit() { # esit <ad> <ölçülen> <beklenen> — eşit değilse yazar, 1 döner
  if [ "$2" = "$3" ]; then echo "  $1 = $2 (beklenen $3)"; return 0; fi
  echo "  FARK $1 = $2 (beklenen $3)"; return 1
}
satir_icerir() { # satir_icerir <satır dosyası> <sabit metin>
  if grep -F -q -- "$2" "$1"; then return 0; fi
  echo "  EKSİK: '$2'"; return 1
}

echo "UIC-AUTO"
sh "$B/uic-auto.sh" "$OUT" > "$OUT/uic-auto.out" 2>&1
UA=$?
cat "$OUT/uic-auto.out"
echo "uic-auto exit=$UA"

# AC-1 (paket :662) — 9 üst başlık, her biri en az bir .antigravity/rules/<dosya>.md:<satır> ya da §<n> atfı
echo "AC-1"
e=0
dosya "$SP" || e=1
esit "## başlık sayısı" "$(say '^## ' "$SP")" 9 || e=1
awk '/^## /{if(h!="")print c"\t"h; h=$0; c=0; next} h!=""{c+=gsub(/\.antigravity\/rules\/[A-Za-z0-9_.-]+\.md:[0-9]+|§[0-9]+/,"&")} END{if(h!="")print c"\t"h}' "$SP" > "$OUT/ac1-basliklar.txt"
cat "$OUT/ac1-basliklar.txt"
z=$(awk -F'\t' '$1==0' "$OUT/ac1-basliklar.txt" | wc -l | tr -d ' ')
esit "atıfsız başlık" "$z" 0 || e=1
t=$(say '\.antigravity/rules/' "$SP")
if [ "$t" = HATA ] || [ "$t" -lt 9 ]; then echo "  FARK .antigravity/rules/ satırı = $t (beklenen >= 9)"; e=1; else echo "  .antigravity/rules/ satırı = $t (>= 9)"; fi
sonuc AC-1 zorunlu $e

# AC-2 (paket :663) ve AC-12 (paket :673) — DL-1/DL-2 commit'leri kural ve çalışma zamanı dosyasına dokunmaz
echo "AC-2"
e=0; e12=0
for c in a0a4050b0 3fbf891bb bb3464954 d3105a332 HEAD; do
  git diff --name-only "$c^" "$c" -- .antigravity services frontend gateway execution/registries > "$OUT/ac2-$c.txt"; g=$?
  n=$(wc -l < "$OUT/ac2-$c.txt" | tr -d ' ')
  echo "  $c: git çıkış $g, değişen $n"
  if [ $g -ne 0 ] || [ "$n" != 0 ]; then e=1; cat "$OUT/ac2-$c.txt"; fi
  git diff --name-only "$c^" "$c" -- .antigravity/rules/module-pack-standard.md > "$OUT/ac12-$c.txt"; g=$?
  n=$(wc -l < "$OUT/ac12-$c.txt" | tr -d ' ')
  if [ $g -ne 0 ] || [ "$n" != 0 ]; then e12=1; echo "  FARK module-pack-standard.md $c"; fi
done
sonuc AC-2 zorunlu $e
echo "AC-12"
esit "paket FU-1 satırı" "$(say '^| FU-1 |' "$PK")" 1 || e12=1
sonuc AC-12 zorunlu $e12

# AC-3 (paket :664) — çatışma kaydı; taraflar paket :773-776
echo "AC-3"
e=0
awk '/^## 9\./{f=1} f && /^\| [1-5] \|/' "$SP" > "$OUT/ac3-satirlar.txt"
esit "§9 satır sayısı (1-5)" "$(wc -l < "$OUT/ac3-satirlar.txt" | tr -d ' ')" 5 || e=1
i=1
while [ $i -le 5 ]; do
  grep "^| $i |" "$OUT/ac3-satirlar.txt" > "$OUT/ac3-$i.txt"
  p=$(sed 's/\\|//g' "$OUT/ac3-$i.txt" | tr -cd '|' | wc -c | tr -d ' ')
  esit "satır $i '|' sayısı (7 sütun)" "$p" 8 || e=1
  i=$((i + 1))
done
for i in 1 2 3; do
  satir_icerir "$OUT/ac3-$i.txt" 'karar: kuralın kendisinde düzeltilir' || e=1
done
satir_icerir "$OUT/ac3-4.txt" 'karar bekliyor' || e=1
n=$(cat "$OUT/ac3-1.txt" "$OUT/ac3-2.txt" "$OUT/ac3-3.txt" "$OUT/ac3-4.txt" | grep -c 'çözüldü')
esit "kural-kural satırında 'çözüldü'" "$n" 0 || e=1
for x in 'premium-modal-standard.md:27' 'frontend-js-standard.md:145' 'frontend-standards.md:194'; do satir_icerir "$OUT/ac3-1.txt" "$x" || e=1; done
for x in 'frontend-js-standard.md:53' 'frontend-datatable-template.md:87' ':100' ':114' 'product-backlog.md:6199' ':6264'; do satir_icerir "$OUT/ac3-2.txt" "$x" || e=1; done
for x in 'frontend-standards.md:208' 'module-pack-standard.md:88' 'views-organization.md:39'; do satir_icerir "$OUT/ac3-3.txt" "$x" || e=1; done
for x in 'premium-modal-standard.md:1' 'dynamic-localization-standard.md:2' '.antigravity/ARCHITECTURE.md:234' '.antigravity/workflows/debug.md:59' 'AGENTS.md:139'; do satir_icerir "$OUT/ac3-4.txt" "$x" || e=1; done
for x in 11 5 181; do
  if grep -E -q -- "(:|, ?:?)$x([^0-9]|$)" "$OUT/ac3-4.txt"; then :; else echo "  EKSİK satır 4 satır numarası $x"; e=1; fi
done
sonuc AC-3 zorunlu $e

# AC-4 (paket :665) — UIC/RC satırları tekil, sayılı ve paketle bayt bayt aynı
echo "AC-4"
e=0
dosya "$CL" || e=1
grep '^| UIC-' "$PK" > "$OUT/ac4-pk-uic.txt"
grep '^| UIC-' "$CL" > "$OUT/ac4-cl-uic.txt"
grep '^| RC-' "$PK" > "$OUT/ac4-pk-rc.txt"
grep '^| RC-' "$CL" > "$OUT/ac4-cl-rc.txt"
esit "UIC satırı" "$(wc -l < "$OUT/ac4-cl-uic.txt" | tr -d ' ')" 32 || e=1
esit "RC satırı" "$(wc -l < "$OUT/ac4-cl-rc.txt" | tr -d ' ')" 24 || e=1
esit "UIC tekrar" "$(grep -oE '^\| UIC-[0-9]+' "$CL" | sort | uniq -d | wc -l | tr -d ' ')" 0 || e=1
esit "RC tekrar" "$(grep -oE '^\| RC-[0-9]+' "$CL" | sort | uniq -d | wc -l | tr -d ' ')" 0 || e=1
if diff "$OUT/ac4-pk-uic.txt" "$OUT/ac4-cl-uic.txt" > "$OUT/ac4-uic.diff"; then echo "  UIC satırları paketle aynı"; else echo "  FARK UIC satırları paketten farklı:"; grep -oE '^[<>] \| UIC-[0-9]+' "$OUT/ac4-uic.diff"; e=1; fi
if diff "$OUT/ac4-pk-rc.txt" "$OUT/ac4-cl-rc.txt" > "$OUT/ac4-rc.diff"; then echo "  RC satırları paketle aynı"; else echo "  FARK RC satırları paketten farklı:"; grep -oE '^[<>] \| RC-[0-9]+' "$OUT/ac4-rc.diff"; e=1; fi
sed 's/\\|//g' "$OUT/ac4-cl-uic.txt" | awk '{n=gsub(/\|/,"|"); if(n!=7)print "  FARK sütun: " substr($0,1,12) " | sayısı " n}' > "$OUT/ac4-sutun.txt"
cat "$OUT/ac4-sutun.txt"
esit "7 '|' olmayan UIC satırı" "$(wc -l < "$OUT/ac4-sutun.txt" | tr -d ' ')" 0 || e=1
sonuc AC-4 zorunlu $e

# AC-5 (paket :666) — uic-auto.sh çıktısında RC-1…RC-24 birer kez ve her birinin exit= satırı (koşunun verify'ı ve bağlı commit'i kayıttadır)
echo "AC-5"
e=0
i=1
while [ $i -le 24 ]; do
  n=$(grep -c "^RC-$i\$" "$OUT/uic-auto.out")
  if [ "$n" != 1 ]; then echo "  FARK RC-$i başlığı $n kez"; e=1; fi
  i=$((i + 1))
done
esit "exit= satırı" "$(say '^exit=' "$OUT/uic-auto.out")" 24 || e=1
sonuc AC-5 zorunlu $e

# AC-7 (paket :668) — Golden Slim doğrulayıcısı ölçüldüğü gibi: RC-3 bloğunda FU-4 kırmızısı ve exit=1
echo "AC-7"
e=0
sed -n '/^RC-3$/,/^exit=/p' "$OUT/uic-auto.out" > "$OUT/ac7-rc3.txt"
esit "RC-3 [PASS]" "$(say '\[PASS\]' "$OUT/ac7-rc3.txt")" 97 || e=1
esit "RC-3 [FAIL]" "$(say '\[FAIL\]' "$OUT/ac7-rc3.txt")" 1 || e=1
satir_icerir "$OUT/ac7-rc3.txt" 'personalizationClient sends tenant header only for tenant users' || e=1
esit "RC-3 exit" "$(tail -n 1 "$OUT/ac7-rc3.txt")" 'exit=1' || e=1
sonuc AC-7 zorunlu $e

# AC-6 (paket :667) — manual.md'de 10 elle madde, biçim UIC-n | sonuç | kanıt | inceleyen rol | tarih
echo "AC-6"
e=0
M=$B/manual.md
dosya "$M" || e=1
satir_icerir "$M" '| UIC | sonuç | kanıt | inceleyen rol | tarih |' || e=1
for u in 8 9 10 11 12 15 16 18 23 28; do
  n=$(grep -c "^| UIC-$u |" "$M")
  if [ "$n" != 1 ]; then echo "  FARK UIC-$u satırı $n kez"; e=1; continue; fi
  s=$(grep "^| UIC-$u |" "$M" | awk -F' [|] ' '{print $2}')
  case "$s" in
    uygun|"uygun değil"|kısmi|ölçülmedi*) echo "  UIC-$u: $(echo "$s" | cut -c1-40)";;
    *) echo "  FARK UIC-$u sonuç hücresi: $s"; e=1;;
  esac
done
sonuc AC-6 zorunlu $e

# AC-8 (paket :669) — RC-11 dört koşul
echo "AC-8"
e=0
sed -n '/^RC-11$/,/^exit=/p' "$OUT/uic-auto.out" > "$OUT/ac8-rc11.txt"
cat "$OUT/ac8-rc11.txt"
rcl=$(sed -n 2p "$OUT/ac8-rc11.txt")
cr=$(sed -n 3p "$OUT/ac8-rc11.txt" | tr -d ' ')
m86=$(sed -n 4p "$OUT/ac8-rc11.txt" | tr -d ' ')
lsf=$(sed -n 5p "$OUT/ac8-rc11.txt" | tr -d ' ')
dosya "$OUT/navkeys.out" || e=1
case "$rcl" in
  rc=0) echo "  (1) rc=0";;
  rc=1)
    grep -v 'key(s) off form' "$OUT/navkeys.out" > "$OUT/ac8-bulgular.txt"
    n=$(grep -v -e 'MOD-0176-expiry-fefo-management.md:645:' -e 'MOD-0177-recall-readiness.md:915:' "$OUT/ac8-bulgular.txt" | wc -l | tr -d ' ')
    esit "(1) rc=1 iken bilinen iki satır dışındaki bulgu" "$n" 0 || e=1;;
  *) echo "  FARK (1) $rcl"; e=1;;
esac
esit "(2) could not read" "$cr" 0 || e=1
esit "(3) MOD-0286" "$m86" 0 || e=1
esit "(4) git ls-files" "$lsf" 1 || e=1
sonuc AC-8 zorunlu $e

# AC-9 (paket :670)
echo "AC-9"
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286 --name "UI Design System & Page Pattern Governance"
g=$?
echo "exit=$g"
sonuc AC-9 zorunlu $g

# AC-10 (paket :671)
echo "AC-10"
e=0
esit "docs/ kökündeki dosya" "$(find docs -maxdepth 1 -type f ! -name README.md ! -name .DS_Store | wc -l | tr -d ' ')" 0 || e=1
sonuc AC-10 zorunlu $e

# AC-11 (paket :672)
echo "AC-11"
e=0
for p in frontend/Diten.Web/Views/Platform/UiGovernance services/Diten.Platform/src/Diten.Platform.Application/Features/UiGovernance; do
  if [ -e "$p" ]; then echo "  FARK var: $p"; e=1; else echo "  yok: $p"; fi
done
sonuc AC-11 zorunlu $e

# AC-13 (paket :674, isteğe bağlı) — sabotage-01 kaydı
echo "AC-13"
e=0
python3 scripts/run_record.py verify "$B/sabotage-01/run-record-v1.json"; g=$?
echo "  verify exit=$g"; [ $g -eq 0 ] || e=1
satir_icerir "$B/sabotage-01/stdout.txt" '[FAIL] _IndexL10n.cshtml exists' || e=1
satir_icerir "$B/sabotage-01/stdout.txt" '[FAIL] index.l10n.js exists' || e=1
sonuc AC-13 isteğe-bağlı $e

# AC-15 (paket :676; tablo paket :290-300, toplam :302)
echo "AC-15"
e=0
esit "## Kapsama tablosu başlığı" "$(say '^## Kapsama tablosu$' "$CL")" 1 || e=1
sed -n '290,300p' "$PK" > "$OUT/ac15-pk.txt"
esit "paket tablo satırı" "$(grep -c '^| `' "$OUT/ac15-pk.txt")" 11 || e=1
awk '/^## Kapsama tablosu$/{f=1;next} /^## /{f=0} f && /^\| `/' "$CL" > "$OUT/ac15-cl.txt"
if diff "$OUT/ac15-pk.txt" "$OUT/ac15-cl.txt" > "$OUT/ac15.diff"; then echo "  tablo satırları paketle aynı"; else echo "  FARK kapsama satırları:"; cat "$OUT/ac15.diff"; e=1; fi
satir_icerir "$CL" 'Total 94 phrases: 57 covered, 6 meta, 31 not covered' || e=1
while IFS='|' read -r _ f n _; do
  f=$(echo "$f" | tr -d ' `'); n=$(echo "$n" | tr -d ' ')
  m=$(grep -n -i 'zorunlu' ".antigravity/rules/$f" | wc -l | tr -d ' ')
  esit "zorunlu $f" "$m" "$n" || e=1
done < "$OUT/ac15-pk.txt"
sonuc AC-15 zorunlu $e

# AC-16 (paket :677) — 22 otomatik UIC kimliği en az bir RC satırının "UIC kapsamı" sütununda
echo "AC-16"
e=0
awk -F'|' '/^\| RC-/{print $3}' "$CL" | grep -oE 'UIC-[0-9]+' | sort -u > "$OUT/ac16-kapsam.txt"
for u in 1 2 3 4 5 6 7 13 14 17 19 20 21 22 24 25 26 27 29 30 31 32; do
  if grep -q "^UIC-$u\$" "$OUT/ac16-kapsam.txt"; then :; else echo "  EKSİK UIC-$u"; e=1; fi
done
sonuc AC-16 zorunlu $e

# CT-1 — eski künye ve taslak ifadeleri (prompt DOĞRULA 8b)
echo "CT-1"
e=0
for f in "$SP" "$CL"; do
  dosya "$f" || e=1
  grep -n -i -e '7f6868d9' -e 'revizyon 06' -e 'taslak' -e 'status: draft' -e 'ayrı iş paketleri' -e 'sahip PR açmadı' -- "$f" > "$OUT/ct1.txt"; g=$?
  if [ $g -eq 1 ]; then echo "  temiz: $f"; elif [ $g -eq 0 ]; then echo "  FARK $f:"; cat "$OUT/ct1.txt"; e=1; else echo "  HATA grep $g: $f"; e=1; fi
done
sonuc CT-1 zorunlu $e

# CT-2 — paket dosyası değişmedi (onay satırı bu sha256'ya bağlı)
echo "CT-2"
e=0
h=$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" "$PK")
esit "paket sha256" "$h" "$PK_SHA" || e=1
sonuc CT-2 zorunlu $e

# CT-3 — iş paketinin commit'i yalnız izinli belgeleri değiştirir
echo "CT-3"
e=0
git diff --name-only HEAD^ HEAD > "$OUT/ct3.txt"; g=$?
[ $g -eq 0 ] || { echo "  HATA git diff $g"; e=1; }
cat "$OUT/ct3.txt"
n=$(grep -v -e "^$SP\$" -e "^$CL\$" -e "^$B/README-p1-03.md\$" "$OUT/ct3.txt" | wc -l | tr -d ' ')
esit "izinli dışında değişen dosya" "$n" 0 || e=1
sonuc CT-3 zorunlu $e

echo "ÖZET"
cat "$R"
gecti=0
for k in $ZORUNLU; do
  n=$(grep -c "^$k zorunlu geçti\$" "$R")
  if [ "$n" = 1 ]; then gecti=$((gecti + 1)); else echo "  zorunlu $k geçmedi"; fi
done
echo "zorunlu geçti $gecti / 17"
if [ $gecti -eq 17 ]; then exit 0; fi
exit 1
