package eu.grandmedical.diten.mobile.feature.competencyskills.presentation.list

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.design.component.DitenListRow
import eu.grandmedical.diten.mobile.core.design.component.DitenTopAppBar
import eu.grandmedical.diten.mobile.core.design.component.StatusChip
import eu.grandmedical.diten.mobile.core.design.component.UiStateContainer
import eu.grandmedical.diten.mobile.feature.competencyskills.domain.CompetencySkillsListItem

/** List route: observes the cache, pull-to-refresh, FAB to create, row -> detail. */
@Composable
fun CompetencySkillsListRoute(
    onOpenDetail: (String) -> Unit,
    onOpenCreate: () -> Unit,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: CompetencySkillsListViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(Unit) {
        viewModel.effect.collect { effect ->
            when (effect) {
                is CompetencySkillsListEffect.ShowMessage ->
                    snackbarHostState.showSnackbar(effect.message)
            }
        }
    }

    CompetencySkillsListScreen(
        state = state,
        snackbarHostState = snackbarHostState,
        onRefresh = { viewModel.onEvent(CompetencySkillsListEvent.Refresh) },
        onOpenDetail = onOpenDetail,
        onOpenCreate = onOpenCreate,
        onBack = onBack,
        modifier = modifier,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Suppress("LongParameterList") // Stateless screen hoists its callbacks + snackbar state.
@Composable
private fun CompetencySkillsListScreen(
    state: CompetencySkillsListState,
    snackbarHostState: SnackbarHostState,
    onRefresh: () -> Unit,
    onOpenDetail: (String) -> Unit,
    onOpenCreate: () -> Unit,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            DitenTopAppBar(
                title = "Yetkinlik & Beceriler",
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(imageVector = Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Geri")
                    }
                },
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        floatingActionButton = {
            FloatingActionButton(onClick = onOpenCreate) {
                Icon(imageVector = Icons.Filled.Add, contentDescription = "Yeni kayıt")
            }
        },
    ) { innerPadding ->
        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = onRefresh,
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding),
        ) {
            UiStateContainer(
                state = state.items,
                emptyMessage = "Henüz kayıt yok. Yeni bir kayıt ekleyin.",
            ) { items ->
                CompetencySkillsList(items = items, onOpenDetail = onOpenDetail)
            }
        }
    }
}

@Composable
private fun CompetencySkillsList(
    items: List<CompetencySkillsListItem>,
    onOpenDetail: (String) -> Unit,
) {
    LazyColumn(modifier = Modifier.fillMaxSize()) {
        items(items, key = { it.id }) { item ->
            DitenListRow(
                title = item.displayName,
                subtitle = item.code,
                onClick = { onOpenDetail(item.id) },
                trailing = { StatusChip(state = item.competencySkillsReadinessState.name) },
            )
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        }
    }
}
