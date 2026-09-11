package eu.grandmedical.diten.mobile.core.design.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider

/**
 * Root theme for the Diten mobile app. This is the AUTHORITATIVE design system;
 * it supersedes the placeholder indigo theme in :app.
 *
 * Brand-first by default: [dynamicColor] is FALSE so the measured brand palette
 * (primary #696CFF) always shows instead of Material You device colors.
 *
 * @param darkTheme whether to use the dark scheme; follows the system by default.
 * @param dynamicColor intentionally kept for API symmetry with the platform theme,
 *   but defaults to false — the design system never adopts Material You.
 */
@Composable
fun DitenTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    @Suppress("UNUSED_PARAMETER") dynamicColor: Boolean = false,
    content: @Composable () -> Unit,
) {
    val colorScheme = if (darkTheme) DitenDarkColors else DitenLightColors

    CompositionLocalProvider(LocalDitenSpacing provides DitenSpacing) {
        MaterialTheme(
            colorScheme = colorScheme,
            typography = DitenTypography,
            shapes = DitenShapes,
            content = content,
        )
    }
}
