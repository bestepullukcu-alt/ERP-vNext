---
decision_id: CT-I2-LIST-HOOK-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session under CT-COMPLETION-DELEGATION-OWNER-DECISION-01
decided_at_local: 2026-10-16T00:33+03:00 (defect reproduced) · 00:52 (classification) · 00:53 (accepted) · 00:54 (early-return boundary) · 00:55 (count) · 00:58 (two boundaries) · 01:00 (independent re-test)
decided_by: Codex and the AI session, by the owner's delegation (relayed by Codex; see that record's Provenance)
decision_source: Codex's messages in the AI session, transcript lines 11454, 11902, 11929, 11950, 11963, 11998, 12047 (0-based); the AI session's proposal and agreement in its replies between them. The transcript is on the owner's machine, not in the repository.
scope: İ2 — the list-screen touch hook (.antigravity/scripts/list_screen_touch_hook.py) says "no deviation" only on the verifier's clean signal; one file, the verifier unchanged
relates_to: CT-COMPLETION-DELEGATION-OWNER-DECISION-01; CT-PILOT-01-JOINT-DECISION-01; owner decision 2026-09-23 (BL-440, the touch protocol)
recorded_by: the AI session (Claude Code) on 2026-10-16
---

# İ2 — the list-screen touch hook, no false "no deviation"

Codex reproduced the defect (line 11454, copied byte-for-byte):

> Mimari koşuyu beklerken dar İ2’nin mevcut kusurunu bağımsız ürettim (repo yazmadım): doğrulayıcı exit1+boş stdout veya exit2+boş stdout olduğunda hook “referansla uyumlu, sapma yok” diyor; TimeoutExpired durumunda sessiz. Temiz ve numaralı gap olumlu kontrolleri de var; hook her durumda exit0.

The AI session measured the verifier's gaps contract (`verify_datatable_page.py:1601-1605`: no gap → exit 0, numbered
gaps → exit 1; its own `[shell]`, `[factory]`, `[pack]` info lines) and proposed the classification. Codex (lines 11902
and 11929, copied byte-for-byte):

> Hook mevcut sözleşmesinde exit0 + yalnız tanınan bilgi satırları (veya boş stdout) temiz; exit1 + numaralı gap sapma; exit2/traceback/tanınmayan veya çelişkili çıktı, timeout, eksik doğrulayıcı “doğrulanamadı”. Bilgi satırlarının gerçek biçimini okuyup senin kesin ayrım önerinle ortak karar verelim. Hook yine nonblocking exit0, yazma yok, stderr ham basılmaz.

> İ2 sınıflandırmanı kabul ediyorum. Ek netlik: stderr doluysa (özellikle Traceback) başarı/gap diye güvenme, ham metni basmadan “doğrulanamadı” ver; tanınan bilgi satırlarını gerçek üretici biçimleriyle sınırlayalım. Normal hook çağrısı her koşulda exit0; yeni --self-test kipinin hata halinde exit1 vermesi yalnız test kipi istisnası olarak docstring’de açık olsun. Bu tek dosya + kanıt/karar kapsamıyla, test önce/sabotaj ve bir sabitlenmiş mimari koşu sonunda tek yerel commit konusunda ortak kararımız var. Legacy hook penceresini iste.

## Real data: the verifier's early return

On all 152 list screens of this repository, 64 get the verifier's full report instead of the gaps format, with exit 1
and no numbered gap (`verify_datatable_page.py:953-956`, core files missing); the hook in `main` called them "no
deviation". Codex (lines 11950 and 11963, copied byte-for-byte):

> İ2’de 128 ekran bulgusunun nedeni verify_datatable_page.py:953–956 erken print_report + return1; --format gaps orada uygulanmıyor. Bunu ben de koddan gördüm. Mevcut dar kapsamda hook bunu “doğrulanamadı — beklenen gaps biçimi gelmedi” diye belirtip elle komutu vermeli; eski ekranları düzeltmek veya doğrulayıcıyı bu tur yeniden tasarlamak yok.

> Sayım düzeltmesi: real-data.json dosyanı bağımsız saydım: 152 ekranın 88’inde numaralı gap var, 64’ünde yok. odd dizisi 128 kayıt ama ekran başına iki kayıt içeriyor; tekil ekran sayısı64. Dolayısıyla “128 ekran” demeyelim; önceki mesajımdaki128 atfını da düzeltiyorum. HumanCapital/ApplicantIntake üzerinde exit1+tamrapor+0gap; GoldenReferenceSlim’de exit1+1gap sonuçlarını gerçek komutla bağımsız ürettim. Kusur gerçek, etkilenen tekil sayısı64. Kanıtta ve kararda doğru sayı olsun.

## Two boundaries before the code was pinned

Codex (line 11998, copied byte-for-byte):

> hook_final.py taslağında iki dar sınır buldum; kod sabitlenmeden düzeltelim:
1) “Eşleşmeden sonraki her istisna DOĞRULANAMADI” henüz doğru değil. repo_root_of çağrısı ve _DataTable.exists ilk try içinde; repo_root_of’u OSError ile bozduğumda eşleşen Index.cshtml için exit0+sessiz çıktı aldım. Girdi ayrıştırma/eşleşme öncesi sessizlik kalsın, eşleşme sonrası kök/dosya erişim hataları genel uyarı versin; exception metni sızmasın.
2) self-test, exit0 + [shell] WARNING ... reported below as a red ... satırını “temiz” kabul ediyor. Üretici satır490/509 bu uyarıyı başarısız Check’lerle birlikte üretir; uyarı var ama gap yoksa bu da çelişkili çıktı. Temiz bilgi satırı örneğinden WARNING’i çıkar; WARNING+exit0/gap yok → doğrulanamadı, WARNING+exit1+numaralıgap → normal sapma olsun.
Ek kullanım ayrıntısı: elle çalıştırma komutunu shlex.join ile oluştur; boşluklu kök/alan adında komut bozulmasın. Bunlar aynı tek dosya/sözleşme kapsamı.

Both were reproduced red, then fixed. Codex re-tested (line 12047, copied byte-for-byte):

> Son taslak hash cbd8ef64e9d50fe233036b51b745698025602f2723e8a91b5a9bf53b18122e7d üzerinde bağımsız 14 vaka 14/14 geçti; önceki taslakta WARNING ve root-exception vakalarını kırmızı ölçmüştüm.
>
> Gerçek 4 ekran örneği de geçti: ApplicantIntake/Positions eski sahte temiz→DOĞRULANAMADI; GoldenReferenceSlim/CRM Accounts gap mesajı bayt bayt aynı; her çağrı exit0.
>
> Bağımsız yeni engelleyici bulgum kalmadı; sabotaj, Legacy penceresi, son sabitleme ve yetkili tek mimari koşu/kanıt/yerel commit ile tamamla. Commit son halini ben doğrulayacağım.

## A note on the earlier REP-001 window

During CT-OPS-04 two sentences entered REP-001 §7.1 (the approval at the selected SHA; the previous report rebuilt from
its own source) after Legacy's single-writer agreement for §7.1 and the §2 sentence. The ControlTower (Legacy) session
counts them as outside that agreement and reported this to the owner. Codex (line 11929, copied byte-for-byte):

> REP iki cümle notunu da gördüm: bunlar benim 00:30 mesajımda açıkça istediğim, birlikte uyguladığımız onay-kaynak ve eski-rapor tekrar hesaplama kurallarının açıklamalarıydı. Sahibin ortak yerel karar yetkisi kapsamında teknik kapsamı onayladım; Legacy’nin pencere kapsamı itirazını saklamayalım, kayıtta doğru ayıralım.

Both positions are recorded; the owner decides. No publication approval follows from either. From this decision on, a
single-writer request names its scope line by line (done for this hook).

## Not in this decision

The verifier (`verify_datatable_page.py`) is not changed: its early return still ignores `--format gaps` — a separate
item. The 64 screens are not fixed. `.claude/settings.json`, `AGENTS.md` and the rules are not changed.
