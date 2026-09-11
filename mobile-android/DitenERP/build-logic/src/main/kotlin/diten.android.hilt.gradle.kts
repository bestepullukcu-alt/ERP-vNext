import org.gradle.api.artifacts.VersionCatalogsExtension
import org.gradle.kotlin.dsl.getByType

// Convention plugin: wires Hilt (Dagger) via KSP into any Android module.
plugins {
    id("com.google.devtools.ksp")
    id("com.google.dagger.hilt.android")
}

// Read coordinates from the shared version catalog of the consuming project.
val libs = extensions.getByType<VersionCatalogsExtension>().named("libs")

dependencies {
    "implementation"(libs.findLibrary("hilt-android").get())
    "ksp"(libs.findLibrary("hilt-compiler").get())
}
