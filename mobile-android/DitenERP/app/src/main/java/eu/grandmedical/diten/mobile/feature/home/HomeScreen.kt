package eu.grandmedical.diten.mobile.feature.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Business
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.PreviewLightDark
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import eu.grandmedical.diten.mobile.core.design.component.DitenListRow
import eu.grandmedical.diten.mobile.core.design.component.DitenScaffold
import eu.grandmedical.diten.mobile.core.design.component.EmptyState
import eu.grandmedical.diten.mobile.core.design.component.LoadingState
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/** Home route: dashboard + permission-gated module menu. */
@Composable
fun HomeRoute(
    onOpenModule: (String) -> Unit,
    modifier: Modifier = Modifier,
    viewModel: HomeViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()
    HomeScreen(
        state = state,
        onEvent = viewModel::onEvent,
        onOpenModule = onOpenModule,
        modifier = modifier,
    )
}

@Composable
private fun HomeScreen(
    state: HomeState,
    onEvent: (HomeEvent) -> Unit,
    onOpenModule: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    DitenScaffold(
        title = "Diten ERP",
        modifier = modifier,
        actions = {
            LegalEntitySwitcher(
                available = state.availableLegalEntities,
                selected = state.selectedLegalEntityId,
                onSelect = { onEvent(HomeEvent.SelectLegalEntity(it)) },
            )
            OverflowMenu(onLogout = { onEvent(HomeEvent.Logout) })
        },
    ) { innerPadding ->
        if (state.isLoading) {
            LoadingState(modifier = Modifier.padding(innerPadding))
            return@DitenScaffold
        }

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding),
        ) {
            item {
                DashboardHeader(
                    email = state.email,
                    tenantId = state.tenantId,
                    selectedLegalEntityId = state.selectedLegalEntityId,
                )
            }
            item {
                Text(
                    text = "Modüller",
                    style = MaterialTheme.typography.titleSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(
                        start = DitenSpacing.lg,
                        end = DitenSpacing.lg,
                        top = DitenSpacing.md,
                        bottom = DitenSpacing.sm,
                    ),
                )
            }
            if (state.modules.isEmpty()) {
                item {
                    // No installed feature is permitted for this user (or none is
                    // installed yet). The menu reflects exactly the installed +
                    // permitted features, so this is the genuine empty state.
                    EmptyState(message = "Yetkili modül yok. Modüller yakında.")
                }
            } else {
                items(state.modules, key = { it.key }) { module ->
                    DitenListRow(
                        title = module.title,
                        subtitle = module.requiredPermission ?: module.route,
                        onClick = { onOpenModule(module.route) },
                    )
                    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
                }
            }
        }
    }
}

@Composable
private fun DashboardHeader(
    email: String?,
    tenantId: String?,
    selectedLegalEntityId: String?,
) {
    Surface(color = MaterialTheme.colorScheme.surface, modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(DitenSpacing.lg),
            verticalArrangement = Arrangement.spacedBy(DitenSpacing.xs),
        ) {
            Text(
                text = "Hoş geldiniz",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = email ?: "—",
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = "Kiracı: ${tenantId ?: "—"}",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = "Tüzel kişilik: ${selectedLegalEntityId ?: "—"}",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/**
 * Legal-entity switcher in the top bar. With several entities it is a dropdown
 * menu whose selection calls back into `selectLegalEntity` (driving the
 * `X-Legal-Entity-Id` header for later requests); with a single entity it is a
 * read-only label; with none it renders nothing.
 */
@Composable
private fun LegalEntitySwitcher(
    available: List<String>,
    selected: String?,
    onSelect: (String) -> Unit,
) {
    if (available.isEmpty()) return

    val label = selected ?: available.first()

    if (available.size == 1) {
        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(end = DitenSpacing.sm)) {
            Icon(imageVector = Icons.Filled.Business, contentDescription = null)
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.padding(start = DitenSpacing.xs),
            )
        }
        return
    }

    var expanded by remember { mutableStateOf(false) }
    TextButton(onClick = { expanded = true }) {
        Icon(imageVector = Icons.Filled.Business, contentDescription = null)
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.padding(horizontal = DitenSpacing.xs),
        )
        Icon(imageVector = Icons.Filled.ArrowDropDown, contentDescription = "Tüzel kişilik seç")
    }
    DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
        available.forEach { entity ->
            DropdownMenuItem(
                text = { Text(text = entity) },
                onClick = {
                    onSelect(entity)
                    expanded = false
                },
            )
        }
    }
}

@Composable
private fun OverflowMenu(onLogout: () -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    IconButton(onClick = { expanded = true }) {
        Icon(imageVector = Icons.Filled.MoreVert, contentDescription = "Menü")
    }
    DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
        DropdownMenuItem(
            text = { Text(text = "Çıkış yap") },
            leadingIcon = { Icon(imageVector = Icons.AutoMirrored.Filled.Logout, contentDescription = null) },
            onClick = {
                expanded = false
                onLogout()
            },
        )
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun HomeScreenPreview() {
    DitenTheme {
        HomeScreen(
            state = HomeState(
                email = "gm@grandmedical.eu",
                tenantId = "diten",
                selectedLegalEntityId = "GM-HU",
                availableLegalEntities = listOf("GM-HU", "GM-DE"),
                modules = listOf(
                    FeatureEntry(
                        key = "applicant-intake",
                        route = "applicant-intake",
                        title = "Aday Başvuru Alımı",
                        requiredPermission = "hcm.applicant-intake",
                    ),
                    FeatureEntry(
                        key = "leave-management",
                        route = "leave-management",
                        title = "İzin Yönetimi",
                        requiredPermission = "hcm.leave-management",
                    ),
                ),
                isLoading = false,
            ),
            onEvent = {},
            onOpenModule = {},
        )
    }
}
