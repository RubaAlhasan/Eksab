import { ChangeDetectionStrategy, Component, ElementRef, NgZone, OnDestroy, inject, input, output, signal, viewChild } from '@angular/core';
import jsQR from 'jsqr';

export type QrScanErrorReason = 'permission-denied' | 'not-supported' | 'unknown';

/**
 * Live camera QR scanner — decodes a customer's wallet QR (shown on their own phone) directly from
 * the device camera, no server round-trip until a code is actually found. Pure client-side decode via
 * `jsqr` (small, dependency-free, works on a plain `<canvas>` frame grab) rather than the browser-native
 * `BarcodeDetector` API, which isn't available in every browser this business-portal tablet/laptop might
 * run (notably Firefox) — this is the first camera/QR-scanning code in the app, so the dependency-free,
 * widest-compatibility option was picked over relying on an unevenly-supported browser API.
 *
 * Self-contained: owns its own start/stop/scan-again UI so callers only need to listen for `scanned`
 * (the decoded QR payload — a single-use wallet token, see `PosAppService.AwardPointsByQrAsync`) and
 * `scanError` (camera permission/support failures). The scan loop runs outside Angular's zone (it ticks
 * every animation frame) and only re-enters the zone when there's an actual state change to render.
 */
@Component({
  selector: 'app-qr-scanner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './qr-scanner.component.html',
  styleUrl: './qr-scanner.component.scss',
})
export class QrScannerComponent implements OnDestroy {
  readonly idleLabel = input("Point the camera at the customer's wallet QR code");
  readonly startLabel = input('Start Scanning');
  readonly scanningLabel = input('Scanning…');
  readonly cancelLabel = input('Cancel');
  readonly capturedLabel = input('QR code captured');
  readonly scanAgainLabel = input('Scan Next Customer');
  /** Disables the start/scan-again button while the parent is processing a just-scanned token. */
  readonly busy = input(false);

  readonly scanned = output<string>();
  readonly scanError = output<QrScanErrorReason>();

  protected readonly phase = signal<'idle' | 'scanning' | 'captured'>('idle');

  private readonly videoRef = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly ngZone = inject(NgZone);

  private stream: MediaStream | null = null;
  private frameHandle: number | null = null;
  private readonly canvas = document.createElement('canvas');
  private readonly canvasCtx = this.canvas.getContext('2d', { willReadFrequently: true });

  ngOnDestroy(): void {
    this.stop();
  }

  protected async start(): Promise<void> {
    if (this.busy() || this.phase() === 'scanning') return;

    if (!navigator.mediaDevices?.getUserMedia) {
      this.scanError.emit('not-supported');
      return;
    }

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
    } catch (err) {
      this.scanError.emit(err instanceof DOMException && err.name === 'NotAllowedError' ? 'permission-denied' : 'unknown');
      return;
    }

    const video = this.videoRef()?.nativeElement;
    if (!video) {
      this.releaseStream();
      return;
    }

    video.srcObject = this.stream;
    try {
      await video.play();
    } catch {
      // Autoplay can reject on some browsers until a user gesture registers — Start Scanning is
      // itself a click, so this is a rare edge case; fail closed rather than scan a black frame.
      this.releaseStream();
      this.scanError.emit('unknown');
      return;
    }

    this.phase.set('scanning');
    this.ngZone.runOutsideAngular(() => this.tick());
  }

  protected stop(): void {
    if (this.frameHandle !== null) {
      cancelAnimationFrame(this.frameHandle);
      this.frameHandle = null;
    }
    this.releaseStream();
    this.phase.set('idle');
  }

  protected scanAgain(): void {
    this.phase.set('idle');
    void this.start();
  }

  private tick = (): void => {
    const video = this.videoRef()?.nativeElement;
    if (video && this.canvasCtx && video.readyState === video.HAVE_ENOUGH_DATA) {
      this.canvas.width = video.videoWidth;
      this.canvas.height = video.videoHeight;
      this.canvasCtx.drawImage(video, 0, 0, this.canvas.width, this.canvas.height);
      const frame = this.canvasCtx.getImageData(0, 0, this.canvas.width, this.canvas.height);
      const result = jsQR(frame.data, frame.width, frame.height, { inversionAttempts: 'dontInvert' });

      if (result?.data) {
        this.onDecoded(result.data);
        return;
      }
    }

    this.frameHandle = requestAnimationFrame(this.tick);
  };

  private onDecoded(data: string): void {
    if (this.frameHandle !== null) {
      cancelAnimationFrame(this.frameHandle);
      this.frameHandle = null;
    }
    this.releaseStream();

    // Re-enter Angular's zone for the state change + output emission — the scan loop itself runs
    // outside it so decoding 30-60 frames/sec doesn't trigger a change-detection pass per frame.
    this.ngZone.run(() => {
      this.phase.set('captured');
      this.scanned.emit(data);
    });
  }

  private releaseStream(): void {
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    const video = this.videoRef()?.nativeElement;
    if (video) video.srcObject = null;
  }
}
