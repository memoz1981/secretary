/** Azerbaijani mobile numbers, in the shape this product writes them: `+994(50)250-58-32`.
 *
 * The operator code has no leading zero inside the brackets — `+994` replaces it. People write
 * `055` locally and `+994 55` internationally, and both are the same number; typing either here
 * produces the same result.
 */

const OPERATOR_CODES = ["50", "51", "55", "70", "77", "10", "99", "60"];

/** Every digit, with the country code and any trunk zero taken off — nine digits for a valid
 *  number: two of operator code and seven of subscriber. */
function subscriberDigits(input: string): string {
  let digits = input.replace(/\D/g, "");

  if (digits.startsWith("994")) {
    digits = digits.slice(3);
  }

  // A local trunk zero, which the country code stands in for. Typing 055 and typing +994 55 has
  // to reach the same number, or half the people entering one get told it is invalid.
  if (digits.startsWith("0")) {
    digits = digits.slice(1);
  }

  return digits;
}

/** Formats as far as what has been typed allows, so the field fills in as somebody types rather
 *  than rewriting itself when they finish. */
export function formatAzPhone(input: string): string {
  const digits = subscriberDigits(input).slice(0, 9);
  if (digits.length === 0) {
    return "";
  }

  const code = digits.slice(0, 2);
  const rest = digits.slice(2);

  if (rest.length === 0) {
    return `+994(${code}`;
  }

  const parts = [rest.slice(0, 3), rest.slice(3, 5), rest.slice(5, 7)].filter((p) => p.length > 0);
  return `+994(${code})${parts.join("-")}`;
}

export function isValidAzPhone(input: string): boolean {
  const digits = subscriberDigits(input);
  return digits.length === 9 && OPERATOR_CODES.includes(digits.slice(0, 2));
}

/** What goes to the API. The server normalises again on its side — this only spares the user a
 *  rejection for a formatting difference nobody can see. */
export function toApiPhone(input: string): string {
  return `+994${subscriberDigits(input)}`;
}

/** What a survey list shows instead of somebody's number.
 *
 * ⚠ Nothing of the real number survives — not the operator code, not the last two digits. A
 * partial mask is a smaller version of the same problem, because two of nine digits plus a name
 * is often enough to find somebody.
 *
 * The full number is still stored and still what the scheduler will dial. Masking is a display
 * decision, not a storage one, so turning it off later is a change to this function alone. */
export function maskAzPhone(_stored: string): string {
  return "+994(00)000-00-00";
}

/** The stored number, formatted the way this product writes them.
 *
 * ⚠ Used on the follow-up list alone, and behind a click. That page exists so a person can ring
 * somebody the agent could not reach, and a list of masked numbers cannot be acted on — the mask
 * would make the page decorative. Everywhere else the number stays hidden, because everywhere
 * else it is only being read. */
export function revealAzPhone(stored: string): string {
  return formatAzPhone(stored) || stored;
}
