import org.gradle.jvm.toolchain.JavaLanguageVersion
import org.gradle.jvm.toolchain.JavaToolchainService
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("diten.android.library")
    id("diten.android.hilt")
    alias(libs.plugins.kotlin.serialization)
}

// The daemon JDK is 25, so Kotlin defaults to a JVM target Robolectric 4.14.1's
// bundled ASM cannot instrument. Pin Kotlin bytecode to 11 (matching the
// convention plugin's Java sourceCompatibility) so the token-store Robolectric
// tests run. Mirrors the :core:database precedent.
kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_11)
    }
}

android {
    namespace = "eu.grandmedical.diten.mobile.core.auth"

    defaultConfig {
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    testOptions {
        unitTests {
            // Robolectric needs Android resources on the unit-test classpath.
            isIncludeAndroidResources = true
        }
    }
}

// Robolectric 4.14.1's bundled ASM cannot instrument classes/JDK images newer
// than Java 21, but the Gradle daemon runs on JDK 25. Fork the unit-test JVM on
// a provisioned JDK 21 (same approach :core:database and root detekt use).
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
    implementation(project(":core:network"))

    implementation(libs.androidx.datastore.preferences)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.core)

    // AuthApi's suspend fns return retrofit2.Response<...>; :core:network exposes
    // Retrofit only as `implementation`, so the type is not transitively visible.
    // The repository consumes those return types, hence Retrofit on the classpath.
    implementation(libs.retrofit.core)

    // Hilt runtime + KSP compiler are contributed by the diten.android.hilt plugin.

    testImplementation(libs.junit)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.test.core.ktx)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.okhttp.mockwebserver)

    // MockWebServer-backed repository/refresh tests build a real AuthApi against
    // the mock server, so the transport stack is exercised end-to-end. These are
    // catalog libraries already used by :core:network's own tests.
    testImplementation(libs.okhttp.core)
    testImplementation(libs.retrofit.core)
    testImplementation(libs.retrofit.kotlinx.serialization.converter)
    testImplementation(libs.kotlinx.serialization.json)
}
