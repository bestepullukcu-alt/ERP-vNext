# PVG-M1-01 — Keşif kararları (iş paketi kaydı)

> Control Tower kaydıdır; onay, paket ya da dağıtım izni değildir. Operasyonel runtime kapısı değişmez.

## Künye

- WP ID: `PVG-M1-01` · Görev sınıfı: yönetişim · Risk: LOW
- Görev kaynağı satırı (birebir): `Keşif kararları: MOD-0230 Case Intake & Triage, MOD-0231 Case Processing, MOD-0232 MedDRA Coding için verify_module_id.py --discover (2026-10-19, üçü de çıkış 3 — mevcut modül); raporların commit'i ve her bulgunun kararı`
- Kuyruk: `pvg` (`execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv`, kayıt `main`'de 4728cb804ef3616381dcb17c7c8484874d009227)
- Repository: `ERP-vNext` · Branch: `chore/ct/pvg-ct-01` · Ölçülen HEAD: 4728cb804ef3616381dcb17c7c8484874d009227 · Worktree: `.claude/worktrees/pvg-ct-01`
- Yetki: DCP-004 `pvg-urgent-w3-development-block` (`approved`, 2026-08-09) · paketler MOD-0230/0231/0232 `ready-for-dev`, `build_gate: open`, `operational_runtime_gate: closed`
- Araç: `.antigravity/scripts/verify_module_id.py` sha256 `37d6c64e08c1032b52288de73aab0f41e2abd9dd9f4d4d4f865e848e820b8eba`

## Keşif satırları (kart §0.1)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/pvg-m1-01-discovery-01/MOD-0230-case-intake-triage.txt@23490dd2ae7bdc8979e4bc7fa224ac9b16dba468 · sha256 ff14384e1b8d3dce3731a421e0f0892d6da7a92ac386dcde4be073b00ba33c85
Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/pvg-m1-01-discovery-01/MOD-0231-case-processing.txt@23490dd2ae7bdc8979e4bc7fa224ac9b16dba468 · sha256 66173c929017b6298f0b496a6f221e55e47a24ef0476814e38a780e40ece1d7f
Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/pvg-m1-01-discovery-01/MOD-0232-meddra-coding.txt@23490dd2ae7bdc8979e4bc7fa224ac9b16dba468 · sha256 0c5e53a91ff04b02d58184140a1707da4a6fbb228026eae681b5b701271f6136

Sorgular: `--discover "Case Intake & Triage" --resource pv-case-intake-triage --sor "Safety Cases"` · `--discover "Case Processing" --sor "Safety Case master record"` · `--discover "MedDRA Coding" --sor "Coded terms on Safety Case"`. Koşu: 2026-10-20, origin/main 4728cb804.

**Maskeleme notu (sahip kararı 2026-10-20, depo herkese açık):** MOD-0230 raporunun 6. satırındaki kişisel e-posta adresi commit'ten önce `[e-posta maskelendi]` ile değiştirildi; aracın bütünlük satırı ölçüldüğü gibi bırakıldı. Bu yüzden o dosyada `--check-report` tasarım gereği "RAPOR DEĞİŞTİRİLMİŞ" (çıkış 2) der; yukarıdaki sha256 maskeleme öncesi "RAPOR GEÇERLİ" ölçümüdür. MOD-0231 ve MOD-0232 raporları değiştirilmedi ve GEÇERLİ doğrular. Kalıcı çözüm (öneri, araç değişikliği — sahip + Codex görüşü ister): aracın raporu yazarken e-postayı kendisinin maskelemesi.

## Keşif kararları — her BULGU için (kart §0.1 "inceleme gerekli")

### MOD-0230 Case Intake & Triage (7 bulgu)

1. blueprint `MOD-0230 'Case Intake & Triage'` — **parent (mevcut modül)**: iş bu kimliğin kendisidir; yeni kimlik açılmaz.
2. sor `'Safety Cases'` sahibi MOD-0230 — **parent**: SoR nesnesi modülün kendi nesnesi; çakışma yok.
3. registry `MOD-0230 (ready-for-dev)` — **parent**: satır var, `ready-for-dev` yalnız build/test kapısı (satır 152); runtime kapısı kapalı kalır.
4. paket `MOD-0230-case-intake-triage.md` (438 ref) — **parent**: mevcut paket kullanılır; sahip `main`'de `NY` (0860632f1).
5. gateway `pv-case-intake-triage` → 5011 — **mevcut iş, engel değil**: domain-config'in yetkilendirdiği tek route ailesi (4 route, ocelot.json 2785–2840); genişletilmez.
6. görev `PVG-M1-01` — **kendi satırı**; mükerrer iş değil.
7. görev `PVG-M1-02`, `PVG-M1-03` — **kendi kuyruğunun alt işleri**; bu kaydın çıktısını tüketirler.

### MOD-0231 Case Processing (6 bulgu)

1. blueprint `MOD-0231 'Case Processing'` — **parent (mevcut modül)**; teslim dilimi "Signal Minimum Scope" bir etiket, modül adı değil (registry satır 153).
2. sor `'Safety Case master record'` sahibi MOD-0231 — **parent**; çakışma yok.
3. registry `MOD-0231 (ready-for-dev)` — **parent**: yalnız operasyonel olmayan sınıf-kütüphanesi sözleşme/test kapısı.
4. paket `MOD-0231-case-processing.md` (438 ref) — **parent**: mevcut paket; sahip kararı aşağıda.
5. gateway `hr-case-management` → 5063 — **yanlış pozitif, ilgisiz**: "Case" sözcük benzerliği; HCM alanı (Employee Relations & HR Case Management). PV ile sözleşme, nesne ya da route ortaklığı yok; MOD-0231 için Gateway route'u yoktur ve açılması paket gereği yasaktır.
6. görev `PVG-M1-01`, `PVG-M1-04` — **kendi kuyruğunun satırları**; mükerrer değil.

### MOD-0232 MedDRA Coding (6 bulgu)

1. blueprint `MOD-0232 'MedDRA Coding'` — **parent (mevcut modül)**.
2. sor `'Coded terms on Safety Case'` sahibi MOD-0232 — **parent**; çakışma yok.
3. registry `MOD-0232 (ready-for-dev)` — **parent**: MSSO lisans kanıtı ve CODESET kaynak sözleşmesi olmadan hiçbir MedDRA verisi onaylı değildir (satır 154).
4. paket `MOD-0232-meddra-coding.md` (438 ref) — **parent**: mevcut paket; sahip kararı aşağıda.
5. gateway — bulgu yok (0); beklenen: paket route açılmasını yasaklar.
6. görev `PVG-M1-01`, `PVG-M1-05` — **kendi kuyruğunun satırları**; mükerrer değil.

Sonuç: üç modülde de yeni iş yoktur; iş, var olan üç modülün bitirilmesidir ("Finish"). Hiçbir bulgu dağıtımı engellemez; dağıtım yine de her satırın kendi DoR'una (kart §1) bağlıdır.

## Açık kararlar

- "sahibi belirsiz: MOD-0230 (owner 'TBD')" — **kapandı**: `origin/main` paketinde `owner: NY`; TBD değeri yalnız iki eski dalın blob'undan (cf4399124 `feature/pvg/mod-0230-build-test-governance`, 4004269a9 `feature/pvg/dcp-004-urgent-w3-governance`).
- "sahibi belirsiz: MOD-0231, MOD-0232 (owner 'TBD')" — **sahip kararı 2026-10-20 (depo sahibi, PVG Control Tower sohbeti): paket sahibi `AT` (rol etiketi; kişisel e-posta yazılmaz).** Kuyruk satırları M1-04 / M1-05 güncellendi. Paket künyesindeki `owner: TBD` alanı bu kayıtla değişmez: kural başlangıcından sonra değişen paket `pack_approval.py` onay satırı ister (kart "Paket onayı"); künye güncellemesi ayrı, onay satırlı bir adımdır.
- DCP-004 kimliği dört dosyada (`pvg-urgent-w3`, `corporate-collection`, `mod-0290-sku`, `work-aggregation-task-center`) — PVG'yi engellemez; kayıtlı çakışma notu yok; sahibe bildirildi 2026-10-20.

## Durum geçişleri (bu kayıtla)

`PVG-M1-01`, `PVG-M1-02`, `PVG-M1-03`, `PVG-M1-04`, `PVG-M1-05`: DISCOVERED → INSPECTION_REQUIRED. Kanıt (ct-sop7-transitions): keşif satırı + araç raporu (`rapor yol@commit`, `rapor-sha256`) = bu kaydın "Keşif satırları" bölümü. Sonraki geçiş INSPECTED, "inceleme notu yol@commit (kod ve çalışma gerçeği ölçüldü)" ister; bu kayıt o not değildir.

## Sonraki iş (öneri — onay değil)

`PVG-M1-02` MOD-0230 kanıt envanteri: `services/Diten.PvgService/**`, `gateway/.../ocelot.json` route ailesi ve `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` için kod/çalışma gerçeği ölçümü ve kabul kaydı olmayan işlerin listesi; kod yok.
