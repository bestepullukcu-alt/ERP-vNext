package eu.grandmedical.diten.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import dagger.hilt.android.AndroidEntryPoint
import eu.grandmedical.diten.mobile.core.design.theme.DitenTheme
import eu.grandmedical.diten.mobile.navigation.AppRoot

/**
 * The single Activity hosting the whole Compose app. Wraps the navigation graph
 * ([AppRoot]) in the authoritative [DitenTheme] from :core:design (the M0.1
 * placeholder theme in `ui/theme` has been retired).
 */
@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            DitenTheme {
                AppRoot()
            }
        }
    }
}
