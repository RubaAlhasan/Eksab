import { Injectable, inject, signal } from '@angular/core';
import { CustomerSmartOffersService } from '../../proxy/controllers/customer-smart-offers.service';

function watchKey(tenantId: string, offerId: string): string {
  return `${tenantId}:${offerId}`;
}

/**
 * Which deals the customer is watching for a price drop. Loaded once and shared, so each deal card can show its own state
 * without a request of its own. A change shows at once and is rolled back if the server refuses it.
 */
@Injectable({ providedIn: 'root' })
export class SmartOfferWatchService {
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);

  private readonly watched = signal<ReadonlySet<string>>(new Set());
  private readonly loadedSignal = signal(false);
  private loading = false;

  // Exposed so a page listing the watched deals (not just a single card) can tell "no watches yet" apart from
  // "haven't heard back yet" and avoid flashing an empty state before the first response arrives.
  readonly loaded = this.loadedSignal.asReadonly();

  ensureLoaded(): void {
    if (this.loadedSignal() || this.loading) return;
    this.loading = true;
    this.customerSmartOffersService.getMyPriceWatches().subscribe({
      next: watches => {
        this.watched.set(new Set(watches.map(w => watchKey(w.tenantId, w.smartOfferId))));
        this.loadedSignal.set(true);
        this.loading = false;
      },
      // Visitors without a customer session, or a failed request: nothing to show, and the next card will try again.
      error: () => {
        this.loading = false;
      },
    });
  }

  isWatched(tenantId: string, offerId: string): boolean {
    return this.watched().has(watchKey(tenantId, offerId));
  }

  toggle(tenantId: string, offerId: string): void {
    const key = watchKey(tenantId, offerId);
    const wasWatched = this.watched().has(key);

    this.setWatched(key, !wasWatched);

    const request = wasWatched
      ? this.customerSmartOffersService.unwatchPrice(tenantId, offerId)
      : this.customerSmartOffersService.watchPrice(tenantId, offerId);

    request.subscribe({
      // The interceptor has already shown the server's reason; put the state back the way it was.
      error: () => this.setWatched(key, wasWatched),
    });
  }

  private setWatched(key: string, watched: boolean): void {
    this.watched.update(set => {
      const next = new Set(set);
      if (watched) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  }
}
