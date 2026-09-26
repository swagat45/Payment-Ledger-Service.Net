import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LedgerService } from '../../core/services/ledger.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, CommonModule],
  template: `
    <div class="animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div class="flex items-end justify-between mb-8">
        <div>
          <h1 class="text-3xl font-extrabold tracking-tight text-slate-900">Platform Overview</h1>
          <p class="mt-2 text-sm text-slate-500">Real-time metrics and system integrity verification.</p>
        </div>
      </div>
      
      <!-- Metrics Grid -->
      <div class="grid grid-cols-1 gap-6 sm:grid-cols-3">
        <!-- Card 1 -->
        <div class="bg-white/80 backdrop-blur-md overflow-hidden shadow-sm hover:shadow-md transition-shadow rounded-2xl border border-slate-200/60 p-6 relative group">
          <div class="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
             <svg class="w-12 h-12 text-indigo-600" fill="currentColor" viewBox="0 0 24 24"><path d="M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"/></svg>
          </div>
          <dt class="text-sm font-medium text-slate-500 truncate mb-1">Total Network Users</dt>
          <dd class="text-4xl font-bold text-slate-900 tracking-tight">0</dd>
        </div>
        
        <!-- Card 2 -->
        <div class="bg-white/80 backdrop-blur-md overflow-hidden shadow-sm hover:shadow-md transition-shadow rounded-2xl border border-slate-200/60 p-6 relative group">
          <div class="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
            <svg class="w-12 h-12 text-blue-600" fill="currentColor" viewBox="0 0 24 24"><path d="M21 18v1c0 1.1-.9 2-2 2H5c-1.11 0-2-.9-2-2V5c0-1.1.89-2 2-2h14c1.1 0 2 .9 2 2v1h-9c-1.11 0-2 .9-2 2v8c0 1.1.89 2 2 2h9zm-9-2h10V8H12v8zm4-2.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z"/></svg>
          </div>
          <dt class="text-sm font-medium text-slate-500 truncate mb-1">Active Wallets</dt>
          <dd class="text-4xl font-bold text-slate-900 tracking-tight">0</dd>
        </div>

        <!-- Card 3 -->
        <div class="bg-gradient-to-br from-slate-900 to-slate-800 overflow-hidden shadow-lg rounded-2xl border border-slate-700 p-6 relative group">
          <div class="absolute -right-6 -top-6 w-24 h-24 bg-green-400/20 rounded-full blur-2xl group-hover:bg-green-400/30 transition-all"></div>
          <dt class="text-sm font-medium text-slate-400 truncate mb-1">Treasury Balance</dt>
          <dd class="text-4xl font-bold text-white tracking-tight">--</dd>
        </div>
      </div>

      <!-- Action Panel -->
      <div class="mt-8 bg-white/60 backdrop-blur-md shadow-sm border border-slate-200/60 rounded-2xl p-8">
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div>
            <h2 class="text-lg font-bold text-slate-900">System Integrity & Auditing</h2>
            <p class="text-sm text-slate-500 mt-1 max-w-xl">Run a complete cryptographic verification of the double-entry ledger to ensure all debits and credits balance to exactly zero.</p>
          </div>
          
          <div class="flex flex-wrap gap-3">
            <a routerLink="/users" class="inline-flex items-center justify-center px-5 py-2.5 text-sm font-medium rounded-xl text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition-colors shadow-sm">
              User Directory
            </a>
            <button (click)="verifyLedger()" class="inline-flex items-center justify-center px-5 py-2.5 text-sm font-semibold rounded-xl text-white bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 transition-all shadow-md hover:shadow-lg transform hover:-translate-y-0.5">
              <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              Run Global Audit
            </button>
          </div>
        </div>
        
        <!-- Audit Result Output -->
        <div *ngIf="ledgerAuditResult()" class="mt-6 p-5 bg-slate-900 rounded-xl border border-slate-700 shadow-inner overflow-hidden animate-in fade-in zoom-in-95 duration-300">
          <div class="flex items-center justify-between mb-3 border-b border-slate-700 pb-3">
            <div class="flex items-center gap-2">
              <div class="w-2 h-2 rounded-full bg-green-500 animate-pulse"></div>
              <h3 class="font-mono text-sm font-semibold text-slate-200">System_Audit_Log</h3>
            </div>
            <button (click)="ledgerAuditResult.set(null)" class="text-slate-400 hover:text-white transition-colors">
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>
          <pre class="text-[13px] font-mono text-green-400 whitespace-pre-wrap overflow-x-auto">{{ ledgerAuditResult() | json }}</pre>
        </div>
      </div>
    </div>
  `
})
export class DashboardComponent {
  ledgerService = inject(LedgerService);
  ledgerAuditResult = signal<any>(null);

  verifyLedger() {
    this.ledgerService.verifyLedgerConsistency().subscribe({
      next: (res) => this.ledgerAuditResult.set(res.data),
      error: (err) => this.ledgerAuditResult.set(err.error)
    });
  }
}
