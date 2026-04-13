'use strict';

// Token'ı çözmek için yardımcı fonksiyon
function getUserInfoFromToken() {
    const token = localStorage.getItem("token");

    if (!token) {
        return null;
    }

    const payloadBase64 = token.split('.')[1]; // İkinci parça (payload) base64 encoded
    const payloadJson = atob(payloadBase64);   // base64 çöz
    const payload = JSON.parse(payloadJson);   // JSON'a çevir

    return payload;
}

// Kullanıcı adını almak için yardımcı fonksiyon
function getUserName() {
    const userInfo = getUserInfoFromToken();
    if (userInfo) {
        return userInfo["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] || "Bilinmeyen Kullanıcı";
    }
    return null;
}

// Kullanıcıyı kontrol etmek ve bilgileri döndürmek
function isAuthenticated() {
    const userInfo = getUserInfoFromToken();
    return userInfo != null;
}
