plugins {
    id("diten.android.library")
    // No Compose convention plugin exists; enable Compose manually the same way
    // :app and :core:design do.
    alias(libs.plugins.kotlin.compose)
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.feature"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildFeatures {
        // Compose is enabled manually in library modules (no convention plugin).
        compose = true
    }
}

dependencies {
    // The framework-agnostic contracts (FeatureEntry / NavRoute / PermissionGate)
    // live in :core:common; expose them transitively so any consumer of
    // :core:feature (the app shell, every feature module) sees FeatureEntry too.
    api(project(":core:common"))

    // Compose-aware nav contract: FeatureNavGraph hands a feature the NavHost's
    // NavGraphBuilder + NavController so it can contribute its destinations.
    api(platform(libs.androidx.compose.bom))
    api(libs.androidx.navigation.compose)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.material3)

    testImplementation(libs.junit)
}
