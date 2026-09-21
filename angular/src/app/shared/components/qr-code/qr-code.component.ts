import { ChangeDetectionStrategy, Component, ElementRef, effect, input, viewChild } from '@angular/core';
import QRCode from 'qrcode';

/**
 * Thin wrapper over the `qrcode` package (canvas renderer) — the only QR-*encoding* library in this
 * app; `jsqr` (already installed) is a decoder only, used by `shared/components/qr-scanner` for the
 * staff-side camera scan. Used by both the customer's wallet QR page and the redeem-reward screen.
 *
 * The `<canvas>` deliberately has NO `[width]`/`[height]` template bindings, even though that looks
 * like the obvious way to size it. `QRCode.toCanvas()`'s own renderer (qrcode/lib/renderer/canvas.js)
 * sets `canvas.width`/`canvas.height` itself before painting — and assigning either of those IDL
 * properties always clears the canvas's drawing buffer, even to its current value (a standard, if
 * surprising, browser behavior). With template bindings also present, Angular's own binding
 * reconciliation re-applying `[width]`/`[height]` on the same element could re-trigger that clear AFTER
 * the library's own paint — confirmed live (this session): the canvas ended up the right size, with
 * `toCanvas()`'s promise resolving successfully, yet every pixel fully transparent. Leaving the library
 * as sole owner of the canvas's width/height removes the race entirely; CSS below sizes it visually.
 */
@Component({
  selector: 'app-qr-code',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas [style.width.px]="size()" [style.height.px]="size()"></canvas>`,
})
export class QrCodeComponent {
  readonly value = input.required<string>();
  readonly size = input(220);

  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    effect(() => {
      const value = this.value();
      const size = this.size();
      QRCode.toCanvas(this.canvasRef().nativeElement, value, { width: size, margin: 1 }).catch(err =>
        // eslint-disable-next-line no-console
        console.error('QrCodeComponent: failed to render QR code', err),
      );
    });
  }
}
