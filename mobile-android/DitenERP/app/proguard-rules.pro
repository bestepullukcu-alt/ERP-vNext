# Diten ERP mobile - R8 / ProGuard keep rules for the release build.
#
# AGP and the Compose/Hilt libraries already ship consumer rules that cover most
# cases; the entries below are explicit, documented safety keeps for this skeleton.

# --- Kotlin metadata (reflection-friendly, keeps sealed/enum/data behaviour) ---
-keepattributes *Annotation*, InnerClasses, Signature, RuntimeVisible*Annotations

# --- Hilt / Dagger generated graph ---
# Hilt ships its own -keep rules, but generated components and injected members
# are kept defensively so the runtime graph resolves after minification.
-keep,allowobfuscation @interface dagger.hilt.android.HiltAndroidApp
-keep class * extends dagger.hilt.android.internal.managers.ApplicationComponentManager { *; }
-keep,allowobfuscation class * extends androidx.lifecycle.ViewModel

# --- Jetpack Compose ---
# Compose ships consumer rules; keep runtime annotations used by tooling.
-keepclassmembers class androidx.compose.runtime.** { *; }
