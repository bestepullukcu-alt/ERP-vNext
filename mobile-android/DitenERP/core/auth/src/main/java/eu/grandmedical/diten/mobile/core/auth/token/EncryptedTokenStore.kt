package eu.grandmedical.diten.mobile.core.auth.token

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import eu.grandmedical.diten.mobile.core.auth.crypto.TokenCipher
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import java.util.Base64
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Offline, restart-surviving store for the session's secrets, persisted in a
 * [DataStore] of [Preferences]. Every value is passed through [TokenCipher]
 * (AES/GCM in prod) BEFORE it touches disk and Base64-encoded for the string
 * preference, so nothing readable is written to the DataStore file.
 *
 * All reads are suspend snapshots of the current value; [clear] wipes the whole
 * store on logout. A missing/corrupt value reads back as null (never throws), so
 * a partially written or tampered store degrades to "unauthenticated".
 */
// One accessor per persisted field plus the mutators is inherently a wide-but-flat
// surface; splitting it would only scatter the store's single responsibility.
@Suppress("TooManyFunctions")
@Singleton
class EncryptedTokenStore @Inject constructor(
    private val dataStore: DataStore<Preferences>,
    private val cipher: TokenCipher,
) {

    suspend fun accessToken(): String? = readString(KEY_ACCESS_TOKEN)

    suspend fun refreshToken(): String? = readString(KEY_REFRESH_TOKEN)

    suspend fun tenantId(): String? = readString(KEY_TENANT_ID)

    suspend fun selectedLegalEntityId(): String? = readString(KEY_SELECTED_LEGAL_ENTITY)

    suspend fun expiresAt(): Long? = readString(KEY_EXPIRES_AT)?.toLongOrNull()

    /** Available legal-entity ids, stored as a CSV of GUIDs. */
    suspend fun availableLegalEntities(): List<String> =
        readString(KEY_AVAILABLE_LEGAL_ENTITIES)
            ?.split(',')
            ?.map { it.trim() }
            ?.filter { it.isNotBlank() }
            .orEmpty()

    /**
     * Replaces the whole persisted session in a single atomic edit. The fields
     * are the session's constituent secrets; bundling them into a wrapper object
     * would add indirection without changing this one-shot write.
     */
    @Suppress("LongParameterList")
    suspend fun persistSession(
        accessToken: String?,
        refreshToken: String?,
        expiresAt: Long?,
        tenantId: String?,
        availableLegalEntities: List<String>,
        selectedLegalEntityId: String?,
    ) {
        dataStore.edit { prefs ->
            prefs.putEncrypted(KEY_ACCESS_TOKEN, accessToken)
            prefs.putEncrypted(KEY_REFRESH_TOKEN, refreshToken)
            prefs.putEncrypted(KEY_EXPIRES_AT, expiresAt?.toString())
            prefs.putEncrypted(KEY_TENANT_ID, tenantId)
            prefs.putEncrypted(KEY_AVAILABLE_LEGAL_ENTITIES, availableLegalEntities.joinToString(","))
            prefs.putEncrypted(KEY_SELECTED_LEGAL_ENTITY, selectedLegalEntityId)
        }
    }

    /** Updates only the rotating token material after a successful refresh. */
    suspend fun updateTokens(accessToken: String?, refreshToken: String?, expiresAt: Long?) {
        dataStore.edit { prefs ->
            prefs.putEncrypted(KEY_ACCESS_TOKEN, accessToken)
            prefs.putEncrypted(KEY_REFRESH_TOKEN, refreshToken)
            prefs.putEncrypted(KEY_EXPIRES_AT, expiresAt?.toString())
        }
    }

    suspend fun setSelectedLegalEntity(id: String?) {
        dataStore.edit { prefs -> prefs.putEncrypted(KEY_SELECTED_LEGAL_ENTITY, id) }
    }

    /** Wipes every stored secret; used on logout / failed refresh. */
    suspend fun clear() {
        dataStore.edit { it.clear() }
    }

    private fun MutablePreferences.putEncrypted(key: Preferences.Key<String>, value: String?) {
        if (value == null) {
            remove(key)
        } else {
            this[key] = Base64.getEncoder().encodeToString(cipher.encrypt(value.encodeToByteArray()))
        }
    }

    private suspend fun readString(key: Preferences.Key<String>): String? {
        val stored = dataStore.data.map { it[key] }.first() ?: return null
        return runCatching {
            cipher.decrypt(Base64.getDecoder().decode(stored)).decodeToString()
        }.getOrNull()
    }

    private companion object {
        val KEY_ACCESS_TOKEN = stringPreferencesKey("access_token")
        val KEY_REFRESH_TOKEN = stringPreferencesKey("refresh_token")
        val KEY_EXPIRES_AT = stringPreferencesKey("expires_at")
        val KEY_TENANT_ID = stringPreferencesKey("tenant_id")
        val KEY_AVAILABLE_LEGAL_ENTITIES = stringPreferencesKey("available_legal_entities")
        val KEY_SELECTED_LEGAL_ENTITY = stringPreferencesKey("selected_legal_entity")
    }
}
