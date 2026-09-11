package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Inbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.PreviewLightDark
import androidx.compose.ui.unit.dp
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/** Centered progress indicator for the loading branch of a [UiResult]. */
@Composable
fun LoadingState(modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(DitenSpacing.lg),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

/** Error placeholder with a danger icon and message, plus an optional retry action. */
@Composable
fun ErrorState(
    message: String,
    modifier: Modifier = Modifier,
    onRetry: (() -> Unit)? = null,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(DitenSpacing.lg),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            imageVector = Icons.Outlined.ErrorOutline,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.error,
            modifier = Modifier.size(40.dp),
        )
        Text(
            text = message,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = DitenSpacing.sm),
        )
        if (onRetry != null) {
            DitenButton(
                text = "Retry",
                onClick = onRetry,
                variant = DitenButtonVariant.Secondary,
                modifier = Modifier.padding(top = DitenSpacing.md),
            )
        }
    }
}

/** Empty placeholder for successful-but-empty content. */
@Composable
fun EmptyState(
    message: String,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(DitenSpacing.lg),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            imageVector = Icons.Outlined.Inbox,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp),
        )
        Text(
            text = message,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = DitenSpacing.sm),
        )
    }
}

/**
 * Ties the design system to :core:common. Renders the loading / error / content
 * branches of a [UiResult]. When the success payload is an empty [Collection] and
 * an [emptyMessage] is supplied, the [EmptyState] is shown instead of [content].
 *
 * @param state the hoisted screen state.
 * @param emptyMessage optional message; enables empty-detection for collection payloads.
 * @param onRetry optional retry action forwarded to [ErrorState].
 * @param content renders the success payload.
 */
@Composable
fun <T> UiStateContainer(
    state: UiResult<T>,
    modifier: Modifier = Modifier,
    emptyMessage: String? = null,
    onRetry: (() -> Unit)? = null,
    content: @Composable (T) -> Unit,
) {
    when (state) {
        is UiResult.Loading -> LoadingState(modifier = modifier)
        is UiResult.Error -> ErrorState(message = state.message, modifier = modifier, onRetry = onRetry)
        is UiResult.Success -> {
            val data = state.data
            val isEmpty = emptyMessage != null && data is Collection<*> && data.isEmpty()
            if (isEmpty) {
                EmptyState(message = emptyMessage, modifier = modifier)
            } else {
                content(data)
            }
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun StatesPreview() {
    DitenTheme {
        Column(verticalArrangement = Arrangement.spacedBy(DitenSpacing.lg)) {
            ErrorState(message = "Could not load readiness data.", onRetry = {})
            EmptyState(message = "No records yet.")
        }
    }
}
