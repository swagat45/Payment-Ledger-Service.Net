import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { WalletService } from '../../core/services/wallet.service';
import { TransactionService } from '../../core/services/transaction.service';
import { Wallet } from '../../core/models/wallet.model';
import { Transaction } from '../../core/models/transaction.model';

@Component({
  selector: 'app-wallet-details',
  standalone: true,
  imports: [CommonModule],
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
          <button type="button" class="inline-flex items-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50">
            Showcase: Concurrency Test
          </button>
          <button type="button" class="inline-flex items-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600">
            Showcase: Idempotency Credit
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
          <div>
            <span class="inline-flex items-center rounded-full bg-green-100 px-3 py-0.5 text-sm font-medium text-green-800">
              {{ wallet()?.status }}
            </span>
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
                            <h4 class="font-bold mb-2">Double-Entry Ledger Records</h4>
                            <div class="grid grid-cols-2 gap-4">
                              <div *ngFor="let entry of tx.ledgerEntries" class="border p-2 rounded" [ngClass]="{'border-green-300 bg-green-50': entry.entryType === 'Credit', 'border-red-300 bg-red-50': entry.entryType === 'Debit'}">
                                <p class="font-semibold">{{ entry.entryType }}</p>
                                <p class="text-gray-600 font-mono text-xs">Wallet: {{ entry.walletId }}</p>
                                <p class="text-lg font-bold">{{ entry.amount | currency:entry.currency }}</p>
                              </div>
                            </div>
                            <div *ngIf="!tx.ledgerEntries || tx.ledgerEntries.length === 0" class="text-gray-500 italic text-center p-2">
                              No ledger entries loaded. Ensure backend includes them in transaction details API.
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
      // In a real app, we might need to fetch transaction details to get the ledger entries if they aren't included in the paginated response.
      // this.txService.getTransactionDetails(txId).subscribe(res => { ... update transactions array with ledger entries ... })
    }
  }
}
