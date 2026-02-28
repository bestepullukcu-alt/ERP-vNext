# Prompt Expander Skill (Conversation → Detailed Prompt)

## Amaç

Kullanıcının yazdığı kısa, dağınık veya yarım "conversation / fikir"
metnini; - net hedefleri olan, - kapsamı belirlenmiş, - kabul kriterleri
ve çıktıları tanımlanmış, - uygulamaya dönük, - copy-paste
kullanılabilir **en detaylı prompt** haline dönüştür.

------------------------------------------------------------------------

## Ne Zaman Kullanılır?

-   Kullanıcı "şunu yapalım", "bunu yazalım", "şuna benzer", "burayı
    geliştir" gibi kısa istek yazdığında
-   Belirsiz scope veya eksik detaylar olduğunda
-   Uzun bir işin tek seferde düzgün prompta çevrilmesi gerektiğinde

------------------------------------------------------------------------

## Çıktı Formatı (Zorunlu)

Her zaman aşağıdaki başlıklarla üret:

1)  Context / Arka Plan\
2)  Goal / Hedef\
3)  Inputs (User Provided)\
4)  Assumptions (Net ve sayılı)\
5)  Scope
    -   In Scope\
    -   Out of Scope\
6)  Constraints\
7)  User Personas (varsa)\
8)  Success Criteria / Acceptance Criteria\
9)  Deliverables\
10) Implementation Notes\
11) Edge Cases & Risks\
12) Questions (Sadece zorunluysa)\
13) Final Prompt (Copy-Paste)

> Not: Kullanıcı ek bilgi vermediyse, "Questions" bölümünü minimumda
> tut.\
> Clarification sormak yerine makul varsayımlar yap ve "Assumptions"
> altında belirt.

------------------------------------------------------------------------

## Dönüştürme Kuralları

-   Kullanıcının verdiği metni değiştirme: "Inputs" altında aynen
    alıntıla.
-   Belirsiz kısımları netleştir: "Assumptions" içine koy.
-   Kapsamı daralt: MVP / Phase-1 öner.
-   İstenirse teknoloji yığınına uy: (.NET 8, CQRS, MongoDB/SQL, YARP
    vs.)
-   Çıktıyı uygulanabilir yap: dosya yolları, isimlendirme, örnek
    payloadlar, görev listesi ekle.
-   "Final Prompt" tek parça ve komut gibi olmalı.

------------------------------------------------------------------------

## Detay Seviyesi

Varsayılan: maksimum detay.

-   Eğer kullanıcı "kısa olsun" demezse her zaman ayrıntılı üret.
-   Task breakdown varsa: 5-15 maddelik adım listesi oluştur.

------------------------------------------------------------------------

## Final Prompt Yazım Şablonu

Final Prompt içinde mutlaka şunlar olsun:

-   Rol: "Sen kıdemli ..."
-   Hedef: "X'i yap"
-   Kısıtlar: "Şunları kullan / şunları kullanma"
-   Çıktılar: "Şu dosyaları üret, şu formatta ver"
-   Kabul kriterleri: "Şunlar sağlanacak"
-   Test: "Şu testleri yaz"
-   Güvenlik/performans: "index/join/cache vb."

------------------------------------------------------------------------

## Özel Notlar (PPM / Multi-workspace uyumu)

Eğer kullanıcı cross-workspace değişiklik etkisi (impact) istiyorsa:

-   "Impact Analysis" adımı ekle
-   "Change Propagation Plan" ekle
-   "Backward compatibility" ve "migration" maddelerini ekle
