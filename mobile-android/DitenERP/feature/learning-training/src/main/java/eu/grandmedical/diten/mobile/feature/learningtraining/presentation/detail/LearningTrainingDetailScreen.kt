package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.detail

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
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingReadiness
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState

/** Detail route: shows all facet states + last-evaluated, with Evaluate/Delete. */
@Composable
fun LearningTrainingDetailRoute(
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: LearningTrainingDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(Unit) {
        viewModel.effect.collect { effect ->
            when (effect) {
                is LearningTrainingDetailEffect.ShowMessage -> snackbarHostState.showSnackbar(effect.message)
                LearningTrainingDetailEffect.NavigateBack -> onBack()
            }
        }
    }

    DitenScaffold(
        title = "Öğrenme & Eğitim Kaydı",
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
                        onEvaluate = { viewModel.onEvent(LearningTrainingDetailEvent.Evaluate) },
                        onDelete = { viewModel.onEvent(LearningTrainingDetailEvent.Delete) },
                    )
                }
            }
        }
    }
}

@Suppress("LongMethod") // A flat facet column reads more clearly than fragmented sub-composables.
@Composable
private fun DetailContent(
    record: LearningTrainingReadiness,
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
            text = "${record.code} · ${record.sourceContractVersion} · v${record.learningTrainingReadinessVersion}",
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

        FacetRow("Genel hazırlık durumu", record.learningTrainingReadinessState)
        FacetRow("Kurs kataloğu sınırı", record.courseCatalogBoundaryState)
        FacetRow("Kayıt iş akışı sınırı", record.enrollmentWorkflowBoundaryState)
        FacetRow("Tamamlama takibi sınırı", record.completionTrackingBoundaryState)
        FacetRow("Sertifikasyon sınırı", record.certificationBoundaryState)
        FacetRow("Değerlendirme puanlama sınırı", record.assessmentScoringBoundaryState)
        FacetRow("Otomatik karar sınırı", record.automatedDecisionBoundaryState)
        FacetRow("Öğrenme içeriği bağımlılığı", record.learningContentDependencyState)
        FacetRow("Yetkinlik taksonomisi bağımlılığı", record.skillTaxonomyDependencyState)
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
