// Convention plugin: shared Android library configuration.
// Module-specific values (namespace) stay in the module's own build script.
plugins {
    id("com.android.library")
}

android {
    compileSdk {
        version = release(37)
    }

    defaultConfig {
        minSdk = 26
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}
