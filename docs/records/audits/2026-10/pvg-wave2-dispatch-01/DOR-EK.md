# PVG ikinci dalga: DoR eki ve dağıtım kapısı (WP-0230-DL2B, WP-0230-DL3B)

> Bu kayıt, Control Tower'ın yazdığı `docs/records/audits/2026-10/pvg-wave2-dor-01/DOR.md` kaydının ekidir. O kayıt değiştirilmez (K4).
> Tarih: 2026-10-26.

## Neden yeni prompt sürümleri

- DOR.md, prompt'ları WP-0230-DL2B v1.3 ve WP-0230-DL3B v1.1 olarak anıyor. İkisinin de Expected HEAD'i `05f311784`.
- PR #223 main'e girdi (`830c6c1a5`). Bu PR ile MOD-0230 revizyon 10 ve onay satırı da main'e geldi.
- Dağıtım kapısı paketin onay satırlarını Expected HEAD'de okuyor. Eski HEAD bu satırları taşımadığı için kapı şu gerekçeyle durdu: "onay dosyası güvenilmez — dalı main ile güncelleyin".
- İki prompt bu yüzden yeni HEAD'e taşındı:

| WP | Dağıtılacak prompt | Expected HEAD · dal · worktree | Bağımsız inceleme |
|---|---|---|---|
| WP-0230-DL2B | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.4.md` sha256 `87e631d5…` @ 242e01536 | `830c6c1a5` · `feature/pvg/wp-0230-dl2b-r1` · `.claude/worktrees/pvg-wp0230-dl2b-r1` (temiz) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL2B-v1.4-inceleme-01.md` @ 242e01536 (inceleyen 16, taban kaydırması incelemesi): kritik 0, engelleyici 0 |
| WP-0230-DL3B | `…/prompts/WP-0230-DL3B-v1.2.md` sha256 `7b0b4dfe…` @ 242e01536 | `830c6c1a5` · `feature/pvg/wp-0230-dl3b-r1` · `.claude/worktrees/pvg-wp0230-dl3b-r1` (temiz) | `…/WP-0230-DL3B-v1.2-inceleme-01.md` @ 242e01536: kritik 0, engelleyici 0 |

- İki HEAD arasında `services`, `frontend`, `gateway`, `tests` ve `.github` altında hiçbir fark yok; inceleyen 16 bunu ölçtü. Bu nedenle v1.3 ve v1.1'in tam incelemeleri (inceleyen 15 ve 13) geçerliliğini koruyor.
- Eski worktree'ler (`pvg-wp0230-dl2b`, `pvg-wp0230-dl3b`, HEAD `05f311784`) kullanılmadı. Kaldırılmaları sahip onayıyla yapılacak.

## Dağıtım kapısı (dağıtım kipi, `--prepare` yok)

| WP | Rapor | Sonuç |
|---|---|---|
| WP-0230-DL2B | `PQG-WP-0230-DL2B-v1.4.txt` | DAĞITIM DENETİMİ · geçti · çıkış 0 · `verify_module_id.py --check-report` RAPOR GEÇERLİ |
| WP-0230-DL3B | `PQG-WP-0230-DL3B-v1.2.txt` | DAĞITIM DENETİMİ · geçti · çıkış 0 · RAPOR GEÇERLİ |

Kuyruk main'de; iki satır da READY. Satırlar, oturumlar açıldığında bu raporlarla DISPATCHED olur.

## Kabulde ayrıca bakılacak notlar (inceleyen 16)

Bu notlar DOR.md'deki listeye eklenir.

**DL2B**
- Prompt'ta iki numaralama var. Sözleşme yüzeyleri revizyon 10'un satırlarını, metin gövdesi ise revizyon 09'un satırlarını kullanıyor. İkisi arasındaki eşleme prompt'ta yazılı.
- NASIL bölümündeki CT ölçümleri `05f311784`'te yapıldı. Kod iki HEAD'de aynı olduğu için sonuçlar değişmez.

**DL3B**
- "Önce oku" listesinde son geçerli inceleme (`WP-0230-DL3B-v1.1-inceleme-02.md`) yok. O incelemenin altı "diğer" notu DOR.md'de yazılı; işçi onları okumayacak, kabulde CT ölçer.
