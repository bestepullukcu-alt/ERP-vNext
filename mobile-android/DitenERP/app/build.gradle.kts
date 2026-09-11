plugins {
    id("diten.android.application")
    alias(libs.plugins.kotlin.compose)
    id("diten.android.hilt")
}

android {
    namespace = "eu.grandmedical.diten.mobile"

    defaultConfig {
        applicationId = "eu.grandmedical.diten.mobile"
        targetSdk = 37
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            // R8 full-mode optimization + shrinking for release.
            optimization {
                enable = true
            }
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }
    buildFeatures {
        compose = true
    }
}

dependencies {
    implementation(project(":core:common"))
    implementation(project(":core:network"))

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.
    implementation(libs.androidx.hilt.navigation.compose)

    // Leak detection - debug builds only, must never reach the release classpath.
    debugImplementation(libs.leakcanary.android)

    testImplementation(libs.junit)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    debugImplementation(libs.androidx.compose.ui.tooling)
}
