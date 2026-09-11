import org.gradle.jvm.toolchain.JavaLanguageVersion
import org.gradle.jvm.toolchain.JavaToolchainService
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("diten.android.library")
    id("diten.android.hilt")
    alias(libs.plugins.ksp)
}

// Same constraint as :core:database — the daemon JDK is 25 so Kotlin defaults to
// a bytecode target Robolectric 4.14.1's bundled ASM cannot instrument. Pin
// Kotlin bytecode to 11 (matching the convention plugin's Java level) so the
// Robolectric-backed handler/worker tests can be instrumented.
kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
    }
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.sync"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    testOptions {
        unitTests {
            // Robolectric + WorkManager test harness need Android resources on the
            // unit-test classpath.
            isIncludeAndroidResources = true
        }
    }
}

// Robolectric 4.14.1's bundled ASM cannot instrument classes/JDK images newer
// than Java 21, but the Gradle daemon runs on JDK 25. Fork the unit-test JVM on a
// provisioned JDK 21 (the :core:database precedent) so the Robolectric-backed
// tests run under a supported runtime.
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
    implementation(project(":core:database"))
    implementation(project(":core:network"))

    implementation(libs.androidx.work.runtime.ktx)
    implementation(libs.androidx.hilt.work)
    ksp(libs.androidx.hilt.compiler)
    implementation(libs.kotlinx.coroutines.core)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.

    testImplementation(libs.junit)
    testImplementation(libs.androidx.work.testing)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.test.core.ktx)
    testImplementation(libs.kotlinx.coroutines.test)
    // The reference handler test builds a real in-memory DitenDatabase to prove the
    // pending -> synced loop. :core:database exposes Room as `implementation` (not
    // `api`), so Room's builder APIs must be on this module's *test* classpath.
    // Uses the already-provisioned catalog alias; the catalog itself is untouched.
    testImplementation(libs.androidx.room.runtime)
}
