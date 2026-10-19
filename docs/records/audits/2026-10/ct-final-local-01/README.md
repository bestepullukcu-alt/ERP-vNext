# Control Tower işleri — güncel yerel kapanış envanteri ve akşam yayın listesi (2026-10-16)

Bu dosya `ct-local-closure-inventory-2026-10-16.md`'nin yerine geçer. O dosya `afb5beeca` ölçümüdür, tarihseldir ve
değiştirilmedi.

**Ölçüm noktası.** Dal `chore/ct/pqg-01` @ `2121420dd`. Yerel `origin/main` `3912d568b`; uzak `main` de `3912d568b`
(Codex, `git ls-remote`, 2026-10-16T09:16:03Z — yalnız o an; fetch yok). Birleşme tabanı `6b8a62d5a`: 26 commit önde, 29
geride; 120 dosya, bunların 12'si `.antigravity` altında. Bu klasörü ekleyen commit yalnız kayıt ekler, test edilen ağaç
değildir.

## 1. Yetki ve bekleyen tek karar

- **Yerel uygulama, test ve commit — `.antigravity` dahil:** sahibin yetkisi
  (`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`, Codex aktardı). Sahip 2026-10-16'da bu yetkiyi kapanış Control
  Tower'ına doğrudan teyit etti (ControlTower (Legacy) bildirdi).
- **İ4 S2 seçimi:** sahip seçimi ve yerel uygulamayı Codex ile AI oturumuna bıraktı (`CT-I4-S2-JOINT-DECISION-01`,
  Codex aktardı).
- **Ortak dosyalar:** her pakette kapanış Control Tower'ının satır düzeyli tek-yazar onayı alındı; iki pakette onay önce
  Codex'in aktarımıyla geldi, sonra Legacy doğrudan teyit etti.
- **Bekleyen tek sahip kararı: yayın** — push, PR, merge, görünürlük. Yeni Control Tower kurallarının `main`'de
  yürürlüğe girmesi yayınla olur.

## 2. Tamamlananlar

| İş | Commit | Durum |
|---|---|---|
| Önceki işler (CT-OPS-01, CT-OPS-02, sahip kayıtları, R8, denetim düzeltmeleri) | `a6082e248` … `e07df6bf1` | bu yetkiden önce; kendi kayıtları geçerli |
| Adım 1 — birleşme öncesi düzeltmeler | `47059c694` · `0b392cb9e` | sahip kabul etti (2026-10-15) |
| Adım 2 — yeni Control Tower açılış paketi + dar İ3 | `543f6ba74` · `171e949f3` · `b5ebb1d96` · `2481004b1` | yerel teknik kabul tamam (Codex) |
| CT-PQG-01 — prompt kalite kapısı | `9e7297000` · `168a46142` | yerel teknik kabul tamam |
| CT-OPS-03 — kabul ve kanıt sözleşmesi (tek "done" otoritesi SOP §29 dahil) | `7597f364c` | yerel teknik kabul tamam |
| CT-OPS-04 — durum raporu yeni kip | `5ecd8b3c1` | yerel teknik kabul tamam |
| CT-PILOT-01 — tek sentetik yaşam döngüsü | `f324e8711` | yerel teknik kabul tamam |
| İ2 (dar) — liste ekranı kancası | `afb5beeca` | yerel teknik kabul tamam |
| Kapanış envanteri (tarihsel) | `bafd9f7ad` · `d2cde8849` | — |
| Doğrulayıcının erken dönüşü | `6188349c8` | yerel teknik kabul tamam |
| Adım 9 paket 2 — beş belge (geniş İ2'nin döngü sınırı ve araç atıfları dahil) | `a75f653ec` | yerel teknik kabul tamam |
| İ4 kayıt kodu kilidi kaydı | `95de60936` | madde açık (sahip) |
| Adım 7 birleşme ön ölçümü | `962093338` | yerel teknik kabul tamam |
| İ4 önerisi ve düzeltmesi | `962093338` · `96b500788` | uygulandı (aşağıda) |
| İ4 S2 — yeni paketin AC'leri kabul kayıtlarına (`module-check`) | `2121420dd` | yerel teknik kabul tamam |

Artık açık sayılmayanlar: **İ1-B** talimat düzeltmeleri `main`'de (`e66404988`); **İ3** çekirdeği adım 2'de; **İ4**'ün
"done" tanımı CT-OPS-03'te, AC bağlantısı S2'de; **dar İ2** ve doğrulayıcının erken dönüşü yukarıda.

## 3. Açık ya da ertelenen

| Madde | Durum |
|---|---|
| İ4 kayıt kodu kilidi | sahibin kararıyla açık (`CT-I4-RECORD-CODE-LOCK-OPEN-01`). RCS-001'de kod kalıcılığı kuralları var; maddenin onları kastettiği NOT VERIFIED |
| AUD-001 ve tarayıcı kontrollerinin birden çok akışta tekrarı | maliyet kanıtı yok; ertelenmiş aday, geliştirme işi değil |
| İ3'ün beyan edilmiş sınırları | paket içerikleri okunmaz; keşif işin kendi kuyruk satırını da bulgu sayar |
| `review_by` tarihi | ayrı politika konusu |
| İ5-B (`88205c3d6`) | bu zincirde değil; hazır durumu Codex ölçtü (`i5b-readonly-status-codex.json`): adaptör `--check` exit 0; `main` ile ve CT zinciriyle ikili `merge-tree` exit 0 — üçlü bütünleşme kanıtı değil. Akşam ayrı yayın kalemi |
| Arındırıcı | scratch yolundaki klonda önce kök normalizasyonu gerekir (kanıtta not) |

## 4. Son yerel ölçümler (bu klasör, `checks.txt`)

| Ölçüm | Sonuç |
|---|---|
| `git merge-tree --write-tree 3912d568b 2121420dd` | exit 0, çakışma yok; ağaç `499ffb0ae7ed67f5252815a6e366e6d3448df9ca` |
| Zincirin 120 dosyası, A (2121420dd) ve B (birleşmiş ağaç) | 118 aynı blob; 2 birleşmiş (`orchestrator.md`, `add-module.md`) |
| `main`'in `e66404988` düzeltmeleri birleşmiş ağaçta | `{ID}.md` paket yolu 0, `ITenantDocument` 0, `GetAccessToken` iki satırda; bizim iki tur sınırı ve SOP §29 satırı da duruyor |
| Altı öz-test, A ve B | ct_queue 69 · acceptance_record 100 · prompt_gate 71 · status_report 75 · kanca 21 · known_failures 24 — vaka satırları birebir aynı |
| `module-check` uçtan uca, birleşmiş ağaçtan başlayan fixture | 17/17 |
| Mimari takım, B üzerinde (tek koşu) | 131/138; the 7 failures are the 7 registered known failures (7 BİLİNEN, exit 0, raw and sanitised); fingerprints as in the 2121420dd run. TRX: clone-root normalisation first, then R1–R4; no personal leftover |
| Yayın öncesi tarama: `6b8a62d5a..2121420dd` eklenen satırlar | 31.779 satır, 12 kalıp (yerel yol, kullanıcı/alan adı, özel anahtar, parola, anahtar, token biçimleri): 0 eşleşme — taranan kalıplarda; her gizli bilginin yokluğunu garanti etmez |
| Araç bağımlılığı | `ct-pqg-01/dependency-check-codex.txt` (temiz venv, PyPI, 2026-10-15); `scripts/requirements-tools.txt` o günden beri değişmedi (sha256 `9078b8f3…`) |

Düzenek notu: B'nin index'i `checkout-index` sonrasında `update-index --refresh` ile tazelendi (ilk `diff-files` exit 1
yalnız stat bilgisiydi); E'deki fixture tabanı, ağaç nesnesi E'nin kendi deposunda yeniden üretilerek kuruldu (ilk
deneme ağacı bulamamıştı). Kaynak her iki düzeltmede de aynı ağaçtır (`499ffb0a…`); yöntem genişletilmedi.

## 5. Akşam yayın kontrol listesi (sahibin kararı; bu belge yetki vermez)

1. Uzak `main`'i yeniden oku. `3912d568b` değilse `merge-tree` ve birleşmiş ağaç testini yenile.
2. Dal `chore/ct/pqg-01`, bu klasörü ekleyen commit; hedef `main`. Beklenen metin çakışması yok: iki tarafın değiştirdiği
   iki dosyada bölgeler örtüşmüyor.
3. `.antigravity` altındaki 12 dosya: `agents/orchestrator.md`, `agents/product-owner.md`, `rules/module-pack-standard.md`,
   `rules/status-reporting-and-evidence.md`, `scripts/list_screen_touch_hook.py`, `scripts/verify_datatable_page.py`,
   `scripts/verify_module_id.py`, `skills/clean-code/SKILL.md`, `skills/lint-and-validate/SKILL.md`,
   `workflows/add-module.md`, `workflows/prepare-module-pack.md`, `workflows/release-checklist.md`.
4. Depo herkese açık: push öncesi son tarama ve PR farkının gözle okunması.
5. PR açıklamasında sınırlar: CI yerelde koşmadı; mimari takımda 7 bilinen hata; ürün kodu yok; kayıt kodu kilidi açık.
6. Merge sonrası: CI, `known_failures.py check`, kapanış Control Tower'ına bildirim; yeni Control Tower kayıt satırları
   `main`'de yürürlüğe girer.
7. İ5-B ayrı kalem: kendi PR'ı ya da sonraya; üçlü bütünleşme ayrıca ölçülür.

## Dosyalar

| Dosya | Ne |
|---|---|
| `checks.txt` | Ölçümlerin çıktıları; yerel yollar `__SCRATCH__`, `__TEST_REPO_B__`, `__HOME__`, `__TMP__` |
| `run.sanitized.trx` | Birleşmiş ağaçta mimari takım; önce klon kökü normalize edildi, sonra R1–R4 |
| `remote-main-observation-codex.json` · `i5b-readonly-status-codex.json` | Codex'in yolsuz özetleri, değiştirilmeden kopyalandı |
