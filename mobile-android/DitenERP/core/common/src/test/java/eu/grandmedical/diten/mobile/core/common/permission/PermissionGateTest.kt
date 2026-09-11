package eu.grandmedical.diten.mobile.core.common.permission

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pure-JVM checks (no Robolectric) of the wildcard-free permission semantics:
 * exact case-insensitive action matching plus resource-prefix visibility.
 */
class PermissionGateTest {

    private val granted = setOf(
        "hcm.applicant-intake.read",
        "hcm.applicant-intake.evaluate",
        "hcm.association-membership.manage",
    )

    @Test
    fun exact_grant_is_granted() {
        assertTrue(PermissionGate.isGranted(granted, "hcm.applicant-intake.read"))
    }

    @Test
    fun matching_is_case_insensitive() {
        assertTrue(PermissionGate.isGranted(granted, "HCM.Applicant-Intake.READ"))
    }

    @Test
    fun ungranted_permission_is_false() {
        assertFalse(PermissionGate.isGranted(granted, "hcm.applicant-intake.manage"))
    }

    @Test
    fun no_wildcard_semantics_are_invented() {
        // A literal star is never treated as "matches anything".
        assertFalse(PermissionGate.isGranted(granted, "hcm.applicant-intake.*"))
        assertFalse(PermissionGate.isGranted(granted, "*"))
    }

    @Test
    fun hasAny_true_when_one_matches() {
        assertTrue(
            PermissionGate.hasAny(
                granted,
                listOf("hcm.applicant-intake.manage", "hcm.applicant-intake.evaluate"),
            ),
        )
    }

    @Test
    fun hasAny_false_when_none_match() {
        assertFalse(
            PermissionGate.hasAny(granted, listOf("fin.invoice.read", "fin.invoice.manage")),
        )
    }

    @Test
    fun hasAll_true_only_when_every_one_matches() {
        assertTrue(
            PermissionGate.hasAll(
                granted,
                listOf("hcm.applicant-intake.read", "hcm.applicant-intake.evaluate"),
            ),
        )
        assertFalse(
            PermissionGate.hasAll(
                granted,
                listOf("hcm.applicant-intake.read", "hcm.applicant-intake.manage"),
            ),
        )
    }

    @Test
    fun hasResourceAccess_true_when_any_action_present() {
        assertTrue(PermissionGate.hasResourceAccess(granted, "hcm.applicant-intake"))
        assertTrue(PermissionGate.hasResourceAccess(granted, "hcm.association-membership"))
    }

    @Test
    fun hasResourceAccess_false_for_unheld_resource() {
        assertFalse(PermissionGate.hasResourceAccess(granted, "hcm.candidate-passport"))
        // A resource that is only a *prefix substring* (not a dotted boundary) must not match.
        assertFalse(PermissionGate.hasResourceAccess(granted, "hcm.applicant"))
    }
}
