import { Injectable, inject } from '@angular/core';
import {
  CUSTOM_HTTP_ERROR_HANDLER_PRIORITY,
  CreateErrorComponentService,
  type CustomHttpErrorHandlerService,
} from '@abp/ng.theme.shared';

/**
 * Routes "the API never answered" (HTTP status 0) to ApiUnavailableComponent.
 *
 * Why this exists rather than `withHttpErrorConfig({ errorScreen: { forWhichErrors: [0] } })`
 * alone: that config IS honoured, but nothing ever reaches it with status 0. ABP's own
 * UnknownStatusCodeErrorHandlerService calls
 *
 *     createErrorComponentService.execute({ title, details, isHomeShow: false })
 *
 * without a `status` field, and CreateErrorComponentService then asks
 * `canCreateCustomError(instance.status)` -- i.e. `[0].indexOf(undefined) > -1`, which is
 * false. So the custom screen is skipped and the stock wrapper renders its raw
 * "Http failure response for <url>: 0 undefined" text instead.
 *
 * ErrorHandler picks the highest-priority custom handler whose canHandle() returns true, so
 * registering at veryHigh (99) outranks ABP's own (normal, 0) and lets us re-dispatch the same
 * call WITH status: 0 set. That is the only missing piece -- everything downstream is stock ABP.
 *
 * canHandle is deliberately broader than ABP's, which also requires
 * `statusText === 'Unknown Error'`; browsers are not consistent about that string.
 */
@Injectable({ providedIn: 'root' })
export class ApiUnavailableErrorHandler implements CustomHttpErrorHandlerService {
  readonly priority = CUSTOM_HTTP_ERROR_HANDLER_PRIORITY.veryHigh;

  private readonly createErrorComponentService = inject(CreateErrorComponentService);
  private message = '';

  canHandle(error: unknown): boolean {
    const status = (error as { status?: unknown } | null)?.status;
    if (status !== 0) {
      return false;
    }
    this.message = String((error as { message?: unknown })?.message ?? '');
    return true;
  }

  execute(): void {
    this.createErrorComponentService.execute({
      // The whole point: ABP omits this, so the custom screen never matches.
      status: 0,
      details: this.message,
      isHomeShow: false,
    });
  }
}
