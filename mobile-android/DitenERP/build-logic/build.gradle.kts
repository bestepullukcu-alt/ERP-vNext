plugins {
    `kotlin-dsl`
}

// Convention plugins that centralize the Android/Hilt configuration shared by
// the app and library modules. Plugin markers (not just the AGP/KSP/Hilt runtime
// jars) are needed so the precompiled script plugins can reference plugin ids in
// their own `plugins { }` blocks. Versions come from the shared version catalog.
dependencies {
    implementation("com.android.application:com.android.application.gradle.plugin:${libs.versions.agp.get()}")
    implementation("com.android.library:com.android.library.gradle.plugin:${libs.versions.agp.get()}")
    implementation(
        "com.google.devtools.ksp:com.google.devtools.ksp.gradle.plugin:${libs.versions.ksp.get()}",
    )
    implementation(
        "com.google.dagger.hilt.android:com.google.dagger.hilt.android.gradle.plugin:" +
            libs.versions.hilt.get(),
    )
}
