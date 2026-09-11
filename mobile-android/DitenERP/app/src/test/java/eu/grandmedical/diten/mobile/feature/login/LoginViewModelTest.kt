package eu.grandmedical.diten.mobile.feature.login

import eu.grandmedical.diten.mobile.core.auth.data.AuthRepository
import eu.grandmedical.diten.mobile.core.auth.data.LoginResult
import eu.grandmedical.diten.mobile.core.auth.session.AuthState
import eu.grandmedical.diten.mobile.core.common.UiResult
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/**
 * Unit-tests the login MVI flow against a fake [AuthRepository]:
 *  - valid credentials -> isLoading during the call, then a single NavigateHome;
 *  - login error        -> error surfaced in state, no navigation;
 *  - MFA required        -> a single NavigateMfa carrying the challenge id.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class LoginViewModelTest {

    private val mainDispatcher = UnconfinedTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(mainDispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun authenticatedState() = AuthState.Authenticated(
        userId = "u1",
        email = "gm@grandmedical.eu",
        tenantId = "diten",
        selectedLegalEntityId = "GM-HU",
        availableLegalEntities = listOf("GM-HU"),
    )

    private fun enterValidCredentials(vm: LoginViewModel) {
        vm.onEvent(LoginEvent.TenantIdChanged("diten"))
        vm.onEvent(LoginEvent.EmailChanged("gm@grandmedical.eu"))
        vm.onEvent(LoginEvent.PasswordChanged("secret"))
    }

    @Test
    fun submit_with_valid_credentials_sets_loading_then_navigates_home_once() = runTest(mainDispatcher) {
        val gate = CompletableDeferred<Unit>()
        val repo = FakeAuthRepository(
            loginHandler = {
                gate.await()
                UiResult.Success(LoginResult.Authenticated(authenticatedState()))
            },
        )
        val vm = LoginViewModel(repo)

        val effects = mutableListOf<LoginEffect>()
        val job = backgroundScope.launch { vm.effect.toList(effects) }

        enterValidCredentials(vm)
        vm.onEvent(LoginEvent.Submit)

        // The login call is suspended at the gate: the screen is in its loading state.
        assertTrue(vm.state.value.isLoading)
        assertTrue(effects.isEmpty())

        gate.complete(Unit)

        assertFalse(vm.state.value.isLoading)
        assertEquals(null, vm.state.value.error)
        assertEquals(listOf(LoginEffect.NavigateHome), effects)

        job.cancel()
    }

    @Test
    fun submit_with_login_error_surfaces_message_and_does_not_navigate() = runTest(mainDispatcher) {
        val repo = FakeAuthRepository(
            loginHandler = { UiResult.Error("Geçersiz kimlik bilgileri") },
        )
        val vm = LoginViewModel(repo)

        val effects = mutableListOf<LoginEffect>()
        val job = backgroundScope.launch { vm.effect.toList(effects) }

        enterValidCredentials(vm)
        vm.onEvent(LoginEvent.Submit)

        assertFalse(vm.state.value.isLoading)
        assertEquals("Geçersiz kimlik bilgileri", vm.state.value.error)
        assertTrue(effects.isEmpty())

        job.cancel()
    }

    @Test
    fun submit_with_mfa_required_emits_navigate_mfa() = runTest(mainDispatcher) {
        val repo = FakeAuthRepository(
            loginHandler = { UiResult.Success(LoginResult.MfaRequired(challengeId = "challenge-42")) },
        )
        val vm = LoginViewModel(repo)

        val effects = mutableListOf<LoginEffect>()
        val job = backgroundScope.launch { vm.effect.toList(effects) }

        enterValidCredentials(vm)
        vm.onEvent(LoginEvent.Submit)

        assertEquals(listOf(LoginEffect.NavigateMfa("challenge-42")), effects)
        assertFalse(vm.state.value.isLoading)

        job.cancel()
    }

    /** Minimal fake driving only the login path under test. */
    private class FakeAuthRepository(
        private val loginHandler: suspend () -> UiResult<LoginResult>,
    ) : AuthRepository {
        override suspend fun login(
            email: String,
            password: String,
            tenantId: String,
            rememberMe: Boolean,
        ): UiResult<LoginResult> = loginHandler()

        override suspend fun verifyMfa(challengeId: String, code: String): UiResult<LoginResult> =
            UiResult.Error("not used")

        override suspend fun refresh(): Boolean = false

        override suspend fun logout() = Unit
    }
}
