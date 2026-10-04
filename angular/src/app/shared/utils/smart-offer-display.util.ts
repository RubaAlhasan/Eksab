import { Currency } from '../../proxy/shared/currency.enum';

// Presentation only. Every amount, window and countdown the UI shows is computed by the server (see SmartOffer and
// CustomerSmartOfferDto). These helpers only format what the server already decided.

/** Formats a money amount in its own currency. USD keeps cents when it has them; SYP is whole pounds. No conversion. */
export function formatSmartPrice(amount: number, currency: Currency | undefined, language: string): string {
  const isUsd = currency === Currency.Usd;
  return new Intl.NumberFormat(language === 'ar' ? 'ar' : 'en', {
    style: 'currency',
    currency: isUsd ? 'USD' : 'SYP',
    // Whole amounts read as "$5", a fraction as "$4.50". SYP never has a fraction on a price.
    minimumFractionDigits: isUsd && !Number.isInteger(amount) ? 2 : 0,
    maximumFractionDigits: isUsd ? 2 : 0,
  }).format(amount);
}

/** "42:10" under an hour, "1:02:05" from an hour up. Negative durations read as zero. */
export function formatCountdown(ms: number): string {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (value: number) => value.toString().padStart(2, '0');
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
}

/** Browser's own zone, used only as the default for a new offer. The owner can change it. */
export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

/**
 * Minute of day an instant falls on in a named zone, for drawing the "now" marker on a timeline. Null when the browser
 * cannot resolve the zone, in which case the marker is simply left off.
 */
export function minuteOfDayIn(timeZoneId: string, instant: Date): number | null {
  try {
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: timeZoneId,
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).formatToParts(instant);
    const hour = Number(parts.find(part => part.type === 'hour')?.value);
    const minute = Number(parts.find(part => part.type === 'minute')?.value);
    return Number.isFinite(hour) && Number.isFinite(minute) ? hour * 60 + minute : null;
  } catch {
    return null;
  }
}

/** Minutes since midnight as "HH:mm", the same form the server uses for stage times. 1440 reads as "24:00". */
export function formatClock(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${pad(hours)}:${pad(mins)}`;
}
