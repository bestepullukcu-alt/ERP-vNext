# MOD-0247 Compliance Interfaces (seri takibi / düzenleyici bildirimi) — sahip kararları 01 (2026-11-12)

Kaynak: modül talebi araştırması (MVP-1 CT, oturum `ca7382cf`, 2026-11-12) — Blueprint 8.8 (MOD-0245, 0246, 0247, 0260, 0273; grup "6)
Regulated Life-Sciences Extensions / Serialization / Track & Trace"), `verify_module_id.py --discover` (İTS: eşleşme yok; registry / paket /
kuyruk 0), backlog BL-616, MOD-0290-FU04 / FU07 İTS notları, ülke düzenlemeleri için web araştırması (ikincil kaynaklar; resmi metinler
okunmadı). Paket yok, kimlik ayrılmadı. Sahip: Beste.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| K-1 | Türkiye İTS | **Kapsam dışı** — Türkiye'de satış yok. Türkiye satışı başlarsa yalnız yeni bir pazar bağdaştırıcısı eklenir. BL-616'nın İTS kısmı bu kararla kapanır | Beste | 2026-11-12 |
| K-2 | Modülün biçimi | **Genel, çok düzenleyicili:** ortak çekirdek (kod siparişi / havuzu, basım ve toplulaştırma kaydı, olay bildirimi, gönderim günlüğü, alındı / hata, uyum kanıtı) + ülke başına bağdaştırıcı | Beste | 2026-11-12 |
| K-3 | Pazarlar | Satış: UZ, AZ, TM, GE, UA, BY, KZ. Üretim (fabrika): PL, ES. Depo: GE. Bağdaştırıcı önceliği araştırmaya göre KZ + UZ (bugün zorunlu), sonra AZ, sonra UA (2028); BY / GE / TM için zorunluluk bulunamadı — **doğrulanacak** | Beste (pazar listesi) · CT (öncelik önerisi) | 2026-11-12 |
| K-4 | Dış seri deposu (MOD-0245 ön koşulu) | Türkiye'de / bugün bir dış depo **yok**; kullanmayabiliriz. Fabrikalardaki (PL, ES) serileştirme hattı yazılımı öğrenilince yeniden değerlendirilir (o yazılım "dış depo" sayılırsa MOD-0245 gerekir) | Beste | 2026-11-12 |
| K-5 | Sıra | **MVP-5'ten sonra** planlanır | Beste | 2026-11-12 |
| K-6 | Domain | Yeni domain (öneri adı `serialization-traceability`; Blueprint takımı "Serialization / Track & Trace") — **işe başlanınca açılır** (AGENTS-REFERENCE Yol A; AGENTS.md §2 / §9 değişikliği ayrı onay) | Beste | 2026-11-12 |

## Açık (işe başlamadan önce cevaplanacak)
1. Bugün KZ / UZ sevkiyatlarında kodlar nasıl alınıyor ve basılıyor (fabrika yazılımı / hizmet sağlayıcı / elle)?
2. PL / ES fabrikalarında serileştirme hattı ve yazılımı (Level 3 / 4) var mı, hangisi?
3. Satış modeli: bu ülkelere kendi şirketimiz mi ithal ediyor, yerel distribütör mü (KZ / UZ'de ithalatçının bildirim yükü)?
4. PL / ES üretiminden AB pazarına ürün çıkıyor mu (AB FMD)?
5. Ülke kurallarının resmi kaynaklardan doğrulanması (KZ 2024-07-01, UZ 2022-09-01, AZ 2026-01, UA 2028-01-01).
