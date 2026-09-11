import org.gradle.jvm.toolchain.JavaLanguageVersion
import org.gradle.jvm.toolchain.JavaToolchainService
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("diten.android.library")
    id("diten.android.hilt")
    alias(libs.plugins.ksp)
}

// The daemon JDK is 25, so Kotlin defaults to the JVM_24 target (class file
// major version 68). Robolectric 4.14.1's bundled ASM cannot instrument v68
// classes, which breaks the DAO unit tests. Pin Kotlin bytecode to 11 to match
// the convention plugin's Java sourceCompatibility and keep instrumentation happy.
kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
    }
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.database"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    // Room's MigrationTestHelper loads the exported schema JSON from the
    // instrumentation context's *assets* (under "<db-class-fqn>/<version>.json").
    // Registering the exported schema dir as assets on both test source sets lets
    // the harness find it under Robolectric unit tests (this module) and under a
    // future on-device instrumented run, from the one version-controlled location.
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
            // Room's MigrationTestHelper and Robolectric need Android resources on the
            // unit-test classpath so the exported schema JSON is resolvable.
            isIncludeAndroidResources = true
        }
    }
}

// Export the Room schema so every version is captured on disk and migration tests
// can open historical schemas. Room 2.7.x reads this KSP argument.
ksp {
    arg("room.schemaLocation", "$projectDir/schemas")
}

// Robolectric 4.14.1's bundled ASM cannot instrument classes/JDK images newer
// than Java 21, but the Gradle daemon runs on JDK 25. Fork the unit-test JVM on
// a provisioned JDK 21 (same approach the root build uses for detekt) so the
// Robolectric DAO tests run under a supported runtime.
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

    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.room.ktx)
    ksp(libs.androidx.room.compiler)
    implementation(libs.kotlinx.coroutines.core)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.

    testImplementation(libs.junit)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.room.testing)
    testImplementation(libs.androidx.test.core.ktx)
    testImplementation(libs.kotlinx.coroutines.test)
}
