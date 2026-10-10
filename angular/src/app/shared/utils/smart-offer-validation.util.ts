// Immediate, form-level feedback for the offer editor. The server is the authority on every rule here (see
// SmartOffer's own validation); this only tells the owner about a mistake while they are still typing, so a save
// is rarely refused for something that could have been caught on screen. Messages are localization keys, not text.

export interface StageDraft {
  startTime: string;
  endTime: string;
  price: number | null;
  quantityLimit: number | null;
}

export interface StageIssues {
  /** Index-aligned with the drafts. Null where a stage is fine. */
  perStage: (string | null)[];
  /** A problem that is about the set of stages as a whole, such as an overlap. */
  overall: string | null;
}

/** "HH:mm" to minutes since midnight. "24:00" is accepted as the end of a stage that runs to midnight. */
export function parseClock(value: string): number | null {
  const match = /^(\d{2}):(\d{2})$/.exec(value?.trim() ?? '');
  if (!match) {
    return null;
  }

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours === 24 && minutes === 0) {
    return 24 * 60;
  }

  if (hours > 23 || minutes > 59) {
    return null;
  }

  return hours * 60 + minutes;
}

export function validateStageDrafts(
  drafts: StageDraft[],
  basePrice: number | null,
  minimumPrice: number | null,
): StageIssues {
  const perStage: (string | null)[] = drafts.map(draft => stageIssue(draft, basePrice, minimumPrice));

  // Overlaps are a property of the set, so they are checked on the sorted, valid windows only. A stage that is
  // already broken is not compared again, so one mistake is reported once.
  const windows = drafts
    .map((draft, index) => ({ index, start: parseClock(draft.startTime), end: parseClock(draft.endTime) }))
    .filter(w => perStage[w.index] === null && w.start !== null && w.end !== null)
    .sort((a, b) => (a.start as number) - (b.start as number));

  let overall: string | null = null;
  for (let i = 1; i < windows.length; i++) {
    if ((windows[i].start as number) < (windows[i - 1].end as number)) {
      perStage[windows[i].index] = '::SmartDeals:Validation:Overlap';
      overall = '::SmartDeals:Validation:OverlapSummary';
    }
  }

  if (drafts.length === 0) {
    overall = '::SmartDeals:Validation:NeedStage';
  }

  return { perStage, overall };
}

function stageIssue(draft: StageDraft, basePrice: number | null, minimumPrice: number | null): string | null {
  const start = parseClock(draft.startTime);
  const end = parseClock(draft.endTime);

  if (start === null) {
    return '::SmartDeals:Validation:StartRequired';
  }

  if (end === null) {
    return '::SmartDeals:Validation:EndRequired';
  }

  if (end <= start) {
    return '::SmartDeals:Validation:EndBeforeStart';
  }

  if (draft.price === null || draft.price === undefined || Number.isNaN(draft.price)) {
    return '::SmartDeals:Validation:PriceRequired';
  }

  if (draft.price <= 0) {
    return '::SmartDeals:Validation:PricePositive';
  }

  if (basePrice !== null && draft.price > basePrice) {
    return '::SmartDeals:Validation:PriceAboveBase';
  }

  if (minimumPrice !== null && draft.price < minimumPrice) {
    return '::SmartDeals:Validation:PriceBelowMinimum';
  }

  return null;
}
