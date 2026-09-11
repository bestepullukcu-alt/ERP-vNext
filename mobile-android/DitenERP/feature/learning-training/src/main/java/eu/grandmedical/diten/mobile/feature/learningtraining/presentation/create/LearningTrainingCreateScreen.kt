package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.create

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import eu.grandmedical.diten.mobile.core.design.component.DitenButton
import eu.grandmedical.diten.mobile.core.design.component.DitenDropdownField
import eu.grandmedical.diten.mobile.core.design.component.DitenScaffold
import eu.grandmedical.diten.mobile.core.design.component.DitenTextField
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.ReadinessState

/** Create route: offline-first form. On success, navigates back. */
@Composable
fun LearningTrainingCreateRoute(
    onDone: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: LearningTrainingCreateViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsState()

    LaunchedEffect(Unit) {
        viewModel.effect.collect { effect ->
            when (effect) {
                LearningTrainingCreateEffect.NavigateBack -> onDone()
            }
        }
    }

    LearningTrainingCreateScreen(
        state = state,
        onEvent = viewModel::onEvent,
        onBack = onDone,
        modifier = modifier,
    )
}

@Suppress("LongMethod") // A single flat form column reads more clearly than fragmented sub-composables.
@Composable
private fun LearningTrainingCreateScreen(
    state: LearningTrainingCreateState,
    onEvent: (LearningTrainingCreateEvent) -> Unit,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    DitenScaffold(
        title = "Yeni Öğrenme & Eğitim Kaydı",
        modifier = modifier,
        navigationIcon = {
            IconButton(onClick = onBack) {
                Icon(imageVector = Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Geri")
            }
        },
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(DitenSpacing.lg)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(DitenSpacing.md),
        ) {
            DitenTextField(
                value = state.code,
                onValueChange = { onEvent(LearningTrainingCreateEvent.CodeChanged(it)) },
                label = "Kod",
                supportingText = "Otomatik üretildi; düzenlenebilir.",
            )
            DitenTextField(
                value = state.displayName,
                onValueChange = { onEvent(LearningTrainingCreateEvent.DisplayNameChanged(it)) },
                label = "Görünen ad",
            )
            DitenTextField(
                value = state.sourceContractVersion,
                onValueChange = { onEvent(LearningTrainingCreateEvent.SourceContractVersionChanged(it)) },
                label = "Kaynak sözleşme sürümü",
            )

            LearningTrainingStateField.entries.forEach { field ->
                val selected = state.states[field] ?: field.default
                DitenDropdownField(
                    label = field.label,
                    options = READINESS_OPTIONS,
                    selected = selected.name,
                    onSelected = { name ->
                        onEvent(LearningTrainingCreateEvent.StateChanged(field, ReadinessState.valueOf(name)))
                    },
                )
            }

            DitenTextField(
                value = state.deferredReason,
                onValueChange = { onEvent(LearningTrainingCreateEvent.DeferredReasonChanged(it)) },
                label = "Erteleme nedeni (opsiyonel)",
                singleLine = false,
            )

            state.errorMessage?.let { message ->
                DitenTextField(
                    value = message,
                    onValueChange = {},
                    label = "Hata",
                    isError = true,
                    enabled = false,
                )
            }

            DitenButton(
                text = if (state.isSubmitting) "Kaydediliyor..." else "Kaydet",
                onClick = { onEvent(LearningTrainingCreateEvent.Submit) },
                enabled = state.canSubmit,
            )
        }
    }
}

private val READINESS_OPTIONS: List<String> = ReadinessState.entries.map { it.name }
