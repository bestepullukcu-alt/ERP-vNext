package eu.grandmedical.diten.mobile.feature.home

import eu.grandmedical.diten.mobile.core.common.permission.PermissionGate

/**
 * A single HR module surfaced in the Home menu. Pure data — no Android/Compose
 * types — so the permission-gating logic is trivially unit-testable.
 *
 * @property key stable module key, also used as the `module/{moduleKey}` nav arg.
 * @property title human-readable menu label.
 * @property resource the `<service>.<resource>` string checked against the JWT
 *   permission claims via [PermissionGate.hasResourceAccess].
 */
data class ModuleEntry(
    val key: String,
    val title: String,
    val resource: String,
)

/**
 * The static catalogue of HR (HCM) modules the shell can navigate to, plus the
 * permission-gating rule. No feature modules exist yet, so tapping an entry lands
 * on the "coming in M1" placeholder — but visibility is already driven by the
 * real permission set so gating is proven end-to-end.
 */
object HomeModules {

    /** Every module the shell knows about, before any permission gating. */
    val all: List<ModuleEntry> = listOf(
        ModuleEntry("hcm.applicant-intake", "Aday Başvuru Alımı", "hcm.applicant-intake"),
        ModuleEntry("hcm.candidate-pipeline", "Aday Havuzu", "hcm.candidate-pipeline"),
        ModuleEntry("hcm.employee-onboarding", "Çalışan Oryantasyonu", "hcm.employee-onboarding"),
        ModuleEntry("hcm.time-attendance", "Mesai ve Devam", "hcm.time-attendance"),
        ModuleEntry("hcm.leave-management", "İzin Yönetimi", "hcm.leave-management"),
        ModuleEntry("hcm.performance-review", "Performans Değerlendirme", "hcm.performance-review"),
        ModuleEntry("hcm.org-directory", "Organizasyon Rehberi", "hcm.org-directory"),
    )

    /**
     * The modules a user holding [permissions] may see: exactly those whose
     * [ModuleEntry.resource] passes [PermissionGate.hasResourceAccess] (i.e. the
     * user holds *some* permission on that resource).
     */
    fun visibleModules(permissions: Set<String>): List<ModuleEntry> =
        all.filter { PermissionGate.hasResourceAccess(permissions, it.resource) }

    /** Looks up an entry by its [ModuleEntry.key] (used by the detail route). */
    fun entryForKey(key: String): ModuleEntry? = all.firstOrNull { it.key == key }
}
