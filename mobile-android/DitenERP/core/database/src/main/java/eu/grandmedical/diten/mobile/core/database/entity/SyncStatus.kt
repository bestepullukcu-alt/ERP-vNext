package eu.grandmedical.diten.mobile.core.database.entity

/**
 * Local sync state of a cached row relative to the backend.
 *
 * Offline-first writes land in the cache immediately; a later sync pass
 * (M0.6) reconciles them with the server. The status lets the UI show a row
 * as authoritative ([SYNCED]), optimistic/in-flight ([PENDING]) or needing
 * attention ([FAILED]).
 *
 * Persisted as its stable [name] via [eu.grandmedical.diten.mobile.core.database.converter.DitenTypeConverters];
 * never persist [ordinal], so reordering these constants stays safe.
 */
enum class SyncStatus {
    /** Row matches the server (or was fetched from it). */
    SYNCED,

    /** Locally created/updated, not yet confirmed by the server. */
    PENDING,

    /** Last sync attempt failed; row needs a retry or manual resolution. */
    FAILED,
}
