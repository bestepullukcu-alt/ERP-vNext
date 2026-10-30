# MOD-0280-FU02 — paket öncesi tasarım kararları 01

> Control Tower kaydıdır; paket onayı, kod ya da dağıtım izni değildir. Paket taslağı (HCM-LEAVE-06) bu kararları
> kaynak gösterir. Kaynak bulgular: bağımsız tasarım denetimi 01
> (`docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md`, KOŞULLU). F-02 kararı ADR-005 "Ek karar 1"dedir.

## Künye

- Kuyruk: `hcm-leave` · iş: `HCM-LEAVE-06` · dal: `chore/ct/hcm-leave-ct-01`
- Kule: CT · İzin ve devamsızlık · modül: MOD-0280-FU02 Leave & Absence Management (öneri kimlik, `planned`)

## D-1 — Kiracı içi erişim ve rol kapsamı (F-03)

- **Sahip kararı:** `erisim-kiraci` (2026-10-27, CT · İzin ve devamsızlık sohbeti).
- **Karar (ilk dilim):**
  1. **Çalışan:** yalnız kendi izin talepleri ve kendi bakiyesi; başka kişinin kaydı görünmez.
  2. **Yönetici:** yalnız kendisine MOD-0023 iş akışında atanmış onay görevleri ve o talebin özeti. Yetki, iş akışının
     atama anlık görüntüsünden hesaplanır (FU01 emsali `TimesheetApprovalService.cs:391–435`). Ekip görünümü yok.
  3. **İK:** izin türlerini ve izin haklarını yönetir; kiracıdaki bütün izin kayıtlarını görür (kiracı geneli kapsam).
  4. **Ayrı anahtarlar:** okuma ve yazma izinleri ayrıdır (çalışanın kendi okuması, talep oluşturma/iptal, İK okuma,
     tür yönetimi, hak yönetimi). Anahtar adları PKS-001'e göre paket taslağında verilir; `platform.*` ve
     `hcm.time-attendance-leave.*` kullanılmaz.
  5. **Onaylayan bulma:** FU01 deseni (`ApproverResolver.cs`): pozisyon → bağlı olunan pozisyon → yönetici; zincir yoksa
     havuz. Kişi kendi talebini onaylayamaz (havuzda dahil). Onaylayan bulunamazsa talep gönderilemez; otomatik onay yok.
  6. **Kiracı sınırı:** kiracı yalnız sunucuda çözülür; başka kiracının kaydı 404 döner (varlık sızdırılmaz).
- **Sonraya:** şirket (tüzel kişi) bazlı İK kapsamı sonraki dilime yazılır; çok şirketli kurulumda İK'nın bütün
  şirketleri görmesi ilk dilimin bilinen sınırıdır ve pakette yazılır.

## D-2 — İlk dilimde hassas veri sınırı (F-04)

- **Sahip kararı:** `hassas-kapali` (2026-10-27, CT · İzin ve devamsızlık sohbeti).
- **Karar (ilk dilim):**
  1. **Tür kategorisi zorunlu ve kapalı listeden:** İK yeni izin türü açarken bir kategori seçer: yıllık, mazeret,
     ücretsiz ya da diğer. "Sağlık / hastalık" kategorisi sistemde tanımlıdır ama **kilitlidir**; yapılandırmayla,
     yönetici ekranından ya da veri girişiyle açılamaz. Kilidi yalnız MOD-0314 HR Governance & Sensitive Access Controls
     kuralı ve hukuk onayıyla gelen yeni bir paket sürümü kaldırır.
  2. **Talep formunda serbest metin ve belge yok:** talep yalnız tür, başlangıç ve bitiş tarihi ve yarım gün seçiminden
     oluşur. Açıklama alanı ve belge yükleme ilk dilimde yoktur; tür tanımında "belge gerekir" seçeneği de yoktur.
  3. **Taşınan alanlar sınırlı:** onay görev kartı, bildirim e-postası ve denetim kaydı yalnız tür, tarih aralığı, gün
     sayısı ve durumu taşır. Kişisel ya da serbest metin alanı taşınmaz.
  4. **Tür adı denetimi bir güvence değildir:** "diğer" kategorisinde açılan bir türün adına bakarak sağlık bilgisi
     ayıklanmaz. Güvence, serbest metin ve belgenin hiç olmamasıdır. Bu sınır pakette bilinen risk olarak yazılır
     (ör. İK'nın "diğer" kategorisinde sağlık çağrıştıran bir ad vermesi).
- **Sonraya:** hastalık / rapor türleri, belge (rapor) yükleme ve açıklama alanı Dilim 2'de (HCM-LEAVE-07), MOD-0314
  kuralı ve hukuk onayından sonra.

## D-3 — Bakiye ve onay yaşam döngüsü (F-05)

- **Sahip kararı:** `bakiye-oneri` (2026-10-27, CT · İzin ve devamsızlık sohbeti) — aşağıdaki yedi kuralın tamamı.
- **Karar (ilk dilim):**
  1. **Bekleyen talep bakiyeyi ayırır:** kalan = hak − onaylı − bekleyen. Kalanı aşan talep gönderilemez (bakiyeden
     düşmeyen türlerde bu kontrol yoktur).
  2. **Çakışma:** aynı kişinin tarihleri çakışan ikinci talebi (bekleyen ya da onaylı biriyle) reddedilir. Aynı anda
     iki gönderimde yalnız biri kaydedilir; kayıt sürüm (ETag) denetimiyle korunur.
  3. **İptal:** başlamamış onaylı izni çalışan iptal edebilir ve günler bakiyeye geri döner. Başlamış izni yalnız İK
     iptal eder. Kısmi iade (izinden erken dönüş) sonraki dilimdedir. Bekleyen talebin iptali MOD-0023 örneğini iptal eder.
  4. **Karar tekilliği:** MOD-0023'ten gelen onay ya da ret kararı bir kez uygulanır; aynı karar yeniden gelirse etkisizdir.
     Karar ile iptal yarışında önce kaydedilen geçerlidir (FU01 emsali `TimesheetApprovalService.cs:239–243`).
  5. **Kural anlık görüntüsü:** talep, açıldığı andaki tür kuralını (kategori, bakiyeden düşer mi, onay gerekir mi)
     kendi üzerinde saklar. Sonraki tür ya da hak değişikliği geçmiş ve onaylı talepleri değiştirmez; bekleyen talepler
     ve yeni talepler yeni hakla hesaplanır.
  6. **Yarım gün ve gün sayısı:** yarım gün yalnız tek günlük talepte (sabah ya da öğleden sonra). Gün sayısı MOD-0362
     Working Calendar & Public Holidays'ten hesaplanır: hafta sonu ve resmi tatil düşülür, yarım tatil günü yarım sayılır
     (FU01'in `IWorkingHoursProvider` yarım gün kuralıyla aynı).
  7. **Yıl geçişi:** hak takvim yılına göre tutulur. Yıl sonunda kalan gün türün devir sınırına kadar yeni yıla taşınır,
     fazlası yanar; İK elle düzeltebilir (denetim izli). İki yıla yayılan talep her yılın bakiyesinden kendi günleri
     kadar düşer.
- **Bilinen sınır:** Türkiye'de yıllık izin genellikle işe giriş yıldönümüne göre işler (CT'nin genel bilgisi; hukuk
  kaynağıyla doğrulanmadı). İlk dilimde haklar elle girildiği için takvim yılı seçildi. Yıldönümü ve kıdem bazlı yasal
  hesap, hukuk kaynağı ve çalışan sözleşmesindeki `hireDate` / `seniorityDate` geldikten sonraki dilimdedir.
