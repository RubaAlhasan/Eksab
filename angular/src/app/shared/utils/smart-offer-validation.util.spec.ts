import { describe, expect, it } from 'vitest';
import { formatCountdown, formatSmartPrice } from './smart-offer-display.util';
import { parseClock, validateStageDrafts, StageDraft } from './smart-offer-validation.util';
import { Currency } from '../../proxy/shared/currency.enum';

const stage = (startTime: string, endTime: string, price: number | null = 5, quantityLimit: number | null = null): StageDraft => ({
  startTime,
  endTime,
  price,
  quantityLimit,
});

describe('parseClock', () => {
  it('reads HH:mm and treats 24:00 as the end of the day', () => {
    expect(parseClock('09:30')).toBe(570);
    expect(parseClock('24:00')).toBe(1440);
  });

  it('rejects anything that is not a real clock time', () => {
    expect(parseClock('24:30')).toBeNull();
    expect(parseClock('12:60')).toBeNull();
    expect(parseClock('9:30')).toBeNull();
    expect(parseClock('')).toBeNull();
  });
});

describe('validateStageDrafts', () => {
  it('accepts an ordered, non-overlapping ladder inside the base price', () => {
    const issues = validateStageDrafts(
      [stage('09:00', '11:00', 10), stage('11:00', '13:00', 7), stage('13:00', '15:00', 5)],
      10,
      null,
    );

    expect(issues.overall).toBeNull();
    expect(issues.perStage).toEqual([null, null, null]);
  });

  it('flags a stage that ends before it starts without also flagging the set', () => {
    const issues = validateStageDrafts([stage('11:00', '09:00', 7)], 10, null);

    expect(issues.perStage[0]).toBe('::SmartDeals:Validation:EndBeforeStart');
    expect(issues.overall).toBeNull();
  });

  it('reports the later of two overlapping stages and summarises the set', () => {
    const issues = validateStageDrafts([stage('09:00', '11:00', 10), stage('10:30', '12:00', 7)], 10, null);

    expect(issues.perStage[0]).toBeNull();
    expect(issues.perStage[1]).toBe('::SmartDeals:Validation:Overlap');
    expect(issues.overall).toBe('::SmartDeals:Validation:OverlapSummary');
  });

  it('enforces the price ceiling and floor', () => {
    expect(validateStageDrafts([stage('09:00', '11:00', 12)], 10, null).perStage[0]).toBe('::SmartDeals:Validation:PriceAboveBase');
    expect(validateStageDrafts([stage('09:00', '11:00', 2)], 10, 3).perStage[0]).toBe('::SmartDeals:Validation:PriceBelowMinimum');
    expect(validateStageDrafts([stage('09:00', '11:00', 0)], 10, null).perStage[0]).toBe('::SmartDeals:Validation:PricePositive');
  });

  it('asks for at least one stage', () => {
    expect(validateStageDrafts([], 10, null).overall).toBe('::SmartDeals:Validation:NeedStage');
  });
});

describe('formatSmartPrice', () => {
  it('keeps cents for USD only when they exist and never converts', () => {
    expect(formatSmartPrice(5, Currency.Usd, 'en')).toBe('$5');
    expect(formatSmartPrice(4.5, Currency.Usd, 'en')).toBe('$4.50');
  });

  it('shows Syrian pounds as whole amounts', () => {
    expect(formatSmartPrice(50000, Currency.Syp, 'en')).toContain('50,000');
  });
});

describe('formatCountdown', () => {
  it('uses minutes and seconds under an hour, and hours from an hour up', () => {
    expect(formatCountdown(42 * 60_000 + 10_000)).toBe('42:10');
    expect(formatCountdown(3_600_000 + 2 * 60_000 + 5_000)).toBe('1:02:05');
  });

  it('never goes negative', () => {
    expect(formatCountdown(-5_000)).toBe('00:00');
  });
});
