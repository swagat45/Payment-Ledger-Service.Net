import { Component, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CorrelationIdService } from '../../../core/services/correlation-id.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  template: `
    <div class="h-screen w-screen flex overflow-hidden bg-slate-950 font-sans text-slate-100 antialiased selection:bg-indigo-500 selection:text-white">
      
      <!-- Mobile Sidebar Backdrop -->
      <div 
        *ngIf="mobileMenuOpen()" 
        (click)="mobileMenuOpen.set(false)"
        class="fixed inset-0 z-40 bg-black/60 backdrop-blur-xs md:hidden transition-opacity">
      </div>

      <!-- Left Sidebar Navigation -->
      <aside 
        class="fixed inset-y-0 left-0 z-50 w-64 bg-slate-900 border-r border-slate-800 flex flex-col justify-between transition-transform duration-200 ease-in-out md:static md:translate-x-0"
        [class.-translate-x-full]="!mobileMenuOpen()"
        [class.translate-x-0]="mobileMenuOpen()">
        
        <!-- Top Section: Platform Branding -->
        <div>
          <div class="h-16 flex items-center justify-between px-5 border-b border-slate-800">
            <a routerLink="/" (click)="mobileMenuOpen.set(false)" class="flex items-center gap-3 group">
              <div class="w-8 h-8 rounded-lg bg-indigo-600 flex items-center justify-center text-white shadow-xs group-hover:bg-indigo-500 transition-colors">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <div class="flex flex-col">
                <span class="font-bold text-sm tracking-tight text-white flex items-center gap-1.5">
                  Payment Ledger
                </span>
                <span class="text-[10px] font-mono tracking-wider text-indigo-400 font-semibold uppercase">Enterprise Core</span>
              </div>
            </a>
            
            <button 
              (click)="mobileMenuOpen.set(false)" 
              class="md:hidden text-slate-400 hover:text-white p-1 rounded-md">
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>

          <!-- Navigation Links -->
          <div class="px-3 py-4 space-y-1">
            <div class="px-3 pb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">Navigation</div>
            
            <a 
              routerLink="/" 
              (click)="mobileMenuOpen.set(false)"
              routerLinkActive="bg-slate-800 text-white font-semibold shadow-xs" 
              [routerLinkActiveOptions]="{exact: true}" 
              class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition-all">
              <svg class="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
              </svg>
              <span>Dashboard</span>
            </a>

            <a 
              routerLink="/users" 
              (click)="mobileMenuOpen.set(false)"
              routerLinkActive="bg-slate-800 text-white font-semibold shadow-xs" 
              class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium text-slate-400 hover:text-slate-100 hover:bg-slate-800/60 transition-all">
              <svg class="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
              <span>Customer Accounts</span>
            </a>
          </div>

          <!-- Architecture & Guarantees section -->
          <div class="px-3 pt-4 border-t border-slate-800/80">
            <div class="px-3 pb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">Ledger Invariants</div>
            
            <div class="px-3 py-2 text-xs space-y-2.5 text-slate-400">
              <div class="flex items-center gap-2">
                <div class="w-1.5 h-1.5 rounded-full bg-emerald-400"></div>
                <span class="text-slate-300 font-medium">Double-Entry Balance</span>
              </div>
              <div class="flex items-center gap-2">
                <div class="w-1.5 h-1.5 rounded-full bg-emerald-400"></div>
                <span class="text-slate-300 font-medium">PostgreSQL Row Locks</span>
              </div>
              <div class="flex items-center gap-2">
                <div class="w-1.5 h-1.5 rounded-full bg-emerald-400"></div>
                <span class="text-slate-300 font-medium">SHA-256 Idempotency</span>
              </div>
              <div class="flex items-center gap-2">
                <div class="w-1.5 h-1.5 rounded-full bg-emerald-400"></div>
                <span class="text-slate-300 font-medium">Non-Negative Balance</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Bottom Section: System Diagnostics & Links -->
        <div class="p-4 border-t border-slate-800 space-y-3">
          <!-- Active Trace Correlation ID card -->
          <div class="bg-slate-950/70 border border-slate-800 rounded-lg p-2.5">
            <div class="flex items-center justify-between mb-1">
              <span class="text-[10px] font-bold uppercase tracking-wider text-slate-400">Trace Correlation</span>
              <div class="flex items-center gap-1.5">
                <div class="w-1.5 h-1.5 rounded-full" [ngClass]="correlationService.latestCorrelationId() ? 'bg-emerald-400 animate-pulse' : 'bg-slate-600'"></div>
                <span class="text-[10px] text-slate-400">{{ correlationService.latestCorrelationId() ? 'Live' : 'Standby' }}</span>
              </div>
            </div>
            <div class="flex items-center justify-between gap-1">
              <span class="text-[11px] font-mono text-indigo-300 truncate max-w-[150px]" [title]="correlationService.latestCorrelationId() || 'No active trace'">
                {{ correlationService.latestCorrelationId() || 'Waiting for I/O...' }}
              </span>
              <button 
                *ngIf="correlationService.latestCorrelationId()"
                (click)="copyCorrelationId()"
                title="Copy correlation ID"
                class="text-slate-400 hover:text-white p-1 rounded transition-colors text-[10px]">
                <span *ngIf="copied()">✓</span>
                <span *ngIf="!copied()">⎘</span>
              </button>
            </div>
          </div>

          <!-- Documentation quick links -->
          <div class="flex items-center justify-between text-xs text-slate-400 pt-1">
            <a href="http://localhost:8080/swagger" target="_blank" class="hover:text-slate-200 transition-colors flex items-center gap-1">
              <span>Swagger UI</span>
              <span class="text-[10px]">↗</span>
            </a>
            <span class="text-slate-700">•</span>
            <a href="http://localhost:8080/health" target="_blank" class="hover:text-slate-200 transition-colors flex items-center gap-1">
              <span>Health API</span>
              <span class="text-[10px]">↗</span>
            </a>
            <span class="text-slate-700">•</span>
            <span class="font-mono text-[10px] text-slate-400">v1.0.0</span>
          </div>
        </div>
      </aside>

      <!-- Main Content Application Shell -->
      <div class="flex-1 flex flex-col h-screen overflow-hidden bg-slate-50">
        
        <!-- Top App Bar -->
        <header class="h-16 bg-white border-b border-slate-200 px-6 flex items-center justify-between shrink-0 shadow-xs z-10">
          <div class="flex items-center gap-4">
            <!-- Mobile Menu Toggle Button -->
            <button 
              (click)="mobileMenuOpen.set(true)" 
              class="md:hidden text-slate-600 hover:text-slate-900 p-1.5 -ml-2 rounded-lg hover:bg-slate-100 transition-colors">
              <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
            
            <div class="flex items-center gap-2 text-sm text-slate-500 font-medium">
              <span class="text-slate-400 font-normal">Core System</span>
              <span class="text-slate-300">/</span>
              <span class="text-slate-800 font-semibold">Ledger & Digital Wallets</span>
            </div>
          </div>

          <!-- Top Right Status Indicators -->
          <div class="flex items-center gap-3">
            <div class="hidden sm:flex items-center gap-2 bg-slate-100 border border-slate-200/80 px-2.5 py-1 rounded-md text-xs font-medium text-slate-600">
              <div class="w-2 h-2 rounded-full bg-emerald-500"></div>
              <span>PostgreSQL 17 • ACID Ready</span>
            </div>

            <div class="hidden lg:flex items-center gap-2 bg-indigo-50 border border-indigo-100 px-2.5 py-1 rounded-md text-xs font-semibold text-indigo-700">
              <span>FOR UPDATE Locking Active</span>
            </div>
          </div>
        </header>

        <!-- Main Scrollable Canvas (Full-Width Responsive) -->
        <main class="flex-1 overflow-y-auto p-6 md:p-8 space-y-6">
          <router-outlet></router-outlet>
        </main>
      </div>

    </div>
  `
})
export class LayoutComponent {
  correlationService = inject(CorrelationIdService);
  mobileMenuOpen = signal(false);
  copied = signal(false);

  copyCorrelationId() {
    const id = this.correlationService.latestCorrelationId();
    if (id) {
      navigator.clipboard.writeText(id).then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
      });
    }
  }
}
