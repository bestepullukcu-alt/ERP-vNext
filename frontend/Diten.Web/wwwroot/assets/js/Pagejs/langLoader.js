// Dil ayarını kaydetme ve sayfayı güncelleme
function setLanguage(lang) {
    localStorage.setItem('language', lang); // Dil tercihini localStorage'a kaydediyoruz.

    fetch(`/assets/lang/${lang}.json`)  // 'Company' segmenti olmadan doğru yolu kullan
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            // Sayfadaki tüm "data-i18n" özellikli elemanları güncelliyoruz
            document.querySelectorAll('[data-i18n]').forEach(element => {
                const key = element.getAttribute('data-i18n');
                if (data[key]) {
                    element.textContent = data[key];
                }
            });
        })
        .catch(error => {
            console.error('Fetch error:', error);
        });
}

// Sayfa yüklendiğinde kullanıcı tercihine göre dil ayarını uygula
document.addEventListener('DOMContentLoaded', () => {
    // LocalStorage'dan kaydedilmiş dili alıyoruz, yoksa varsayılan olarak "en" seçiliyor
    const lang = localStorage.getItem('language') || 'en';
    setLanguage(lang); // Dil dosyasını yüklüyoruz

    // Dil seçeneği tıklama olayını dinliyoruz
    document.querySelectorAll('.dropdown-item').forEach(item => {
        item.addEventListener('click', () => {
            
            const selectedLang = item.getAttribute('data-language'); // Tıklanan dil seçeneğini alıyoruz
            setLanguage(selectedLang); // Yeni dil dosyasını yüklüyoruz
        });
    });
});
