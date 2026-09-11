package eu.grandmedical.diten.mobile.core.auth.jwt

import eu.grandmedical.diten.mobile.core.auth.buildJwt
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Pure-JVM tests for the claim decoder; no Android, no signature verification. */
class JwtDecoderTest {

    private val decoder = JwtDecoder()

    @Test
    fun `decodes csv legal entities tenant permissions and expiry (array form)`() {
        val payload = """
            {
              "sub": "user-1",
              "email": "gm@grandmedical.eu",
              "tenant_id": "tenant-guid-1",
              "legal_entities": "idA, idB ,idC",
              "permission": ["hcm.read", "hcm.write"],
              "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Admin", "Auditor"],
              "exp": 4102444800
            }
        """.trimIndent()

        val claims = decoder.decode(buildJwt(payload))

        assertEquals("user-1", claims.subject)
        assertEquals("gm@grandmedical.eu", claims.email)
        assertEquals("tenant-guid-1", claims.tenantId)
        // CSV split, trimmed, blanks dropped.
        assertEquals(listOf("idA", "idB", "idC"), claims.legalEntities)
        assertEquals(listOf("hcm.read", "hcm.write"), claims.permissions)
        assertEquals(listOf("Admin", "Auditor"), claims.roles)
        assertEquals(4102444800L, claims.expiresAt)
    }

    @Test
    fun `handles permission and role as bare strings`() {
        val payload = """
            {
              "tenant_id": "t2",
              "legal_entities": "only-one",
              "permission": "single.permission",
              "role": "Viewer"
            }
        """.trimIndent()

        val claims = decoder.decode(buildJwt(payload))

        assertEquals(listOf("only-one"), claims.legalEntities)
        assertEquals(listOf("single.permission"), claims.permissions)
        assertTrue(claims.roles.contains("Viewer"))
        assertNull(claims.expiresAt)
    }

    @Test
    fun `blank or malformed token yields empty claims without throwing`() {
        assertEquals(emptyList<String>(), decoder.decode(null).legalEntities)
        assertEquals(emptyList<String>(), decoder.decode("").legalEntities)
        assertEquals(emptyList<String>(), decoder.decode("not-a-jwt").legalEntities)
        assertNull(decoder.decode("a.b").tenantId)
    }

    @Test
    fun `empty legal_entities claim yields empty list`() {
        val claims = decoder.decode(buildJwt("""{"legal_entities": "", "tenant_id": "t"}"""))
        assertEquals(emptyList<String>(), claims.legalEntities)
        assertEquals("t", claims.tenantId)
    }
}
