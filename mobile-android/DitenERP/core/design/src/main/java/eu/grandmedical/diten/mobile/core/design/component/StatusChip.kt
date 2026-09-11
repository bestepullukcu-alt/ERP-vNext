package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.PreviewLightDark
import androidx.compose.ui.unit.dp
import eu.grandmedical.diten.mobile.core.design.status.color
import eu.grandmedical.diten.mobile.core.design.status.statusLabel
import eu.grandmedical.diten.mobile.core.design.status.statusToken
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/**
 * Compact readiness pill mirroring the web "golden-compact" status badge:
 * a tinted rounded surface, a solid accent dot, and the human label.
 *
 * @param state raw readiness state string (case-insensitive; safe fallback).
 */
@Composable
fun StatusChip(
    state: String,
    modifier: Modifier = Modifier,
) {
    val accent: Color = statusToken(state).color()
    val label = statusLabel(state)

    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(50),
        color = accent.copy(alpha = 0.14f),
        contentColor = accent,
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(DitenSpacing.xs),
            modifier = Modifier.padding(horizontal = DitenSpacing.sm, vertical = DitenSpacing.xs),
        ) {
            Box(
                modifier = Modifier
                    .size(8.dp)
                    .background(color = accent, shape = CircleShape),
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
            )
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun StatusChipPreview() {
    DitenTheme {
        Row(
            horizontalArrangement = Arrangement.spacedBy(DitenSpacing.sm),
            modifier = Modifier.padding(DitenSpacing.md),
        ) {
            StatusChip(state = "Ready")
            StatusChip(state = "deferred")
            StatusChip(state = "BLOCKED")
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun StatusChipAllStatesPreview() {
    DitenTheme {
        Row(
            horizontalArrangement = Arrangement.spacedBy(DitenSpacing.sm),
            modifier = Modifier.padding(DitenSpacing.md),
        ) {
            StatusChip(state = "Draft")
            StatusChip(state = "NotRequired")
            StatusChip(state = "Archived")
            StatusChip(state = "something-else")
        }
    }
}
