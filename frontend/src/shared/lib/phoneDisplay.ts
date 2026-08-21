/** Who a call was with, for a column that has a name, a number, or neither.
 *
 * ⚠ The "neither" case had been showing `local-device-call` — the placeholder the backend writes
 * where a phone number would go for a call placed from a browser. It is a value, not a number,
 * and reading developer text in a customer-facing column is worse than reading nothing.
 *
 * Kept as one function because all three call logs had the same `name ?? number` expression and
 * therefore the same hole in it. */
const LOCAL_DEVICE_CALL = "local-device-call";

export function callerLabel(name: string | null, phoneNumber: string, unknownLabel: string): string {
  if (name !== null && name.trim().length > 0) {
    return name;
  }

  return phoneNumber === LOCAL_DEVICE_CALL || phoneNumber.trim().length === 0 ? unknownLabel : phoneNumber;
}
