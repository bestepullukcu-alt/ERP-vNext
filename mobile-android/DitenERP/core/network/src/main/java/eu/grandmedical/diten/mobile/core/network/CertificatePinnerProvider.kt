package eu.grandmedical.diten.mobile.core.network

import okhttp3.CertificatePinner
import javax.inject.Inject

/**
 * Configurable hook for TLS certificate pinning. Returns a possibly-empty
 * [CertificatePinner]; an empty pinner enforces nothing (the default in dev,
 * where traffic is cleartext to the emulator loopback anyway).
 *
 * Production pins are added later by returning a pinner built with
 * `CertificatePinner.Builder().add("host", "sha256/...").build()`. Keeping this
 * behind an interface lets `:core:auth`/app override the binding without
 * touching the OkHttp wiring.
 */
fun interface CertificatePinnerProvider {
    fun pinner(): CertificatePinner
}

/** Default: no pins. Structured so prod pins can be dropped in later. */
class EmptyCertificatePinnerProvider @Inject constructor() : CertificatePinnerProvider {
    override fun pinner(): CertificatePinner = CertificatePinner.Builder().build()
}
