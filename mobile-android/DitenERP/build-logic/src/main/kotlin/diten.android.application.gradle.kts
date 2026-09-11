// Convention plugin: shared Android application configuration.
// App-specific values (namespace, applicationId, targetSdk, versionCode/Name,
// build types, compose) stay in the module's own build script.
plugins {
    id("com.android.application")
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
