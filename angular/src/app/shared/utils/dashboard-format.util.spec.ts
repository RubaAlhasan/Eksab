import { describe, expect, it } from 'vitest';
import { addDays, amountIn, formatPercent, formatRatio, pickLocalized } from './dashboard-format.util';
import { Currency } from '../../proxy/shared/currency.enum';

describe('dashboard-format.util', () => {
  describe('amountIn', () => {
    it('reads one currency and never folds the other into it', () => {
      const amounts = [
        { currency: Currency.Syp, amount: 100_000 },
        { currency: Currency.Usd, amount: 40 },
      ];

      expect(amountIn(amounts, Currency.Syp)).toBe(100_000);
      expect(amountIn(amounts, Currency.Usd)).toBe(40);
    });

    it('returns zero for a currency with no entry or no data at all', () => {
      expect(amountIn([{ currency: Currency.Usd, amount: 5 }], Currency.Syp)).toBe(0);
      expect(amountIn(null, Currency.Usd)).toBe(0);
      expect(amountIn(undefined, Currency.Usd)).toBe(0);
    });
  });

  describe('addDays', () => {
    it('moves a calendar date across a month boundary', () => {
      expect(addDays('2026-10-05', -29)).toBe('2026-09-06');
    });

    it('moves across a year boundary and a leap day', () => {
      expect(addDays('2026-01-01', -1)).toBe('2025-12-31');
      expect(addDays('2028-02-28', 1)).toBe('2028-02-29');
    });

    it('returns the same day for a zero delta', () => {
      expect(addDays('2026-10-05', 0)).toBe('2026-10-05');
    });
  });

  describe('formatting', () => {
    it('shows a missing percentage as a dash rather than zero', () => {
      expect(formatPercent(null)).toBe('—');
      expect(formatRatio(undefined)).toBe('—');
    });

    it('formats a ratio as a percentage', () => {
      expect(formatRatio(0.5)).toContain('50');
    });
  });

  describe('pickLocalized', () => {
    it('prefers the Arabic name when the session is Arabic', () => {
      expect(pickLocalized('عرض', 'Deal', 'ar')).toBe('عرض');
    });

    it('prefers the English name otherwise', () => {
      expect(pickLocalized('عرض', 'Deal', 'en')).toBe('Deal');
    });

    it('falls back to the other language rather than showing a blank name', () => {
      expect(pickLocalized(null, 'Deal', 'ar')).toBe('Deal');
      expect(pickLocalized('عرض', null, 'en')).toBe('عرض');
      expect(pickLocalized(null, null, 'en')).toBe('');
    });
  });
});
