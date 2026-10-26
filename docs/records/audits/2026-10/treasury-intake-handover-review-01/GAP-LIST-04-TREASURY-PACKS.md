---
record_id: TREASURY-PACKS-GAP-LIST-04
kind: sahip inceleme bulguları ve kapanışları (dört pack taslağı) — onay değildir
received_at_local: 2026-10-24
received_from: depo sahibi (K1), sohbette yapıştırılan iki inceleme metni — (1) "CONDITIONAL PASS — kapsam genişlemiş; yeni pack'lerde kapanması gereken boşluklar var", (2) G2 ve AP için iki ek düzeltme
reviewed_input: .ct-teslim/treasury-intake/treasury-packs-review-48567ecc0.zip (sha256 3922a49cb56707b0eff43a8c340ac3dfb5efe77ee10da56360bf20f6685b9ba4)
fixed_in: MOD-0126, MOD-0119, MOD-0120, MOD-0124 pack'leri (bu kaydın commit'i)
decisions: yok (D14, D15 metinleriyle tutarlı açıklamalar)
previous: GAP-LIST-03-MOD-0126.md
---

# Treasury pack'leri — gap listesi 4 ve kapanışları

| # | Öncelik | Gap (özet) | Kapanış |
|---|---|---|---|
| G1 | P1 | `read-tenant-wide` yazma kapsamını genişletebilir | Dört pack: `IFinanceTreasuryDataScopeSource` iki sonuç döner — okuma kapsamı (kendi LE hakları ya da `…read-tenant-wide` ile tüm LE'ler) ve yazma kapsamı (yalnız kendi LE hakları; `TenantWideScope()` asla). Yalnız tenant-wide ile görünen kayda yazma → 404. Yeni AC'ler (sabotajlı): MOD-0126 AC-33, AP AC-33, AR AC-35, MOD-0124 AC-32. |
| G2 | P1 | Reconcile muafiyeti başlatmayı yeniden oynatabilir | Reconcile önce uzak durumu okur. Varsa sonucu kaydeder (LE kuralından muaf, D15/2). **Sahip ek düzeltmesi:** bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled` **sayılmaz**, niyet `Unknown` kalır; `Cancelled` yalnız uzak tarafın kesin iptal/kapalı durumuyla. Kesin boş ise yeniden başlatma yeni işlemdir: LE uygunluğu + SoD + izin + **asıl aktör** yeniden kontrol edilir, yalnız hepsi geçerse **aynı** deterministik uzak anahtarla tek çağrı; LE uygun değilse çağrı yok, niyet `Unknown` + neden kodu. Görev geçişi / MOD-0031 bağlama yeniden denemesi, uzak tarafın anahtarı yeniden oynattığı dilim başında ölçülene kadar yapılmaz. Yeni AC'ler (a)–(d) + sabotaj: MOD-0126 AC-34, AP AC-34, AR AC-36, MOD-0124 AC-33. |
| G3 | P1 | Cash Positioning değişmezlik kuralı Export ile çelişiyor | Yayımlanmış anlık görüntünün **içeriği** değişmez (`PublishedContentHash` yayım işleminde yazılır); Export ve Export-Reconcile yalnız ayrı niyet / makbuz / bağlantı-sonucu / olay kayıtları yazar; niyet `SnapshotContentHash` taşır, uyuşmazsa uzak çağrısız 409. Yeni AC-34 (önce/sonra belge karşılaştırması + sabotaj). |
| G4 | P2 | AR tahsilat kapsamı tutarsız | O-9 "bu sürüme dahil mi?" = **AÇIK**; yeni O-9a "hangi dilim?" = **D15/1: DL-3**. AC-33 "koşullu — O-9 AÇIK"; engel listesi ve DL-3 önkoşulu aynı durumda. |
| G5 | P2 | Ortak yapı taşlarının ilk yazarı belirsiz | Tek sahip ve yazar **MOD-0126 DL-1** (iskelet, ortak makbuz ve iki indeksi, veri kapsamı kaynağı, LE/para birimi/kayıt kodu portları, CentralAudit kaydı). AP, AR ve Cash Positioning DL-1'leri ona bağımlı ve yeniden tanımlamaz; aksi ifadeler kaldırıldı; AP O-21 kapandı. |
| AP | ek | Alacak dekontu kapasitesi | Asıl faturada `CreditReservedAmount`, `CreditedAmount`, `CreditStatus`; `OpenAmount = Gross − CreditedAmount`. Kural: onaylı + bekleyen dekontlar ≤ **brüt tutar** (sabit, sıradan bağımsız). Gönderimde rezerv; onayda (tx2 ya da Reconcile) kapasite yeniden kontrolü + rezervden düşüm + `OpenAmount` + durum **tek işlemde**. **Sahip ek düzeltmesi:** CAS çakışmasında fatura yeniden okunur ve kapasite yeniden hesaplanır (kör tekrar değil). Yeni kod `AP_CREDIT_NOTE_EXCEEDS_CAPACITY` (422); AC-35 (1 000 / 600 / 400 örneği), AC-36 (a)–(d) yarış ve belirsizlik + sabotaj. |

**Ajanın eklediği, sahip teyidi beklenen iki güvenlik kuralı** (D9–D15'te yok):
1. Reconcile'ın yeniden başlatması yalnız **asıl aktörden** gelebilir (yeni 409 `…_RECONCILE_RESTART_ACTOR_MISMATCH`), çünkü uzak başlatma kullanıcının token'ıyla yapılır ve başka kullanıcı iş akışı başlatanı olup SoD R2'yi bozar.
2. Görev geçişi / kanıt bağlama yeniden denemesi, uzak tarafın anahtarı yeniden oynattığı ölçülene kadar yapılmaz.

Kontroller (Treasury Intake CT, commit öncesi): dört pack'te 24 bölüm; AC kimlikleri tekil (35 / 36 / 36 / 34);
`pack_approval.slices()` dördünde liste döner; "zaman aşımı/404 → Cancelled" geçen her satır olumsuzlama. Bağımsız
inceleme (CT-APR-02) yapılmadı; build/test yok.
