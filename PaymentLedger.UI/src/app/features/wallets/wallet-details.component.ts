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
    <div *ngIf="wallet()">
      <!-- Header -->
      <div class="md:flex md:items-center md:justify-between mb-8">
        <div class="min-w-0 flex-1">
          <h2 class="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
            Wallet View
          </h2>
          <p class="text-sm text-gray-500 mt-1">ID: {{ wallet()?.walletId }}</p>
        </div>
        <div class="mt-4 flex md:ml-4 md:mt-0 space-x-3">
          <!-- Wallet Actions -->
          <button *ngIf="wallet()?.status === 'Active'" (click)="blockWallet()" type="button" class="inline-flex items-center rounded-md bg-yellow-100 px-3 py-2 text-sm font-semibold text-yellow-800 shadow-sm hover:bg-yellow-200">
            Block Wallet
          </button>
          <button *ngIf="wallet()?.status === 'Blocked'" (click)="unblockWallet()" type="button" class="inline-flex items-center rounded-md bg-green-100 px-3 py-2 text-sm font-semibold text-green-800 shadow-sm hover:bg-green-200">
            Unblock Wallet
          </button>
          <button *ngIf="wallet()?.status !== 'Closed'" (click)="closeWallet()" type="button" class="inline-flex items-center rounded-md bg-red-100 px-3 py-2 text-sm font-semibold text-red-800 shadow-sm hover:bg-red-200">
            Close Wallet
          </button>

          <!-- Showcase Actions -->
          <button (click)="openCreditModal()" type="button" class="inline-flex items-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600">
            Showcase: Credit & Idempotency
          </button>
          <button (click)="openDebitModal()" type="button" class="inline-flex items-center rounded-md bg-blue-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-blue-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600">
            Showcase: Debit & Concurrency
          </button>
        </div>
      </div>

      <!-- Balance Card -->
      <div class="bg-white overflow-hidden shadow rounded-lg mb-8 border border-gray-200">
        <div class="px-4 py-5 sm:p-6 flex justify-between items-center">
          <div>
            <dt class="text-sm font-medium text-gray-500 truncate">Available Balance</dt>
            <dd class="mt-1 text-4xl font-semibold text-gray-900">
              {{ wallet()?.availableBalance || wallet()?.balance | currency:wallet()?.currency }}
            </dd>
          </div>
          <div class="flex items-center space-x-4">
            <button (click)="verifyWallet()" class="text-sm text-indigo-600 hover:text-indigo-900 border border-indigo-200 rounded px-2 py-1">Verify Consistency</button>
            <span class="inline-flex items-center rounded-full bg-green-100 px-3 py-0.5 text-sm font-medium text-green-800">
              {{ wallet()?.status }}
            </span>
          </div>
        </div>
      </div>

      <div *ngIf="walletAuditResult()" class="mb-8 p-4 bg-gray-50 border border-gray-200 rounded-md">
        <h3 class="font-bold text-gray-800">Wallet Audit Result:</h3>
        <pre class="text-sm text-gray-600 mt-2 whitespace-pre-wrap">{{ walletAuditResult() | json }}</pre>
        <button (click)="walletAuditResult.set(null)" class="mt-2 text-sm text-red-600">Close</button>
      </div>

      <!-- Modals (Simple implementation) -->
      <div *ngIf="isCreditModalOpen()" class="fixed inset-0 bg-gray-500 bg-opacity-75 flex items-center justify-center p-4 z-50">
        <div class="bg-white rounded-lg p-6 max-w-md w-full">
          <h3 class="text-lg font-medium mb-4">Credit Wallet (Idempotency Test)</h3>
          <input [(ngModel)]="txAmount" type="number" placeholder="Amount" class="mb-4 shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3">
          <input [(ngModel)]="idempotencyKey" type="text" placeholder="Idempotency Key (e.g. credit-001)" class="mb-4 shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3">
          
          <div class="flex justify-end space-x-3">
            <button (click)="isCreditModalOpen.set(false)" class="px-4 py-2 bg-gray-200 text-gray-800 rounded hover:bg-gray-300">Cancel</button>
            <button (click)="submitCredit(1)" class="px-4 py-2 bg-indigo-600 text-white rounded hover:bg-indigo-700">Submit (1x)</button>
            <button (click)="submitCredit(3)" class="px-4 py-2 bg-purple-600 text-white rounded hover:bg-purple-700" title="Fires 3 concurrent requests with same key to test Idempotency">Submit (3x Burst)</button>
          </div>
        </div>
      </div>

      <div *ngIf="isDebitModalOpen()" class="fixed inset-0 bg-gray-500 bg-opacity-75 flex items-center justify-center p-4 z-50">
        <div class="bg-white rounded-lg p-6 max-w-md w-full">
          <h3 class="text-lg font-medium mb-4">Debit Wallet (Concurrency Test)</h3>
          <input [(ngModel)]="txAmount" type="number" placeholder="Amount" class="mb-4 shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3">
          <p class="text-xs text-gray-500 mb-4">Hint: To test concurrency locking, enter an amount equal to your total balance and hit the 5x Burst button.</p>
          <div class="flex justify-end space-x-3">
            <button (click)="isDebitModalOpen.set(false)" class="px-4 py-2 bg-gray-200 text-gray-800 rounded hover:bg-gray-300">Cancel</button>
            <button (click)="submitDebit(1)" class="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Submit (1x)</button>
            <button (click)="submitDebit(5)" class="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700" title="Fires 5 concurrent requests with DIFFERENT keys to test DB locking">Submit (5x Burst)</button>
          </div>
        </div>
      </div>

      <!-- Transactions Table -->
      <div class="mt-8">
        <h3 class="text-lg font-medium leading-6 text-gray-900 mb-4">Transaction History</h3>
        <div class="flex flex-col">
          <div class="-my-2 -mx-4 overflow-x-auto sm:-mx-6 lg:-mx-8">
            <div class="inline-block min-w-full py-2 align-middle md:px-6 lg:px-8">
              <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 md:rounded-lg">
                <table class="min-w-full divide-y divide-gray-300">
                  <thead class="bg-gray-50">
                    <tr>
                      <th scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">Date</th>
                      <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Type</th>
                      <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Amount</th>
                      <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Status</th>
                      <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Reference</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-200 bg-white">
                    <ng-container *ngFor="let tx of transactions()">
                      <!-- Main Row -->
                      <tr class="hover:bg-gray-50 cursor-pointer" (click)="toggleExpand(tx.transactionId)">
                        <td class="whitespace-nowrap py-4 pl-4 pr-3 text-sm text-gray-500 sm:pl-6">{{ tx.createdAt | date:'short' }}</td>
                        <td class="whitespace-nowrap px-3 py-4 text-sm font-medium">
                           <span [ngClass]="{'text-green-600': tx.type === 'Credit', 'text-red-600': tx.type === 'Debit'}">
                             {{ tx.type }}
                           </span>
                        </td>
                        <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-900 font-semibold">{{ tx.amount | currency:tx.currency }}</td>
                        <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                          <span class="inline-flex items-center rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
                            {{ tx.status }}
                          </span>
                        </td>
                        <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500 font-mono">{{ tx.transactionCode || 'N/A' }}</td>
                      </tr>
                      
                      <!-- Expanded Ledger Entries View -->
                      <tr *ngIf="expandedTx() === tx.transactionId" class="bg-gray-100">
                        <td colspan="5" class="px-6 py-4">
                          <div class="text-sm border border-gray-300 rounded p-4 bg-white shadow-inner">
                            <div class="flex justify-between items-center mb-4">
                              <h4 class="font-bold">Double-Entry Ledger Records</h4>
                              <div class="space-x-2">
                                <button *ngIf="tx.type !== 'Refund' && tx.status === 'Posted'" (click)="refundTransaction(tx); $event.stopPropagation()" class="px-3 py-1 bg-yellow-500 text-white rounded text-xs hover:bg-yellow-600">Refund</button>
                                <button (click)="verifyTransaction(tx.transactionId); $event.stopPropagation()" class="px-3 py-1 bg-gray-600 text-white rounded text-xs hover:bg-gray-700">Verify</button>
                              </div>
                            </div>

                            <div *ngIf="txAuditResult() && txAuditResult().transactionId === tx.transactionId" class="mb-4 p-2 bg-blue-50 border border-blue-200 rounded">
                               <pre class="text-xs">{{ txAuditResult() | json }}</pre>
                               <button (click)="txAuditResult.set(null)" class="text-red-500 text-xs mt-1">Close Audit</button>
                            </div>

                            <div class="grid grid-cols-2 gap-4">
                              <div *ngFor="let entry of tx.ledgerEntries" class="border p-2 rounded" [ngClass]="{'border-green-300 bg-green-50': entry.entryType === 'Credit', 'border-red-300 bg-red-50': entry.entryType === 'Debit'}">
                                <p class="font-semibold">{{ entry.entryType }}</p>
                                <p class="text-gray-600 font-mono text-xs">Wallet: {{ entry.walletId }}</p>
                                <p class="text-lg font-bold">{{ entry.amount | currency:entry.currency }}</p>
                              </div>
                            </div>
                            <div *ngIf="!tx.ledgerEntries || tx.ledgerEntries.length === 0" class="text-gray-500 italic text-center p-2">
                              Loading ledger entries or none available...
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
