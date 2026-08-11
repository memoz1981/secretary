import type { Language } from "@/shared/i18n/translations";

/** Month names written out, per language.
 *
 *  Not `toLocaleDateString("az-AZ", …)`: Azerbaijani month names are not reliably present in
 *  every browser's ICU data, and the failure is silent — you get English, or the locale falls
 *  back and nobody notices until a customer does. Twelve words are cheaper than that risk. */
const MONTHS: Record<Language, readonly string[]> = {
  az: [
    "Yanvar", "Fevral", "Mart", "Aprel", "May", "İyun",
    "İyul", "Avqust", "Sentyabr", "Oktyabr", "Noyabr", "Dekabr",
  ],
  ru: [
    "января", "февраля", "марта", "апреля", "мая", "июня",
    "июля", "августа", "сентября", "октября", "ноября", "декабря",
  ],
  en: [
    "January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December",
  ],
};

/** "12 Avqust". The year is left off deliberately — everything on these pages happened in the
 *  last few weeks, and a year on every row is noise you read past. */
export function formatDayMonth(value: string | Date, language: Language): string {
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) {
    return "—";
  }

  return `${date.getDate()} ${MONTHS[language][date.getMonth()]}`;
}

/** "12 Avqust, 14:35" — the same date with the time, for a log where the hour is the point. */
export function formatDayMonthTime(value: string | Date, language: Language): string {
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) {
    return "—";
  }

  const time = date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", hour12: false });
  return `${formatDayMonth(date, language)}, ${time}`;
}
