package eu.grandmedical.diten.mobile.core.sync.di

import dagger.BindsOptionalOf
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import eu.grandmedical.diten.mobile.core.sync.readiness.ReadinessRemoteSink
import eu.grandmedical.diten.mobile.core.sync.readiness.ReadinessScopeProvider

/**
 * Wires the sync framework's optional collaborators into the Hilt graph.
 *
 * The `Set<SyncHandler>` multibinding that
 * [eu.grandmedical.diten.mobile.core.sync.SyncEngine] consumes is populated by the
 * FEATURE modules — each contributes one `@Binds @IntoSet SyncHandler` (see any
 * `:feature:*` DI module). The M0.6 reference [ReadinessSyncHandler] is intentionally
 * NOT contributed here: with no sink wired it would always report "retry" and poison
 * every sync pass to a perpetual RETRY, so the real per-feature handlers are the only
 * members of the set.
 *
 * The reference sink and scope provider stay declared **optional** ([BindsOptionalOf])
 * so the reference handler still compiles as documentation of the pattern.
 */
@Module
@InstallIn(SingletonComponent::class)
interface SyncBindingsModule {

    @BindsOptionalOf
    fun optionalReadinessRemoteSink(): ReadinessRemoteSink

    @BindsOptionalOf
    fun optionalReadinessScopeProvider(): ReadinessScopeProvider
}
