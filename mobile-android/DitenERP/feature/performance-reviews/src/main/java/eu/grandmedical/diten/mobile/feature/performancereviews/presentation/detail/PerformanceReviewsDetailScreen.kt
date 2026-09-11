package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.detail

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.design.component.DitenButton
import eu.grandmedical.diten.mobile.core.design.component.DitenButtonVariant
import eu.grandmedical.diten.mobile.core.design.component.DitenScaffold
import eu.grandmedical.diten.mobile.core.design.component.EmptyState
import eu.grandmedical.diten.mobile.core.design.component.StatusChip
import eu.grandmedical.diten.mobile.core.design.component.UiStateContainer
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsReadiness
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.ReadinessState

/** Detail route: shows all facet states + last-evaluated, with Evaluate/Delete. */
@Composable
fun PerformanceReviewsDetailRoute(
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: PerformanceReviewsDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(Unit) {
        viewModel.effect.collect { effect ->
            when (effect) {
                is PerformanceReviewsDetailEffect.ShowMessage -> snackbarHostState.showSnackbar(effect.message)
                PerformanceReviewsDetailEffect.NavigateBack -> onBack()
            }
        }
    }

    DitenScaffold(
        title = "Değerlendirme Kaydı",
        modifier = modifier,
        navigationIcon = {
            IconButton(onClick = onBack) {
                Icon(imageVector = Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Geri")
            }
        },
    ) { innerPadding ->
        Column(modifier = Modifier.fillMaxSize().padding(innerPadding)) {
            SnackbarHost(snackbarHostState)
            UiStateContainer(state = state.record) { record ->
                if (record == null) {
                    EmptyState(message = "Kayıt bulunamadı.")
                } else {
                    DetailContent(
                        record = record,
                        isBusy = state.isBusy,
                        onEvaluate = { viewModel.onEvent(PerformanceReviewsDetailEvent.Evaluate) },
                        onDelete = { viewModel.onEvent(PerformanceReviewsDetailEvent.Delete) },
                    )
                }
            }
        }
    }
}

@Suppress("LongMethod") // A single flat facet column reads more clearly than fragmented sub-composables.
@Composable
private fun DetailContent(
    record: PerformanceReviewsReadiness,
    isBusy: Boolean,
    onEvaluate: () -> Unit,
    onDelete: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(DitenSpacing.lg)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(DitenSpacing.md),
    ) {
        Text(text = record.displayName, style = MaterialTheme.typography.titleLarge)
        Text(
            text = "${record.code} · ${record.sourceContractVersion} · v${record.performanceReviewReadinessVersion}",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = "Son değerlendirme: ${record.lastEvaluatedAt?.toString() ?: "—"}",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = "Senkron: ${record.syncStatus.name}",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        FacetRow("Değerlendirme durumu", record.performanceReviewReadinessState)
        FacetRow("Değerlendirme döngüsü sınırı", record.reviewCycleBoundaryState)
        FacetRow("Hedef bağımlılığı", record.goalDependencyState)
        FacetRow("Puanlama sınırı", record.scoringBoundaryState)
        FacetRow("Derecelendirme sınırı", record.ratingBoundaryState)
        FacetRow("Kalibrasyon sınırı", record.calibrationBoundaryState)
        FacetRow("Sıralama sınırı", record.rankingBoundaryState)
        FacetRow("Otomatik karar sınırı", record.automatedDecisionBoundaryState)
        FacetRow("Yönetici değerlendirme UX sınırı", record.managerReviewUxBoundaryState)
        FacetRow("Çalışan değerlendirme UX sınırı", record.employeeReviewUxBoundaryState)
        FacetRow("Ücret verisi sınırı", record.compensationDataBoundaryState)
        FacetRow("Yan haklar verisi sınırı", record.benefitsDataBoundaryState)
        FacetRow("Bordro verisi sınırı", record.payrollDataBoundaryState)
        FacetRow("Belge bağımlılığı", record.documentDependencyState)
        FacetRow("Bildirim bağımlılığı", record.notificationDependencyState)
        FacetRow("Rıza ön koşulu", record.consentPreconditionState)
        FacetRow("Veri minimizasyonu", record.dataMinimizationState)
        FacetRow("Saklama politikası", record.retentionPolicyState)
        FacetRow("Kanıt politikası", record.evidencePolicyState)

        DitenButton(
            text = if (isBusy) "İşleniyor..." else "Değerlendir",
            onClick = onEvaluate,
            enabled = !isBusy,
            modifier = Modifier.fillMaxWidth(),
        )
        DitenButton(
            text = "Sil",
            onClick = onDelete,
            variant = DitenButtonVariant.Secondary,
            enabled = !isBusy,
            modifier = Modifier.fillMaxWidth(),
        )
    }
}

@Composable
private fun FacetRow(label: String, state: ReadinessState) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
        StatusChip(state = state.name)
    }
}
