package eu.grandmedical.diten.mobile.feature.home

import eu.grandmedical.diten.mobile.core.common.navigation.FeatureEntry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure test of the feature-plugin menu builder: [HomeMenu.visibleEntries] keeps
 * exactly the injected [FeatureEntry] contributions whose `requiredPermission`
 * resource passes `PermissionGate.hasResourceAccess` for the given permission set
 * (a granted feature shows, an ungranted one hides), sorted stably by title.
 */
class HomeMenuTest {

    private fun entry(key: String, title: String, permission: String?) =
        FeatureEntry(key = key, route = key, title = title, requiredPermission = permission)

    private val features = setOf(
        entry("applicant-intake", "Aday Başvuru Alımı", "hcm.applicant-intake"),
        entry("leave-management", "İzin Yönetimi", "hcm.leave-management"),
        entry("performance-review", "Performans Değerlendirme", "hcm.performance-review"),
        entry("about", "Hakkında", null),
    )

    @Test
    fun granted_feature_shows_and_ungranted_feature_hides() {
        val permissions = setOf(
            "hcm.applicant-intake.read",
            "hcm.applicant-intake.manage",
            "hcm.leave-management.evaluate",
        )

        val visibleKeys = HomeMenu.visibleEntries(features, permissions).map { it.key }.toSet()

        // Granted resources + the always-visible (null-permission) entry.
        assertTrue(visibleKeys.contains("applicant-intake"))
        assertTrue(visibleKeys.contains("leave-management"))
        assertTrue(visibleKeys.contains("about"))
        assertFalse(visibleKeys.contains("performance-review"))
    }

    @Test
    fun a_null_permission_entry_is_always_visible() {
        val visible = HomeMenu.visibleEntries(features, emptySet())
        assertEquals(listOf("about"), visible.map { it.key })
    }

    @Test
    fun entries_are_sorted_by_title() {
        val permissions = setOf(
            "hcm.applicant-intake.read",
            "hcm.leave-management.read",
            "hcm.performance-review.read",
        )

        val titles = HomeMenu.visibleEntries(features, permissions).map { it.title }

        assertEquals(titles.sorted(), titles)
    }

    @Test
    fun matching_is_case_insensitive_and_needs_an_action_suffix() {
        val single = setOf(entry("org-directory", "Organizasyon Rehberi", "hcm.org-directory"))

        // Case-insensitive prefix match on "<resource>.".
        assertTrue(
            HomeMenu.visibleEntries(single, setOf("HCM.Org-Directory.READ"))
                .any { it.key == "org-directory" },
        )
        // A bare resource with no "<resource>." action segment does NOT grant access.
        assertTrue(HomeMenu.visibleEntries(single, setOf("hcm.org-directory")).isEmpty())
    }

    @Test
    fun no_permissions_and_no_null_entries_yields_empty_menu() {
        val gatedOnly = features.filter { it.requiredPermission != null }.toSet()
        assertTrue(HomeMenu.visibleEntries(gatedOnly, emptySet()).isEmpty())
    }
}
