# Doküman Klasör Yapısı (`docs/`)

> Bu kural 2026-09-07'de yazıldı çünkü `docs/` çöplüğe dönmüştü ve **onu
> engelleyecek hiçbir kural yoktu.** Ölçüm o gün alındı:
>
>     docs/ kökünde dosya          29   (15 md · 6 csv · 5 xlsx · 1 docx · 1 txt)
>     docs/audits/ tek düzeyde    172   dosya, hiç alt klasör yok
>     toplam                      293   dosya, 8.0 MB
>
> Kural olmayan yere herkes kök dizine atar. Bu dosya nereye ne konacağını söyler.

---

## 1. Yapı

    docs/
      README.md              ← TEK giriş noktası; nereye ne konduğunu anlatır
      architecture/          ← mimari kararlar, servis sınırları, entegrasyon
      modules/               ← modül belgeleri; dosya adı MOD-xxxx ile BAŞLAR
                             (bir modülün 3+ belgesi varsa alt klasör açılır)
      guides/<modül>/        ← KULLANIM KILAVUZLARI — resimli, tek dosya HTML
      audits/<yyyy-mm>/      ← denetim ve inceleme çıktıları, ay klasörlerinde
      plans/                 ← ileriye dönük planlar
      operations/            ← SOP, runbook, dev ortam kurulumu, control-tower
      backlog/               ← product-backlog ve kapanmış maddeler
      vendor/                ← DIŞARIDAN gelen belgeler (müşteri/yönetici Excel'leri)

## 2. Kurallar

**K1 · Kökte dosya olmaz.** `docs/` kökünde yalnız `README.md` durur. Başka her
dosya bir klasöre aittir. Uygun klasör yoksa önce klasör ve `README.md` satırı
eklenir, sonra dosya konur.

**K2 · Dışarıdan gelen belge `vendor/` altındadır ve DÜZENLENMEZ.** Müşterinin
veya yöneticinin gönderdiği Excel/Word olduğu gibi durur; sürüm bilgisi dosya
adında taşınır. Bizim ondan türettiğimiz çalışma dosyası `vendor/` içine değil,
ilgili klasöre konur.

**K3 · Kullanım kılavuzu `guides/` altındadır ve resimlidir.** Markdown kılavuz
üretilmez — biçim ve gerekçe `.antigravity/agents/user-manual-generator.md`
içindedir.

**K4 · Denetim çıktısı ay klasörüne girer.** `audits/2026-09/…`. Tek düzeyde
biriken denetim dosyası 172'ye ulaştığında kimse aradığını bulamaz; ölçüldü.

**K5 · Taşıma referans kırar — protokolü uygulanmadan taşınmaz.** Bkz. §3.

**K6 · 1 MB üzeri ikili dosya tartışılır.** Depoya girmeden önce gerçekten
sürüm takibi gerekiyor mu sorulur. Git ikili dosyayı sıkıştırmaz; her sürüm
tam boy saklanır.

## 3. Taşıma protokolü

Bir belge yer değiştirdiğinde ona işaret eden her şey kırılır. Ölçüldü:
`docs/` içinde 241 md-içi bağlantı, `execution/` altında 79 dosya `docs/`
yoluna referans veriyor, `.antigravity/` workflow'ları belirli yollara bağlı
(`docs/product-backlog.md`, Blueprint xlsx, `docs/sop/upstream/`).

Bu yüzden taşıma şu sırayla yapılır:

1. **Ölç** — dosyaya kaç yerden referans var:

       grep -rlF "<dosya-adı>" .antigravity execution docs services frontend

2. **`git mv` kullan.** Kopyala-sil değil; geçmiş korunur.
3. **Referansları aynı commit'te güncelle.** Taşıma ve link düzeltmesi ayrı
   commit'lere bölünmez — arada kalan commit'te belgeler kırıktır.
4. **Doğrula** — taşımadan sonra ölü bağlantı kalmadığını göster (§4).
5. **Referansı çok olan dosya en sona bırakılır.** `product-backlog.md` ve
   Blueprint xlsx gibi dosyalar birden çok workflow'un kanonik kaynağıdır.

## 4. Denetim — kural yazılı değil, ölçülür

Bu kuralın tutup tutmadığı komutla görülür, göz kararıyla değil:

    # K1 — kökte README.md disinda dosya var mi? (0 olmali)
    find docs -maxdepth 1 -type f ! -name 'README.md' ! -name '.DS_Store' | wc -l

    # K4 — audits/ kokunde dagilmis dosya var mi? (README disinda 0 olmali)
    find docs/audits -maxdepth 1 -type f ! -name 'README.md' | wc -l

    # K6 — 1 MB ustu ikili
    find docs -type f -size +1M

    # §3 — olu md bagi kaldi mi
    grep -rho '](\S*\.md)' docs --include='*.md' | sed 's/](//;s/)//' | sort -u

## 5. Bu kural yeni dosya içindir, geçmiş temizliği ayrı iştir

Bugünkü 293 dosya bu kuralla kendiliğinden düzelmez. Temizlik fazlıdır ve
sırası referans maliyetine göredir: önce `audits/` (dış referansı az), sonra
kök (referansı çok), en sonda `docs/platform/` altındaki tenant modülleri —
o taşıma 21 dosyanın bağını kırar ve Control Tower kararı bekler.
