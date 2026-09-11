@file:Suppress("MatchingDeclarationName") // File is named for the DitenSpacing token instance it exports.

package eu.grandmedical.diten.mobile.core.design.theme

import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/**
 * Spacing tokens for the compact Diten layout grid (4dp base step).
 * Access via [DitenSpacing] directly, or through [LocalDitenSpacing] when a
 * composable subtree needs to override density.
 */
data class DitenSpacingTokens(
    val none: Dp = 0.dp,
    val xs: Dp = 4.dp,
    val sm: Dp = 8.dp,
    val md: Dp = 12.dp,
    val lg: Dp = 16.dp,
    val xl: Dp = 24.dp,
    val xxl: Dp = 32.dp,
)

/** Default spacing scale used across the design system. */
val DitenSpacing = DitenSpacingTokens()

/** CompositionLocal so a subtree can supply a custom spacing scale if ever needed. */
val LocalDitenSpacing = staticCompositionLocalOf { DitenSpacing }
