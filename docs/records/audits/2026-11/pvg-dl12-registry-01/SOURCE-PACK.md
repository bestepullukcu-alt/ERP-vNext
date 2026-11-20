# Yönetişim işçisi kaynak paketi

2026-11-20. Prompt v1.0 LF SHA-256 `790f1b1f2614720031c4b9292ab4506da6c334cc3784b8a30b25c71d878c2777`,20 kaynak. Gerçek `prompt_gate.py pack` çıkış 0; kaynak paketi özeti `2d19f21e299bca1b9ecdcab67d62fc3e6fd7f101c808f47d41114226e3f97986`. ExpectedHEAD `6902667db14e9ba2f0bd5518fccf228608913260`; bu modülün ayrı registry ağacı temiz ve bu HEAD'de.

İlk sandbox içi okuma, dış işçi ağacındaki Git nesnelerini okuyamadığından araç “Expected HEAD yok” çıkış 2 verdi. `source-pack.txt.sandbox-negative`/stderr eski çıktıyı korur. Aynı kaynak/HEAD gerçekten yok değildi; dar salt okunur erişimle yeniden ölçülen çıktı0 `source-pack.txt` içindedir. Araç veya ref değiştirilmedi. Normal dağıtım kapısı ve bağımsız prompt incelemesi henüz çalışmadı; bu kayıt dağıtım izni değildir.
