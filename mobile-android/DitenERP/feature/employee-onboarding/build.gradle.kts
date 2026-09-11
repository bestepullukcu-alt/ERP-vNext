import org.gradle.jvm.toolchain.JavaLanguageVersion
import org.gradle.jvm.toolchain.JavaToolchainService
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("diten.android.library")
    id("diten.android.hilt")
    alias(libs.plugins.ksp)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
}

// The daemon JDK is 25, so Kotlin defaults to a JVM target Robolectric 4.14.1's
// bundled ASM cannot instrument. Pin Kotlin bytecode to 11 (matching the
// convention plugin's Java sourceCompatibility) so the Robolectric-backed
// repository/sync/DAO tests can be instrumented. Mirrors :core:database.
kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
    }
}

android {
    namespace = "eu.grandmedical.diten.mobile.feature.employeeonboarding"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildFeatures {
        // Compose is enabled manually in library modules (no convention plugin).
        compose = true
    }

    // Room's MigrationTestHelper (and exportSchema) reads/writes the exported
    // schema JSON; expose this module's own schemas/ dir to both test source sets.
    sourceSets {
        getByName("test") {
            assets.srcDir("$projectDir/schemas")
        }
        getByName("androidTest") {
            assets.srcDir("$projectDir/schemas")
        }
    }

    testOptions {
        unitTests {
            // Robolectric + Room need Android resources on the unit-test classpath.
            isIncludeAndroidResources = true
        }
    }
}

// Export the feature database schema to this module's own schemas/ dir.
ksp {
    arg("room.schemaLocation", "$projectDir/schemas")
}

// Robolectric 4.14.1's bundled ASM cannot instrument classes/JDK images newer
// than Java 21, but the Gradle daemon runs on JDK 25. Fork the unit-test JVM on a
// provisioned JDK 21 (the :core:database precedent).
private val javaToolchainService = extensions.getByType(JavaToolchainService::class.java)
tasks.withType<Test>().configureEach {
    javaLauncher.set(
        javaToolchainService.launcherFor {
            languageVersion.set(JavaLanguageVersion.of(21))
        },
    )
}

dependencies {
    implementation(project(":core:common"))
    // Feature-plugin contract (FeatureNavGraph + re-exported FeatureEntry).
    implementation(project(":core:feature"))
    implementation(project(":core:design"))
    implementation(project(":core:database"))
    implementation(project(":core:network"))
    implementation(project(":core:sync"))
    implementation(project(":core:auth"))

    // Room (runtime/ktx + KSP compiler) for this feature's own database.
    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)

    // Retrofit + kotlinx.serialization for the feature Api and DTOs.
    implementation(libs.retrofit.core)
    implementation(libs.retrofit.kotlinx.serialization.converter)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.core)

    // Compose UI (BOM-managed) for the presentation layer.
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.foundation)
    implementation(libs.androidx.compose.material.icons.extended)
    implementation(libs.androidx.compose.ui.tooling.preview)

    // Compose navigation graph contribution + hiltViewModel.
    implementation(libs.androidx.navigation.compose)
    implementation(libs.androidx.hilt.navigation.compose)

    debugImplementation(libs.androidx.compose.ui.tooling)

    // JVM + Robolectric unit tests (no emulator).
    testImplementation(libs.junit)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.room.testing)
    // Initializes a test WorkManager so the offline-first `create` (which
    // schedules a one-time sync) runs under Robolectric without a real scheduler.
    testImplementation(libs.androidx.work.testing)
    testImplementation(libs.androidx.test.core.ktx)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.turbine)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.okhttp.core)
    testImplementation(libs.retrofit.core)
    testImplementation(libs.retrofit.kotlinx.serialization.converter)
    testImplementation(libs.kotlinx.serialization.json)

    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
}
