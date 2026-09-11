package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local

import androidx.room.TypeConverter
import kotlinx.serialization.builtins.MapSerializer
import kotlinx.serialization.builtins.serializer
import kotlinx.serialization.json.Json

/**
 * Feature-local Room converters. Extends (does NOT modify) the shared
 * `DitenTypeConverters` with the one type this feature adds: the
 * `dependencyStates` map. Kept inside the feature module as allowed by the
 * template rules.
 *
 * The map is stored as a compact JSON string. An empty/blank column decodes to
 * an empty map (never throws), so a legacy/blank row is safe.
 */
class EmployeeOnboardingConverters {

    @TypeConverter
    fun dependencyStatesToString(value: Map<String, Int>?): String =
        JSON.encodeToString(MAP_SERIALIZER, value ?: emptyMap())

    @TypeConverter
    fun stringToDependencyStates(value: String?): Map<String, Int> {
        if (value.isNullOrBlank()) return emptyMap()
        return runCatching { JSON.decodeFromString(MAP_SERIALIZER, value) }.getOrDefault(emptyMap())
    }

    private companion object {
        val JSON = Json { ignoreUnknownKeys = true }
        val MAP_SERIALIZER = MapSerializer(String.serializer(), Int.serializer())
    }
}
