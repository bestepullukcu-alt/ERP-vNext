# TEAM PLAYBOOK — Paralel Geliştirme, Sıfır Soru

> **Amaç:** 3-4 kişi, her biri 1-2 MVP alır, kendi bilgisayarında Claude (AI ajan) ile geliştirir. **Geliştirirken kimse insana soru sormaz** — her şey dosyalarda yazılı; AI okur, üretir, varsayımını raporlar. Sonunda 1 kişi birleştirmeyi yapar.

## 0. Altın kural (herkes)
> **OWNS'a yaz · CONSUMES'u sadece FROZEN oku · MUST-NOT'a asla dokunma · başka seam'e yazman gerekiyorsa dur, contract'ı kullan; icat etme.**

## 1. İş akışı (push sonrası) — CT-per-person modeli
```
1. Sen push at → herkes pull (contract'lar + Work Package Brief'ler repo'da)
2. İş paylaşımı: kişi başı 1-2 MVP (aşağıdaki tablo)
3. Her kişi KENDİ Control Tower'ını kurar (control-tower SOP'a göre)
4. Kişinin CT'si: Work Package Brief'i + frozen contract'ları + repo'yu ÖLÇER →
   INTAKE→INSPECT→DoR→PACKAGE/PROMPT → DEV PROMPTUNU KENDİ ÜRETİR
5. Dev Agent Lane: CT'nin ürettiği promptla geliştirir → self-test → rapor
6. Kişinin CT'si: DOĞRULAR (gate exit checklist) → kendi branch'ine commit
7. Sonda: integrator branch'leri birleştirir + cross-bağlantı + INTEGRATION GATE
```
> **Not:** Repo'daki WP dosyaları **Brief**'tir (CT girdisi), dev-prompt değil. Dev promptu her kişinin CT'si üretir (SOP §17). Böylece SOP disiplini (DoR/no-invent/single-writer/evidence) her lokal CT'de çalışır.

## 2. Kim hangi MVP (öneri — sen ayarla)
| Kişi | MVP | Modüller | Branch |
|---|---|---|---|
| A (foundation) | **MVP-1** | 0290 · 0173 · 0174 · 0175 · 0176 · 0177 · Location | `feature/mvp1-foundation` |
| B | **MVP-2** Procurement | 0140-0145 | `feature/mvp2-procurement` |
| C | **MVP-3 + MVP-5** | 0193 · 0178/80/81/82 | `feature/mvp3-bom` + `feature/mvp5-warehouse` |
| D | **MVP-4 (+MVP-6)** | 0188-0192 (0183-87) | `feature/mvp4-planning` |
> Kural: **her MVP kendi branch'inde** (§21 disjoint scope). Çakışma engellenir.

## 3. ⭐ SIFIR-SORU POLİTİKASI (kişinin CT'si ürettiği prompta GÖMER)
AI geliştirirken normalde takılınca insana sorar. **Biz bunu istemiyoruz.** Kişinin Control Tower'ı, dev promptunu üretirken şu bloğu prompta **gömmeli**:

> **No-block policy:** Bir belirsizlikle karşılaşırsan:
> 1. Önce ilgili dosyaları RE-READ et: contract OpenAPI + `inventory-capability-scope-and-dependency-report.md` (kararlar §0.0, kurallar §21, kabul §20).
> 2. Cevap ordaysa uygula.
> 3. Gerçekten yoksa: **dokümante edilmiş default'u kullan** (raporda "ASSUMPTION: …" olarak yaz), **DURMA, insana SORMA.**
> 4. Sadece contract'ı bozacak / güvenlik-veri kaybı riski olan durumda dur ve raporla (implement etme).

Bu çalışır çünkü: **19 karar RESOLVED + 8 contract frozen** → sorulacak meşru bir şey kalmadı. AI belirsizliği dosyadan çözer.

## 4. Herkes bağımsız geliştirsin diye — MOCK kullan
Bir kişi kendi MVP'sini geliştirirken **başkasının MVP'sine ihtiyaç duymaz** — bağımlılıkları **mock**'a karşı geliştirir:
- Her contract'ın OpenAPI'ı var (`docs/analysis/contracts/`)
- AI, bağımlı servisi (ör. MVP-2 → INVENTORY) mock'a karşı çağırır
- Gerçek servisler birleştirmede bağlanır (integrator)
> Mock çalıştırma (owner isterse): `npx @stoplight/prism-cli@4.10.5 mock <contract>.openapi.yaml`
> Ama çoğu durumda AI, contract'a uyan kodu doğrudan yazar; mock sadece entegrasyon testi için.

## 5. Çakışma engelleme (merge sorunsuz olsun)
- **Tek-yazıcı seam'ler (§21):** 0173/0290/0188/Location/gateway/permission → sadece SAHİBİ yazar. Başka MVP bunlara YAZMAZ, contract'tan okur.
- Her kişi **sadece kendi modülünün path'lerine** dokunur (WP'de "Allowed/Protected paths" yazılı).
- Ortak dosyalar (gateway ocelot.json, shared registrations) → **integrator** birleştirmede ekler, MVP developer'ları elle düzenlemez.

## 6. Integrator (son kişi) ne yapar
- Branch'leri sırayla merge eder (dependency sırasına göre: foundation önce)
- Cross-module bağlantıları (gerçek servis çağrıları, gateway route'ları) bağlar
- INTEGRATION GATE çalıştırır (§27): servisler birlikte ayağa kalkıyor mu, e2e akış çalışıyor mu
- Mock→gerçek geçişini doğrular

## 7. Her kişinin "bitti" kriteri (kendi gate'i)
Her MVP'nin Work Package'ında **exit checklist** var (§20 gate). Kişi AI'a bunu doğrulatır:
- Golden flow çalışıyor (runtime)
- Contract'a uyuyor (response şekilleri OpenAPI ile eşleşiyor)
- Kurallar ihlal edilmemiş (OWNS/MUST-NOT)
> "AI works dedi" yetmez — gate checklist PASS + kanıt.

---

## Özet — senin cümlenle
> "Herkes kendi MVP'sini Claude ile geliştirir, bana soru sormaz, paralel gider."

**Nasıl garanti:** (1) 19 karar + 8 contract dosyada kilitli → sorulacak şey yok · (2) her WP'de no-block policy → AI belirsizliği dosyadan çözer, insana sormaz · (3) her MVP ayrı branch + mock → bağımsız · (4) tek-yazıcı seam kuralları → merge çakışmaz · (5) integrator sonda birleştirir.
