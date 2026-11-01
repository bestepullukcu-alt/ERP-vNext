# S-115 — dal onayı: onay ve inceleme satırları CT dalında sayılır, ikinci insan modül PR'ında ölçülür

Bu dosyanın main'e girdiği commit, kuralın başladığı yerdir (sahip kararı 2026-11-01, CT-Setup sohbetinde düğmeyle
"Evet, hepsini başlat"; tasarım `docs/roadmap/plans/ct-branch-approval-01/DESIGN.md`; sözleşme eki
`docs/roadmap/plans/ct-dev-readiness-approval-01/CONTRACT.md` §7). Tarih değil commit sırası ölçülür.

- **İki okuma:** dağıtım kapısı (`scripts/prompt_gate.py`) ve kabul kaydı (`scripts/acceptance_record.py`) onay ve
  inceleme satırlarını CT'nin kendi ref'inden okur (`pack_approval.py check <paket> --deferred`): henüz main'de olmayan
  satır sayılır, insan adımı "beklemede"dir. Satır main'e girince sıkı okuma (varsayılan `check`, CI) geçerlidir.
- **Sahip sözü (bu commit'te ya da sonrasında eklenen satırlar):** dalda bekleyen HIGH onay satırının `note` hücresi
  `onayla` sözünü taşır (`onayla · <kule sohbeti> · <tarih saat>`); satır `pack_approval.py row --deferred --note …`
  ile üretilir. Not yoksa dal onayı geçmez.
- **PR'ı açan = satırı yazan (bu commit'te ya da sonrasında eklenen satırlar):** satırı main'e getiren PR'ı açan hesabın
  kişisi, onay satırının `author` ve inceleme satırının `author` kişisidir. Birleştiren: listede `high = evet`, açan
  değil, yazan değil; onaylayanın kendisi olması gerekmez (S-41 kalktı; S-91 istisna satırları "onaylayanı birleştirir"
  kuralını korur).
- **Önek ve yalnız ekleme:** main'in satırları dalın önekidir (geride kalan dal reddedilir); dalın geçmişi yalnız
  ekleme ile ilerler. Yalnız sırayı değiştiren, satır kaybetmeyen `git merge origin/main` (main satırları önce, sonra
  bizimkiler) GitHub'a sorulmadan geçer; satır kaybeden merge bugünkü gibi sorulur.
- **Kabul kaydının yeniden ölçümü:** önek denetimi kaydın commit'i ile main'in ortak atasına (merge-base) göre yapılır;
  bugünkü main'deki aynı paketin iptal ya da olumsuz inceleme satırları yine durdurur.
- **Eski satırlar:** bu commit'ten önce main'e girmiş satırlar geriye dönük düşmez.
- **Bu dosya silinemez:** eklendikten sonra yokluğu kuralı kapatmaz; onay geçmez.
