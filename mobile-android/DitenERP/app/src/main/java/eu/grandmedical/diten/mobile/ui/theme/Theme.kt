package eu.grandmedical.diten.mobile.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext

private val DitenDarkColorScheme = darkColorScheme(
    primary = DitenIndigoLight,
    onPrimary = DitenIndigoDark,
    secondary = DitenTealLight,
    tertiary = DitenAmberLight,
)

private val DitenLightColorScheme = lightColorScheme(
    primary = DitenIndigo,
    onPrimary = Color.White,
    secondary = DitenTeal,
    tertiary = DitenAmber,
)

@Composable
fun DitenERPTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    // Brand-first: Material You dynamic color is off by default so the Di10 indigo shows.
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit,
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }

        darkTheme -> DitenDarkColorScheme
        else -> DitenLightColorScheme
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content,
    )
}
