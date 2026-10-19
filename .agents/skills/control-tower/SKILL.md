---
name: control-tower
description: "Bir modül için Control Tower başlangıcı. Repo, HEAD ve araçların varlığını ölçer; modülü salt okunur araştırır (Blueprint, domain-config, module pack, görev kaynakları, mükerrer iş keşfi). Var olan modülde onayın kapsamını ve iddia edilen uygulamayı kod ve kanıt sürümüyle karşılaştırır; olmayan modülde parent / FU / yeni / aday ayrımını ve gerekçeli tek bir ilk kapsam önerisini sunar. Kullanıcı bir modülü başlatmak, sürdürmek ya da durumunu denetletmek istediğinde kullan (örnek: 'Employee modülünü yapalım'). Salt okunur: kod yazmaz, kuyruğa ya da kayda yazmaz, onay, paket ya da dağıtım izni vermez."
---

> **Üretilmiş kopya — elle düzenlemeyin.** Kaynak: `.antigravity/skills/control-tower/SKILL.md` · kaynak özeti: `sha256:a68022d3ca75` · yeniden üret: `python3 .antigravity/scripts/sync_adapters.py`

# control-tower — bir modülün Control Tower başlangıcı

Çağrı: Claude Code `/control-tower <modül adı>`, Codex `$control-tower <modül adı>`; ikisi de yoksa
`@.antigravity/skills/control-tower/SKILL.md <modül adı>`. Modül adını çağrının kendi metninden oku; ad yoksa yalnız
onu sor.

Bu skill yeni yetki yaratmaz. Kurallar `AGENTS.md`, CONTROL TOWER SOP ve işletim kartındadır
(`docs/guides/operations/control-tower-operating-card.md`). Komutlar kartın "Bugün hangi komut" tablosunda ve §0'dadır;
burada kopyalanmaz, oradan okunur. İlk çağrı salt okunurdur.

## 1. Başlangıç ve kimlik

- Dal, HEAD ve `git status --porcelain` çıktısını kendin ölç; bunları ve kaynaklardan okunabilen hiçbir alanı kullanıcıya
  sorma.
- Kartın tablosundaki araçların bu ağaçta olup olmadığını ölç. Olmayanı "bu ağaçta yok" diye yaz; çalıştırmış gibi
  davranma, yerine kendi kontrolünü uydurma.
- Görev kaynağını ve kaydını oku (kartın §0'ı): kuyruk ve kayıt satırı var mı, `main`'de yürürlükte mi. Yürürlükte
  değilse yalnız hazırlık önerilir (`--prepare` sınırı). Kayıt, sahiplik ya da onay uydurma.

## 2. Araştırma

- Yalnız işle ilgili girdileri oku: Blueprint ve modül kimlik kaydı, ilgili `domain-config.md`, module pack, görev
  kaynakları. Bütün depoyu tarama.
- Mükerrer ya da süren iş için kartın keşif komutunu koş. Erişilemeyen kaynak ya da ref'i yaz; görünmeyen işi
  doğrulanmış sayma.
- Keşfi bir kez koş ve çıktıyı ekrandan oku; geçici dosya dahil hiçbir dosyaya yönlendirme. Aynı kaynak ve sorguyla
  tekrar koşma; kaynak değiştiyse ya da teknik bir hata olduysa gerekçesini yazıp bir kez yeniden koşabilirsin. Çıktı
  kesildiyse "kısmi" de.
- Önceki bir keşif raporu: `--check-report` yalnız bütünlüğü kanıtlar, güncelliği değil. Raporu yalnız aynı iş için,
  HEAD, kirli kaynak baytları ve ref kimliklerinin değişmediği ölçüldüyse yeniden kullan; yeni işte ya da bunlar
  kanıtlanamıyorsa yeniden keşfet.

## 3. Var olan modül

- Mevcut kimliği ve paketi kullan; yeni ya da mükerrer bir kimlik açma.
- Paketin `status` alanını tek başına okuma; her çağrıda paketin güncel `approval_status` alanını ve kapsam metnini de
  oku. Onaylı dilimi ve dışarıda kalanı ayrı yaz. Onayı bütün modüle yayma. (Tarihsel örnek, 2026-10-18: MOD-0251
  paketinde yalnız P2 draft / reference-validation dilimi onaylıydı; bugünün izni paketin kendisinden okunur.)
- Belgede iddia edilen uygulamayı ilgili kodla ve kanıtın kaydedildiği sürümle karşılaştır (`dosya:satır@commit`).
  Çalışma testi yapılmadıysa "çalışma testi yapılmadı" yaz; okumadan "doğru" deme.

## 4. Olmayan modül

- Bulguları parent / FU / yeni / aday olarak ayır. Kimlik uydurma; kaynak Blueprint'tir.
- Blueprint'te var ama pack yoksa: kod değil, taslak module pack hazırlama adımını öner. Blueprint'te de yoksa: parent /
  FU uygunluğunu araştır; uymuyorsa dur ve Master'da Module Change Request öner (Master-first kuralı, Blueprint 8.7); yeni
  `CAND-CAP` ya da ID uydurma.
- Gerekçeli tek bir ilk kapsam öner ve "öneri — onay değil" diye işaretle. Tek modül için Delivery Capability Pack
  oluşturma.

## 5. Sınırlar

- Kapsam önerisi, paket taslağı, kod geliştirme ve dağıtım dört ayrı yetkidir; bu skill hiçbirini vermez. Sahibin onayı
  olmadan ürün kodu, commit, push ya da PR yok.
- İlk çağrıda kuyruğa ya da kayda yazma, `guard` ya da prompt taslağı dosyası yok; geçici dosya dahil hiçbir dosyaya
  yazma. Görev, test, kabul ve başka bir
  MVP'ye soru için kartın ilgili satırını ve aracını kullan; yeni yetki yaratma, zorunlu kapıları atlama.

## 6. Yanıt biçimi

İlk yanıt kısadır (hedef yaklaşık 12 satır) ve bu sıradadır:

1. Tek satırda durum: dal, HEAD, görev kaydı.
2. Bilinen eksik ya da okunamayan kaynaklar, adlarıyla; eksik varsa "kısmi inceleme". Sayısını bilmiyorsan sayı uydurma.
3. Kritik çakışma ya da engel (kimlik çakışması, onaysız görünen kod, kayıtlı kuyruğun yokluğu): satır hedefi bunları
   gizlemez.
4. Gerekçeli tek öneri, "öneri — onay değil".
5. Yalnız kullanıcı kararı gerekiyorsa, en çok bloklayan kararı tek soruyla sor; gerekmiyorsa soru uydurma.

Kısa yanıtta da olguyu çıkarımdan ayır. Kaynak tablosu, olgular (`kaynak:satır`), çıkarımlar ve aday listesi istenince
ayrıntılı verilir. Satır numarasını yalnız numaralı bir okumadan ver (`grep -n`, `nl -ba` ya da okuma aracının satır
numarası); yoksa yalnız `yol@commit` yaz.

İncelenen kaynak tablosundan geniş "tam inceledim" deme. Zaman ya da token tasarrufu garantisi verme.
