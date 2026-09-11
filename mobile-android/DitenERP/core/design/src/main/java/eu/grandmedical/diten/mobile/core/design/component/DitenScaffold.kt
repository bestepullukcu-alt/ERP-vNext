package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarColors
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.PreviewLightDark
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/**
 * Diten top app bar: a compact, brand-surfaced header. Title uses the semibold
 * title style; navigation and action slots are hoisted by the caller.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DitenTopAppBar(
    title: String,
    modifier: Modifier = Modifier,
    navigationIcon: @Composable () -> Unit = {},
    actions: @Composable () -> Unit = {},
    colors: TopAppBarColors = TopAppBarDefaults.topAppBarColors(
        containerColor = MaterialTheme.colorScheme.surface,
        titleContentColor = MaterialTheme.colorScheme.onSurface,
    ),
) {
    TopAppBar(
        modifier = modifier,
        title = {
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        },
        navigationIcon = navigationIcon,
        actions = { actions() },
        colors = colors,
    )
}

/**
 * Diten scaffold: thin wrapper over [Scaffold] wiring a [DitenTopAppBar] and the
 * themed background. Content receives the inner [PaddingValues] to hoist insets.
 */
@Composable
fun DitenScaffold(
    title: String,
    modifier: Modifier = Modifier,
    navigationIcon: @Composable () -> Unit = {},
    actions: @Composable () -> Unit = {},
    content: @Composable (PaddingValues) -> Unit,
) {
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            DitenTopAppBar(
                title = title,
                navigationIcon = navigationIcon,
                actions = actions,
            )
        },
        content = content,
    )
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun DitenScaffoldPreview() {
    DitenTheme {
        DitenScaffold(title = "Association Operations") { innerPadding ->
            Text(
                text = "Screen content",
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding)
                    .padding(DitenSpacing.lg),
                style = MaterialTheme.typography.bodyMedium,
            )
        }
    }
}
