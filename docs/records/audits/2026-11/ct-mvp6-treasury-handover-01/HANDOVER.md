# MVP-6 CT ve Treasury CT — devir notu (2026-11-17)

Yazan: MVP-6 CT oturumu (Claude), Natig adına. Devralan: MVP-6 ve Treasury işlerini devralan sahip.
Ölçüm tabanı: `origin/main` @ `1bc6cff17` (2026-11-17). Bu not gizli bilgi, parola ya da bağlantı adresi içermez.

**Sınırlar.** MVP-6 bölümü bu oturumun kendi kayıtlarından ve depodan ölçüldü. Treasury bölümü depodaki karar kayıtlarından
(`treasury-intake-owner-decisions-06.md`, `-07.md`) ve MVP-6 oturumunun notlarından geldi. Treasury Intake CT oturumu
(`f8880468…`) bu notu yazmadı; onun 2026-11-09 sonrasındaki iç durumu burada yalnız depoda görünen kadarıyla var.

## 1. Açık PR'lar

| PR | Ne | Durum | Sonraki adım |
|---|---|---|---|
| #436 | MOD-0183 r11 | **kapatılacak** | Dalında bozuk bir inceleme satırı var (`a51c84d76`: 6c8c5c95 satırı yerine araç hata metni). Beste 2026-11-17'de 2. yolu onayladı |
| yeni (r11 temiz dal) | MOD-0183 r11, aynı paket sürümü `6c8c5c95` | açılıyor | Dal `docs/sce/mod0183-r11-02`; satırlar `pack_approval.py review-row` / `row` ile üretildi. Beste onaylar ve merge commit ile birleştirir |
| #434, #435 | Treasury paketi, DCP-011 D145/D146 | main'de | DCP-011 F-1 (satır 145, MOD-0220 "§22 proposed"): bir sonraki revizyonda, son tarih 2026-11-23 |

## 2. MVP-6 CT kuyruğu

| İş | Durum | Kapı / sahip | Son tarih |
|---|---|---|---|
| MOD-0183 **DL-8** (korelasyon kökü sunucuda, R11-1…R11-8, SHIPMENT-BUNDLE 3.5.1) | **acil**; kod r11 main'e girince başlar | Kod oturumu "MOD-0183 dev fixes" (`local_8083df30…`); MVP-6 CT | Beste'nin MOD-0177-FU01 canlı turu buna bağlı; dev'e gelince Beste'ye haber verilecek |
| MOD-0183 DL-9 (Details ekranında GUID yok) | DL-8 ile ya da sonra | MVP-6 CT | — |
| MOD-0183 DL-5…DL-7 (r10: depo alımı, reservationId, Item) | r10 onaylı (`2df5a5f8`) | DL-5: DL-8 main'de + WMS-BUNDLE 1.1.0 FROZEN + SHIPMENT-BUNDLE 3.6.0 yama dosyası incelenmiş + Gateway rotası (integration-agent). DL-7: `Item` taşıyan ilk FROZEN WAREHOUSE-OUTBOUND / WMS-BUNDLE (beklenen 1.2.0 / 1.4.0, MOD-0178) | — |
| Returns (MOD-0186), Claims (MOD-0187), Loads (MOD-0185) | Gateway arkasında aynı korelasyon hatası | Her biri için ayrı pack revizyonu (MOD-0183 §26.5) | — |
| SHP kayıt kodu (RCS-001) | açılmadı | MOD-0183 FU | — |
| MOD-0148 DC-03 revizyonu (P1–P4) | 2. tur CT-APR-02 UYGUN (`94745e7c`); dal `docs/sce/mod-0148-dc03-consumer-rules-01` @ `a4d649cae` itildi | Kalan: bulgu dosyasının commit'i, `reviews.tsv` / `approvals.tsv` satırları (araçla), PR, Beste onayı | — |
| MOD-0140 r9.3 inceleme dosyası | dal `docs/sce/mod0140-r93-review-01`; dosya ve satırı main'de yok | Cihan (Procurement) / MVP-6 CT | — |

### Açık MVP-6 sahip kararları ve sorular

- **OD-R10-4:** r10'da açık kalan madde (Beste'nin r28 cevabını bekliyor).
- **OD-R10-6:** MOD-0178 metni (Item adımının sırası ve sürüm numaraları). Sahibi MOD-0178 (Beste, XMC-001). MOD-0178,
  MOD-0178-FU01 paketi ve `mod0178-fu01-outbound-form-v2-owner-decisions-01.md` hâlâ 1.1.0 / 1.2.0 diyor
  (`mod0183-r11-owner-decisions-2026-11-16.md`).
- **MOD-0148 F-2 / F-8 (Auth, Ali):**
  - iki portal izni Auth'un otomatik verilmeyen listesine girsin (D2 korunur);
  - FU01 AC-19 (401) ile SQ-3 kaydı (403) arasındaki fark.

  İkisi de Ali'ye iletilecek; henüz gönderilmedi.
- **BL-652 Opt 2 + `service_name`:** FU29'dan sonra (Ali, tarih belirsiz). MOD-0148 bunu bekliyor.
- **DC-04 / DC-05:** ertelendi (servis yok).

## 3. Treasury CT kuyruğu (depodan)

| İş | Durum | Kapı / sahip | Son tarih |
|---|---|---|---|
| DCP-011 kod alanı (D145, altı üye) ve D146 (TRM-14 kapısı `MDM-0220-DL1` kabulüyle) | main'de (#435) | F-1 düzeltmesi bir sonraki DCP revizyonunda | 2026-11-23 |
| WP-0289-DL1 (MOD-0289 banka ana verisi) | dağıtım kayıtları ve D141–D144 main'de (#434) | P-12 / P-13: Ali'nin dört MDM denetim dosyası için XMC-001 cevabı; rapor gelince kesin işlem adları | — |
| WP-0126-DL1 prompt v1.0 taslağı | dal `docs/ct/treasury-wp0126-dl1-p-01` @ `1f1a592ae` itildi; paket pini eski (`e8e5ce47`) | Pin güncellenip prompt kapısından geçmeli | — |
| AGR öneki (D114, RCS-001 MOD-0217 CTR → AGR) | dal `chore/ct/treasury-rcs-agr-01` @ `fbbdcdff0` itildi; main'de değil | `.antigravity/rules/record-coding-standard.md` değişir: korunan yol, sahip onayı gerekir; Ali'nin yazılı izni D114'te | — |
| `currency-minor-units` canlı yüklemesi | yüklü değil | MDM ekibi bakım penceresinin tarihini yazacak, sonra doğrulama çıktısını paylaşacak (iki küme `be823006` altında Published, currency-minor-units = 165) | tarih MDM'de |
| MOD-0220 §22 (TRM-14) | ölçüm | Fark çıkarsa MDM'ye bildirilir | — |
| MOD-0179 | görüş bekleniyor | Treasury CT'nin ayrıntılı görüşü; 3PL paketine etkisi olursa karşı taraf döner | — |
| MOD-0057 | park | FU28 (Ali) | — |

## 4. Süren oturumlar

| Oturum | Ne | Durum |
|---|---|---|
| CT · Tedarik Zinciri (`local_fd59ba5f…`) | MVP-6 CT; bu notu yazan oturum | devrediliyor |
| MOD-0183 dev fixes (`local_8083df30…`) | r11 taslağı; DL-8 kodunu yazacak | r11 main'de bekleniyor |
| MOD-0148 pack: DC-03 portal decisions (`local_4a2e6676…`) | MOD-0148 revizyonu | CT-APR-01 satırı ve PR bekliyor |
| CT-APR-02 inceleme oturumları (`local_d8c415de…` r11, `local_0e1473af…` MOD-0148, `local_de7b3a8e…` r10) | salt okunur | iş bitti; kapatılabilir |
| Treasury Intake CT (`f8880468…`) | Treasury CT | durumunu kendisi ölçmeli (bkz. Sınırlar) |

## 5. Başka ekiplere verilmiş sözler

- **MVP-1 (Beste):** r11 DL-8 kodu dev'e gelince haber; MOD-0177-FU01 canlı turu o zaman tamamlanır. Turda gerçek bir SKU
  kullanılacak (sözleşme örnek SKU'su dev Product Master'da yok).
- **MVP-5 / MOD-0178 (Beste):** onay satırları main'e girince haber (r10 verildi; r11 yeni PR ile).
- **MDM / Auth (Ali):**
  - MOD-0148 F-2 ve F-8 istekleri (Ali'ye iletilecek);
  - WP-0289-DL1 P-13 XMC-001 hatırlatması (MDM tarafı da hatırlatıyor);
  - AGR (D114) main'e alma.
- **MDM (currency-minor-units):** yüklemeden sonra doğrulama çıktısını MDM paylaşacak; Treasury kontrol edecek.
- **MVP-4:** bu oturumun kayıtlarında MVP-4'e verilmiş bir söz yok; Treasury Intake CT oturumu ayrıca ölçmeli.

## 6. Dallar, worktree'ler ve stash'ler (2026-11-17 ölçümü)

Komutlar depo kökünde koşuldu: `git fetch origin --prune`, `git branch -vv`, `git log --branches --not --remotes`,
`git worktree list`, `git stash list` ve her worktree'de `git status --short`. Bir commit'in yamasının (`git cherry`) ve
dosyalarının (`git cat-file -e origin/main:<yol>`) main'de olup olmadığı ayrıca ölçüldü.

### 6.1 Sorulan uzak dallar

| Dal | Karar | Gerekçe |
|---|---|---|
| `docs/sce/mod0140-r9-review-01`, `docs/sce/mod0140-r81-review-file-01` | sil | Dosyalar main'de |
| `docs/ct/treasury-mod0057-owner-01` | sil | D131, D138, D140 #434 ile main'de; MOD-0057 paketi aynı |
| `docs/sce/mod0140-r93-review-01` | PR | r9.3 inceleme dosyası (`2026-11-05-6cb57a5a.md`) main'de yok; `reviews.tsv`'de satırı da yok. Cihan ile birlikte karar |
| `docs/sce/dc02-supplier-eligibility-draft-01`, `feature/sce/mod-0147-0148-dc02-eligibility-seam-01` | eskidi | DC-02 D2 MOD-0147 / MOD-0148 paketlerine girdi (main) |
| `feature/sce/mod-0148-dc03-decision-prep-01`, `docs/sce/dc03-supplier-actor-binding-draft-01` | eskidi | DC-03: MOD-0360-FU01 r2 (Ali onaylı) ve DC-03 karar kaydı; yeni MOD-0148 revizyonu `docs/sce/mod-0148-dc03-consumer-rules-01` |
| `contracts/shipment-bundle-3.3-demand-history` | eskidi | SHIPMENT-BUNDLE main'de 3.5.0; 3.3.0 notu ayrıca gerekmez |
| `feature/sce/mvp6-g5-logistics-golden-flow`, `feature/sce/contract-example-walker` | eskidi | MVP-6 entegre doğrulama (Q151) ve 6 ekran VER main'de. Ekim raporlarının bir kısmı main'de yok; gerekirse arşiv olarak saklanır |
| `feature/fin/treasury-intake-prep` | eskidi | Treasury intake DCP-011 ve karar kayıtları ile main'de |
| `fix/platform-mdm-403-unavailable` | sil | Tek commit'in yaması main'de (`git cherry`: 0/1) |
| `feature/pvg/*` (4), `feature/pss/*` (5), Ağustos | MVP-6 / Treasury dışında | Sahibi Natig karar verir |

### 6.2 Yalnız yerelde olan dallar

İtildi (2026-11-17): `docs/sce/mod-0148-dc03-consumer-rules-01`, `chore/ct/treasury-rcs-agr-01`,
`docs/ct/treasury-wp0126-dl1-p-01`, `docs/sce/mvp6-plan-v10-01` (v10.2 planı).

| Yerel dal | Karar | Gerekçe |
|---|---|---|
| `docs/ct/treasury-dcp011-mod0217-code-01` (`f4b5ab78a`, D137) | sil | Yerini D145 (#435) aldı |
| `docs/ct/treasury-dcp011-row-fix-01` | sil | DCP-011 onayları main'de (`168c71b0`, `293fdab5`) |
| `chore/ct/treasury-mod0048-r2-01` | sil | Yerini r2-02 / r2-03 aldı (main) |
| `chore/ct/treasury-wp0289-dl1-dispatch-01`, `docs/ct/treasury-wp0289-dl1-p-01…03`, `docs/ct/treasury-mod0120-crm-01` | sil | İçerik #434 ile ya da önceki PR'larla main'de |
| `docs/sce/mod0147-v1-scorecard-01`, `docs/sce/mvp6-decisions-20261101-01`, `feature/sce/mod0186-receipt-exit-01` | sil | Dosyalar main'de (sonra revize edilmiş) |
| `docs/sce/wp-p1-g2-g4-patches-01` | eskidi | WP-P1 r2; yerini r3…r6 aldı (main) |
| `feature/sce/source-intake-le-01` | eskidi | Tüzel kişi düzeltmesi main'de; yalnız `gateway_tour_probe.py` main'de yok |
| `docs/sce/mod0183-r11-01` | sil (yeni PR birleşince) | Yerini `docs/sce/mod0183-r11-02` aldı |
| PVG / PSS yerel dalları (Ağustos) | MVP-6 / Treasury dışında | Sahibi Natig karar verir |

### 6.3 Worktree'ler ve stash'ler

- Altı Codex worktree'si (`~/.codex/worktrees/…`, Eylül, `4a8d4d4b3`) commit edilmemiş değişiklik taşıyor (33–99 dosya).
  Bunlar MOD-0186 / 0187 / 0190 / 0192 ve sevkiyat entegrasyonunun Eylül denemeleri; MVP-6 çekirdeği sonradan main'e girdi.
  **Karar: eskidi**; silmeden önce Natig bakar.
- Ana klasörde izlenmeyen altı yol var:
  - `docs/records/audits/2026-10/` altında iki yol;
  - `docs/roadmap/plans/erp-finance-legal-module-intake-01/`;
  - `docs/vendor/MOD-0140-…`;
  - `services/Diten.Platform/tests/…/CompositionRootGuardTests.cs`, kaynağı belirsiz.

  Saklanacaksa PR ile gelmeli; **Natig karar verir**.
- `-webjs` worktree'sinde yalnız `node_modules` var. Diğer proje worktree'leri temiz.
- Stash'ler: üç tane, Ağustos ve Eylül (`feature/mvp6-logistics`, iki PVG dalı). **Karar: sil**; Natig onaylar.
