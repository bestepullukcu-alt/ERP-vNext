package eu.grandmedical.diten.mobile.core.auth.seam

import dagger.Lazy
import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.network.session.TokenRefresher
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Real [TokenRefresher] the OkHttp [Authenticator] calls on a 401. It simply
 * delegates to [AuthRepository.refresh].
 *
 * CYCLE-BREAKER: the OkHttp client (which this refresher is wired into via the
 * authenticator) is what [AuthRepository] needs to build [AuthApi]. Depending on
 * [AuthRepository] EAGERLY would form AuthApi -> OkHttp -> Authenticator ->
 * TokenRefresher -> AuthRepository -> AuthApi. Injecting [dagger.Lazy] defers the
 * repository's construction until [refresh] is actually invoked, so the OkHttp
 * graph builds first and the cycle never forms.
 */
@Singleton
class TokenRefresherImpl @Inject constructor(
    private val authRepository: Lazy<AuthRepository>,
) : TokenRefresher {

    override suspend fun refresh(): Boolean = authRepository.get().refresh()
}
