/**
 * The reader's own calendar day as `yyyy-MM-dd`, optionally a number of days back.
 *
 * Built from local date parts and never from `toISOString()`, which returns the UTC day: someone
 * logging progress at 23:30 would have it dated tomorrow, and someone in a timezone behind UTC
 * would see today's dashboard filed under yesterday. The whole reason the client sends the date
 * at all is that only the client knows which day the reader is living in.
 */
export function localDate(daysAgo = 0): string {
  const date = new Date();
  date.setDate(date.getDate() - daysAgo);

  const pad = (value: number) => String(value).padStart(2, '0');

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
