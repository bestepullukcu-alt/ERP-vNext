package eu.grandmedical.diten.mobile.feature.performancereviews.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Light projection used by the list screen — just enough to render a row
 * ([DitenListRow][eu.grandmedical.diten.mobile.core.design.component.DitenListRow]
 * plus a [StatusChip][eu.grandmedical.diten.mobile.core.design.component.StatusChip]
 * on [performanceReviewReadinessState]) without materialising the full aggregate.
 */
data class PerformanceReviewsListItem(
    val id: String,
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: ReadinessState,
    val sourceContractVersion: String,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
