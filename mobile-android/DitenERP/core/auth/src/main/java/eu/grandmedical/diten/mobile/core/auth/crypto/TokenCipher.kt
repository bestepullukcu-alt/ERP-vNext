package eu.grandmedical.diten.mobile.core.auth.crypto

/**
 * Symmetric cipher seam used by the token store to protect secrets at rest.
 *
 * Two implementations exist:
 *  - [KeystoreTokenCipher] (prod, bound in DI) - AES/GCM/NoPadding with a
 *    non-exportable key in the AndroidKeyStore;
 *  - `PlaintextTokenCipher` (test only) - a passthrough so the store can be
 *    round-tripped as a pure JVM/Robolectric unit test WITHOUT depending on
 *    Robolectric's limited AndroidKeyStore emulation.
 *
 * The store treats the output of [encrypt] as an opaque blob and never inspects
 * it, so a real implementation is free to prepend the IV to the ciphertext.
 */
interface TokenCipher {
    fun encrypt(plain: ByteArray): ByteArray

    fun decrypt(blob: ByteArray): ByteArray
}
