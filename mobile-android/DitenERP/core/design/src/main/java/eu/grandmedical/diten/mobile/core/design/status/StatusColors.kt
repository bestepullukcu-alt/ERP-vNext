package eu.grandmedical.diten.mobile.core.design.status

import androidx.compose.ui.graphics.Color
import eu.grandmedical.diten.mobile.core.design.theme.DitenDanger
import eu.grandmedical.diten.mobile.core.design.theme.DitenInfo
import eu.grandmedical.diten.mobile.core.design.theme.DitenSecondary
import eu.grandmedical.diten.mobile.core.design.theme.DitenSuccess
import eu.grandmedical.diten.mobile.core.design.theme.DitenWarning

/** A "muted" gray for archived/inactive states — a desaturated variant of secondary. */
private val DitenMuted = Color(0xFFA9B2BD)

/** Resolves a semantic [StatusToken] to its measured brand [Color]. */
fun StatusToken.color(): Color = when (this) {
    StatusToken.Success -> DitenSuccess
    StatusToken.Warning -> DitenWarning
    StatusToken.Secondary -> DitenSecondary
    StatusToken.Danger -> DitenDanger
    StatusToken.Info -> DitenInfo
    StatusToken.Muted -> DitenMuted
}

/**
 * Case-insensitive convenience: the accent [Color] for a readiness [state] string.
 * Delegates the decision to the pure [statusToken] mapper, then resolves the color.
 */
fun statusColor(state: String?): Color = statusToken(state).color()
