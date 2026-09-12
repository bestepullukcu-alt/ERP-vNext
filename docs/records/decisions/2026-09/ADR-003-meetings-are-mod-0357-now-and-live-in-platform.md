# ADR-003 — Toplantılar MOD-0357 kimliğiyle şimdi başlar ve Diten.Platform'da barınır

| | |
|---|---|
| **Durum** | Kabul edildi |
| **Tarih** | 2026-09-11 |
| **Karar veren** | Sahip (ürün: 2026-09-10 üç karar, 2026-09-11 öne çekme + barınma) · CONTROL TOWER (mimari) |
| **İlgili** | Blueprint 8.1 `MOD-0357 Management Review & Cadence` (W-5) · `execution/registries/module-id-registry.md:160` · MOD-0024 kapanış paketi "The meeting case" · BL-026 davet yanıtı · BL-064 inceleme toplantısı seçeneği · BL-015 takvim · BL-340 `tasks` bölme tetikleyicisi · ADR-001 §3 (izinler kaynak modülde) |
| **Etkilenen** | `execution/domains/management-governance/domain-config.md` · gelecek MOD-0357 paketi · `Diten.Platform` · Görev Merkezi sağlayıcı listesi |

---

## Soru

Yönetici toplantı modülünden dört şey istiyor: toplantıdan **önce**, toplantı **sırasında** ve **tutanak yazılırken**
görev açmak; görevin içinden o göreve **bağlı toplantı** açmak. Depoda toplantı için kimlik, paket ve kod yok.
Üç soru: **hangi kimlik**, **ne zaman**, **nerede barınacak**.

## Karar

1. **Kimlik: `MOD-0357` — Management Review & Cadence** (ürün adı: *Toplantılar*). Blueprint kanonik; DCP-002 ön kontrolü
   2026-09-11'de geçti (`verify_module_id.py --check-id MOD-0357`: OK). Yeni kimlik ya da CAND-CAP açılmaz. Blueprint'in kendi
   notu bizim modelle aynı: kararlar MOD-0007'nin, görevler MOD-0024'ün, kurullar MOD-0359'un; MOD-0357 yalnız toplantı
   döngüsü, gündem, katılım, sonuç ve tekrarlama kayıtlarının sahibidir.
2. **Sıra: şimdi.** Blueprint 5. dalgaya koymuştu; sahip öne çekti. Henüz olmayan bağımlılıklar **ertelenir, uydurulmaz**:
   MOD-0359 kurullar → toplantı türü ayarıyla idare edilir · MOD-0007 karar kaydı → kararlar tutanakta ayrı satır olarak
   durur, MOD-0007 gelince bağlanır · kanıt bağlama → Doküman Yönetimi sözleşmesi hazır olunca · MOD-0023 iş akışı →
   ilk aşamada tutanak onayı iş akışsız (yayınla/kilitle) · MOD-0026 zamanlayıcı → mevcut Hangfire seam'i.
3. **Barınma: `Diten.Platform`.** Blueprint yerleşimi "Domain App (Management & Governance)"; o servis yok ve yeni servisler
   DCP-006 OD-04'ün açık kapılarına (izin zorlama entegrasyonu, denetim sözleşmesi) takılı. Sapma burada kayıtlı;
   taşınabilirlik sınırları paketin sözleşmesidir: kendi koleksiyonları (`meeting_*`), kendi izin ailesi
   (`platform.meetings.*`), `TaskItem`'a alan eklenmez, görevlere yalnız **bağ kaydı** üzerinden dokunur; Görev Merkezi'ne
   dördüncü `IWorkItemProvider` olarak girer (ADR-001 §3), MOD-0024'ün `relatedRecords`'u bağ kaydından okunur.
4. **Model ilkesi:** toplantı ve görev **iki ayrı kayıt**, bağ **çoka-çok** ve iki yönlü. Tutanak toplantının kapanış
   kaydıdır; toplantıdan doğan görevin kapanış kaydını görevi yapan yazar; ikisi birleşmez, görevin kapanışı tutanağı
   değiştirmez. Bağ asla örtük engel değildir — tek istisna görev tipinin mevcut `reviewMeetingPolicy.required` kuralı;
   kilit toplantı **yapıldı** işaretlenince açılır, planlanınca değil.
5. **Aşama 1 kapsamı (paket ayrıntılar):** toplantı kaydı (tür, zaman, yer/bağlantı, düzenleyen, katılımcılar, gündem) ·
   davet ERP içinde (Görev Merkezi "yanıt bekleyen": kabul / ret) + `.ics` e-posta (mevcut SMTP; sağlayıcı isteğine ek
   gerekir) · toplantıdan görev: önce (hazırlık), sırasında (aksiyon, hızlı giriş), tutanakta (karar → görev) — hepsi normal
   görev · görevden toplantı (`scheduleReviewMeeting`) · tutanak: katılım, kararlar (görev değil), taslak → yayınlandı,
   düzeltme gerekçeli yeni sürüm · devam toplantısı: öncekinin açık aksiyonları gündemin başında · erteleme/iptal: bağlı
   görev sahiplerine bildirim, görev değişmez · toplantı türü ayarı (gündem şablonu, aksiyonların varsayılan görev tipi,
   kalite kaydı mı / e-imza gerekir mi — QA doldurur) · 7 dil · yetkiler ve kiracı sınırı.
6. **Ertelenen:** Google Calendar/Meet (Aşama 2) · tutanak metnine e-imza (DM e-imzası yalnız alan özetini imzalar;
   QA kararı) · dosya ekleri (üründe depo yok) · dış katılımcıya görev · oda rezervasyonu · tekrarlayan seri · yapay zekâ
   aksiyon önerisi · Kurumsal Strateji "Strategic Reviews" ekranlarının bu motora bağlanması.

## Neden

- Kural önce Blueprint'e bakmayı şart koşuyor; MOD-0357 tam bu yeteneği tanımlıyor. Yeni kimlik açmak DCP-002 ihlali olurdu.
- SAP (aktivite → takip kaydı, belge akışı), Oracle (randevu → görüşme raporu → takip görevi; Primavera Unifier tutanak
  satırı → aksiyon kaydı), Microsoft Teams (not → Planner görevi): hepsi **iki kayıt + bir bağ**. Görevi toplantının
  içine koyan yok; koymanın sonucu görevi kapatan kişinin yazmadığı bir tutanağın yazarı olmasıdır.
- ISO 9001 §9.3.2 yönetim gözden geçirmesinin zorunlu girdisi "önceki toplantının aksiyon durumu"; şirketin kalite
  belgelerinde Yönetim Gözden Geçirme SOP'u ve MGMT-REVIEW görev tipi (GxP kalite kaydı önerisi) var → tutanağın kayıt
  statüsü toplantı türü ayarıdır, kodda uydurulmaz.
- Ayrı servis, tesisatı çekilmemiş binadır: görev, bildirim, denetim izi, izin, e-imza Platform'da hazır; bağ aynı süreçte
  kurulur, ağ köprüsü yok.

## Sonuçlar

- `management-governance/domain-config.md` MOD-0357'yi kanonik yetenek olarak alır, barınma sapmasıyla.
- Paket `@module-pack-author` ile `draft` yazılır; sahip onaylayınca `ready-for-dev`. Kayıt defteri durumu paket onayında güncellenir.
- BL-340 (tasks modülünü ayar/çalışma diye bölme) tetikleyicisi geldi: toplantı kendi modülü ve kendi izin ailesiyle
  gelir, `tasks` bölünmez → madde kapatılabilir (backlog kaydı ayrı).
- Toplantı ↔ görev bağı **ayrı koleksiyon** (çoka-çok, kaynak/hedef modül + kimlik + bağ türü: hazırlık · gündemde ·
  toplantıdan doğdu); ilk sahibi MOD-0357, MOD-0024 yalnız okur; ikinci tüketici çıkarsa ortak yapıya çıkarılır.
