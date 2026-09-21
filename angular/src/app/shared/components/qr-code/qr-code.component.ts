import { ChangeDetectionStrategy, Component, ElementRef, effect, input, viewChild } from '@angular/core';
import QRCode from 'qrcode';

/** Thin wrapper over the `qrcode` package (canvas renderer) — the only QR-*encoding* library in this
 *  app; `jsqr` (already installed) is a decoder only, used by `shared/components/qr-scanner` for the
 *  staff-side camera scan. Used by both the customer's wallet QR page and the redeem-reward screen. */
@Component({
  selector: 'app-qr-code',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas [width]="size()" [height]="size()"></canvas>`,
})
export class QrCodeComponent {
  readonly value = input.required<string>();
  readonly size = input(220);

  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    effect(() => {
      const value = this.value();
      const size = this.size();
      QRCode.toCanvas(this.canvasRef().nativeElement, value, { width: size, margin: 1 }).catch(() => undefined);
    });
  }
}
