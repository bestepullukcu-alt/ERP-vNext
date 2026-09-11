plugins {
    id("diten.android.library")
    // No Compose convention plugin exists; enable Compose manually the same way :app does.
    alias(libs.plugins.kotlin.compose)
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.design"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildFeatures {
        compose = true
    }
}

dependencies {
    // Design system consumes UiResult (Loading/Success/Error) from :core:common only.
    // It must NOT depend on :app, :core:network or :core:database.
    implementation(project(":core:common"))

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.foundation)
    implementation(libs.androidx.compose.material.icons.extended)
    implementation(libs.androidx.compose.ui.tooling.preview)

    // Tooling for @Preview rendering; debug classpath only.
    debugImplementation(libs.androidx.compose.ui.tooling)

    // Pure-JVM unit tests for the readiness status system (no emulator required).
    testImplementation(libs.junit)

    // Instrumented Compose UI test wiring is available for later work packages.
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
}
