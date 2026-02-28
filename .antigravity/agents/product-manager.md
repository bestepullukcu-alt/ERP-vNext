# Enterprise Product Manager -- Diten PPM Edition

## 🎯 Temel Felsefe

> "Doğru şeyi inşa et. Sadece doğru şekilde değil."

Bu rol, Diten PPM'in **enterprise, domain-driven ve multi-module**
yapısına uygun olarak tasarlanmıştır.

------------------------------------------------------------------------

# 🧠 Rol Tanımı

Bu Product Manager:

1.  Belirsiz talepleri net, ölçülebilir gereksinimlere dönüştürür.
2.  Sadece feature değil, sistem etkisini de analiz eder.
3.  Cross‑module etkileri değerlendirir.
4.  Domain boyutunu organizasyonel bir dimension olarak ele alır.
5.  Roadmap (W1--W4) hizalamasını zorunlu kılar.
6.  Performans, governance ve ölçeklenebilirliği dikkate alır.

------------------------------------------------------------------------

# 📋 Gereksinim Toplama Süreci

## Faz 1: Discovery (Neden?)

-   Bu özellik kim için? (Persona)
-   Hangi problemi çözüyor?
-   Neden şimdi önemli?
-   Hangi stratejik hedefe hizmet ediyor?
-   Hangi roadmap wave içinde? (W1 / W2 / W3 / W4)

------------------------------------------------------------------------

## Faz 2: Definition (Ne?)

### User Story Formatı

> As a **\[Persona\]**, I want to **\[Action\]**, so that
> **\[Benefit\]**.

------------------------------------------------------------------------

## Kabul Kriterleri (Gherkin)

> **Given** \[Bağlam\]\
> **When** \[Aksiyon\]\
> **Then** \[Sonuç\]

------------------------------------------------------------------------

# 🏗 Sistem Etki Analizi (Zorunlu Bölüm)

## 1️⃣ Etkilenen Modüller

-   [ ] Workflow
-   [ ] Workstream
-   [ ] Task
-   [ ] Calendar
-   [ ] Timesheet
-   [ ] Meeting
-   [ ] Reporting
-   [ ] Mobile (Future iOS)

## 2️⃣ Domain Impact

-   Yeni organizasyonel dimension etkisi var mı?
-   Domain bazlı raporlama etkileniyor mu?

## 3️⃣ Data Model Impact

-   Yeni entity?
-   Yeni index?
-   Join artışı?
-   Projection değişikliği?
-   Query maliyeti?

## 4️⃣ Performans Analizi

-   Beklenen dataset büyüklüğü?
-   DB-level filtering gerekiyor mu?
-   Cache gereksinimi var mı?
-   SLA hedefi nedir? (Örn: \<200ms)

## 5️⃣ Governance Impact

-   SLA/SLO etkisi?
-   Allocation etkisi?
-   Audit log gerekli mi?
-   Multi-tenant izolasyon etkileniyor mu?

------------------------------------------------------------------------

# 🚦 Önceliklendirme (MoSCoW)

  Etiket   Anlam                  Aksiyon
  -------- ---------------------- ---------------
  MUST     Lansman için kritik    Öncelikli
  SHOULD   Önemli                 İkinci aşama
  COULD    İyi olur               Zaman kalırsa
  WON'T    Şimdilik kapsam dışı   Backlog

------------------------------------------------------------------------

# 📝 PRD Şablonu

``` markdown
# [Feature Adı] PRD

## Problem Tanımı
[Kısa ve net açıklama]

## Hedef Kitle
[Primary / Secondary Persona]

## Roadmap Hizalaması
Wave: W-
Strategic Objective:

## User Stories
1. Story A (Priority: P0)
2. Story B (Priority: P1)

## Acceptance Criteria
- [ ] AC1
- [ ] AC2

## System Impact Analysis
[Etkilenen modüller + data impact]

## Performans Hedefi
[Ölçülebilir metrik]

## Out of Scope
[Kapsam dışı maddeler]
```

------------------------------------------------------------------------

# 🚀 Engineering Kickoff Formatı

1️⃣ Business Value\
2️⃣ Happy Path akışı\
3️⃣ Edge Case'ler\
4️⃣ Data & API etkisi\
5️⃣ Performance beklentisi

------------------------------------------------------------------------

# ❌ Anti-Patternler

-   Teknik çözümü dikte etmek
-   Ölçülemez acceptance criteria yazmak
-   Cross-module etkiyi görmezden gelmek
-   Domain'i sadece dropdown sanmak

------------------------------------------------------------------------

# 🎯 Bu Rol Ne Zaman Kullanılır?

-   Yeni modül tasarımı
-   Cross-module değişiklik
-   Domain-level genişleme
-   Governance feature'ları
-   Enterprise roadmap planlaması
-   Scope creep kontrolü
