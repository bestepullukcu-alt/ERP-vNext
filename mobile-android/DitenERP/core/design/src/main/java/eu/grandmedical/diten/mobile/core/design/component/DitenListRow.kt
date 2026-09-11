package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.PreviewLightDark
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/**
 * Compact list row mirroring the web "golden-compact" table/list line:
 * a title with an optional subtitle on the left, and an optional trailing slot
 * (typically a [StatusChip]) on the right.
 *
 * Stateless; [onClick] is optional and hoisted.
 */
@Composable
fun DitenListRow(
    title: String,
    modifier: Modifier = Modifier,
    subtitle: String? = null,
    onClick: (() -> Unit)? = null,
    trailing: @Composable (() -> Unit)? = null,
) {
    val rowModifier = if (onClick != null) {
        modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
    } else {
        modifier.fillMaxWidth()
    }

    Surface(color = MaterialTheme.colorScheme.surface) {
        Row(
            modifier = rowModifier.padding(horizontal = DitenSpacing.lg, vertical = DitenSpacing.md),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(DitenSpacing.md),
        ) {
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(DitenSpacing.xs),
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleSmall,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                if (subtitle != null) {
                    Text(
                        text = subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }
            if (trailing != null) {
                trailing()
            }
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun DitenListRowPreview() {
    DitenTheme {
        Column {
            DitenListRow(
                title = "Grand Medical Kft.",
                subtitle = "Membership #A-1042",
                trailing = { StatusChip(state = "Ready") },
                onClick = {},
            )
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            DitenListRow(
                title = "Északi Klinika",
                subtitle = "Membership #A-1043",
                trailing = { StatusChip(state = "Blocked") },
                onClick = {},
            )
        }
    }
}
