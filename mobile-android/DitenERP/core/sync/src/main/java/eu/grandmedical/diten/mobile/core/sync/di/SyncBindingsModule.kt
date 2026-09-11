package eu.grandmedical.diten.mobile.core.sync.di

import dagger.Binds
import dagger.BindsOptionalOf
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import eu.grandmedical.diten.mobile.core.sync.SyncHandler
import eu.grandmedical.diten.mobile.core.sync.readiness.ReadinessRemoteSink
import eu.grandmedical.diten.mobile.core.sync.readiness.ReadinessScopeProvider
import eu.grandmedical.diten.mobile.core.sync.readiness.ReadinessSyncHandler

/**
 * Wires the sync framework into the Hilt graph.
 *
 * - [ReadinessSyncHandler] is contributed to the `Set<SyncHandler>` multibinding
 *   ([IntoSet]) that [eu.grandmedical.diten.mobile.core.sync.SyncEngine] consumes.
 *   Every future feature handler adds one `@Binds @IntoSet` line the same way.
 * - The readiness push sink and scope provider are declared **optional**
 *   ([BindsOptionalOf]). No generic readiness backend exists in `:core:network`
 *   yet, so the module builds without a real implementation; the feature module
 *   that owns readiness (M1+) supplies the concrete `@Binds`, at which point the
 *   handler's loop activates. Until then the handler safely reports "retry".
 */
@Module
@InstallIn(SingletonComponent::class)
interface SyncBindingsModule {

    @Binds
    @IntoSet
    fun bindReadinessSyncHandler(impl: ReadinessSyncHandler): SyncHandler

    @BindsOptionalOf
    fun optionalReadinessRemoteSink(): ReadinessRemoteSink

    @BindsOptionalOf
    fun optionalReadinessScopeProvider(): ReadinessScopeProvider
}
