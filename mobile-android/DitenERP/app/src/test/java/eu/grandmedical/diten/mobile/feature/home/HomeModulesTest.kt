package eu.grandmedical.diten.mobile.feature.home

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure test of the permission-gated menu: [HomeModules.visibleModules] returns
 * exactly the modules whose resource passes `PermissionGate.hasResourceAccess`
 * for the given permission set — a granted resource shows, an ungranted one hides.
 */
class HomeModulesTest {

    @Test
    fun visible_modules_are_exactly_those_with_a_granted_resource() {
        val permissions = setOf(
            "hcm.applicant-intake.read",
            "hcm.applicant-intake.manage",
            "hcm.leave-management.evaluate",
        )

        val visibleKeys = HomeModules.visibleModules(permissions).map { it.key }.toSet()

        assertEquals(setOf("hcm.applicant-intake", "hcm.leave-management"), visibleKeys)
    }

    @Test
    fun granted_resource_shows_and_ungranted_resource_hides() {
        val permissions = setOf("hcm.candidate-pipeline.read")

        val visible = HomeModules.visibleModules(permissions)

        assertTrue(visible.any { it.key == "hcm.candidate-pipeline" })
        assertFalse(visible.any { it.key == "hcm.performance-review" })
        assertFalse(visible.any { it.key == "hcm.employee-onboarding" })
    }

    @Test
    fun matching_is_case_insensitive_and_needs_an_action_suffix() {
        // Case-insensitive prefix match on "<resource>.".
        assertTrue(
            HomeModules.visibleModules(setOf("HCM.Org-Directory.READ"))
                .any { it.key == "hcm.org-directory" },
        )
        // A bare resource with no "<resource>." action segment does NOT grant access.
        assertTrue(HomeModules.visibleModules(setOf("hcm.org-directory")).isEmpty())
    }

    @Test
    fun no_permissions_yields_no_modules() {
        assertTrue(HomeModules.visibleModules(emptySet()).isEmpty())
    }
}
