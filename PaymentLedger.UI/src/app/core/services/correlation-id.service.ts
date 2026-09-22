import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class CorrelationIdService {
  latestCorrelationId = signal<string | null>(null);

  setLatestCorrelationId(id: string) {
    this.latestCorrelationId.set(id);
  }
}
