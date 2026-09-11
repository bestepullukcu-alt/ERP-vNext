package eu.grandmedical.diten.mobile.core.design.theme

import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color

/**
 * Diten design-system color tokens.
 *
 * These are the MEASURED values of the web frontend (Sneat theme) and are the
 * single source of truth for the mobile palette. Do not invent new brand colors
 * here; add semantic aliases that resolve to one of the measured base tokens.
 */

// --- Measured brand palette (from the web Sneat theme) --------------------------------
val DitenPrimary = Color(0xFF696CFF)
val DitenSecondary = Color(0xFF8592A3)
val DitenSuccess = Color(0xFF71DD37)
val DitenWarning = Color(0xFFFFAB00)
val DitenDanger = Color(0xFFFF3E1D)
val DitenInfo = Color(0xFF03C3EC)

// --- Neutral surfaces (typical Sneat light/dark grounds) ------------------------------
val DitenLightBackground = Color(0xFFF5F5F9)
val DitenLightSurface = Color(0xFFFFFFFF)
val DitenLightSurfaceVariant = Color(0xFFEDEDF2)
val DitenLightOnSurface = Color(0xFF384551)
val DitenLightOnSurfaceVariant = Color(0xFF67697D)
val DitenLightOutline = Color(0xFFD9DEE3)

val DitenDarkBackground = Color(0xFF232333)
val DitenDarkSurface = Color(0xFF2B2C40)
val DitenDarkSurfaceVariant = Color(0xFF44475B)
val DitenDarkOnSurface = Color(0xFFCBCBE2)
val DitenDarkOnSurfaceVariant = Color(0xFFA3A4CC)
val DitenDarkOutline = Color(0xFF585A72)

// --- On-brand foregrounds -------------------------------------------------------------
val DitenOnPrimary = Color(0xFFFFFFFF)
val DitenOnDanger = Color(0xFFFFFFFF)

// --- Light / dark tonal containers derived from the brand hues ------------------------
val DitenPrimaryContainerLight = Color(0xFFE7E7FF)
val DitenPrimaryContainerDark = Color(0xFF3B3D8F)
val DitenSecondaryContainerLight = Color(0xFFE9ECEF)
val DitenSecondaryContainerDark = Color(0xFF4A5160)
val DitenDangerContainerLight = Color(0xFFFFE0DB)
val DitenDangerContainerDark = Color(0xFF8F2314)

/**
 * Light Material 3 scheme mapping the measured palette onto standard color roles.
 * Brand primary is #696CFF; error role carries the danger red; tertiary carries info cyan.
 */
val DitenLightColors = lightColorScheme(
    primary = DitenPrimary,
    onPrimary = DitenOnPrimary,
    primaryContainer = DitenPrimaryContainerLight,
    onPrimaryContainer = Color(0xFF23246B),
    secondary = DitenSecondary,
    onSecondary = Color(0xFFFFFFFF),
    secondaryContainer = DitenSecondaryContainerLight,
    onSecondaryContainer = Color(0xFF2F3540),
    tertiary = DitenInfo,
    onTertiary = Color(0xFF00323D),
    error = DitenDanger,
    onError = DitenOnDanger,
    errorContainer = DitenDangerContainerLight,
    onErrorContainer = Color(0xFF5F1206),
    background = DitenLightBackground,
    onBackground = DitenLightOnSurface,
    surface = DitenLightSurface,
    onSurface = DitenLightOnSurface,
    surfaceVariant = DitenLightSurfaceVariant,
    onSurfaceVariant = DitenLightOnSurfaceVariant,
    outline = DitenLightOutline,
    outlineVariant = Color(0xFFE7EAEE),
)

/**
 * Dark Material 3 scheme. The brand primary stays saturated (#696CFF) against the
 * Sneat dark grounds so the brand still reads on dark surfaces.
 */
val DitenDarkColors = darkColorScheme(
    primary = DitenPrimary,
    onPrimary = DitenOnPrimary,
    primaryContainer = DitenPrimaryContainerDark,
    onPrimaryContainer = Color(0xFFE7E7FF),
    secondary = DitenSecondary,
    onSecondary = Color(0xFF1B1F27),
    secondaryContainer = DitenSecondaryContainerDark,
    onSecondaryContainer = Color(0xFFE9ECEF),
    tertiary = DitenInfo,
    onTertiary = Color(0xFF00323D),
    error = DitenDanger,
    onError = DitenOnDanger,
    errorContainer = DitenDangerContainerDark,
    onErrorContainer = Color(0xFFFFE0DB),
    background = DitenDarkBackground,
    onBackground = DitenDarkOnSurface,
    surface = DitenDarkSurface,
    onSurface = DitenDarkOnSurface,
    surfaceVariant = DitenDarkSurfaceVariant,
    onSurfaceVariant = DitenDarkOnSurfaceVariant,
    outline = DitenDarkOutline,
    outlineVariant = Color(0xFF3A3C50),
)
