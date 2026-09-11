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
    testImplementation(libs.junit)
}
