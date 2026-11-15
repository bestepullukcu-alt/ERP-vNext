# WP-MOD0181-ANTIGRAVITY-ROWS — MOD-0181 Sayım: `.antigravity` kural satırları (PKS-001 §4, RCS-001 §3)

| Alan | Değer |
|---|---|
| Modül | MOD-0181 Cycle Counting (pack r7, ready-for-dev, sahip «onayla» 2026-11-06) |
| Kaynak | Pack §6 "İzlenen `.antigravity/**` kapısı (r4, 02/F-7)", F-17, Q-7 = B; §18 hazırlık listesindeki işaretsiz madde |
| Tür | Yalnız kural belgesi (korunan yol). **Kod yok.** |
| Onaylayan | Beste (AGENTS.md Çekirdek madde 6: `.antigravity/**` değişikliği kullanıcı onayı ister) |
| Yazan | MVP-5 CT, bu WP ile, sahip onayından sonra |
| Kapanış adımı | MOD-0181 modül PR'ı açılmadan önce (paket onayını ve dilimleri engellemez) |
| Durum | **Taslak — sahip onayı bekliyor** (2026-11-10) |

## 1. Değişecek dosyalar (yalnız bunlar)

1. `.antigravity/rules/permission-key-standard.md` — §4 "Module ownership" tablosuna **bir satır**.
2. `.antigravity/rules/record-coding-standard.md` — §3 "Kayıt türleri tablosu"na **bir satır**.

Başka `.antigravity/**` dosyası, kod, test, ekran, `ocelot.json` değişmez. `sync_adapters.py` gerekmez (Çekirdek blok değişmiyor).

## 2. Eklenecek satırlar (önerilen metin)

### 2.1 PKS-001 §4 (tabloya, `pvg.*` satırından sonra)

```
| `cycle-count.*` | `Diten.InventoryService` — MOD-0181 Cycle Counting (sayımlar, fark izi, kanıt paketi, şirket sayım ayarları; owner decision Q-08 — one namespace per WMS module, MOD-0178 pack §14.1; pack r7 approved 2026-11-06) | MOD-0181 (Cycle Counting) |
```

Bu namespace altında bugün kodda ve pakette tanımlı 15 anahtar (bilgi; tabloya yazılmaz):
`cycle-count.counts.{read,create,update,cancel,record,bulk-delete}` ·
`cycle-count.variances.{read,approve,post,recount,resolve}` · `cycle-count.evidence.{read,export}` ·
`cycle-count.settings.{read,configure}`.

### 2.2 RCS-001 §3 (tabloya, son satırdan sonra)

```
| Sayım (MOD-0181) | OTOMATİK | `CNT` | kod var — Inventory servisinde MOD-0178'in geçici sayacından (`IWarehouseRecordCodeAllocator`, Q-7 = B, sahip kararı 2026-11-06); ortak servise (MOD-0040-FU02) geçişte DL-8'e `CNT` eklenir |
```

## 3. Kabul ölçütleri

- [ ] AC-1 `git diff --stat` yalnız §1'deki iki dosyayı gösterir; her dosyada **+1 satır, −0 satır**.
- [ ] AC-2 Eklenen satırlar §2'deki metinle birebir aynıdır (sahibin onayladığı metin).
- [ ] AC-3 `cycle-count.*` başka bir namespace satırıyla çakışmaz; tablo sütun sayısı bozulmaz (Markdown tablo 3 / 4 sütun).
- [ ] AC-4 `CNT` öneki §3 tablosundaki başka bir satırla çakışmaz (`git grep -n "\`CNT\`" .antigravity/rules/record-coding-standard.md` → 1 satır).
- [ ] AC-5 Commit yalnız açık dosya yoluyla stage edilir; `main`'e commit yok; push / PR sahibin sözüyle.
- [ ] AC-6 MOD-0181 pack §18 hazırlık listesindeki `.antigravity` maddesi, bu commit'in numarasıyla CT kaydına işlenir (pack metni değişmez).

## 4. Kapsam dışı — ayrı paket olarak plana alındı (sahip kararı 2026-11-10: "ayrı paket olarak yaparız")

- **AS-1:** `inventory.*` (MOD-0173) ve `warehouse.*` (MOD-0178) namespace'leri de §4 tablosunda yok (pack F-17 notu: geliştirme
  engeli değil). Aynı adımda bu iki satır da eklensin mi? **Öneri: hayır, bu WP yalnız MOD-0181; ayrı WP** (sahipleri farklı onay
  zincirinde).
- **AS-2:** RCS-001 §3'te MOD-0178'in kendi geçici önekleri de yok. Aynı soru; öneri: ayrı WP.
