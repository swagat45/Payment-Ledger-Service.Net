import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';

// Optional: A service to store the latest correlation ID for the UI to display
import { CorrelationIdService } from '../services/correlation-id.service';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationService = inject(CorrelationIdService);

  return next(req).pipe(
    tap(event => {
      // If it's an HttpResponse, try to read the correlation ID
      if (event.type === 4) { // HttpResponse type is 4
        const correlationId = (event as any).headers?.get('X-Correlation-ID');
        if (correlationId) {
          correlationService.setLatestCorrelationId(correlationId);
        }
      }
    }),
    catchError((error: HttpErrorResponse) => {
      let errorMsg = 'An unknown error occurred!';
      if (error.error && error.error.errors) {
        // Map standardized backend ErrorResponse
        errorMsg = error.error.errors.map((e: any) => e.message).join(', ');
      } else if (error.error && error.error.message) {
        errorMsg = error.error.message;
      }
      
      const correlationId = error.headers?.get('X-Correlation-ID') || error.error?.correlationId;
      if (correlationId) {
        correlationService.setLatestCorrelationId(correlationId);
      }

      console.error('API Error:', errorMsg, 'CorrelationId:', correlationId);
      // In a real app, you might trigger a Toast service here.
      
      return throwError(() => error);
    })
  );
};
