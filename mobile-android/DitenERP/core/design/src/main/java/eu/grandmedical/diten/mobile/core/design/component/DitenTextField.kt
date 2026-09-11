package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.PreviewLightDark
import eu.grandmedical.diten.mobile.core.design.theme.DitenSpacing
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme

/**
 * Diten outlined text field. Fully stateless: value + [onValueChange] are hoisted.
 * Mirrors the web compact form input (single-line label + helper/error text).
 */
@Suppress("LongParameterList") // Design-system field intentionally mirrors the full web input API.
@Composable
fun DitenTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
    supportingText: String? = null,
    isError: Boolean = false,
    singleLine: Boolean = true,
    enabled: Boolean = true,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier.fillMaxWidth(),
        enabled = enabled,
        label = { Text(text = label, style = MaterialTheme.typography.bodySmall) },
        placeholder = placeholder?.let { { Text(text = it) } },
        supportingText = supportingText?.let { { Text(text = it) } },
        isError = isError,
        singleLine = singleLine,
        shape = MaterialTheme.shapes.small,
        textStyle = MaterialTheme.typography.bodyMedium,
    )
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun DitenTextFieldPreview() {
    DitenTheme {
        var text by remember { mutableStateOf("Grand Medical") }
        DitenTextField(
            value = text,
            onValueChange = { text = it },
            label = "Company name",
            supportingText = "As registered with the association",
            modifier = Modifier.padding(DitenSpacing.md),
        )
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@PreviewLightDark
@Composable
private fun DitenTextFieldErrorPreview() {
    DitenTheme {
        DitenTextField(
            value = "",
            onValueChange = {},
            label = "Tax ID",
            placeholder = "Enter tax id",
            supportingText = "Required",
            isError = true,
            modifier = Modifier.padding(DitenSpacing.md),
        )
    }
}
