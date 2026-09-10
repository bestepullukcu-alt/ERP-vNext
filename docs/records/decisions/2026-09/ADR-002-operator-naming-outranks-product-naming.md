# ADR-002 — Operatörün adı, bizim varsayılanımızı yener

| | |
|---|---|
| **Durum** | Kabul edildi |
| **Tarih** | 2026-09-08 |
| **Karar veren** | CONTROL TOWER |
| **İlgili** | ADR-001 §2 — bu kararı açıkta bırakmıştı |
| **Etkilenen** | `ModuleLabel.buildMap` · gelecek her ad köprüsü |

---

## Soru

ADR-001 §2 Rol İzinleri ekranına özel bir modül adı katmanı (`Perm.Module.*`)
getirdi ve sırasını *"izin ekranına özel ad varsa onu kullan, yoksa menü adına
düş"* diye yazdı. **Kiracının kendi override'ının nereye düştüğünü söylemedi.**

Uygulama onu en üste koydu. Sonucu şuydu: operatör modülü katalogda yeniden
adlandırırsa **sol menü değişir, Rol İzinleri ekranı değişmez.**

## Karar

**Kiracı override'ı kazanır.** Sıra:

    nav varsayılanı  →  resx (menü adı)  →  Perm.Module.* (ekrana özel)  →  kiracı override'ı
                                                                            ↑ en güçlü

## Neden

**1 · Aynı dosya bu kuralı zaten kuruyor ve test ediyor.** `module-label.js`
üzerindeki mevcut test satırı: *"tenant override > resx > nav default"*.
Dördüncü katmanı override'ın üstüne koymak, o kuralı tek ekran için tersine
çevirmek olurdu — ve kural bir yerde delinince kural olmaktan çıkar.

**2 · Niyet farkı.** Override, operatörün **kasıtlı bir eylemidir**: "bizim
şirkette bu modülün adı budur." `Perm.Module.*` ise **bizim varsayılanımızdır**:
bir yüzeyde daha iyi okusun diye biz seçtik. Bir varsayılan, kasıtlı bir eylemi
geçemez.

**3 · Kullanıcının çıkaracağı sonuç yanlış olurdu.** Adı değiştiren operatör
menüde değiştiğini, bir ekranda değişmediğini görür. Vardığı sonuç "bu ekran
farklı bir isim kullanıyor" değil, **"yeniden adlandırma bozuk"** olur.

**4 · Kurumsal sistemlerde de böyledir.** SAP, Oracle ve Workday'de müşteri
tanımlı etiket, teslim edilen etiketi **her yerde** geçer. Ürün varsayılanı
yüzeye göre değişebilir; müşterinin sözü değişmez.

## Sonucu

Bir kiracı `tasks` modülünü "İş Emirleri" diye adlandırırsa, hem menüde hem Rol
İzinleri ekranında "İş Emirleri" görür. Bizim iki farklı varsayılanımız
(*Görev Tanımları* / *Görevler*) yalnız o adlandırma **yokken** iş görür.

⚠ Bu, ADR-001 §2'nin kararını **değiştirmez** — `tasks` hâlâ bölünmüyor ve ekrana
özel ad hâlâ menü adını yeniyor. Yalnız ADR-001'in cevaplamadığı soruyu
cevaplar.

## Nasıl ölçülür

    npx vitest run tests/role-assignments-module-label.test.js

Sıra bozulursa kırmızı olur: sabotaj yapıldı (katman override'ın üstüne alındı),
test düştü, geri alındı.

## Bu kararın kapsamadığı

Bir kiracının **yüzeye özel** override istemesi — yani "menüde şu, izin ekranında
bu" demek istemesi. Bugün böyle bir talep yok ve varsaymıyoruz. Çıkarsa bu ADR
değil, yeni bir karar gerekir: override'ın kendisi yüzey taşımalı.
