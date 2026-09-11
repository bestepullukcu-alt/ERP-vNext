plugins {
    id("diten.android.library")
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.common"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
}

dependencies {
    // :core:common is the dependency-free presentation base. It may only depend on
    // coroutines + the ViewModel KTX (for the MVI base); it MUST NOT depend on
    // :core:auth / :core:network / :core:database / :core:sync so that any layer
    // can depend on it without pulling in the whole graph.
    implementation(libs.kotlinx.coroutines.core)
    implementation(libs.androidx.lifecycle.viewmodel.ktx)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
}
