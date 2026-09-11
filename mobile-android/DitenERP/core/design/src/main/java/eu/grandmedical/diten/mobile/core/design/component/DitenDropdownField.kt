package eu.grandmedical.diten.mobile.core.design.component

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
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
 * Exposed dropdown (readiness state selector and similar). Stateless: the selected
 * value and [onSelected] are hoisted; only the open/closed menu is local UI state.
 *
 * Mirrors the web readiness-state <select> control.
 */
@Suppress("LongParameterList") // Stateless selector mirrors the web select's full API.
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DitenDropdownField(
    label: String,
    options: List<String>,
    selected: String,
    onSelected: (String) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    var expanded by remember { mutableStateOf(false) }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { if (enabled) expanded = it },
        modifier = modifier,
    ) {
        OutlinedTextField(
            value = selected,
            onValueChange = {},
            readOnly = true,
            enabled = enabled,
            label = { Text(text = label, style = MaterialTheme.typography.bodySmall) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            shape = MaterialTheme.shapes.small,
            textStyle = MaterialTheme.typography.bodyMedium,
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable, enabled),
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
        ) {
            options.forEach { option ->
                DropdownMenuItem(
                    text = { Text(text = option, style = MaterialTheme.typography.bodyMedium) },
                    onClick = {
                        onSelected(option)
                        expanded = false
                    },
                )
            }
        }
    }
}

@Suppress("UnusedPrivateMember") // Rendered by the Compose preview tooling.
@OptIn(ExperimentalMaterial3Api::class)
@PreviewLightDark
@Composable
private fun DitenDropdownFieldPreview() {
    DitenTheme {
        val options = listOf("Draft", "Deferred", "Ready", "Blocked", "Not Required", "Archived")
        var selected by remember { mutableStateOf("Ready") }
        DitenDropdownField(
            label = "Readiness state",
            options = options,
            selected = selected,
            onSelected = { selected = it },
            modifier = Modifier.padding(DitenSpacing.md),
        )
    }
}
