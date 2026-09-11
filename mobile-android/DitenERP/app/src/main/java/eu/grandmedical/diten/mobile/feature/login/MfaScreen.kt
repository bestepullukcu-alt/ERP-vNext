package eu.grandmedical.diten.mobile.feature.login

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.PreviewLightDark
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.design.component.DitenButton
import eu.grandmedical.diten.mobile.core.design.component.DitenScaffold
import eu.grandmedical.diten.mobile.core.design.component.DitenTextField
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme
import kotlinx.coroutines.flow.collectLatest

/** MFA route: enter the second-factor code, then continue to Home once verified. */
@Composable
fun MfaRoute(
    onNavigateHome: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: MfaViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()

    LaunchedEffect(viewModel) {
        viewModel.effect.collectLatest { effect ->
            when (effect) {
                MfaEffect.NavigateHome -> onNavigateHome()
            }
        }
    }

    MfaScreen(state = state, onEvent = viewModel::onEvent, modifier = modifier)
}

@Composable
private fun MfaScreen(
    state: MfaState,
    onEvent: (MfaEvent) -> Unit,
    modifier: Modifier = Modifier,
) {
    DitenScaffold(title = "İki adımlı doğrulama", modifier = modifier) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(DitenSpacing.lg),
            verticalArrangement = Arrangement.spacedBy(DitenSpacing.md),
        ) {
            Text(
                text = "İkinci faktör gerekli. Size gönderilen doğrulama kodunu girin.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onBackground,
            )

            DitenTextField(
                value = state.code,
                onValueChange = { onEvent(MfaEvent.CodeChanged(it)) },
                label = "Doğrulama kodu",
                placeholder = "123456",
                enabled = !state.isLoading,
                isError = state.error != null,
                supportingText = state.error,
            )

            DitenButton(
                text = if (state.isLoading) "Doğrulanıyor…" else "Doğrula",
                onClick = { onEvent(MfaEvent.Submit) },
                enabled = state.canSubmit,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun MfaScreenPreview() {
    DitenTheme {
        MfaScreen(state = MfaState(challengeId = "abc", code = "123"), onEvent = {})
    }
}
