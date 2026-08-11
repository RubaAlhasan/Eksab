/**
 * Triggers a browser download for an in-memory `Blob` via a throwaway `ObjectURL` + anchor click —
 * the standard pattern for consuming a `responseType: 'blob'` Angular HTTP response (e.g. the
 * `IRemoteStreamContent` Excel-export endpoints across this app's token-gated download flow, see
 * CLAUDE.md's Excel-export pattern). Extracted here after the SAME inline copy showed up independently
 * in `business-coupons.component.ts` (this app's first-ever blob download) and
 * `business-transactions.component.ts` — past the "extract on second use" point.
 */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
