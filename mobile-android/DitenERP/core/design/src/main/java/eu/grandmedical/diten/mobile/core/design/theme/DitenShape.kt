package eu.grandmedical.diten.mobile.core.design.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Shapes
import androidx.compose.ui.unit.dp

/**
 * Diten shapes. The web theme uses ~0.5rem (8dp) card radii; components lean on
 * [Shapes.small] for chips/fields and [Shapes.medium] for cards.
 */
val DitenShapes = Shapes(
    extraSmall = RoundedCornerShape(4.dp),
    small = RoundedCornerShape(6.dp),
    medium = RoundedCornerShape(8.dp),
    large = RoundedCornerShape(12.dp),
    extraLarge = RoundedCornerShape(20.dp),
)
