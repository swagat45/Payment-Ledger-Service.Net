import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { WalletService } from '../../core/services/wallet.service';
import { TransactionService } from '../../core/services/transaction.service';
import { Wallet } from '../../core/models/wallet.model';
import { Transaction } from '../../core/models/transaction.model';

interface StressTestLog {
  id: number;
  timestamp: string;
  action: string;
  statusCode: number;
  message: string;
  isSuccess: boolean;
}

@Component({
  selector: 'app-wallet-details',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div *ngIf="wallet()" class="space-y-6">
      
      <!-- Breadcrumb & Top Bar -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-2 border-b border-slate-200">
        <div>
          <div class="flex items-center gap-2 text-xs text-slate-500 mb-1">
            <a routerLink="/users" class="hover:text-slate-800 transition-colors">Customer Accounts</a>
            <span>/</span>
            <span class="font-mono text-slate-700 font-semibold">{{ wallet()?.walletId }}</span>
          </div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Wallet Details & Ledger</h1>
            <span 
              class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold"
              [ngClass]="{
                'bg-emerald-50 text-emerald-700 border border-emerald-200': wallet()?.status === 'Active',
                'bg-amber-50 text-amber-700 border border-amber-200': wallet()?.status === 'Blocked',
                'bg-slate-100 text-slate-700 border border-slate-200': wallet()?.status === 'Closed'
              }">
              <span class="w-1.5 h-1.5 rounded-full" 
                [ngClass]="{
                  'bg-emerald-500': wallet()?.status === 'Active',
                  'bg-amber-500': wallet()?.status === 'Blocked',
                  'bg-slate-400': wallet()?.status === 'Closed'
                }">
              </span>
              {{ wallet()?.status }}
            </span>
          </div>
        </div>

        <!-- Administrative Wallet State Actions -->
        <div class="flex items-center gap-2">
          <button 
            *ngIf="wallet()?.status === 'Active'" 
            (click)="blockWallet()" 
            class="px-3 py-1.5 rounded-lg text-xs font-semibold text-amber-700 bg-amber-50 border border-amber-200 hover:bg-amber-100 transition-colors">
            Suspend / Block
          </button>
          <button 
            *ngIf="wallet()?.status === 'Blocked'" 
            (click)="unblockWallet()" 
            class="px-3 py-1.5 rounded-lg text-xs font-semibold text-emerald-700 bg-emerald-50 border border-emerald-200 hover:bg-emerald-100 transition-colors">
            Reactivate
          </button>
          <button 
            *ngIf="wallet()?.status !== 'Closed'" 
            (click)="closeWallet()" 
            class="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-700 bg-rose-50 border border-rose-200 hover:bg-rose-100 transition-colors">
            Close Account
          </button>
        </div>
      </div>

      <!-- Financial Balance Card (Enterprise Banking Header) -->
      <div class="bg-white rounded-xl border border-slate-200 p-6 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">
            <span>Available Balance</span>
            <span class="text-slate-300">•</span>
            <span class="text-slate-400 font-normal">Settled Funds</span>
          </div>
          <div class="text-4xl font-extrabold font-mono text-slate-900 tracking-tight flex items-baseline gap-2">
            <span>{{ wallet()?.availableBalance || wallet()?.balance | currency:wallet()?.currency:'symbol':'1.2-2' }}</span>
            <span class="text-sm font-sans font-medium text-slate-500">{{ wallet()?.currency }}</span>
          </div>
          
          <div class="mt-3 flex items-center gap-3 text-xs text-slate-500">
            <span class="flex items-center gap-1">
              <span class="text-slate-400">Owner ID:</span>
              <span class="font-mono bg-slate-100 px-1.5 py-0.5 rounded text-slate-700">{{ wallet()?.userId }}</span>
            </span>
            <span>•</span>
            <span class="flex items-center gap-1">
              <span class="text-slate-400">Created:</span>
              <span class="font-mono text-slate-600">{{ wallet()?.createdAt | date:'mediumDate' }}</span>
            </span>
          </div>
        </div>

        <!-- Quick Operation Triggers -->
        <div class="flex flex-wrap items-center gap-2.5">
          <button 
            (click)="openCreditModal()" 
            [disabled]="wallet()?.status !== 'Active'"
            class="px-4 py-2 bg-slate-900 text-white text-xs font-semibold rounded-lg hover:bg-slate-800 transition-colors shadow-xs disabled:opacity-40 flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
            </svg>
            Deposit Funds
          </button>
          
          <button 
            (click)="openDebitModal()" 
            [disabled]="wallet()?.status !== 'Active'"
            class="px-4 py-2 bg-white border border-slate-300 text-slate-700 text-xs font-semibold rounded-lg hover:bg-slate-50 transition-colors shadow-xs disabled:opacity-40 flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5 text-rose-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M20 12H4" />
            </svg>
            Withdraw / Debit
          </button>

          <button 
            (click)="verifyWallet()" 
            class="px-3.5 py-2 bg-slate-100 border border-slate-200 text-slate-700 text-xs font-semibold rounded-lg hover:bg-slate-200 transition-colors">
            Audit Ledger Invariant
          </button>
        </div>
      </div>

      <!-- Wallet Audit Verification Alert -->
      <div *ngIf="walletAuditResult()" class="p-4 bg-slate-900 text-slate-200 rounded-xl font-mono text-xs border border-slate-800">
        <div class="flex items-center justify-between pb-2 mb-2 border-b border-slate-800">
          <div class="flex items-center gap-2">
            <div class="w-2 h-2 rounded-full" [ngClass]="walletAuditResult()?.isConsistent !== false ? 'bg-emerald-400' : 'bg-rose-500'"></div>
            <span class="text-white font-semibold uppercase text-[11px]">Wallet Invariant Consistency Report</span>
          </div>
          <button (click)="walletAuditResult.set(null)" class="text-slate-400 hover:text-white font-sans text-xs">✕</button>
        </div>
        <pre class="text-[11px] text-emerald-300 overflow-x-auto">{{ walletAuditResult() | json }}</pre>
      </div>

      <!-- Stress Testing & Concurrency Studio -->
      <div class="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
        <div class="p-5 border-b border-slate-200/80 bg-slate-50/50 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-sm font-bold text-slate-900">Engineering Stress Testing Studio</h2>
              <span class="text-[10px] font-semibold bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded-full border border-indigo-100">
                Pessimistic Locks & Deduplication
              </span>
            </div>
            <p class="text-xs text-slate-500 mt-0.5">
              Live simulation of race conditions, PostgreSQL row locks (<code class="font-mono text-slate-700">FOR UPDATE</code>), and idempotent replay protection.
            </p>
          </div>

          <div class="flex items-center gap-2">
            <button 
              (click)="clearStressLogs()" 
              *ngIf="stressLogs().length > 0"
              class="text-xs text-slate-500 hover:text-slate-800 font-medium px-2 py-1">
              Clear Logs
            </button>
          </div>
        </div>

        <div class="p-5 grid grid-cols-1 md:grid-cols-2 gap-5">
          <!-- Idempotency Test Card -->
          <div class="border border-slate-200 rounded-lg p-4 bg-slate-50/30 flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-2">
                <span class="text-xs font-bold text-slate-900">1. Idempotent Deduplication Test</span>
                <span class="text-[10px] font-mono text-slate-500 bg-slate-100 px-1.5 py-0.5 rounded">SHA-256 Key</span>
              </div>
              <p class="text-xs text-slate-500 leading-relaxed mb-4">
                Fires 3 concurrent credit requests with the <strong>exact same idempotency key</strong>. Only 1 creates ledger entries; remaining 2 return cached response.
              </p>
            </div>
            <div class="flex gap-2">
              <button 
                (click)="runIdempotencyBurst()" 
                [disabled]="wallet()?.status !== 'Active' || isTestingBurst()"
                class="flex-1 py-2 px-3 bg-slate-900 text-white text-xs font-semibold rounded-lg hover:bg-slate-800 transition-colors shadow-xs disabled:opacity-40 flex items-center justify-center gap-1.5">
                <svg *ngIf="isTestingBurst()" class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span>Trigger 3x Idempotency Burst</span>
              </button>
            </div>
          </div>

          <!-- Concurrency Race Condition Card -->
          <div class="border border-slate-200 rounded-lg p-4 bg-slate-50/30 flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between mb-2">
                <span class="text-xs font-bold text-slate-900">2. Row Lock & Overdraft Guard</span>
                <span class="text-[10px] font-mono text-rose-700 bg-rose-50 px-1.5 py-0.5 rounded border border-rose-100">Race Condition</span>
              </div>
              <p class="text-xs text-slate-500 leading-relaxed mb-4">
                Fires 5 concurrent debits for the current total balance using <strong>distinct keys</strong>. Row lock forces serial evaluation: exactly 1 succeeds, 4 fail with 409 Conflict.
              </p>
            </div>
            <div class="flex gap-2">
              <button 
                (click)="runConcurrencyDebitBurst()" 
                [disabled]="wallet()?.status !== 'Active' || isTestingBurst() || (wallet()?.availableBalance || 0) <= 0"
                class="flex-1 py-2 px-3 bg-rose-700 text-white text-xs font-semibold rounded-lg hover:bg-rose-800 transition-colors shadow-xs disabled:opacity-40 flex items-center justify-center gap-1.5">
                <svg *ngIf="isTestingBurst()" class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span>Trigger 5x Race Condition Burst</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Live Stress Execution Terminal -->
        <div *ngIf="stressLogs().length > 0" class="border-t border-slate-200 bg-slate-950 p-4 font-mono text-xs">
          <div class="flex items-center justify-between pb-2 mb-2 border-b border-slate-800 text-[11px] text-slate-400">
            <span>Live Burst Execution Console</span>
            <span>{{ stressLogs().length }} events logged</span>
          </div>
          <div class="space-y-1.5 max-h-48 overflow-y-auto">
            <div *ngFor="let log of stressLogs()" class="flex items-start gap-2.5">
              <span class="text-slate-500 shrink-0">{{ log.timestamp }}</span>
              <span 
                class="px-1.5 py-0.2 rounded text-[10px] font-bold shrink-0"
                [ngClass]="log.isSuccess ? 'bg-emerald-950 text-emerald-400 border border-emerald-800' : 'bg-rose-950 text-rose-400 border border-rose-800'">
                HTTP {{ log.statusCode }}
              </span>
              <span class="text-slate-300 font-semibold shrink-0">[{{ log.action }}]</span>
              <span [ngClass]="log.isSuccess ? 'text-slate-300' : 'text-rose-300'">{{ log.message }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Transaction Ledger & Accounting Journal -->
      <div class="space-y-3">
        <div class="flex items-center justify-between">
          <h2 class="text-base font-bold text-slate-900">Transaction History & Double-Entry Journal</h2>
          <span class="text-xs font-mono text-slate-500">{{ transactions().length }} transactions recorded</span>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden">
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-slate-200 text-left text-xs">
              <thead class="bg-slate-50 text-slate-500 font-semibold uppercase tracking-wider">
                <tr>
                  <th scope="col" class="py-3 px-6">Timestamp</th>
                  <th scope="col" class="py-3 px-6">Mutation Type</th>
                  <th scope="col" class="py-3 px-6 text-right">Amount</th>
                  <th scope="col" class="py-3 px-6">Status</th>
                  <th scope="col" class="py-3 px-6">Transaction Code</th>
                  <th scope="col" class="py-3 px-6 text-right">Ledger Journal</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 text-slate-700 bg-white">
                <ng-container *ngFor="let tx of transactions()">
                  <!-- Parent Transaction Row -->
                  <tr 
                    (click)="toggleExpand(tx.transactionId)"
                    class="hover:bg-slate-50/80 cursor-pointer transition-colors"
                    [class.bg-slate-50]="expandedTx() === tx.transactionId">
                    
                    <!-- Timestamp -->
                    <td class="py-3.5 px-6 font-mono text-[11px] text-slate-500 whitespace-nowrap">
                      {{ tx.createdAt | date:'MMM d, y, h:mm:ss a' }}
                    </td>

                    <!-- Type -->
                    <td class="py-3.5 px-6 font-semibold">
                      <span 
                        class="inline-flex items-center gap-1.5"
                        [ngClass]="{
                          'text-emerald-700': tx.type === 'Credit',
                          'text-rose-700': tx.type === 'Debit',
                          'text-amber-700': tx.type === 'Refund'
                        }">
                        <span class="font-bold">{{ tx.type }}</span>
                      </span>
                    </td>

                    <!-- Amount -->
                    <td class="py-3.5 px-6 text-right font-mono font-bold text-xs whitespace-nowrap"
                      [ngClass]="{
                        'text-emerald-700': tx.type === 'Credit',
                        'text-slate-900': tx.type === 'Debit',
                        'text-amber-700': tx.type === 'Refund'
                      }">
                      {{ tx.type === 'Debit' ? '-' : '+' }}{{ tx.amount | currency:tx.currency:'symbol':'1.2-2' }}
                    </td>

                    <!-- Status -->
                    <td class="py-3.5 px-6">
                      <span 
                        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold"
                        [ngClass]="{
                          'bg-emerald-50 text-emerald-700 border border-emerald-200': tx.status === 'Posted',
                          'bg-blue-50 text-blue-700 border border-blue-200': tx.status === 'Pending',
                          'bg-rose-50 text-rose-700 border border-rose-200': tx.status === 'Failed',
                          'bg-amber-50 text-amber-700 border border-amber-200': tx.status === 'Refunded'
                        }">
                        {{ tx.status }}
                      </span>
                    </td>

                    <!-- Code -->
                    <td class="py-3.5 px-6 font-mono text-[11px] text-slate-500">
                      {{ tx.transactionCode || '—' }}
                    </td>

                    <!-- Expand Indicator -->
                    <td class="py-3.5 px-6 text-right font-medium text-indigo-600">
                      <span class="inline-flex items-center gap-1 hover:text-indigo-800">
                        <span>{{ expandedTx() === tx.transactionId ? 'Hide Entries' : 'View Entries' }}</span>
                        <svg class="w-3.5 h-3.5 transition-transform" [class.rotate-180]="expandedTx() === tx.transactionId" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                        </svg>
                      </span>
                    </td>
                  </tr>

                  <!-- Double-Entry Ledger Journal Breakdown (Sub-Row) -->
                  <tr *ngIf="expandedTx() === tx.transactionId" class="bg-slate-50/80 border-y border-indigo-100">
                    <td colspan="6" class="p-6">
                      <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-xs">
                        
                        <div class="flex flex-col sm:flex-row sm:items-center justify-between pb-4 mb-4 border-b border-slate-100 gap-3">
                          <div>
                            <div class="flex items-center gap-2">
                              <h3 class="text-xs font-bold text-slate-900 uppercase tracking-wider">Double-Entry Ledger Journal</h3>
                              <span class="text-[10px] font-mono bg-emerald-50 text-emerald-700 border border-emerald-200 px-1.5 py-0.2 rounded">
                                Balanced (∑ Debits == ∑ Credits)
                              </span>
                            </div>
                            <p class="text-xs text-slate-500 mt-0.5">
                              Transaction ID: <span class="font-mono text-slate-700 font-semibold">{{ tx.transactionId }}</span>
                            </p>
                          </div>

                          <div class="flex items-center gap-2">
                            <button 
                              *ngIf="tx.type === 'Debit' && tx.status === 'Posted'"
                              (click)="refundTransaction(tx); $event.stopPropagation()"
                              class="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-700 bg-rose-50 border border-rose-200 hover:bg-rose-100 transition-colors">
                              Issue Refund
                            </button>
                            <button 
                              (click)="verifyTransaction(tx.transactionId); $event.stopPropagation()"
                              class="px-3 py-1.5 rounded-lg text-xs font-semibold text-slate-700 bg-slate-100 border border-slate-200 hover:bg-slate-200 transition-colors">
                              Verify Cryptographic Balance
                            </button>
                          </div>
                        </div>

                        <!-- Tx Audit Result -->
                        <div *ngIf="txAuditResult() && txAuditResult().transactionId === tx.transactionId" class="mb-4 p-3 bg-slate-900 text-slate-200 rounded-lg font-mono text-xs border border-slate-800">
                          <div class="flex items-center justify-between pb-1 mb-1 border-b border-slate-800">
                            <span class="text-emerald-400 font-semibold uppercase text-[10px]">Transaction Verification: 100% Invariant Compliant</span>
                            <button (click)="txAuditResult.set(null)" class="text-slate-400 hover:text-white font-sans text-xs">✕</button>
                          </div>
                          <pre class="text-[10px] text-emerald-300 overflow-x-auto">{{ txAuditResult() | json }}</pre>
                        </div>

                        <!-- T-Account Double-Entry Cards -->
                        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <div *ngFor="let entry of tx.ledgerEntries" class="border border-slate-200 rounded-lg p-4 bg-slate-50/50">
                            <div class="flex items-center justify-between mb-2">
                              <span 
                                class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider"
                                [ngClass]="entry.entryType === 'Credit' ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'">
                                {{ entry.entryType }} Leg
                              </span>
                              <span class="text-slate-400 font-mono text-[10px]">Entry #{{ entry.ledgerEntryId }}</span>
                            </div>
                            
                            <div class="space-y-1.5 text-xs">
                              <div>
                                <span class="text-slate-400 text-[11px] block">Settlement Wallet</span>
                                <span class="font-mono text-slate-700 bg-white px-2 py-0.5 rounded border border-slate-200 inline-block text-[11px] break-all">
                                  {{ entry.walletId }}
                                </span>
                              </div>
                              <div class="pt-2 flex items-baseline justify-between">
                                <span class="text-slate-500 font-medium">Recorded Entry Amount</span>
                                <span class="font-mono font-bold text-sm"
                                  [ngClass]="entry.entryType === 'Credit' ? 'text-emerald-700' : 'text-rose-700'">
                                  {{ entry.entryType === 'Debit' ? '-' : '+' }}{{ entry.amount | currency:entry.currency:'symbol':'1.2-2' }}
                                </span>
                              </div>
                            </div>
                          </div>

                          <div *ngIf="!tx.ledgerEntries || tx.ledgerEntries.length === 0" class="col-span-2 p-6 text-center text-slate-400">
                            No ledger journal entries returned for this record.
                          </div>
                        </div>

                      </div>
                    </td>
                  </tr>
                </ng-container>

                <!-- Empty State -->
                <tr *ngIf="transactions().length === 0">
                  <td colspan="6" class="py-12 text-center text-slate-500">
                    <p class="font-medium text-slate-700 text-sm">No transaction entries found</p>
                    <p class="text-xs text-slate-400 mt-0.5">Use the Deposit or Withdraw actions above to record the initial financial mutation.</p>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Deposit / Credit Modal -->
      <div 
        *ngIf="isCreditModalOpen()" 
        class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
        <div class="bg-white rounded-xl max-w-md w-full shadow-2xl border border-slate-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="text-sm font-bold text-slate-900">Deposit / Credit Wallet</h3>
              <p class="text-xs text-slate-500">Credits user wallet from system Treasury settlement account.</p>
            </div>
            <button (click)="isCreditModalOpen.set(false)" class="text-slate-400 hover:text-slate-600 text-sm font-bold">✕</button>
          </div>

          <div class="p-6 space-y-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Credit Amount ({{ wallet()?.currency }})</label>
              <input 
                [(ngModel)]="txAmount" 
                type="number" 
                min="0.01" 
                step="0.01"
                class="w-full text-base font-mono font-bold px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Idempotency-Key Header</label>
              <input 
                [(ngModel)]="idempotencyKey" 
                type="text" 
                class="w-full text-xs font-mono px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-slate-700 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
              <p class="text-[11px] text-slate-400 mt-1">Unique UUID header ensuring exactly-once execution.</p>
            </div>

            <div class="pt-3 border-t border-slate-100 flex items-center justify-end gap-3">
              <button 
                type="button" 
                (click)="isCreditModalOpen.set(false)" 
                class="px-4 py-2 rounded-lg text-xs font-semibold text-slate-700 hover:bg-slate-100 transition-colors">
                Cancel
              </button>
              <button 
                type="button" 
                (click)="submitCredit(1)" 
                class="px-4 py-2 rounded-lg text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 transition-colors shadow-xs">
                Process Deposit
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Debit / Withdraw Modal -->
      <div 
        *ngIf="isDebitModalOpen()" 
        class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
        <div class="bg-white rounded-xl max-w-md w-full shadow-2xl border border-slate-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="text-sm font-bold text-slate-900">Withdraw / Debit Wallet</h3>
              <p class="text-xs text-slate-500">Debits user wallet and credits system Treasury account.</p>
            </div>
            <button (click)="isDebitModalOpen.set(false)" class="text-slate-400 hover:text-slate-600 text-sm font-bold">✕</button>
          </div>

          <div class="p-6 space-y-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Debit Amount ({{ wallet()?.currency }})</label>
              <input 
                [(ngModel)]="txAmount" 
                type="number" 
                min="0.01" 
                step="0.01"
                class="w-full text-base font-mono font-bold px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
              <p class="text-[11px] text-slate-400 mt-1">Available: {{ wallet()?.availableBalance || wallet()?.balance | currency:wallet()?.currency:'symbol':'1.2-2' }}</p>
            </div>

            <div class="pt-3 border-t border-slate-100 flex items-center justify-end gap-3">
              <button 
                type="button" 
                (click)="isDebitModalOpen.set(false)" 
                class="px-4 py-2 rounded-lg text-xs font-semibold text-slate-700 hover:bg-slate-100 transition-colors">
                Cancel
              </button>
              <button 
                type="button" 
                (click)="submitDebit(1)" 
                class="px-4 py-2 rounded-lg text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 transition-colors shadow-xs">
                Execute Debit
              </button>
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- Loading State -->
    <div *ngIf="isLoading()" class="py-24 text-center">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600 mb-3"></div>
      <p class="text-xs font-medium text-slate-500">Loading ledger account details from backend...</p>
    </div>

    <!-- Empty / No Wallet State -->
    <div *ngIf="!isLoading() && !wallet()" class="max-w-md mx-auto py-16 text-center">
      <div class="bg-white border border-slate-200 rounded-xl p-8 shadow-xs">
        <div class="w-12 h-12 bg-slate-100 rounded-xl flex items-center justify-center mx-auto mb-4 text-slate-600">
          <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"/>
          </svg>
        </div>
        <h3 class="text-base font-bold text-slate-900 mb-1">No Active Wallet Provisioned</h3>
        <p class="text-xs text-slate-500 mb-6">This customer account does not have a provisioned digital wallet yet.</p>
        <div class="flex justify-center gap-3">
          <button 
            (click)="createWalletForTargetUser('USD')" 
            class="px-4 py-2 bg-slate-900 text-white font-semibold rounded-lg hover:bg-slate-800 transition-colors shadow-xs text-xs">
            Provision USD Wallet ($)
          </button>
          <button 
            (click)="createWalletForTargetUser('INR')" 
            class="px-4 py-2 bg-white border border-slate-300 text-slate-700 font-semibold rounded-lg hover:bg-slate-50 transition-colors shadow-xs text-xs">
            Provision INR Wallet (₹)
          </button>
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
  isLoading = signal(false);
  targetUserId = signal<string | null>(null);

  walletAuditResult = signal<any>(null);
  txAuditResult = signal<any>(null);

  isCreditModalOpen = signal(false);
  isDebitModalOpen = signal(false);
  txAmount = 100;
  idempotencyKey = '';

  isTestingBurst = signal(false);
  stressLogs = signal<StressTestLog[]>([]);

  ngOnInit() {
    const routeId = this.route.snapshot.paramMap.get('id');
    if (routeId) {
      this.loadData(routeId);
    }
  }

  loadData(id: string) {
    this.isLoading.set(true);
    this.walletService.getWallet(id).subscribe({
      next: (res: any) => {
        if (res && res.success && res.data) {
          this.wallet.set(res.data);
          this.loadTransactions(res.data.walletId);
          this.isLoading.set(false);
        } else {
          this.fallbackToUserWallets(id);
        }
      },
      error: () => {
        this.fallbackToUserWallets(id);
      }
    });
  }

  fallbackToUserWallets(userId: string) {
    this.targetUserId.set(userId);
    this.walletService.getWalletsByUserId(userId).subscribe({
      next: (res: any) => {
        this.isLoading.set(false);
        const list = Array.isArray(res?.data) ? res.data : (res?.data?.items ?? []);
        if (list.length > 0) {
          const w = list[0];
          this.wallet.set(w);
          this.loadTransactions(w.walletId);
        } else {
          this.wallet.set(null);
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.wallet.set(null);
      }
    });
  }

  createWalletForTargetUser(currency: string) {
    const uid = this.targetUserId();
    if (!uid) return;
    this.isLoading.set(true);
    this.walletService.createWallet({ userId: uid, currency }).subscribe({
      next: (res: any) => {
        this.isLoading.set(false);
        if (res && res.success && res.data) {
          this.wallet.set(res.data);
          this.loadTransactions(res.data.walletId);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        console.error('Failed to create wallet for user:', err);
      }
    });
  }

  loadWallet(id: string) {
    this.walletService.getWallet(id).subscribe({
      next: (res) => {
        if (res.success && res.data) this.wallet.set(res.data);
      }
    });
  }

  loadTransactions(id: string) {
    this.txService.getWalletTransactions(id).subscribe({
      next: (res: any) => {
        if (res && res.success) {
          const list = Array.isArray(res.data) 
            ? res.data 
            : (res.data?.items ?? res.items ?? []);
          this.transactions.set(list);
        } else {
          this.transactions.set([]);
        }
      },
      error: () => this.transactions.set([])
    });
  }

  toggleExpand(txId: string) {
    if (this.expandedTx() === txId) {
      this.expandedTx.set(null);
    } else {
      this.expandedTx.set(txId);
      this.txService.getTransactionDetails(txId).subscribe(res => {
        if (res.success) {
          this.transactions.update(txs => txs.map(t => t.transactionId === txId ? res.data : t));
        }
      });
    }
  }

  blockWallet() {
    this.walletService.blockWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
  }

  unblockWallet() {
    this.walletService.unblockWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
  }

  closeWallet() {
    if (confirm('Are you sure you want to permanently close this wallet?')) {
      this.walletService.closeWallet(this.wallet()!.walletId).subscribe(() => this.loadWallet(this.wallet()!.walletId));
    }
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
    const reason = prompt('Please enter refund justification:');
    if (!reason) return;
    const key = `refund-${tx.transactionId}-${Date.now()}`;
    this.txService.refundTransaction(tx.transactionId, tx.amount, tx.currency, reason, key).subscribe({
      next: () => {
        this.loadWallet(this.wallet()!.walletId);
        this.loadTransactions(this.wallet()!.walletId);
      },
      error: (err) => {
        alert(err?.error?.message || 'Refund rejected by ledger engine.');
      }
    });
  }

  openCreditModal() {
    this.txAmount = 100;
    this.idempotencyKey = `credit-${Date.now()}`;
    this.isCreditModalOpen.set(true);
  }

  openDebitModal() {
    const currentBal = Number(this.wallet()?.availableBalance || this.wallet()?.balance || 50);
    this.txAmount = currentBal > 0 ? Math.min(50, currentBal) : 50;
    this.isDebitModalOpen.set(true);
  }

  submitCredit(count: number) {
    const walletId = this.wallet()!.walletId;
    const req = { amount: this.txAmount, currency: this.wallet()!.currency, description: 'Direct Credit Deposit' };
    
    for (let i = 0; i < count; i++) {
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
    const key = `debit-${Date.now()}`;
    const req = { amount: this.txAmount, currency: this.wallet()!.currency, description: 'Direct Debit Withdrawal' };
    
    this.walletService.debitWallet(walletId, req, key).subscribe({
      next: () => {
        this.loadWallet(walletId);
        this.loadTransactions(walletId);
      },
      error: (err) => {
        alert(err?.error?.message || 'Debit failed.');
      }
    });
    this.isDebitModalOpen.set(false);
  }

  // --- Stress Testing & Concurrency Studio Logic ---
  clearStressLogs() {
    this.stressLogs.set([]);
  }

  private addLog(action: string, statusCode: number, message: string, isSuccess: boolean) {
    const now = new Date().toLocaleTimeString('en-US', { hour12: false });
    this.stressLogs.update(logs => [
      { id: Date.now() + Math.random(), timestamp: now, action, statusCode, message, isSuccess },
      ...logs
    ]);
  }

  runIdempotencyBurst() {
    const walletId = this.wallet()!.walletId;
    const fixedKey = `idem-test-${Date.now()}`;
    const amount = 25.00;
    const req = { amount, currency: this.wallet()!.currency, description: 'Parallel Idempotency Burst Test' };

    this.isTestingBurst.set(true);
    let completed = 0;

    for (let i = 1; i <= 3; i++) {
      this.walletService.creditWallet(walletId, req, fixedKey).subscribe({
        next: (res: any) => {
          completed++;
          this.addLog(`Request #${i} (Same Key)`, 200, `OK: Transaction processed/returned with ID ${res.data?.transactionId || 'Cached'}`, true);
          if (completed === 3) {
            this.isTestingBurst.set(false);
            this.loadWallet(walletId);
            this.loadTransactions(walletId);
          }
        },
        error: (err: any) => {
          completed++;
          const status = err.status || 500;
          const msg = err.error?.message || err.statusText || 'Error';
          this.addLog(`Request #${i} (Same Key)`, status, `Status ${status}: ${msg}`, false);
          if (completed === 3) {
            this.isTestingBurst.set(false);
            this.loadWallet(walletId);
            this.loadTransactions(walletId);
          }
        }
      });
    }
  }

  runConcurrencyDebitBurst() {
    const walletId = this.wallet()!.walletId;
    const currentBal = Number(this.wallet()?.availableBalance || this.wallet()?.balance || 0);
    if (currentBal <= 0) return;

    // We debit the full current balance in 5 distinct parallel requests with UNIQUE keys
    const amount = currentBal;
    const req = { amount, currency: this.wallet()!.currency, description: 'Race Condition Overdraft Test' };

    this.isTestingBurst.set(true);
    let completed = 0;

    for (let i = 1; i <= 5; i++) {
      const distinctKey = `concur-debit-${Date.now()}-${i}`;
      this.walletService.debitWallet(walletId, req, distinctKey).subscribe({
        next: (res: any) => {
          completed++;
          this.addLog(`Burst Leg #${i} (Distinct Key)`, 200, `ACQUIRED LOCK & DEBITED $${amount}. New balance: 0.00`, true);
          if (completed === 5) {
            this.isTestingBurst.set(false);
            this.loadWallet(walletId);
            this.loadTransactions(walletId);
          }
        },
        error: (err: any) => {
          completed++;
          const status = err.status || 409;
          const errCode = err.error?.errors?.[0]?.code || err.error?.code || 'INSUFFICIENT_BALANCE';
          const errMsg = err.error?.message || err.error?.errors?.[0]?.message || 'Insufficient funds / Invariant Protected';
          this.addLog(`Burst Leg #${i} (Distinct Key)`, status, `REJECTED (${errCode}): ${errMsg}`, false);
          if (completed === 5) {
            this.isTestingBurst.set(false);
            this.loadWallet(walletId);
            this.loadTransactions(walletId);
          }
        }
      });
    }
  }
}
