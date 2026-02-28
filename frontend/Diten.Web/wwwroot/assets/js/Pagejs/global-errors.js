$(document).ajaxError(function (event, jqxhr, settings, thrownError) {
    console.error("Hata oluştu:", jqxhr.status, thrownError);

    // Hata durumunda error.html sayfasına yönlendir
    if (jqxhr.status === 502) {
        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
    }
});