plugins {
    id("diten.android.library")
    id("diten.android.hilt")
    alias(libs.plugins.kotlin.serialization)
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.network"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        // Gateway base URL and logging flag are compiled in via BuildConfig so no
        // endpoint/secret is hard-coded in source. Per-build-type values below.
        buildConfigField("String", "BASE_URL", "\"http://10.0.2.2:5080/\"")
        buildConfigField("boolean", "NETWORK_LOG", "true")
    }

    buildTypes {
        debug {
            // Android emulator -> host loopback; verbose logging with redaction.
            buildConfigField("String", "BASE_URL", "\"http://10.0.2.2:5080/\"")
            buildConfigField("boolean", "NETWORK_LOG", "true")
        }
        release {
            // TODO(M0.4+): replace with the real HTTPS gateway origin before shipping.
            buildConfigField("String", "BASE_URL", "\"https://REPLACE-ME.diten.invalid/\"")
            buildConfigField("boolean", "NETWORK_LOG", "false")
        }
    }

    buildFeatures {
        buildConfig = true
    }
}

dependencies {
    implementation(project(":core:common"))

    implementation(libs.retrofit.core)
    implementation(libs.retrofit.kotlinx.serialization.converter)
    implementation(libs.okhttp.core)
    implementation(libs.okhttp.logging.interceptor)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.core)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.

    testImplementation(libs.junit)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.turbine)
    testImplementation(libs.retrofit.core)
    testImplementation(libs.retrofit.kotlinx.serialization.converter)
    testImplementation(libs.kotlinx.serialization.json)
}
