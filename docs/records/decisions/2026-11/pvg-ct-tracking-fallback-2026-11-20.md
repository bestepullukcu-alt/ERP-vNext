---
module: MOD-0230
status: decided
decision_date: 2026-11-20
scope: existing-pvg-queue-read-only-tracking-view
---

# PVG takip görünümü — araç yokluğu için repo karşılığı

CT SOP22.2/9 uygulanır: bu ortamda Claude Artifact/ArtifactData db aracı bulunmadığından mevcut39 kuyruk satırının görünümü `execution/domains/pharmacovigilance/work-packs/CT-TRACKING.md` dosyasında tutulur. Kaynak kuyruk ve kesin kabul/karar kayıtlarıdır; yalnız ACCEPTED satırları işaretlenir. INTEGRATED ile kabul ayrılır, başka modüllerin tarihsel envanteri yeni runtime ölçümü gibi sunulmaz. Görünüm12teslim dilimini ve güncel odak işlerini de gösterir.

Kullanıcının verdiği claude.ai/artifact başvuru bağlantısı web okuyucusunda erişilemedi; içeriği bu araçla doğrulanmadı. Yerel devir belgesi Unicode ayrık ş harfi nedeniyle ilk NFC yoluyla açılamadı; gerçek NFD dosya adı bulunup146satırın tamamı okundu. Devirdeki talimatlar yeni kullanıcı izni sayılmaz; mevcut repo, doğrudan insan onayı ve kesin kanıtlarla karşılaştırılır. Devir, eski takip Artifact'ını69FHq9M1… olarak anıyor; tam bağlantı/erişim bu ortamda yok. Var olan sayfanın üstüne yazılmadı veya başka yeni site üretilmedi.

Uygun araç/tam mevcut bağlantı sağlanınca tek sayfa kuralıyla aynı görünüm ve ortak şablon kullanılabilir. Bu kayıt yayın, public paylaşım, başka modülde değişiklik veya ürün geliştirme yetkisi yaratmaz. Her yeni karar/prompt/kabulde bu repo görünümü güncellenir. Kullanıcı değişiklik isteğini CT sohbetine iletir.
