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
    implementation(project(":core:auth"))
    implementation(project(":core:design"))
    implementation(project(":core:sync"))
    // Feature-plugin contract: the shell injects Set<FeatureEntry> (menu) and
    // Set<FeatureNavGraph> (nav) contributed by feature modules via Hilt.
    implementation(project(":core:feature"))

    // Feature modules are pulled in ONLY to place their @IntoSet contributions
    // (FeatureEntry + FeatureNavGraph + SyncHandler) on the app classpath. The
    // shell references NO feature type directly — adding a module needs zero edits
    // here beyond this one line.
    implementation(project(":feature:applicant-intake"))

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.compose.material.icons.extended)
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.kotlinx.coroutines.core)

    // Single-Activity Compose navigation for the app shell.
    implementation(libs.androidx.navigation.compose)

    // WorkManager app-init: the Application provides a HiltWorkerFactory so
    // :core:sync's @HiltWorker (SyncWorker) can be constructed with injected deps.
    implementation(libs.androidx.work.runtime.ktx)
    implementation(libs.androidx.hilt.work)
    ksp(libs.androidx.hilt.compiler)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.
    implementation(libs.androidx.hilt.navigation.compose)

    // Leak detection - debug builds only, must never reach the release classpath.
    debugImplementation(libs.leakcanary.android)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    debugImplementation(libs.androidx.compose.ui.tooling)
}
