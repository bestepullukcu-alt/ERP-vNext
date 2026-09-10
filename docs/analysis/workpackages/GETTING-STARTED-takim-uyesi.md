# BAŞLANGIÇ REHBERİ — Takım Üyesi (MVP Geliştirme)

> Yazılım bilmesen de takip edebilirsin. Her adımı sırayla yap. Takılırsan takım liderine sor (Claude'a değil — Claude'a soru sordurmayacağız).

---

## ADIM 0 — Hazırlık (bir kez)
1. **Claude Code** kurulu olsun (bilgisayarında).
2. Repo'yu al:
   ```bash
   git clone <repo-url>
   cd ERP-vNext
   git pull
   ```
3. Fleet'i çalıştırabilmen için (runtime test): `watch-diten-bg.ps1` (takım liderinden öğren).

## ADIM 1 — Hangi MVP senin? Öğren
- Aç: `docs/analysis/workpackages/README.md`
- Tablodan **senin MVP'ni** bul → **Brief dosyanı** ve **branch adını** not et.
  - Örn: sen MVP-2 aldın → Brief `WP-MVP2-procurement.md` → branch `feature/mvp2-procurement`
- Aç: `docs/analysis/workpackages/TEAM-PLAYBOOK-parallel-no-questions.md` → altın kuralı oku.

## ADIM 2 — Kendi branch'ine geç
```bash
git checkout -b feature/mvp2-procurement
```
*(kendi MVP'nin branch adını yaz)*

## ADIM 3 — Claude'u kendi Control Tower'ın yap
Claude Code oturumu aç. Şunu **yapıştır** (MVP/dosya adını kendine göre değiştir):

```
Seni Control Tower olarak kullanacağım. docs/control-tower/control-tower-sop.md ve
docs/control-tower/ kısımlarını incele, görevlerini anla, ona göre çalış.

Benim Work Package Brief'im: docs/analysis/workpackages/WP-MVP2-procurement.md
Frozen contract'lar: docs/analysis/contracts/
Kararlar + kurallar: docs/analysis/inventory-capability-scope-and-dependency-report.md
Takım kuralları: docs/analysis/workpackages/TEAM-PLAYBOOK-parallel-no-questions.md

Görev sırası:
1. Bu brief'i INSPECT et (repo + contract + kararları ölç).
2. DoR (Definition of Ready) kontrol et — eksik varsa söyle.
3. Dev promptunu ÜRET (SOP §17 — NE/NEDEN/NASIL/YAPMA/DOĞRULA).
4. O promptla modülü GELİŞTİR (dev Agent Lane), frozen contract'lara birebir uy,
   bağımlılıkları mock'a karşı yap.
5. Geliştirirken bana SORU SORMA: belirsizliği contract+rapor+SOP'tan çöz,
   dokümante default'u kullan, raporda "ASSUMPTION: ..." yaz, DURMA.
   (Sadece contract-bozan veya veri-kaybı riskinde dur ve raporla.)
6. Bitince CT olarak BAĞIMSIZ DOĞRULA: brief'teki ACCEPTANCE + gate exit checklist (§20).
```

## ADIM 4 — Claude çalışsın (sen izle)
- Claude **ölçer → prompt üretir → kodu yazar → test eder → doğrular**.
- Sana soru sormaz (yukarıdaki kural). Belirsizlikleri "ASSUMPTION" olarak raporlar.
- Sen sadece **ilerlemeyi izlersin**. Bir şey sorarsa: "brief + contract + SOP'a bak, default kullan, ASSUMPTION yaz" de.

## ADIM 5 — "Bitti mi?" kontrolü (kendi gate'in)
Claude'un raporunda şunlar PASS olmalı (brief'teki ACCEPTANCE):
- Golden flow çalışıyor (runtime kanıt var)
- Response'lar frozen contract'la uyumlu
- Kurallar ihlal edilmemiş (OWNS/MUST-NOT)
- Kanıt var ("works" demek yetmez)
> PASS değilse: Claude'a "şu kriter FAIL, düzelt" de. PASS olana kadar tekrar.

## ADIM 6 — Kaydet (commit + push)
```bash
git add .
git commit -m "MVP-2 procurement — <ne yaptın>"
git push -u origin feature/mvp2-procurement
```

## ADIM 7 — MVP'nde birden fazla modül varsa
- Brief'te birden fazla modül varsa (ör. MVP-1'de 0290/0173/0174…), **ADIM 3-6'yı her modül için tekrarla** (bağımlılık sırasına göre; brief'te yazılı).

## ADIM 8 — Bitince
- Takım liderine / integrator'a haber ver: "MVP-2 bitti, branch push'landı."
- **Integrator** en sonda tüm branch'leri birleştirir + cross-bağlantıları + INTEGRATION GATE yapar. Sen kendi kısmından sorumlusun.

---

## ⚠️ Altın kurallar (hep aklında)
1. **Sadece kendi branch'inde** çalış (başka MVP'ye dokunma).
2. **CONSUMES'u sadece frozen contract'tan** (mock'a karşı) — başka kişinin bitmesini bekleme.
3. **MUST-NOT'a dokunma** (başka seam'e yazma, shadow stok, kimlik icat).
4. **Claude sana soru sorarsa** → "dosyalardan çöz, default kullan, ASSUMPTION yaz" de.
5. Ortak dosyalar (gateway/ocelot, shared) → **integrator ekler**, sen elleme.

## Takılırsan
- Contract eksik/çelişkili görünüyorsa → **takım liderine** ilet (Claude uydurmaz, sen de uydurma).
- Fleet/runtime sorunu → takım liderine.
- İş kuralı belirsizliği → önce rapor §0.0 (kararlar) + brief'e bak; yoksa takım liderine.
