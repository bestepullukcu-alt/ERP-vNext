package eu.grandmedical.diten.mobile.feature.compensationbenefits.data

import java.time.LocalDate
import java.time.format.DateTimeFormatter
import kotlin.random.Random

/**
 * Client-side defaults for a new compensation-and-benefits record.
 *
 * The auto-generated [defaultCode] must be MARKER-SAFE: the backend rejects
 * certain forbidden marker substrings in text fields, so the code is built only
 * from a fixed safe prefix, the date, and Crockford base32 characters (digits +
 * uppercase letters, excluding I/L/O/U to avoid ambiguity). It stays short and
 * user-editable.
 */
object CodeDefaults {

    private const val PREFIX = "CB-"

    /** Crockford base32 alphabet — unambiguous, alphanumeric, marker-safe. */
    private const val ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"

    private const val SUFFIX_LENGTH = 4

    private val dateFormat = DateTimeFormatter.ofPattern("yyMMdd")

    /**
     * Produces a short, unique-ish, marker-safe, editable code such as
     * `CB-260911-K7QM`. Two calls differ (random suffix).
     */
    fun defaultCode(
        today: LocalDate = LocalDate.now(),
        random: Random = Random.Default,
    ): String {
        val suffix = buildString {
            repeat(SUFFIX_LENGTH) {
                append(ALPHABET[random.nextInt(ALPHABET.length)])
            }
        }
        return "$PREFIX${today.format(dateFormat)}-$suffix"
    }
}
