package eu.grandmedical.diten.mobile.feature.competencyskills.data.scope

import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.auth.session.SessionManager
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import javax.inject.Inject

/**
 * Resolves the active data scope (tenant + selected legal entity) for the cache.
 *
 * A seam (interface) so the offline-first tests can inject a fixed scope without
 * standing up the whole `:core:auth` session stack. The production binding reads
 * the current [AuthState] from [SessionManager].
 */
interface CompetencySkillsScopeProvider {

    /** The current write scope, or null when there is no authenticated selection. */
    suspend fun currentScope(): ScopeKeys?

    /** The scopes a sync pass should reconcile (the current scope, if any). */
    suspend fun scopesToSync(): List<ScopeKeys>
}

/** [SessionManager]-backed scope provider used in production. */
class SessionScopeProvider @Inject constructor(
    private val sessionManager: SessionManager,
) : CompetencySkillsScopeProvider {

    override suspend fun currentScope(): ScopeKeys? {
        val authenticated = sessionManager.authState.value as? AuthState.Authenticated ?: return null
        val tenantId = authenticated.tenantId
        val legalEntityId = authenticated.selectedLegalEntityId
        return if (tenantId != null && legalEntityId != null) {
            ScopeKeys(tenantId = tenantId, legalEntityId = legalEntityId)
        } else {
            null
        }
    }

    override suspend fun scopesToSync(): List<ScopeKeys> = listOfNotNull(currentScope())
}
