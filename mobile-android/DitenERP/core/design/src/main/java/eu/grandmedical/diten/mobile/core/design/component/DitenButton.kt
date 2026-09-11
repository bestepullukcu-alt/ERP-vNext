@file:Suppress("MatchingDeclarationName") // File groups the button + its variant enum by intent.

package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.PreviewLightDark
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/**
 * Diten button variants.
 *
 * @param variant [DitenButtonVariant.Primary] is the filled brand button;
 *   [DitenButtonVariant.Secondary] is the outlined, lower-emphasis variant.
 */
enum class DitenButtonVariant { Primary, Secondary }

@Composable
fun DitenButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    variant: DitenButtonVariant = DitenButtonVariant.Primary,
    enabled: Boolean = true,
) {
    when (variant) {
        DitenButtonVariant.Primary ->
            Button(
                onClick = onClick,
                modifier = modifier,
                enabled = enabled,
                shape = MaterialTheme.shapes.small,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                ),
            ) {
                Text(text = text, style = MaterialTheme.typography.labelLarge)
            }

        DitenButtonVariant.Secondary ->
            OutlinedButton(
                onClick = onClick,
                modifier = modifier,
                enabled = enabled,
                shape = MaterialTheme.shapes.small,
                colors = ButtonDefaults.outlinedButtonColors(
                    contentColor = MaterialTheme.colorScheme.primary,
                ),
            ) {
                Text(text = text, style = MaterialTheme.typography.labelLarge)
            }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun DitenButtonPreview() {
    DitenTheme {
        Column(
            verticalArrangement = Arrangement.spacedBy(DitenSpacing.sm),
            modifier = Modifier.padding(DitenSpacing.md),
        ) {
            DitenButton(text = "Save", onClick = {})
            DitenButton(text = "Cancel", onClick = {}, variant = DitenButtonVariant.Secondary)
            DitenButton(text = "Disabled", onClick = {}, enabled = false)
        }
    }
}
