import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CorrelationIdService } from '../../../core/services/correlation-id.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen flex flex-col">
      <!-- Navbar -->
      <nav class="bg-indigo-600 text-white shadow-md">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center justify-between h-16">
            <div class="flex items-center space-x-8">
              <a routerLink="/" class="font-bold text-xl tracking-tight">LedgerAdmin</a>
              <div class="hidden md:block">
                <div class="flex items-baseline space-x-4">
                  <a routerLink="/" routerLinkActive="bg-indigo-700" [routerLinkActiveOptions]="{exact: true}" class="px-3 py-2 rounded-md text-sm font-medium hover:bg-indigo-500">Dashboard</a>
                  <a routerLink="/users" routerLinkActive="bg-indigo-700" class="px-3 py-2 rounded-md text-sm font-medium hover:bg-indigo-500">Users</a>
                </div>
              </div>
            </div>
            <!-- Global Info / Correlation Tracker -->
            <div class="text-sm font-mono bg-indigo-800 px-3 py-1 rounded text-indigo-200">
              Trace ID: {{ correlationService.latestCorrelationId() || 'N/A' }}
            </div>
          </div>
        </div>
      </nav>

      <!-- Main Content -->
      <main class="flex-grow max-w-7xl w-full mx-auto py-6 sm:px-6 lg:px-8">
        <router-outlet></router-outlet>
      </main>
      
      <!-- Footer -->
      <footer class="bg-white border-t border-gray-200 mt-auto">
        <div class="max-w-7xl mx-auto py-4 px-4 sm:px-6 lg:px-8">
          <p class="text-center text-sm text-gray-500">
            Payment Ledger Service Platform &copy; 2026
          </p>
        </div>
      </footer>
    </div>
  `
})
export class LayoutComponent {
  correlationService = inject(CorrelationIdService);
}
