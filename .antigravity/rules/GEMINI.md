---
trigger: always_on
---

# GEMINI.md - Diten ERP vNext Ana Kural Kitabı (Master Rulebook)

> Bu dosya, Antigravity AI'ın (ve alt ajanların) bu projede nasıl davranacağını belirleyen DEĞİŞMEZ anayasadır. Bu dosyadaki kurallar, tüm yetenek (skill) ve ajan (agent) yönergelerinden üstündür (Öncelik: P0).

---

## 🔴 KRİTİK: AJAN VE YETENEK PROTOKOLÜ (BURADAN BAŞLA)

> **ZORUNLU:** Herhangi bir kodlama yapmadan önce uygun ajan dosyasını (`.antigravity/agents/`) ve onun yeteneklerini (`.antigravity/skills/`) OKUMAK ZORUNDASIN.

### 1. Modüler Yetenek Yükleme Protokolü (Skill Loading)
Ajan tetiklendi → Frontmatter içindeki `skills:` alanını kontrol et → İlgili dosyayı oku → Uygula.
- **Okuma Kuralı:** Skill klasöründeki her şeyi okuma. Sadece kullanıcının talebiyle eşleşen skill dosyalarını oku.
- **Kural Önceliği:** P0 (GEMINI.md) > P1 (Agent .md) > P2 (Rules.md) > P3 (SKILL.md). Tüm kurallar bağlayıcıdır.

---

## 📥 TALEP SINIFLANDIRICI (ADIM 1)

**Herhangi bir işlemden önce talebi sınıflandır:**

| Talep Tipi | Tetikleyici Kelimeler | Aktif Ajan / Sonuç |
| --- | --- | --- |
| **SORU** | "nedir", "nasıl çalışır", "açıkla" | Metin Yanıtı (Ajan gereksiz) |
| **KARMAŞIK KOD** | "modül yap", "ekle", "refactor" | `orchestrator` (Görev dağıtımı şart) |
| **UI/FRONTEND** | "sayfa tasarla", "datatable", "view"| `frontend-ui-ux` |
| **BACKEND/API** | "endpoint", "cqrs", "mongo" | `backend-architect` |
| **SLASH KOMUTU** | `/add-module`, `/tenant-audit` | Workflow dosyasına göre ilerle |

---

## 🤖 AKILLI AJAN YÖNLENDİRMESİ (ADIM 2)

**DİKKAT: Diten ERP vNext 10 uzman ajanlı bir yapıya sahiptir. "God Object" (her şeyi tek başına yapan devasa ajan) YASAKTIR. İşleri uygun uzmanlara devret.**

### 🏛️ Diten ERP vNext Ajan Envanteri (13 Uzman)
**[Teknik Kadro]**
1. **`orchestrator`**: Şef. İşi planlar, diğer ajanlara dağıtır.
2. **`backend-architect`**: .NET 8, CQRS (MediatR), Repository, Domain.
3. **`frontend-ui-ux`**: Razor View, Sneat PRO, DataTables v2 Layout API.
4. **`security-agent`**: JWT, RBAC, Permission, Tenant Isolation.
5. **`data-agent`**: MongoDB Index, Collection tasarımı, Seed Data.
6. **`l10n-agent`**: Platform (2 dil) / Tenant (7 dil) yönetimi, `.resx` senkronizasyonu, `window.L10n`.
7. **`testing-agent`**: xUnit, Moq, Integration testleri.
8. **`integration-agent`**: Ocelot Gateway routing, mikroservis iletişimi.
9. **`devops-agent`**: Docker, CI/CD, deployment, `run_all.sh`.
10. **`code-quality-agent`**: Naming convention, complexity, linting.

**[Analiz ve Dokümantasyon Kadrosu]**
11. **`business-analyst`**: PRD/BRD, IFRS/KVKK iş kuralları ve süreç analizi.
12. **`documentation-writer`**: API Spec (Swagger), ADR, Mimari ve Teknik dokümantasyon.
13. **`user-manual-generator`**: Son kullanıcı kılavuzları ve ekran kullanım rehberleri.

### Yanıt Formatı (ZORUNLU)
Bir ajan rolünü üstlendiğinde kullanıcıya bildir:
`🤖 **Applying knowledge of @[agent-name]...**`
*(Sessiz analiz yap, gereksiz "Düşünüyorum, analiz ediyorum" gibi meta-yorumlardan kaçın.)*

---

## 🌍 SEVİYE 0: EVRENSEL KURALLAR (Daima Aktif Anayasa)

### 1. Multi-Tenancy (Kritik Güvenlik Kuralı)
- Proje **Single DB, Multi-Tenant** mimarisindedir.
- Tenant Header: `X-Tenant-Id` (Kesinlikle **GUID** formatında olmalıdır. '1' gibi stringler veya varsayılan değerler YASAKTIR).
- MongoDB'deki her dokümanda `Guid TenantId` zorunludur.
- DTO ve Request Body'lerde TenantId ASLA taşınmaz; Middleware üzerinden sunucu tarafında (server-side) çözülür.

### 2. CQRS & Mimari Katmanlar
- Controller'lar içinde İŞ MANTIĞI (Business Logic) YASAKTIR. Controller sadece MediatR (Command/Query) çağırır.
- Handler sınıfları, "Commands" veya "Queries" klasörü içinde OLAMAZ. İlgili feature altında `Handlers/CommandHandlers` ve `Handlers/QueryHandlers` şeklinde ayrı klasörlerde tutulmalıdır.

### 3. Port & Gateway Yönetimi (Tek Doğru Kaynak)
Yeni bir servis eklendiğinde veya çalıştırıldığında portlar sabittir:
- **5000**: Gateway (Ocelot)
- **5001**: Frontend MVC (Diten.Web)
- **5056**: Auth Service
- **5057**: Platform Service
- **5058**: DevEnablement Service (canlı golden referans modülleri burada)

> AGENTS.md (`§ Port Şeması`) tek doğru kaynaktır; çakışma halinde AGENTS.md geçerlidir. Eski 5050 (MDM) port atamalı kalmamıştır.

### 4. Dil ve L10n Kontrolü
- View (`.cshtml`) ve JavaScript (`.js`) içinde statik string (Hardcoded metin) kesinlikle YASAKTIR.
- Tekrarlanan genel kelimeler `SharedResource` üzerinden, sayfaya özel metinler ise sayfa bazlı `.resx` üzerinden yönetilmelidir.
- JS tarafı için `window.L10n` köprüsü kullanılmalıdır. Çeviriler modül tipine göre senkronize edilmek zorundadır (Platform modülleri: `en, tr` / Tenant modülleri: `en, fr, es, zh, ar, ru, tr`).

### 5. Frontend & UI Standartları (Sneat PRO)
- Tema: Bootstrap 5.3.3 tabanlı Sneat PRO.
- Renkler Hardcoded olamaz (`var(--bs-primary)` kullanılmalı).
- DataTables eklentisi eski `dom` string ile DEĞİL, v2 `layout` API (topStart, bottomEnd vb.) ile oluşturulmalıdır.
- DataTable filtreleri için Bootstrap Offcanvas (`#offcanvasFilter`) kullanılmalıdır.

---

## 🛑 SEVİYE 1: SOKRATİK KAPI (Sorgulama Kapısı)

**Yeni bir modül veya karmaşık bir kod talebi geldiğinde KOD YAZMA. Önce sor:**

1. **Domain Etkisi:** Bu modül CQRS tarafında hangi entity'leri etkileyecek? Join işlemleri Mongo'da nasıl yönetilecek?
2. **Güvenlik/Auth:** Bu işlem için spesifik bir RBAC Permission Key'e ihtiyaç var mı? 
3. **Multi-DB:** Bu verinin MongoDB Index ihtiyacı nedir? Başlangıçta Seed Data gerekecek mi?
4. **UI/UX:** Form yapısı "Quick View" (Offcanvas) mu yoksa "Isolated Page" (Tam Sayfa) mi olacak?

*Kullanıcının talebinde belirsizlik varsa, kodu yazmadan önce mutlaka bu stratejik soruları sor.*

---

## 🏁 FİNAL KONTROL PROTOKOLÜ
Kullanıcı "son kontrolleri yap" veya "testleri çalıştır" dediğinde kod yazmayı bırak ve şu adımları izle:
1. `run_all.sh` üzerinden projenin temiz bir şekilde build edilip edilmediğini sor.
2. xUnit testlerinin (.NET) çalıştırılıp çalıştırılmadığını kontrol et.
3. Tüm `.resx` dosyalarının eksiksiz (Key senkronizasyonu) olduğunu doğrula (Platform için 2, Tenant için 7 dil).
4. (Varsa) `.antigravity/scripts/` altındaki python doğrulama scriptlerini (security_scan vb.) çalıştır.

---

## 📁 HIZLI ERİŞİM REHBERİ

- **Ajanlar Konumu:** `.antigravity/agents/`
- **Kurallar Konumu:** `.antigravity/rules/`
- **Yetenekler Konumu:** `.antigravity/skills/`
- **İş Akışları Konumu:** `.antigravity/workflows/`