import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

// Read-only 5-star display, rounded to the nearest whole star (no half-stars) — the rating itself is
// already an average of whole 1-5 integers server-side, so a half-star would imply more precision than
// the underlying data has. Reused everywhere a business's rating shows: Discover cards, the Store
// header summary and each row in the Store's Reviews tab.
@Component({
  selector: 'app-star-rating',
  templateUrl: './star-rating.component.html',
  styleUrls: ['./star-rating.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StarRatingComponent {
  readonly rating = input(0);

  protected readonly stars = computed(() => {
    const filledCount = Math.round(Math.min(5, Math.max(0, this.rating())));
    return Array.from({ length: 5 }, (_, i) => i < filledCount);
  });
}
