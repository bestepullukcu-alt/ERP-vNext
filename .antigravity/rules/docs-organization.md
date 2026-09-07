# Doküman Klasör Yapısı (`docs/`)

> Bu kural 2026-09-07'de yazıldı çünkü `docs/` çöplüğe dönmüştü ve onu
> engelleyecek hiçbir kural yoktu. O günkü ölçüm: kökte 29 dosya,
> `audits/` tek düzeyde 172 dosya, toplam 293 dosya / 8.0 MB.

---

## 1. Mantık — tek soru

Bir belgeyi nereye koyacağını **belgenin zamanla nasıl davrandığı** söyler.
Konusu değil, yazarı değil, kime ait olduğu değil. Sırayla sor, ilk "evet"te dur:

| # | soru | klasör |
| :-- | :--- | :--- |
| 1 | Biz mi yazdık? **Hayır**, dışarıdan geldi | `vendor/` |
| 2 | Belirli bir tarihte olan bir şeyi mi kaydediyor, bir daha değişmeyecek mi? | `records/` |
| 3 | Henüz olmamış bir şeyi mi anlatıyor? | `roadmap/` |
| 4 | Birine bir işi nasıl yapacağını mı öğretiyor? | `guides/` |
| 5 | Bugünün gerçeğini mi anlatıyor, değişince güncellenecek mi? | `reference/` |

Beş sorunun dışında kalan belge yoktur. Yeni bir klasör açmadan önce bu tabloyu
oku: yeni bir üst klasör, bu beş sorudan birinin cevabının değiştiği anlamına
gelir ve Control Tower kararıdır.

## 2. Yapı

    docs/
      README.md                       ← kökteki TEK dosya

      reference/                      ← bugünün gerçeği; değişince güncellenir
        architecture/                 mimari kararlar, port sözleşmeleri, spec'ler
        modules/platform/<yetenek>/   platform tarafı modül belgeleri
        modules/tenant/<modül>/       kiracı tarafı modül belgeleri
        blueprint/                    MOD-xxxx kimliklerinin kanonik kaynağı
        integrations/<karşı-taraf>/   dış sistem entegrasyon sözleşmeleri
        reference-data/               referans veri içe aktarım tanımları

      guides/                         ← nasıl yapılır; okuyan bir işi yapar
        <modül>/                      son kullanıcı kılavuzu (resimli, tek dosya HTML)
        operations/                   dev ortam, modül ekleme, ajan kullanımı
        control-tower/                Control Tower SOP ve işletim kartı
        sop-upstream/                 üst kaynaktan gelen SOP kuralları

      records/                        ← o gün ne olduğu; ARTIK DEĞİŞMEZ
        audits/<yyyy-mm>/             denetim ve inceleme çıktıları
        analysis/<konu>/              karşılaştırma ve durum analizleri
        acceptance-reports/           kabul raporları
        releases/                     sürüm notları

      roadmap/                        ← henüz olmamış
        plans/                        ileriye dönük planlar
        backlog/                      product-backlog ve kapanmış maddeler

      vendor/                         ← biz yazmadık; OLDUĞU GİBİ durur
        <paket>/                      bir teslimat = bir klasör

## 3. Kurallar

**K1 · Kökte dosya olmaz.** Yalnız `README.md`.

**K2 · Üst klasör beş taneden biridir.** Altıncısı Control Tower kararı ister.
Sebep: her yeni üst klasör, "belgem nereye gider" sorusunu bir dal daha
karmaşıklaştırır ve karmaşıklaşan kural delinir.

**K3 · Her klasörün bir kırılım ekseni olmalı.** Eksen klasöre göre değişir:
`audits/` **tarih** (`yyyy-mm`), `analysis/` **konu**, `modules/`
**platform/tenant → modül**, `vendor/` **teslimat paketi**.

Ekseni OLMAYAN bir klasörde 20'yi aşan dosya birikmişse kırılım gecikmiştir.
Ekseni olan klasörde sayı ölçüt değildir: `audits/2026-08` 84 dosya taşır ve
bu bir kusur değil — o ay 84 denetim yazılmıştır, ve denetim tarihle aranır.
Yoğun bir ayı ikinci bir eksene bölmek (modül, konu) aramayı kolaylaştırmaz;
bir modülün denetimlerini iki aya dağıtarak zorlaştırır.

**K4 · `records/` yazıldıktan sonra düzeltilmez.** Bir denetim yanlışsa yenisi
yazılır, eskisi durur. Kaydın değeri o gün ne bilindiğini göstermesidir.

**K5 · `vendor/` içeriği düzenlenmez.** Dışarıdan gelen belge geldiği adla durur;
sürüm bilgisi ad içindedir. Ondan türettiğimiz çalışma dosyası `vendor/` içine
değil, beş sorunun gösterdiği yere gider.

**K6 · Kullanım kılavuzu `guides/<modül>/` altındadır ve resimlidir.** Markdown
kılavuz üretilmez; biçim `.antigravity/agents/user-manual-generator.md`.

**K7 · 1 MB üzeri ikili dosya tartışılır.** Git ikiliyi sıkıştırmaz; her sürüm
tam boy saklanır.

**K8 · Taşıma referans kırar — protokolsüz taşınmaz.** Bkz. §4.

## 4. Taşıma protokolü

Bir belge yer değiştirdiğinde ona işaret eden her şey kırılır. 2026-09-07
temizliğinde 780'den fazla referans güncellendi; ikisi neredeyse kaçıyordu ve
ikisi de **kod içindeydi** — bir Python kapısı ve bir CSS yorumu.

1. **Ölç** — kaç yerden referans var:

       grep -rl "docs/<eski-yol>" .antigravity execution docs services frontend gateway

2. **`git mv` kullan.** Kopyala-sil değil; geçmiş korunur.
3. **Referansları AYNI commit'te güncelle.** Ayrı commit'e bölme; arada kalan
   commit'te belgeler kırıktır.
4. **Kod uzantılarını da tara.** `.md` yetmez: `.py .sh .cs .cshtml .js .css
   .html .json .yaml .xml .resx`. Bu adım atlanırsa kırılan şey belge değil,
   çalışan bir kapı olur.
5. **Doğrula** — ölü bağ kalmadığını göster (§5), iddia etme.
6. **Referansı çok olan en sona.** Blueprint xlsx ve `product-backlog.md`
   birden çok workflow'un kanonik kaynağıdır.

## 5. Denetim — kural yazılı değil, ölçülür

    # K1 — kökte README disinda dosya (0 olmali)
    find docs -maxdepth 1 -type f ! -name 'README.md' ! -name '.DS_Store' | wc -l

    # K2 — ust klasor sayisi (5 olmali: guides records reference roadmap vendor)
    find docs -maxdepth 1 -type d -mindepth 1 | wc -l

    # K3 — ekseni OLMAYAN klasorde 20'yi asan dosya (tarih klasorleri haric)
    find docs -type d ! -path '*/audits/2*' -exec sh -c \
      'n=$(ls -p "$1" | grep -vc /); [ "$n" -gt 20 ] && echo "$n $1"' _ {} \;

    # K7 — 1 MB ustu ikili
    find docs -type f -size +1M

    # §4 — olu md bagi
    grep -rho '](\S*\.md)' docs --include='*.md' | sed 's/](//;s/)//' | sort -u
