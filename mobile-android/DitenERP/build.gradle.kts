// Top-level build file where you can add configuration options common to all sub-projects/modules.
import org.gradle.api.plugins.JvmToolchainsPlugin
import org.gradle.jvm.toolchain.JavaLanguageVersion
import org.gradle.jvm.toolchain.JavaToolchainService
import org.jlleitschuh.gradle.ktlint.KtlintExtension

plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.compose) apply false
    alias(libs.plugins.ksp) apply false
    alias(libs.plugins.hilt) apply false
    alias(libs.plugins.ktlint) apply false
}

val detektVersion = libs.versions.detekt.get()

// Centralized static analysis for every Kotlin module.
//
// ktlint runs in-process on the JDK 25 daemon without trouble.
//
// Detekt 1.23.8 (latest) is compiled against Kotlin 2.0.21, whose bundled
// IntelliJ JavaVersion parser cannot read the JDK 25 daemon runtime version
// string ("IllegalArgumentException: 25.0.3"), and detekt hard-guards its
// embedded compiler so it cannot be swapped. The detekt Gradle task also runs
// in-process (no fork/launcher option). We therefore run detekt-cli forked on a
// provisioned JDK 21 via JavaExec, which keeps `./gradlew detekt` green on the
// pinned JDK 25 daemon while still enforcing the full default rule set.
subprojects {
    apply(plugin = "org.jlleitschuh.gradle.ktlint")
    // Registers the `javaToolchains` service used to fork detekt on JDK 21.
    pluginManager.apply(JvmToolchainsPlugin::class.java)
    val toolchains = extensions.getByType(JavaToolchainService::class.java)

    configure<KtlintExtension> {
        version.set("1.5.0")
        android.set(true)
    }

    val detektCli = configurations.create("detektCli")
    dependencies {
        detektCli("io.gitlab.arturbosch.detekt:detekt-cli:$detektVersion")
    }

    val sourceRoots = listOf(
        "src/main/java",
        "src/main/kotlin",
        "src/test/java",
        "src/test/kotlin",
        "src/androidTest/java",
        "src/androidTest/kotlin",
    ).map { layout.projectDirectory.dir(it).asFile }
        .filter { it.exists() }

    val detektTask = tasks.register<JavaExec>("detekt") {
        group = "verification"
        description = "Runs detekt static analysis (forked on JDK 21)."
        onlyIf { sourceRoots.isNotEmpty() }
        javaLauncher.set(
            toolchains.launcherFor {
                languageVersion.set(JavaLanguageVersion.of(21))
            },
        )
        classpath = detektCli
        mainClass.set("io.gitlab.arturbosch.detekt.cli.Main")
        // Capture only serializable primitives/Files (no Project) for the config cache.
        val reportDir = layout.buildDirectory.dir("reports/detekt").get().asFile
        val inputArg = sourceRoots.joinToString(",") { it.absolutePath }
        val configArg = rootProject.file("config/detekt/detekt.yml").absolutePath
        val reportArg = "xml:${reportDir.resolve("detekt.xml").absolutePath}"
        doFirst { reportDir.mkdirs() }
        argumentProviders.add(
            CommandLineArgumentProvider {
                listOf(
                    "--input", inputArg,
                    "--config", configArg,
                    "--build-upon-default-config",
                    "--jvm-target", "11",
                    "--report", reportArg,
                )
            },
        )
    }

    tasks.matching { it.name == "check" }.configureEach {
        dependsOn(detektTask)
    }
}
