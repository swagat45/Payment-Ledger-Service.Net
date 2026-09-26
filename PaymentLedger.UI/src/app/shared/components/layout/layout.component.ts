import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CorrelationIdService } from '../../../core/services/correlation-id.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  template: `
    <div class="min-h-screen flex flex-col relative text-slate-800">
      
      <!-- Floating Glassmorphism Navbar -->
      <nav class="fixed top-0 left-0 right-0 z-50 pt-4 px-4 sm:px-6 lg:px-8 pointer-events-none">
        <div class="max-w-7xl mx-auto pointer-events-auto">
          <div class="bg-white/70 backdrop-blur-xl border border-white/40 shadow-[0_8px_30px_rgb(0,0,0,0.04)] rounded-2xl px-6 py-3 flex items-center justify-between transition-all duration-300">
            
            <!-- Left: Logo & Links -->
            <div class="flex items-center space-x-10">
              <a routerLink="/" class="flex items-center gap-2 group">
                <div class="w-8 h-8 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center shadow-lg shadow-indigo-200 group-hover:scale-105 transition-transform duration-300">
                  <svg class="w-4 h-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                </div>
                <span class="font-extrabold text-xl tracking-tight bg-clip-text text-transparent bg-gradient-to-r from-slate-900 to-slate-700">Ledger</span>
              </a>
              
              <div class="hidden md:flex items-center space-x-1 bg-slate-100/50 rounded-lg p-1 border border-slate-200/50">
                <a routerLink="/" routerLinkActive="bg-white shadow-sm text-indigo-700 font-semibold" [routerLinkActiveOptions]="{exact: true}" class="px-4 py-2 rounded-md text-sm font-medium text-slate-600 hover:text-slate-900 transition-all duration-200">Overview</a>
                <a routerLink="/users" routerLinkActive="bg-white shadow-sm text-indigo-700 font-semibold" class="px-4 py-2 rounded-md text-sm font-medium text-slate-600 hover:text-slate-900 transition-all duration-200">Directory</a>
              </div>
            </div>

            <!-- Right: Observability Tracker -->
            <div class="hidden sm:flex items-center gap-3">
              <div class="flex flex-col items-end">
                <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400">Trace Network</span>
                <span class="text-xs font-mono font-medium text-slate-600 bg-slate-100/80 px-2 py-0.5 rounded border border-slate-200/50">
                  {{ correlationService.latestCorrelationId() || 'Waiting...' }}
                </span>
              </div>
              <div class="w-2 h-2 rounded-full shadow-[0_0_8px_rgba(34,197,94,0.6)]" [ngClass]="correlationService.latestCorrelationId() ? 'bg-green-500 animate-pulse' : 'bg-slate-300'"></div>
            </div>
            
          </div>
        </div>
      </nav>

      <!-- Main Content Area with top padding for floating nav -->
      <main class="flex-grow max-w-7xl w-full mx-auto pt-32 pb-12 px-4 sm:px-6 lg:px-8">
        <router-outlet></router-outlet>
      </main>
      
      <!-- Minimalist Footer -->
      <footer class="mt-auto border-t border-slate-200/60 bg-white/30 backdrop-blur-sm">
        <div class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8 flex flex-col md:flex-row justify-between items-center gap-4">
          <p class="text-sm text-slate-500 font-medium">
            Payment Ledger Service &copy; 2026
          </p>
          <div class="flex gap-4 text-sm text-slate-400">
            <span class="hover:text-slate-600 cursor-pointer transition-colors">Idempotent</span>
            <span class="hover:text-slate-600 cursor-pointer transition-colors">&bull;</span>
            <span class="hover:text-slate-600 cursor-pointer transition-colors">ACID Compliant</span>
            <span class="hover:text-slate-600 cursor-pointer transition-colors">&bull;</span>
            <span class="hover:text-slate-600 cursor-pointer transition-colors">Double-Entry</span>
          </div>
        </div>
      </footer>
    </div>
  `
})
export class LayoutComponent {
  correlationService = inject(CorrelationIdService);
}
