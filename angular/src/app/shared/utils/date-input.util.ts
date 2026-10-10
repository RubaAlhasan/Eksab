export function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

// `date.toISOString()` converts to UTC first — for any timezone AHEAD of UTC, local midnight (what
// `startOfMonth`/"today" actually mean to the user) rolls back to the previous day once converted,
// so an `<input type="date">` bound to that string silently shows the wrong default day. A real bug
// caught live (this session's own browser walkthrough, timezone UTC+something showed "07/31" instead
// of "08/01" as the month-start default) — use local date PARTS directly, never `.toISOString()`, to
// build a `yyyy-MM-dd` value from a local `Date`.
export function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
