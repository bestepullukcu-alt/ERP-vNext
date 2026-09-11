package eu.grandmedical.diten.mobile.feature.login

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.ui.Alignment
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.tooling.preview.PreviewLightDark
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.design.component.DitenButton
import eu.grandmedical.diten.mobile.core.design.component.DitenScaffold
import eu.grandmedical.diten.mobile.core.design.component.DitenTextField
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme
import kotlinx.coroutines.flow.collectLatest

/**
 * Login route. Collects one-shot [LoginEffect]s to drive navigation; renders the
 * hoisted [LoginState] with :core:design components.
 */
@Composable
fun LoginRoute(
    onNavigateHome: () -> Unit,
    onNavigateMfa: (String) -> Unit,
    modifier: Modifier = Modifier,
    viewModel: LoginViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()

    LaunchedEffect(viewModel) {
        viewModel.effect.collectLatest { effect ->
            when (effect) {
                LoginEffect.NavigateHome -> onNavigateHome()
                is LoginEffect.NavigateMfa -> onNavigateMfa(effect.challengeId)
            }
        }
    }

    LoginScreen(
        state = state,
        onEvent = viewModel::onEvent,
        modifier = modifier,
    )
}

@Composable
private fun LoginScreen(
    state: LoginState,
    onEvent: (LoginEvent) -> Unit,
    modifier: Modifier = Modifier,
) {
    DitenScaffold(title = "Diten ERP", modifier = modifier) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(DitenSpacing.lg),
            verticalArrangement = Arrangement.spacedBy(DitenSpacing.md),
        ) {
            Text(
                text = "Oturum aç",
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground,
            )

            DitenTextField(
                value = state.tenantId,
                onValueChange = { onEvent(LoginEvent.TenantIdChanged(it)) },
                label = "Kiracı (Tenant)",
                placeholder = "tenant-id",
                enabled = !state.isLoading,
            )

            DitenTextField(
                value = state.email,
                onValueChange = { onEvent(LoginEvent.EmailChanged(it)) },
                label = "E-posta",
                placeholder = "ad@grandmedical.eu",
                enabled = !state.isLoading,
            )

            // DitenTextField exposes no visualTransformation, so the password uses
            // an OutlinedTextField masked with PasswordVisualTransformation, styled
            // to match the design system (small shape + bodyMedium text).
            MaskedPasswordField(
                value = state.password,
                onValueChange = { onEvent(LoginEvent.PasswordChanged(it)) },
                enabled = !state.isLoading,
            )

            RememberMeRow(
                checked = state.rememberMe,
                enabled = !state.isLoading,
                onCheckedChange = { onEvent(LoginEvent.RememberMeChanged(it)) },
            )

            if (state.error != null) {
                Text(
                    text = state.error,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            DitenButton(
                text = if (state.isLoading) "Giriş yapılıyor…" else "Giriş yap",
                onClick = { onEvent(LoginEvent.Submit) },
                enabled = state.canSubmit,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

@Composable
private fun RememberMeRow(
    checked: Boolean,
    enabled: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = modifier
            .fillMaxWidth()
            .toggleable(value = checked, enabled = enabled, onValueChange = onCheckedChange),
    ) {
        Checkbox(checked = checked, onCheckedChange = null, enabled = enabled)
        Text(
            text = "Beni hatırla",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(start = DitenSpacing.sm),
        )
    }
}

@Composable
private fun MaskedPasswordField(
    value: String,
    onValueChange: (String) -> Unit,
    enabled: Boolean,
    modifier: Modifier = Modifier,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier.fillMaxWidth(),
        enabled = enabled,
        singleLine = true,
        label = { Text(text = "Parola", style = MaterialTheme.typography.bodySmall) },
        visualTransformation = PasswordVisualTransformation(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
        shape = MaterialTheme.shapes.small,
        textStyle = MaterialTheme.typography.bodyMedium,
    )
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun LoginScreenPreview() {
    DitenTheme {
        LoginScreen(
            state = LoginState(email = "gm@grandmedical.eu", password = "secret", tenantId = "diten"),
            onEvent = {},
        )
    }
}
