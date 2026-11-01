# HCM-LEAVE-01 — Keşif kararları (iş paketi kaydı)

> Control Tower kaydıdır; onay, paket ya da dağıtım izni değildir. Kod, commit izni ya da kapsam onayı vermez.

## Künye

- WP ID: `HCM-LEAVE-01` · Görev sınıfı: yönetişim · Risk: LOW
- Görev kaynağı satırı (birebir): `Keşif kararları: MOD-0280 Time, Attendance & Leave Management izin ve devamsızlık parçası için iki verify_module_id.py --discover raporu (docs/records/audits/2026-10/hcm-leave-discovery-01/ @5debfa84e; "Leave & Absence" çıkış 0, "Time, Attendance & Leave Management" çıkış 3, 8 bulgu); her bulgu için karar (parent / FU / child / yeni) ve gerekçe`
- Kuyruk: `hcm-leave` (`execution/domains/human-capital-management/work-packs/hcm-leave/CT-QUEUE.tsv`, taban 71705738598ec19e5ee8c2e1e0ce4142ff2f601d). Kayıt satırı `main`'de **değil**: yalnız `chore/ct/hcm-leave-ct-01` dalında, `owner_confirmation confirmed`; PR, ilk işçi başlatılırken iki HCM kulesi için tek PR olarak açılır (sahip kararı `dalga-sonu`, `not-ekle`, 2026-10-27). O zamana kadar yalnız hazırlık.
- Repository: `ERP-vNext` · Branch: `chore/ct/hcm-leave-ct-01` · Ölçülen HEAD: d9849746894ee3fc312b2484ed8409eb9afc97d9 (origin/main 3a607547e + 5 yerel commit) · Worktree: `.claude/worktrees/hr-leave-ct-01`
- Yetki: MOD-0280 için module pack yok (kimlik kaydında `reserved / planned`); tek çocuk `MOD-0280-FU01 Time Entry & Weekly Timesheet` (`ready-for-dev`, bu kulenin değil). Kule–modül dağılımı sahip kararı `dagilim-evet` (2026-10-27): bu kule MOD-0280'in izin ve devamsızlık parçaları; MOD-0251 Core HR / Employee Master ve `Diten.HcmService`'e yazmaz.
- Araç: `.antigravity/scripts/verify_module_id.py` sha256 `72d27f77dcf4aa6c40cf6bd6c44808fe79b15c9911f4cae835da637e0763368f`

## Keşif satırları (kart §0.1)

Keşif: incelenen kaynaklarda eşleşme bulunamadı · rapor docs/records/audits/2026-10/hcm-leave-discovery-01/MOD-0280-leave-absence.txt@5debfa84e7eba4457663fa4dd6c9967723479995 · sha256 873bb0bf8aec086271da31f3fc0957fca65b4bfe4e411e29fe234577e0bdc463
Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/hcm-leave-discovery-01/MOD-0280-time-attendance-leave-management.txt@5debfa84e7eba4457663fa4dd6c9967723479995 · sha256 427f305dd8a4114b5617851adcc494cae3be66baeaca7d313da91dc3057cf52c

Sorgular: `--discover "Leave & Absence" --sor "Leave Balance"` · `--discover "Time, Attendance & Leave Management" --resource time-attendance-leave`. Koşu: 2026-10-27, origin/main 3a607547e. İkisi de `--check-report` → RAPOR GEÇERLİ.

**Neden iki rapor (sahip kararı `iki-rapor`, 2026-10-27):** araç adları birebir eşleştirir. "Leave & Absence" sorgusu çıkış 0 verdi, ama bu işin yeni olduğunu kanıtlamaz: bilinen çakışmalar yalnız üst modülün adıyla görünür. Tek başına commit edilseydi "çakışma yok" diye yanlış bir kanıt kalırdı.

**Satır sonu notu:** Windows'ta ilk koşu (`> dosya`) satır sonlarına CR ekledi ve `--check-report` "RAPOR BOZUK: satır sonunda CR var" dedi (teknik hata). Keşif bir kez, `sys.stdout.reconfigure(newline='\n')` ile LF yazılarak yeniden koşuldu. Araç ve sorgu değişmedi. Diğer Windows kulelerini de etkileyebilir; araç düzeltmesi önerisi `.antigravity/**` değişikliğidir ve sahip onayı ister.

**Kişisel veri kontrolü:** iki raporda e-posta adresi yok (`grep` ile ölçüldü, 2026-10-27); maskeleme gerekmedi.

## Keşif kararları — her BULGU için (kart §0.1 "inceleme gerekli")

Sahip kararı: `hepsi-evet` (2026-10-27, CT · İzin ve devamsızlık sohbeti) — aşağıdaki 8 kararın tamamı.

### "Time, Attendance & Leave Management" raporu (8 bulgu)

1. blueprint `MOD-0280 'Time, Attendance & Leave Management'` — **parent (mevcut modül)**: izin ve devamsızlık bu modülün parçasıdır; Blueprint_Data SoR "Time Entry; Attendance; Schedule; Leave Balance", Soft Pages "Leave Request; Absence Record" dahil. İş yeni bir alt modül (FU) olarak açılır; yeni üst kimlik açılmaz.
2. blueprint `MOD-0315 'Time, Attendance & Leave Workspace'` — **ayrı modül, tüketici**: Blueprint onu "Workspace over MOD-0280 internal time, attendance, and leave records" diye tanımlar (SoR_Map). İzin kulesi onun sayfalarını (Leave Balance View, Absence Record View, Timesheet / Roster Interface) yapmaz; çalışanın kendi bakiyesi izin talebi sayfasında gösterilir.
3. sor `'HR leave request views; attendance interface snapshots; exception routing record'` sahibi MOD-0315 — **üretici–tüketici ilişkisi, çakışma değil**: asıl kayıt (izin talebi, bakiye) MOD-0280'de; MOD-0315 bunların görünümlerinin sahibidir.
4. registry `MOD-0280 (reserved / planned)` — **parent**: kimlik kullanılır. Kayıt notu devamsızlık, vardiya ve izin bakiyelerini "sonraki çocuklar" olarak yazar; izin parçası için ayrı bir alt kimlik (öneri `MOD-0280-FU02`, onay değil) HCM-LEAVE-02'de ayrılır.
5. paket `CAND-CAP-0036-time-attendance-leave-interface.md` (yalnız `origin/hr-future`, `draft`) — **başka modül, dokunulmaz**: eski adı MOD-0315; `main`'deki `Diten.HumanCapitalService` `time-attendance-leave` kodu yalnız hazırlık kaydıdır (`TimeAttendanceLeaveReadinessMetadata`) ve izin talebini ve bakiyeyi kendisi reddeder (`TimeAttendanceLeaveGuard.cs:156,161`@c51568aa8). Kimlik kaydı da "a different module, not this SoR" der. İzin bu kodu kullanmaz, değiştirmez.
6. paket `MDM-079-time-and-attendance-ukg-kronos.md` (8 eski dalda, `draft`) — **eski model, engellemez**: dış sağlayıcı (UKG/Kronos) yaklaşımı; Blueprint CHANGE_LOG UPD-04 (2026-06-15) MOD-0280'i dahili İK modülüne çevirdi. Kullanılmaz.
7. paket `MOD-0280-time-attendance-ukg-kronos-external-provider.md` (yalnız `origin/hr-future`, `ready-for-dev`) — **kimlik çakışması, engellemez**: aynı MOD-0280 kimliği eski dış sağlayıcı anlamıyla kullanılmış; `main`'de değil. Kullanılmaz. Çakışma Master'a gidecek Blueprint düzeltme talebine (HCM-LEAVE-03) not olarak eklenir.
8. gateway `time-attendance-leave` → 5063 (`HumanCapitalService`) — **izin bu rotayı kullanmaz**: rota hazırlık koduna gider. İznin kendi rotası farklı bir adla açılır; ad ve port paket taslağında (HCM-LEAVE-06) ve servis yeri kararında (HCM-LEAVE-04) belirlenir. `ocelot.json` yalnız `integration-agent` tarafından değiştirilir.

### "Leave & Absence" raporu

Bulgu yok (çıkış 0). Karar gerekmez; yukarıdaki "Neden iki rapor" notu geçerlidir.

## Sınırlar

- Rapordaki eksikler geçerlidir: bu makinede olmayan ya da son fetch'ten sonra itilen dallar, commit edilmemiş ya da başka makinedeki işler ve kayıtlı olmayan görev kaynakları görülmedi; paket iş kapsamı okunmadı.
- Bu kayıt HCM-LEAVE-02…08'i açmaz; her biri kendi kapısından geçer.
