package eu.grandmedical.diten.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import dagger.hilt.android.AndroidEntryPoint
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.ui.theme.DitenERPTheme
import javax.inject.Inject
import javax.inject.Named

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    /** Injected by Hilt to prove the runtime graph resolves. */
    @Inject
    @Named("appVersion")
    lateinit var appVersion: String

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        // Wrap the injected value in the shared UiResult so :core:common is really used.
        val version: UiResult<String> = UiResult.Success(appVersion)

        setContent {
            DitenERPTheme {
                PlaceholderScreen(versionState = version)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PlaceholderScreen(versionState: UiResult<String>) {
    val version = when (versionState) {
        is UiResult.Success -> versionState.data
        is UiResult.Error -> versionState.message
        UiResult.Loading -> "…"
    }

    Scaffold(
        modifier = Modifier.fillMaxSize(),
        topBar = { TopAppBar(title = { Text("Diten ERP") }) },
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = "Mobil · İskele hazır (v$version)",
                style = MaterialTheme.typography.titleMedium,
            )
        }
    }
}

@Preview(showBackground = true)
@Composable
fun PlaceholderScreenPreview() {
    DitenERPTheme {
        PlaceholderScreen(versionState = UiResult.Success("0.1.0-skeleton"))
    }
}
