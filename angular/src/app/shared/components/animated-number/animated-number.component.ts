import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';

/**
 * A figure that counts up to its value when it first appears, and eases to a new value when that value changes.
 * Used for the points balance, where a number arriving with motion reads as live rather than as a static label.
 * With prefers-reduced-motion the final value is shown at once.
 */
@Component({
  selector: 'app-animated-number',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `{{ displayed() | number }}`,
  imports: [DecimalPipe],
})
export class AnimatedNumberComponent {
  readonly value = input.required<number>();
  readonly duration = input(1400);

  protected readonly displayed = signal(0);

  // Plain fields, not signals: the effect below must not depend on the value it writes.
  private current = 0;
  private frame: number | null = null;

  constructor() {
    effect(() => this.animateTo(this.value() ?? 0));
    inject(DestroyRef).onDestroy(() => this.cancel());
  }

  private animateTo(target: number): void {
    this.cancel();
    const from = this.current;
    if (from === target || this.prefersReducedMotion()) {
      this.setValue(target);
      return;
    }
    const start = performance.now();
    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / this.duration());
      // Ease-out cubic: fast to begin, settling gently, so the figure lands rather than stops.
      const eased = 1 - Math.pow(1 - progress, 3);
      this.setValue(Math.round(from + (target - from) * eased));
      this.frame = progress < 1 ? requestAnimationFrame(step) : null;
    };
    this.frame = requestAnimationFrame(step);
  }

  private setValue(value: number): void {
    this.current = value;
    this.displayed.set(value);
  }

  private cancel(): void {
    if (this.frame !== null) cancelAnimationFrame(this.frame);
    this.frame = null;
  }

  private prefersReducedMotion(): boolean {
    return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
}
