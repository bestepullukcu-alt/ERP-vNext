package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.detail

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
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineReadiness
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.ReadinessState

/** Detail route: shows all facet states + last-evaluated, with Evaluate/Delete. */
@Composable
fun CandidatePipelineDetailRoute(
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: CandidatePipelineDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(Unit) {
        viewModel.effect.collect { effect ->
            when (effect) {
                is CandidatePipelineDetailEffect.ShowMessage -> snackbarHostState.showSnackbar(effect.message)
                CandidatePipelineDetailEffect.NavigateBack -> onBack()
            }
        }
    }

    DitenScaffold(
        title = "Aday Havuzu Kaydı",
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
                        onEvaluate = { viewModel.onEvent(CandidatePipelineDetailEvent.Evaluate) },
                        onDelete = { viewModel.onEvent(CandidatePipelineDetailEvent.Delete) },
                    )
                }
            }
        }
    }
}

@Composable
private fun DetailContent(
    record: CandidatePipelineReadiness,
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
            text = "${record.code} · ${record.sourceContractVersion} · v${record.pipelineReadinessVersion}",
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

        FacetRow("Havuz durumu", record.pipelineReadinessState)
        FacetRow("Aşama yönetişimi", record.pipelineStageGovernanceState)
        FacetRow("Mülakat planlama hazırlığı", record.interviewSchedulingReadinessState)
        FacetRow("Mülakatçı atama hazırlığı", record.interviewerAssignmentReadinessState)
        FacetRow("Değerlendirme yönetişimi", record.evaluationGovernanceState)
        FacetRow("Aday iletişim sınırı", record.candidateCommunicationBoundaryState)
        FacetRow("Rıza ön koşulu", record.consentPreconditionState)
        FacetRow("Veri minimizasyonu", record.dataMinimizationState)
        FacetRow("Saklama politikası", record.retentionPolicyState)
        FacetRow("Kanıt politikası", record.evidencePolicyState)
        FacetRow("Takvim bağımlılığı", record.calendarDependencyState)
        FacetRow("Bildirim bağımlılığı", record.notificationDependencyState)
        FacetRow("Belge bağımlılığı", record.documentDependencyState)
        FacetRow("Otomatik karar sınırı", record.automatedDecisionBoundaryState)

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
