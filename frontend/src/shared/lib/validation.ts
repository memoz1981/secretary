export function isRequired(value: string): boolean {
  return value.trim().length > 0;
}

export function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

export function isPositiveNumber(value: string): boolean {
  const n = Number(value);
  return value.trim() !== "" && Number.isFinite(n) && n > 0;
}

export function isNonNegativeNumber(value: string): boolean {
  const n = Number(value);
  return value.trim() !== "" && Number.isFinite(n) && n >= 0;
}

export function isMinLength(value: string, min: number): boolean {
  return value.length >= min;
}
