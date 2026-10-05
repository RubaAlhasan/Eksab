import type { CurrencyAmountDto } from '../../proxy/billing/models';
import { Currency } from '../../proxy/shared/currency.enum';

// Formatting and lookup helpers shared by the Business and Admin dashboards. Money is always read per currency through
// amountIn(), never summed across SYP and USD. Dates are ISO calendar strings ("2026-10-05"), parsed as local dates so a
// day never shifts by one in a negative-offset browser.

export function amountIn(amounts: CurrencyAmountDto[] | null | undefined, currency: Currency): number {
  return amounts?.find((a) => a.currency === currency)?.amount ?? 0;
}

export function formatAmount(value: number, currency: Currency): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: currency === Currency.Syp ? 0 : 2 }).format(value);
}

export function formatCount(value: number): string {
  return new Intl.NumberFormat(undefined).format(value);
}

export function formatPercent(value: number | null | undefined): string {
  return value == null ? '—' : `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value)}%`;
}

// A plain ratio (0 to 1) shown as a percentage, for fields that follow the RedemptionRate convention.
export function formatRatio(value: number | null | undefined): string {
  return value == null ? '—' : formatPercent(value * 100);
}

// Shifts an ISO calendar date by whole days, in calendar arithmetic (no time-of-day or DST involved).
export function addDays(isoDate: string, delta: number): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day + delta)).toISOString().slice(0, 10);
}

export function formatIsoDate(isoDate: string, options: Intl.DateTimeFormatOptions): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  return new Date(year, month - 1, day).toLocaleDateString(undefined, options);
}

// Arabic when the session is in Arabic, otherwise English. Falls back to the other language when a name hasn't been
// set, so a deal or reward never shows up blank.
export function pickLocalized(ar: string | null | undefined, en: string | null | undefined, language: string): string {
  const preferred = language.startsWith('ar') ? ar : en;
  const fallback = language.startsWith('ar') ? en : ar;
  return preferred || fallback || '';
}

export function pad2(value: number): string {
  return value.toString().padStart(2, '0');
}
