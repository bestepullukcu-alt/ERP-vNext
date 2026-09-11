package eu.grandmedical.diten.mobile.core.auth

import eu.grandmedical.diten.mobile.core.auth.crypto.TokenCipher

/**
 * Passthrough [TokenCipher] used by unit tests so the token store can be
 * round-tripped WITHOUT the AndroidKeyStore. Robolectric's keystore emulation is
 * incomplete/unreliable for AES/GCM, so a fake cipher keeps the store tests fast,
 * deterministic and pure-logic; the real [eu.grandmedical.diten.mobile.core.auth.crypto.KeystoreTokenCipher]
 * is exercised on-device.
 */
class PlaintextTokenCipher : TokenCipher {
    override fun encrypt(plain: ByteArray): ByteArray = plain

    override fun decrypt(blob: ByteArray): ByteArray = blob
}
