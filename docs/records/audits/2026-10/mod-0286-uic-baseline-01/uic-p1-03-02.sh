# MOD-0286 PSS-UI-P1-03 kanıt betiği, sürüm 02 — Control Tower yazdı; iş paketi bu dosyayı değiştirmez.
# Sürüm 01 (uic-p1-03.sh, d3a32783a) prompt incelemesi v1.3'te reddedildi: 2f/2h ölçülmüyordu, AC-3 hücre değil satır
# arıyordu, AC-5 yalnız yapıyı sayıyordu, CT-3/AC-2 yalnız HEAD^..HEAD'e bakıyordu. Bu sürüm dördünü de ölçer.
# Kullanım (depo kökünden): sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-p1-03-02.sh <çıktı-dizini>
# Kayıtlı koşu: python3 scripts/run_record.py run --out <B>/auto-02 -- sh <B>/uic-p1-03-02.sh <B>/auto-02
# Yalnız POSIX sh; yazdığı her dosya <çıktı-dizini> altındadır.
# 1) uic-auto.sh'yi olduğu gibi koşar (RC-1…RC-24; toplam çıkışı yazılır, paket AC-5 gereği yargılanmaz).
# 2) Paket AC-1…AC-16 (AC-14 hariç: elle) ve CT-1…CT-3'ü ölçer; her blok "<kimlik> sonuç=geçti|kaldı (<tür>)" yazar.
# 3) Çıkış 0 yalnız 17 zorunlu kimliğin her biri tam bir kez "geçti" yazdıysa; aksi 1. Kullanım hatası 2.
# İşin tabanı (BASE): Control Tower'ın kırmızı taban koşusunun commit'i = p1-03-taban-02/run-record-v1.json'u ekleyen
# son commit. İş paketinin değişikliği BASE..HEAD aralığıdır (tam bir commit). BASE yoksa CT-3 kalır.
OUT="$1"
if [ -z "$OUT" ] || [ ! -d "$OUT" ]; then echo "kullanım: sh uic-p1-03-02.sh <var olan çıktı-dizini>"; exit 2; fi
LC_ALL=C; export LC_ALL
B=docs/records/audits/2026-10/mod-0286-uic-baseline-01
D=docs/reference/modules/platform/ui-design-system-page-pattern-governance
SP=$D/spec.md
CL=$D/compliance-checklist.md
PK=execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
PK_SHA=89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44
ZORUNLU="AC-1 AC-2 AC-3 AC-4 AC-5 AC-6 AC-7 AC-8 AC-9 AC-10 AC-11 AC-12 AC-15 AC-16 CT-1 CT-2 CT-3"
BASE=$(git log -n 1 --format=%H -- "$B/p1-03-taban-02/run-record-v1.json")
R="$OUT/sonuc.txt"
: > "$R"
UA_OUT="$OUT/uic-auto.out"

sonuc() { # $1 kimlik, $2 zorunlu|isteğe-bağlı, $3 hata sayısı (0 = geçti)
  if [ "$3" -eq 0 ] 2>/dev/null; then s=geçti; else s=kaldı; fi
  echo "$1 sonuç=$s ($2)"
  echo "$1 $2 $s" >> "$R"
}
dosya() {
  if [ -f "$1" ]; then return 0; fi
  echo "  HATA dosya yok: $1"; return 1
}
say() { # say <desen> <dosya> — eşleşen satır sayısı; grep hatasında (çıkış 2) "HATA"
  n=$(grep -c -- "$1" "$2"); rc=$?
  if [ $rc -gt 1 ]; then echo HATA; else echo "$n"; fi
}
esit() { # esit <ad> <ölçülen> <beklenen>
  if [ "$2" = "$3" ]; then echo "  $1 = $2 (beklenen $3)"; return 0; fi
  echo "  FARK $1 = $2 (beklenen $3)"; return 1
}
icerir() { # icerir <dosya> <sabit metin>
  if grep -F -q -- "$2" "$1"; then return 0; fi
  echo "  EKSİK: '$2'"; return 1
}
icermez() { # icermez <dosya> <sabit metin> — grep çıkışı 1 olmalı (2 = hata)
  grep -F -q -- "$2" "$1"; g=$?
  if [ $g -eq 1 ]; then return 0; fi
  if [ $g -eq 0 ]; then echo "  VAR (olmamalı): '$2'"; else echo "  HATA grep $g: $1"; fi
  return 1
}
eslesir() { # eslesir <dosya> <ERE>
  if grep -E -q -- "$2" "$1"; then return 0; fi
  echo "  EKSİK desen: $2"; return 1
}
blok() { # blok <n> — uic-auto çıktısında RC-n bloğu (başlık hariç, exit= dahil); mutlak depo yolu <KÖK> olur
  awk -v k="RC-$1" '$0==k{f=1;next} f{print} f&&/^exit=/{exit}' "$UA_OUT" | sed "s#$(pwd)/#<KÖK>/#g"
}
hucre() { # hucre <satır dosyası> <alan no> — kaçışlı \| korunarak tablo hücresi (2 = ilk sütun)
  awk -v n="$2" '{gsub(/\\\|/, "\001"); split($0, a, "|"); s = a[n]; gsub(/\001/, "\\|", s); print s}' "$1"
}

echo "UIC-AUTO"
sh "$B/uic-auto.sh" "$OUT" > "$UA_OUT" 2>&1
UA=$?
cat "$UA_OUT"
echo "uic-auto exit=$UA"
echo "BASE=$BASE"

# AC-1 (paket :662)
echo "AC-1"
e=0
dosya "$SP" || e=1
esit "## başlık sayısı" "$(say '^## ' "$SP")" 9 || e=1
awk '/^## /{if(h!="")print c"\t"h; h=$0; c=0; next} h!=""{c+=gsub(/\.antigravity\/rules\/[A-Za-z0-9_.-]+\.md:[0-9]+|§[0-9]+/,"&")} END{if(h!="")print c"\t"h}' "$SP" > "$OUT/ac1-basliklar.txt"
cat "$OUT/ac1-basliklar.txt"
esit "atıfsız başlık" "$(awk -F'\t' '$1==0' "$OUT/ac1-basliklar.txt" | wc -l | tr -d ' ')" 0 || e=1
t=$(say '\.antigravity/rules/' "$SP")
if [ "$t" = HATA ] || [ "$t" -lt 9 ]; then echo "  FARK .antigravity/rules/ satırı = $t (beklenen >= 9)"; e=1; else echo "  .antigravity/rules/ satırı = $t (>= 9)"; fi
sonuc AC-1 zorunlu $e

# AC-2 (paket :663) ve AC-12 (paket :673) — DL-1/DL-2 commit'leri ve işin BASE..HEAD aralığındaki her commit
echo "AC-2"
e=0; e12=0
if [ -n "$BASE" ]; then ARALIK=$(git rev-list "$BASE..HEAD"); else ARALIK=$(git rev-parse HEAD); fi
for c in a0a4050b0 3fbf891bb bb3464954 d3105a332 $ARALIK; do
  git diff --name-only "$c^" "$c" -- .antigravity services frontend gateway execution/registries > "$OUT/ac2.txt"; g=$?
  n=$(wc -l < "$OUT/ac2.txt" | tr -d ' ')
  echo "  $(echo "$c" | cut -c1-9): git çıkış $g, değişen $n"
  if [ $g -ne 0 ] || [ "$n" != 0 ]; then e=1; cat "$OUT/ac2.txt"; fi
  git diff --name-only "$c^" "$c" -- .antigravity/rules/module-pack-standard.md > "$OUT/ac12.txt"; g=$?
  if [ $g -ne 0 ] || [ -s "$OUT/ac12.txt" ]; then e12=1; echo "  FARK module-pack-standard.md $c"; fi
done
sonuc AC-2 zorunlu $e
echo "AC-12"
esit "paket FU-1 satırı" "$(say '^| FU-1 |' "$PK")" 1 || e12=1
icermez "$SP" 'UI uyum: UIC-' || e12=1
icermez "$CL" 'UI uyum: UIC-' || e12=1
sonuc AC-12 zorunlu $e12

# AC-3 (paket :664) — §9 satırları; hücre hücre: 2 = #, 3 = taraflar, 8 = durum; taraflar paket :773-776
echo "AC-3"
e=0
awk '/^## 9\./{f=1} f && /^\| [1-5] \|/' "$SP" > "$OUT/ac3-satirlar.txt"
esit "§9 satır sayısı (1-5)" "$(wc -l < "$OUT/ac3-satirlar.txt" | tr -d ' ')" 5 || e=1
i=1
while [ $i -le 5 ]; do
  grep "^| $i |" "$OUT/ac3-satirlar.txt" > "$OUT/ac3-$i.txt"
  esit "satır $i '|' sayısı (7 sütun)" "$(sed 's/\\|//g' "$OUT/ac3-$i.txt" | tr -cd '|' | wc -c | tr -d ' ')" 8 || e=1
  hucre "$OUT/ac3-$i.txt" 3 > "$OUT/ac3-$i-taraf.txt"
  hucre "$OUT/ac3-$i.txt" 8 > "$OUT/ac3-$i-durum.txt"
  echo "  satır $i durum:$(cat "$OUT/ac3-$i-durum.txt" | cut -c1-90)"
  i=$((i + 1))
done
for i in 1 2 3; do
  icerir "$OUT/ac3-$i-durum.txt" 'karar: kuralın kendisinde düzeltilir' || e=1
  icermez "$OUT/ac3-$i-durum.txt" 'karar bekliyor' || e=1
done
icerir "$OUT/ac3-4-durum.txt" 'karar bekliyor' || e=1
for i in 1 2 3 4; do icermez "$OUT/ac3-$i-durum.txt" 'çözüldü' || e=1; done
# satır 1: premium-modal-standard.md:27-28,35-36,43-44,51-52; frontend-js-standard.md:145; frontend-standards.md:194
eslesir "$OUT/ac3-1-taraf.txt" 'premium-modal-standard\.md:27-28(, ?:?)35-36(, ?:?)43-44(, ?:?)51-52' || e=1
icerir "$OUT/ac3-1-taraf.txt" 'frontend-js-standard.md:145' || e=1
icerir "$OUT/ac3-1-taraf.txt" 'frontend-standards.md:194' || e=1
# satır 2: frontend-js-standard.md:53; frontend-datatable-template.md:87, :100, :114; product-backlog.md:6199, :6264
icerir "$OUT/ac3-2-taraf.txt" 'frontend-js-standard.md:53' || e=1
eslesir "$OUT/ac3-2-taraf.txt" 'frontend-datatable-template\.md:87`?(, ?`?:?)100`?(, ?`?:?)114' || e=1
icerir "$OUT/ac3-2-taraf.txt" 'product-backlog.md:6199' || e=1
eslesir "$OUT/ac3-2-taraf.txt" ':6264([^0-9]|$)' || e=1
# satır 3: frontend-standards.md:208; module-pack-standard.md:88; views-organization.md:39
for x in 'frontend-standards.md:208' 'module-pack-standard.md:88' 'views-organization.md:39'; do icerir "$OUT/ac3-3-taraf.txt" "$x" || e=1; done
# satır 4: premium-modal-standard.md:1,11; dynamic-localization-standard.md:2,5,181; ARCHITECTURE.md:234; debug.md:59; AGENTS.md:139
eslesir "$OUT/ac3-4-taraf.txt" 'premium-modal-standard\.md:1(, ?:?)11([^0-9]|$)' || e=1
eslesir "$OUT/ac3-4-taraf.txt" 'dynamic-localization-standard\.md:2(, ?:?)5(, ?:?)181([^0-9]|$)' || e=1
for x in '.antigravity/ARCHITECTURE.md:234' '.antigravity/workflows/debug.md:59' 'AGENTS.md:139'; do icerir "$OUT/ac3-4-taraf.txt" "$x" || e=1; done
sonuc AC-3 zorunlu $e

# AC-4 (paket :665)
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
if diff "$OUT/ac4-pk-uic.txt" "$OUT/ac4-cl-uic.txt" > "$OUT/ac4-uic.diff"; then echo "  UIC satırları paketle aynı"; else echo "  FARK UIC satırları:"; grep -oE '^[<>] \| UIC-[0-9]+' "$OUT/ac4-uic.diff"; e=1; fi
if diff "$OUT/ac4-pk-rc.txt" "$OUT/ac4-cl-rc.txt" > "$OUT/ac4-rc.diff"; then echo "  RC satırları paketle aynı"; else echo "  FARK RC satırları:"; grep -oE '^[<>] \| RC-[0-9]+' "$OUT/ac4-rc.diff"; e=1; fi
sed 's/\\|//g' "$OUT/ac4-cl-uic.txt" | awk '{n=gsub(/\|/,"|"); if(n!=7)print "  FARK sütun: " substr($0,1,12) " | sayısı " n}' > "$OUT/ac4-sutun.txt"
cat "$OUT/ac4-sutun.txt"
esit "7 '|' olmayan UIC satırı" "$(wc -l < "$OUT/ac4-sutun.txt" | tr -d ' ')" 0 || e=1
sonuc AC-4 zorunlu $e

# AC-5 (paket :666) — her RC satırı paketin "Expected stdout / exit" sütununa (paket :220-243) karşı
echo "AC-5"
e=0
i=1
while [ $i -le 24 ]; do
  n=$(grep -c "^RC-$i\$" "$UA_OUT")
  if [ "$n" != 1 ]; then echo "  FARK RC-$i başlığı $n kez"; e=1; fi
  i=$((i + 1))
done
esit "exit= satırı" "$(say '^exit=' "$UA_OUT")" 24 || e=1
# Birebir beklenen bloklar (paket satırı yorumda). RC-6: paket :225 kuralı `1` der, fikstür eksik ölçülür `0` (FU-12, "quotes the 0 as measured").
bekle() { # bekle <n> — stdin'deki beklenen blokla karşılaştır
  cat > "$OUT/rc$1-beklenen.txt"
  blok "$1" > "$OUT/rc$1.txt"
  if diff "$OUT/rc$1-beklenen.txt" "$OUT/rc$1.txt" > "$OUT/rc$1.diff"; then echo "  RC-$1 beklenenle aynı"; return 0; fi
  echo "  FARK RC-$1:"; cat "$OUT/rc$1.diff"; return 1
}
printf 'exit=0\n' | bekle 1 || e=1                                   # :220 boş
printf '1\n0\nexit=1\n' | bekle 2 || e=1                             # :221
printf '0\n1\nexit=0\n' | bekle 4 || e=1                             # :223
printf '0\n1\nexit=0\n' | bekle 5 || e=1                             # :224
{ for l in en tr fr es zh ar ru; do printf '%s ok\n6\n0\n12\n' $l; done; printf 'marker ok\nen ok\ntr ok\nexit=0\n'; } | bekle 6 || e=1   # :225
printf '0\nexit=1\n' | bekle 7 || e=1                                # :226 boş, sonra 0
printf '2\n1\nexit=0\n' | bekle 8 || e=1                             # :227 boş, 2, 1
printf 'frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json:2\nfrontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json:2\nexit=0\n' | bekle 9 || e=1   # :228
printf 'exit=1\n' | bekle 10 || e=1                                  # :229 boş
printf '1\n0\n2\nexit=0\n' | bekle 13 || e=1                         # :232
printf '6\n16:form_field_count: 6\nexit=0\n' | bekle 15 || e=1       # :234
printf '10\n5\nexit=0\n' | bekle 16 || e=1                           # :235
printf '1\n0\nexit=1\n' | bekle 19 || e=1                            # :238
printf '0\nexit=1\n' | bekle 20 || e=1                               # :239
{ for l in 1 2 3 4 5 6 7; do echo 2; done; echo exit=0; } | bekle 21 || e=1   # :240
{ echo 1; echo '            ["USERS"] = [new("USERROLES", "bx-shield-quarter"), new("ROLES", "bx-id-card")],'; echo 'codes=USERROLES ROLES'
  for l in en tr fr es zh ar ru; do printf '%s USERROLES=1\n%s ROLES=1\n%s related_total=2\n%s k=5 range_ok=yes\n%s seq_diff=empty\n%s title_diff=empty\n' $l $l $l $l $l $l; done; echo exit=0; } | bekle 23 || e=1   # :242
blok 24 > "$OUT/rc24.txt"                                            # :243 her dilde new_missing=0 new_equal=0
esit "RC-24 dil satırı" "$(grep -c '^[a-z][a-z] missing=' "$OUT/rc24.txt")" 6 || e=1
esit "RC-24 new_missing=0 new_equal=0 satırı" "$(grep -c ' new_missing=0 new_equal=0 ' "$OUT/rc24.txt")" 6 || e=1
# Kural satırları
blok 3 > "$OUT/rc3.txt"                                              # :222 bugün exit 1, 97 [PASS] / 1 [FAIL] FU-4
esit "RC-3 [PASS]" "$(say '\[PASS\]' "$OUT/rc3.txt")" 97 || e=1
esit "RC-3 [FAIL]" "$(say '\[FAIL\]' "$OUT/rc3.txt")" 1 || e=1
icerir "$OUT/rc3.txt" '[FAIL] personalizationClient sends tenant header only for tenant users' || e=1
icerir "$OUT/rc3.txt" 'index.js calls DitenDataTable.createList' || e=1
esit "RC-3 exit" "$(tail -n 1 "$OUT/rc3.txt")" 'exit=1' || e=1
for k in 12 14; do                                                   # :231, :233 exit 1, tek numaralı açık FU-4
  blok $k > "$OUT/rc$k.txt"
  esit "RC-$k numaralı açık" "$(grep -c '^[0-9][0-9]*) ' "$OUT/rc$k.txt")" 1 || e=1
  icerir "$OUT/rc$k.txt" '1) personalizationClient sends tenant header only for tenant users' || e=1
  esit "RC-$k exit" "$(tail -n 1 "$OUT/rc$k.txt")" 'exit=1' || e=1
done
blok 17 > "$OUT/rc17.txt"                                            # :236 1, 1, öznitelik satırı, 2
esit "RC-17 satır 1" "$(sed -n 1p "$OUT/rc17.txt")" 1 || e=1
esit "RC-17 satır 2" "$(sed -n 2p "$OUT/rc17.txt")" 1 || e=1
sed -n 3p "$OUT/rc17.txt" > "$OUT/rc17-3.txt"
icerir "$OUT/rc17-3.txt" "title: l.Filter, 'aria-controls': collapseId, 'aria-expanded': 'false'" || e=1
esit "RC-17 satır 4" "$(sed -n 4p "$OUT/rc17.txt")" 2 || e=1
esit "RC-17 exit" "$(tail -n 1 "$OUT/rc17.txt")" 'exit=0' || e=1
blok 18 > "$OUT/rc18.txt"                                            # :237 yalnız _CreateEditOffcanvas.cshtml:88 data-dt-primary="enter"
esit "RC-18 satır" "$(wc -l < "$OUT/rc18.txt" | tr -d ' ')" 2 || e=1
icerir "$OUT/rc18.txt" 'GoldenReferenceSlim/_CreateEditOffcanvas.cshtml:88:' || e=1
icerir "$OUT/rc18.txt" 'data-dt-primary="enter"' || e=1
blok 22 > "$OUT/rc22.txt"                                            # :241 Title Case 0 iki bağlamda; rc 0|1; .out dolu
esit "RC-22 rc satırı (0|1)" "$(grep -c '^rc=[01]$' "$OUT/rc22.txt")" 2 || e=1
esit "RC-22 Title Case (tenant)" "$(sed -n 2p "$OUT/rc22.txt")" 0 || e=1
esit "RC-22 Title Case (platform)" "$(sed -n 6p "$OUT/rc22.txt")" 0 || e=1
for f in resx-tenant.out resx-platform.out; do if [ -s "$OUT/$f" ]; then echo "  $f dolu"; else echo "  FARK $f boş"; e=1; fi; done
sonuc AC-5 zorunlu $e

# AC-7 (paket :668) — Golden Slim doğrulayıcısı ölçüldüğü gibi (RC-3)
echo "AC-7"
e=0
esit "RC-3 [FAIL] satırı FU-4" "$(grep -c '\[FAIL\] personalizationClient sends tenant header only for tenant users' "$OUT/rc3.txt")" 1 || e=1
esit "RC-3 exit" "$(tail -n 1 "$OUT/rc3.txt")" 'exit=1' || e=1
sonuc AC-7 zorunlu $e

# AC-6 (paket :667)
echo "AC-6"
e=0
M=$B/manual.md
dosya "$M" || e=1
icerir "$M" '| UIC | sonuç | kanıt | inceleyen rol | tarih |' || e=1
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

# AC-8 (paket :669)
echo "AC-8"
e=0
blok 11 > "$OUT/rc11.txt"
cat "$OUT/rc11.txt"
rcl=$(sed -n 1p "$OUT/rc11.txt")
dosya "$OUT/navkeys.out" || e=1
case "$rcl" in
  rc=0) echo "  (1) rc=0";;
  rc=1)
    n=$(grep -v 'key(s) off form' "$OUT/navkeys.out" | grep -v -e 'MOD-0176-expiry-fefo-management.md:645:' -e 'MOD-0177-recall-readiness.md:915:' | wc -l | tr -d ' ')
    esit "(1) rc=1 iken bilinen iki satır dışındaki bulgu" "$n" 0 || e=1;;
  *) echo "  FARK (1) $rcl"; e=1;;
esac
esit "(2) could not read" "$(sed -n 2p "$OUT/rc11.txt" | tr -d ' ')" 0 || e=1
esit "(3) MOD-0286" "$(sed -n 3p "$OUT/rc11.txt" | tr -d ' ')" 0 || e=1
esit "(4) git ls-files" "$(sed -n 4p "$OUT/rc11.txt" | tr -d ' ')" 1 || e=1
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

# AC-13 (paket :674, isteğe bağlı)
echo "AC-13"
e=0
python3 scripts/run_record.py verify "$B/sabotage-01/run-record-v1.json"; g=$?
echo "  verify exit=$g"; [ $g -eq 0 ] || e=1
icerir "$B/sabotage-01/stdout.txt" '[FAIL] _IndexL10n.cshtml exists' || e=1
icerir "$B/sabotage-01/stdout.txt" '[FAIL] index.l10n.js exists' || e=1
sonuc AC-13 isteğe-bağlı $e

# AC-15 (paket :676; tablo paket :290-300, toplam :302) — satırlar paketle birebir; yeniden ölçülen N paketle aynı olmalı
echo "AC-15"
e=0
esit "## Kapsama tablosu başlığı" "$(say '^## Kapsama tablosu$' "$CL")" 1 || e=1
sed -n '290,300p' "$PK" > "$OUT/ac15-pk.txt"
esit "paket tablo satırı" "$(grep -c '^| `' "$OUT/ac15-pk.txt")" 11 || e=1
awk '/^## Kapsama tablosu$/{f=1;next} /^## /{f=0} f && /^\| `/' "$CL" > "$OUT/ac15-cl.txt"
if diff "$OUT/ac15-pk.txt" "$OUT/ac15-cl.txt" > "$OUT/ac15.diff"; then echo "  tablo satırları paketle aynı"; else echo "  FARK kapsama satırları:"; cat "$OUT/ac15.diff"; e=1; fi
icerir "$CL" 'Total 94 phrases: 57 covered, 6 meta, 31 not covered' || e=1
while IFS='|' read -r _ f n _; do
  f=$(echo "$f" | tr -d ' `'); n=$(echo "$n" | tr -d ' ')
  esit "zorunlu $f" "$(grep -n -i 'zorunlu' ".antigravity/rules/$f" | wc -l | tr -d ' ')" "$n" || e=1
done < "$OUT/ac15-pk.txt"
sonuc AC-15 zorunlu $e

# AC-16 (paket :677)
echo "AC-16"
e=0
awk -F'|' '/^\| RC-/{print $3}' "$CL" | grep -oE 'UIC-[0-9]+' | sort -u > "$OUT/ac16-kapsam.txt"
for u in 1 2 3 4 5 6 7 13 14 17 19 20 21 22 24 25 26 27 29 30 31 32; do
  if grep -q "^UIC-$u\$" "$OUT/ac16-kapsam.txt"; then :; else echo "  EKSİK UIC-$u"; e=1; fi
done
sonuc AC-16 zorunlu $e

# CT-1 — eski künye, taslak ve rev 06 ifadeleri (prompt Adım 2b, 2d, 2f, 2h)
echo "CT-1"
e=0
for f in "$SP" "$CL"; do
  dosya "$f" || e=1
  grep -n -i -e '7f6868d9' -e 'revizyon 06' -e 'taslak' -e 'status: draft' -e 'ayrı iş paketleri' -e 'sahip PR açmadı' \
    -e 'karar gelene dek' -e 'dört satır' -e 'FU-17' -e 'kabul edildi' -e 'paket:147-178' -e 'paket:512-520' -- "$f" > "$OUT/ct1.txt"; g=$?
  if [ $g -eq 1 ]; then echo "  eski ifade yok: $f"; elif [ $g -eq 0 ]; then echo "  FARK $f:"; cut -c1-160 "$OUT/ct1.txt"; e=1; else echo "  HATA grep $g: $f"; e=1; fi
done
# paket satır atıfları yalnız rev 12'nin iki çapası: DL-2 tablosu paket:156-200, çatışma kaydı paket:769-777
grep -o -h 'paket:[0-9][0-9-]*' "$SP" "$CL" | sort -u > "$OUT/ct1-atif.txt"
cat "$OUT/ct1-atif.txt"
esit "izinli dışı paket: atfı" "$(grep -v -x -e 'paket:156-200' -e 'paket:769-777' "$OUT/ct1-atif.txt" | wc -l | tr -d ' ')" 0 || e=1
esit "paket:156-200 atfı" "$(grep -c -x 'paket:156-200' "$OUT/ct1-atif.txt")" 1 || e=1
esit "paket:769-777 atfı" "$(grep -c -x 'paket:769-777' "$OUT/ct1-atif.txt")" 1 || e=1
# P2 geçen her satır ayrı paketi (D-4) adlandırır
grep -n 'P2' "$SP" > "$OUT/ct1-p2.txt"
esit "D-4 demeyen P2 satırı" "$(grep -v 'D-4' "$OUT/ct1-p2.txt" | wc -l | tr -d ' ')" 0 || { grep -v 'D-4' "$OUT/ct1-p2.txt" | cut -c1-120; e=1; }
sonuc CT-1 zorunlu $e

# CT-2 — paket dosyası değişmedi
echo "CT-2"
e=0
esit "paket sha256" "$(python3 -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest())" "$PK")" "$PK_SHA" || e=1
sonuc CT-2 zorunlu $e

# CT-3 — işin aralığı BASE..HEAD tam bir commit ve yalnız izinli belgeleri değiştirir
echo "CT-3"
e=0
if [ -z "$BASE" ]; then
  echo "  FARK BASE yok (p1-03-taban-02 kaydı commit'li değil)"; e=1
else
  esit "BASE..HEAD commit" "$(git rev-list --count "$BASE..HEAD")" 1 || e=1
  git diff --name-only "$BASE" HEAD > "$OUT/ct3.txt"; g=$?
  [ $g -eq 0 ] || { echo "  HATA git diff $g"; e=1; }
  cat "$OUT/ct3.txt"
  esit "değişen dosya" "$(wc -l < "$OUT/ct3.txt" | tr -d ' ')" "$(grep -c -x -e "$SP" -e "$CL" -e "$B/README-p1-03.md" "$OUT/ct3.txt")" || e=1
  icerir "$OUT/ct3.txt" "$SP" || e=1
  icerir "$OUT/ct3.txt" "$CL" || e=1
fi
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
