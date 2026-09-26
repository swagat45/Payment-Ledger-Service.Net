import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { WalletService } from '../../core/services/wallet.service';
import { TransactionService } from '../../core/services/transaction.service';
import { Wallet } from '../../core/models/wallet.model';
import { Transaction } from '../../core/models/transaction.model';

@Component({
  selector: 'app-wallet-details',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="wallet()" class="animate-in fade-in slide-in-from-bottom-4 duration-500">
      <!-- Header -->
      <div class="md:flex md:items-center md:justify-between mb-8">
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-3">
            <h2 class="text-3xl font-extrabold tracking-tight text-slate-900">
              Wallet Overview
            </h2>
            <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold uppercase tracking-wide border"
              [ngClass]="{
                'bg-emerald-50 text-emerald-700 border-emerald-200': wallet()?.status === 'Active',
                'bg-yellow-50 text-yellow-700 border-yellow-200': wallet()?.status === 'Blocked',
                'bg-slate-100 text-slate-700 border-slate-200': wallet()?.status === 'Closed'
              }">
              {{ wallet()?.status }}
            </span>
          </div>
          <p class="text-sm font-mono text-slate-500 mt-2 bg-slate-100/50 inline-block px-2 py-1 rounded border border-slate-200/60">ID: {{ wallet()?.walletId }}</p>
        </div>
        <div class="mt-4 flex md:ml-4 md:mt-0 space-x-3">
          <!-- Wallet Actions -->
          <button *ngIf="wallet()?.status === 'Active'" (click)="blockWallet()" type="button" class="inline-flex items-center rounded-xl bg-white px-4 py-2 text-sm font-semibold text-yellow-600 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-yellow-50 transition-colors">
            Block
          </button>
          <button *ngIf="wallet()?.status === 'Blocked'" (click)="unblockWallet()" type="button" class="inline-flex items-center rounded-xl bg-white px-4 py-2 text-sm font-semibold text-emerald-600 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-emerald-50 transition-colors">
            Unblock
          </button>
          <button *ngIf="wallet()?.status !== 'Closed'" (click)="closeWallet()" type="button" class="inline-flex items-center rounded-xl bg-white px-4 py-2 text-sm font-semibold text-red-600 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-red-50 transition-colors">
            Close
          </button>
        </div>
      </div>

      <!-- Balance Card (Premium Gradient) -->
      <div class="bg-gradient-to-br from-indigo-900 via-indigo-800 to-purple-900 overflow-hidden shadow-xl rounded-3xl mb-8 relative group">
        <!-- Decorative Glow -->
        <div class="absolute -right-20 -top-20 w-64 h-64 bg-indigo-500/30 rounded-full blur-3xl group-hover:bg-indigo-400/40 transition-all duration-700"></div>
        <div class="absolute -left-10 -bottom-10 w-40 h-40 bg-purple-500/30 rounded-full blur-2xl group-hover:bg-purple-400/40 transition-all duration-700"></div>
        
        <div class="px-8 py-10 relative z-10 flex flex-col md:flex-row md:justify-between md:items-end gap-6">
          <div>
            <dt class="text-sm font-medium text-indigo-200/80 mb-2 uppercase tracking-wider">Available Balance</dt>
            <dd class="text-5xl md:text-7xl font-extrabold text-white tracking-tight flex items-baseline gap-2">
              {{ wallet()?.availableBalance || wallet()?.balance | currency:wallet()?.currency }}
              <span class="text-xl font-medium text-indigo-300">{{ wallet()?.currency }}</span>
            </dd>
          </div>
          
          <div class="flex flex-col gap-3">
             <div class="flex items-center gap-3 bg-white/10 backdrop-blur-md p-1.5 rounded-2xl border border-white/20">
               <button (click)="openCreditModal()" class="flex-1 bg-white text-indigo-900 px-5 py-2.5 rounded-xl font-bold text-sm hover:bg-indigo-50 transition-colors shadow-sm">
                 Credit (Add)
               </button>
               <button (click)="openDebitModal()" class="flex-1 bg-indigo-600/50 text-white px-5 py-2.5 rounded-xl font-bold text-sm hover:bg-indigo-600/70 transition-colors border border-indigo-400/30">
                 Debit (Pay)
               </button>
             </div>
             <button (click)="verifyWallet()" class="text-xs text-indigo-200 hover:text-white transition-colors text-right px-2">Verify Wallet Integrity &rarr;</button>
          </div>
        </div>
      </div>

      <div *ngIf="walletAuditResult()" class="mb-8 p-5 bg-slate-900 rounded-2xl border border-slate-700 shadow-xl overflow-hidden animate-in fade-in zoom-in-95 duration-300 relative">
        <button (click)="walletAuditResult.set(null)" class="absolute top-4 right-4 text-slate-400 hover:text-white transition-colors">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
        <h3 class="font-mono text-sm font-semibold text-slate-200 mb-3 flex items-center gap-2"><div class="w-2 h-2 rounded-full bg-blue-400 animate-pulse"></div> Wallet_Audit_Log</h3>
        <pre class="text-[13px] font-mono text-blue-300 whitespace-pre-wrap overflow-x-auto">{{ walletAuditResult() | json }}</pre>
      </div>

      <!-- Transactions Table -->
      <div class="mt-10">
        <h3 class="text-xl font-bold text-slate-900 mb-5">Transaction Ledger</h3>
        <div class="bg-white/70 backdrop-blur-xl shadow-sm border border-slate-200/60 rounded-3xl overflow-hidden">
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-slate-200/60">
              <thead class="bg-slate-50/50">
                <tr>
                  <th scope="col" class="py-4 pl-6 pr-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Timestamp</th>
                  <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Type</th>
                  <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Amount</th>
                  <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                  <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Tx Reference</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 bg-white/50">
                <ng-container *ngFor="let tx of transactions()">
                  <!-- Main Row -->
                  <tr class="hover:bg-slate-50/80 cursor-pointer transition-colors group" (click)="toggleExpand(tx.transactionId)">
                    <td class="whitespace-nowrap py-5 pl-6 pr-3 text-sm text-slate-500 font-medium">
                      {{ tx.createdAt | date:'MMM d, y, h:mm a' }}
                    </td>
                    <td class="whitespace-nowrap px-3 py-5 text-sm font-semibold">
                        <span class="flex items-center gap-1.5" [ngClass]="{'text-emerald-600': tx.type === 'Credit', 'text-rose-600': tx.type === 'Debit', 'text-amber-500': tx.type === 'Refund'}">
                          <svg *ngIf="tx.type === 'Credit' || tx.type === 'Refund'" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M19 14l-7 7m0 0l-7-7m7 7V3"></path></svg>
                          <svg *ngIf="tx.type === 'Debit'" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 10l7-7m0 0l7 7m-7-7v18"></path></svg>
                          {{ tx.type }}
                        </span>
                    </td>
                    <td class="whitespace-nowrap px-3 py-5 text-sm font-bold text-slate-900">{{ tx.amount | currency:tx.currency }}</td>
                    <td class="whitespace-nowrap px-3 py-5 text-sm">
                      <span class="inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset"
                        [ngClass]="{
                          'bg-emerald-50 text-emerald-700 ring-emerald-600/20': tx.status === 'Posted' || tx.status === 'Refunded',
                          'bg-blue-50 text-blue-700 ring-blue-700/10': tx.status === 'Pending',
                          'bg-rose-50 text-rose-700 ring-rose-600/20': tx.status === 'Failed'
                        }">
                        {{ tx.status }}
                      </span>
                    </td>
                    <td class="whitespace-nowrap px-3 py-5 text-sm text-slate-400 font-mono flex items-center justify-between">
                      {{ tx.transactionCode || 'N/A' }}
                      <svg class="w-5 h-5 text-slate-300 group-hover:text-slate-500 transition-colors" [ngClass]="{'rotate-180': expandedTx() === tx.transactionId}" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" /></svg>
                    </td>
                  </tr>
                  
                  <!-- Expanded Ledger Entries View (The "Receipt") -->
                  <tr *ngIf="expandedTx() === tx.transactionId" class="bg-slate-50/50 shadow-[inset_0_4px_6px_-4px_rgba(0,0,0,0.05)] border-b-2 border-indigo-100">
                    <td colspan="5" class="px-8 py-8 animate-in slide-in-from-top-2 duration-300">
                      <div class="max-w-4xl mx-auto bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden relative">
                        
                        <!-- Decorative top edge like a receipt -->
                        <div class="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500"></div>
                        
                        <div class="p-6 md:p-8">
                          <div class="flex flex-col md:flex-row justify-between items-start md:items-center mb-8 gap-4 border-b border-slate-100 pb-6">
                            <div>
                              <h4 class="text-lg font-bold text-slate-900 tracking-tight">Double-Entry Ledger Records</h4>
                              <p class="text-sm text-slate-500 mt-1">Immutable accounting pairs for Tx: <span class="font-mono bg-slate-100 px-1 rounded">{{tx.transactionId}}</span></p>
                            </div>
                            <div class="flex gap-2">
                              <button *ngIf="tx.type !== 'Refund' && tx.status === 'Posted'" (click)="refundTransaction(tx); $event.stopPropagation()" class="px-4 py-2 bg-white border border-slate-200 text-slate-700 font-semibold rounded-xl text-sm hover:bg-slate-50 shadow-sm transition-all hover:text-rose-600 hover:border-rose-200">
                                Issue Refund
                              </button>
                              <button (click)="verifyTransaction(tx.transactionId); $event.stopPropagation()" class="px-4 py-2 bg-slate-900 text-white font-semibold rounded-xl text-sm hover:bg-slate-800 shadow-sm transition-all">
                                Verify Tx Integrity
                              </button>
                            </div>
                          </div>

                          <div *ngIf="txAuditResult() && txAuditResult().transactionId === tx.transactionId" class="mb-6 p-4 bg-slate-900 rounded-xl border border-slate-700 relative">
                             <button (click)="txAuditResult.set(null)" class="absolute top-3 right-3 text-slate-400 hover:text-white"><svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg></button>
                             <h5 class="text-emerald-400 text-xs font-bold uppercase tracking-wider mb-2">Cryptographic Verification Passed</h5>
                             <pre class="text-xs text-slate-300 font-mono overflow-x-auto">{{ txAuditResult() | json }}</pre>
                          </div>

                          <!-- T-Account Visualization -->
                          <div class="grid grid-cols-1 md:grid-cols-2 gap-0 border border-slate-200 rounded-2xl overflow-hidden bg-slate-50 relative">
                            <!-- Visual connector line -->
                            <div class="hidden md:block absolute top-0 bottom-0 left-1/2 w-px bg-slate-200"></div>
                            <div class="hidden md:block absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-8 h-8 bg-white rounded-full border border-slate-200 flex items-center justify-center z-10 shadow-sm">
                              <svg class="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" /></svg>
                            </div>

                            <div *ngFor="let entry of tx.ledgerEntries" class="p-6 md:p-8 bg-white transition-colors" [ngClass]="{'md:border-r border-b md:border-b-0 border-slate-200': true}">
                              <div class="flex items-center justify-between mb-4">
                                <span class="px-3 py-1 rounded-lg text-xs font-bold uppercase tracking-widest"
                                  [ngClass]="{'bg-emerald-100 text-emerald-800': entry.entryType === 'Credit', 'bg-rose-100 text-rose-800': entry.entryType === 'Debit'}">
                                  {{ entry.entryType }} Entry
                                </span>
                                <span class="text-slate-400 text-xs font-mono">#{{entry.ledgerEntryId}}</span>
                              </div>
                              <div class="mb-4">
                                <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Target Account / Wallet</p>
                                <p class="text-sm font-mono text-slate-700 bg-slate-50 px-2 py-1 rounded border border-slate-100 break-all">{{ entry.walletId }}</p>
                              </div>
                              <div>
                                <p class="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-1">Posted Amount</p>
                                <p class="text-3xl font-extrabold" [ngClass]="{'text-emerald-600': entry.entryType === 'Credit', 'text-rose-600': entry.entryType === 'Debit'}">
                                  {{ entry.entryType === 'Debit' ? '-' : '+' }}{{ entry.amount | currency:entry.currency }}
                                </p>
                              </div>
                            </div>
                            
                            <div *ngIf="!tx.ledgerEntries || tx.ledgerEntries.length === 0" class="col-span-2 p-8 text-center text-slate-500">
                              <svg class="w-8 h-8 mx-auto text-slate-300 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" /></svg>
                              <p>No ledger entries returned by API.</p>
                            </div>
                          </div>
                        </div>
                      </div>
                    </td>
                  </tr>
                </ng-container>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Enhanced Modals -->
      <div *ngIf="isCreditModalOpen()" class="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 z-[60] animate-in fade-in duration-200">
        <div class="bg-white rounded-3xl p-8 max-w-md w-full shadow-2xl border border-slate-100 animate-in zoom-in-95 duration-200">
          <div class="flex justify-between items-center mb-6">
            <h3 class="text-2xl font-bold text-slate-900">Credit Wallet</h3>
            <span class="bg-indigo-100 text-indigo-700 text-[10px] font-bold uppercase tracking-wider px-2 py-1 rounded-full">Idempotency Test</span>
          </div>
          
          <div class="space-y-4 mb-8">
            <div>
              <label class="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Amount ({{wallet()?.currency}})</label>
              <div class="relative">
                <span class="absolute inset-y-0 left-0 flex items-center pl-4 text-slate-400 font-bold">$</span>
                <input [(ngModel)]="txAmount" type="number" class="pl-8 bg-slate-50 border border-slate-200 text-slate-900 text-lg rounded-xl focus:ring-indigo-500 focus:border-indigo-500 block w-full py-3 px-4 font-semibold transition-colors">
              </div>
            </div>
            <div>
              <label class="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Idempotency-Key Header</label>
              <input [(ngModel)]="idempotencyKey" type="text" class="bg-slate-50 border border-slate-200 text-slate-700 font-mono text-sm rounded-xl focus:ring-indigo-500 focus:border-indigo-500 block w-full py-3 px-4 transition-colors">
              <p class="text-[11px] text-slate-400 mt-2">To test, send identical keys multiple times.</p>
            </div>
          </div>
          
          <div class="flex flex-col gap-3">
            <button (click)="submitCredit(1)" class="w-full py-3.5 bg-slate-900 text-white font-bold rounded-xl hover:bg-slate-800 transition-colors shadow-md">
              Process Standard Credit (1x)
            </button>
            <button (click)="submitCredit(3)" class="w-full py-3.5 bg-gradient-to-r from-indigo-500 to-purple-500 text-white font-bold rounded-xl hover:from-indigo-600 hover:to-purple-600 transition-all shadow-md transform hover:-translate-y-0.5">
              Force Concurrent Retry (3x Burst)
            </button>
            <button (click)="isCreditModalOpen.set(false)" class="w-full py-3 text-slate-500 font-semibold rounded-xl hover:bg-slate-100 transition-colors mt-2">
              Cancel
            </button>
          </div>
        </div>
      </div>

      <div *ngIf="isDebitModalOpen()" class="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 z-[60] animate-in fade-in duration-200">
        <div class="bg-white rounded-3xl p-8 max-w-md w-full shadow-2xl border border-slate-100 animate-in zoom-in-95 duration-200">
          <div class="flex justify-between items-center mb-6">
            <h3 class="text-2xl font-bold text-slate-900">Debit Wallet</h3>
            <span class="bg-rose-100 text-rose-700 text-[10px] font-bold uppercase tracking-wider px-2 py-1 rounded-full">Concurrency Test</span>
          </div>
          
          <div class="space-y-4 mb-8">
            <div>
              <label class="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Amount ({{wallet()?.currency}})</label>
              <div class="relative">
                <span class="absolute inset-y-0 left-0 flex items-center pl-4 text-slate-400 font-bold">$</span>
                <input [(ngModel)]="txAmount" type="number" class="pl-8 bg-slate-50 border border-slate-200 text-slate-900 text-lg rounded-xl focus:ring-rose-500 focus:border-rose-500 block w-full py-3 px-4 font-semibold transition-colors">
              </div>
            </div>
            <div class="bg-amber-50 border border-amber-200 rounded-xl p-4 flex gap-3 items-start">
               <svg class="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
               <p class="text-xs text-amber-800 leading-relaxed font-medium">To test DB pessimistic locking (`FOR UPDATE`), enter your exact total balance and fire a 5x Burst. Only 1 will succeed.</p>
            </div>
          </div>
          
          <div class="flex flex-col gap-3">
            <button (click)="submitDebit(1)" class="w-full py-3.5 bg-slate-900 text-white font-bold rounded-xl hover:bg-slate-800 transition-colors shadow-md">
              Process Standard Debit (1x)
            </button>
            <button (click)="submitDebit(5)" class="w-full py-3.5 bg-gradient-to-r from-rose-500 to-orange-500 text-white font-bold rounded-xl hover:from-rose-600 hover:to-orange-600 transition-all shadow-md transform hover:-translate-y-0.5">
              Force Race Condition (5x Burst)
            </button>
            <button (click)="isDebitModalOpen.set(false)" class="w-full py-3 text-slate-500 font-semibold rounded-xl hover:bg-slate-100 transition-colors mt-2">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class WalletDetailsComponent implements OnInit {
  route = inject(ActivatedRoute);
  walletService = inject(WalletService);
  txService = inject(TransactionService);

  wallet = signal<Wallet | null>(null);
  transactions = signal<Transaction[]>([]);
  expandedTx = signal<string | null>(null);

  walletAuditResult = signal<any>(null);
  txAuditResult = signal<any>(null);

  isCreditModalOpen = signal(false);
  isDebitModalOpen = signal(false);
  txAmount = 0;
  idempotencyKey = '';

  ngOnInit() {
    const walletId = this.route.snapshot.paramMap.get('id');
    if (walletId) {
      this.loadWallet(walletId);
      this.loadTransactions(walletId);
    }
  }

  loadWallet(id: string) {
    this.walletService.getWallet(id).subscribe(res => {
      if(res.success) this.wallet.set(res.data);
    });
  }

  loadTransactions(id: string) {
    this.txService.getWalletTransactions(id).subscribe(res => {
      if(res.success) this.transactions.set(res.data.items);
    });
  }

  toggleExpand(txId: string) {
    if (this.expandedTx() === txId) {
      this.expandedTx.set(null);
    } else {
      this.expandedTx.set(txId);
      // Fetch details to get ledger entries
      this.txService.getTransactionDetails(txId).subscribe(res => {
         if(res.success) {
            this.transactions.update(txs => txs.map(t => t.transactionId === txId ? res.data : t));
         }
      });
    }
  }

  // Wallet Actions
  blockWallet() {
    this.walletService.blockWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
  }

  unblockWallet() {
    this.walletService.unblockWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
  }

  closeWallet() {
    this.walletService.closeWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
  }

  verifyWallet() {
    this.walletService.verifyWalletConsistency(this.wallet()!.walletId).subscribe({
      next: (res) => this.walletAuditResult.set(res.data),
      error: (err) => this.walletAuditResult.set(err.error)
    });
  }

  verifyTransaction(txId: string) {
    this.txService.verifyTransactionConsistency(txId).subscribe({
      next: (res) => this.txAuditResult.set(res.data),
      error: (err) => this.txAuditResult.set(err.error)
    });
  }

  refundTransaction(tx: Transaction) {
    const reason = prompt('Enter refund reason:');
    if(!reason) return;
    const key = `refund-${tx.transactionId}-${Date.now()}`;
    this.txService.refundTransaction(tx.transactionId, tx.amount, tx.currency, reason, key).subscribe(() => {
       this.loadWallet(this.wallet()!.walletId);
       this.loadTransactions(this.wallet()!.walletId);
    });
  }

  // Modals & Showcase
  openCreditModal() {
    this.txAmount = 100;
    this.idempotencyKey = `credit-${Date.now()}`;
    this.isCreditModalOpen.set(true);
  }

  openDebitModal() {
    this.txAmount = 50;
    this.isDebitModalOpen.set(true);
  }

  submitCredit(count: number) {
    const walletId = this.wallet()!.walletId;
    const req = { amount: this.txAmount, currency: this.wallet()!.currency, description: 'Test Credit' };
    
    // For concurrency test, we fire multiple requests without waiting
    for(let i = 0; i < count; i++) {
      this.walletService.creditWallet(walletId, req, this.idempotencyKey).subscribe({
        next: () => {
          this.loadWallet(walletId);
          this.loadTransactions(walletId);
        },
        error: (err) => console.error('Credit failed', err)
      });
    }
    this.isCreditModalOpen.set(false);
  }

  submitDebit(count: number) {
    const walletId = this.wallet()!.walletId;
    
    // For debit concurrency, if count > 1, we use DIFFERENT keys to bypass idempotency and test DB locking
    for(let i = 0; i < count; i++) {
      const key = count > 1 ? `debit-${Date.now()}-${i}` : `debit-${Date.now()}`;
      const req = { amount: this.txAmount, currency: this.wallet()!.currency, description: 'Test Debit' };
      
      this.walletService.debitWallet(walletId, req, key).subscribe({
        next: () => {
          this.loadWallet(walletId);
          this.loadTransactions(walletId);
        },
        error: (err) => console.error('Debit failed', err)
      });
    }
    this.isDebitModalOpen.set(false);
  }
}
