import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UserService } from '../../core/services/user.service';
import { WalletService } from '../../core/services/wallet.service';
import { User } from '../../core/models/user.model';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="space-y-6">
      
      <!-- Top Action Bar -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-2 border-b border-slate-200">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Customer Accounts</h1>
            <span class="text-xs font-mono font-semibold bg-slate-100 text-slate-700 px-2 py-0.5 rounded-full border border-slate-200">
              {{ users().length }} records
            </span>
          </div>
          <p class="mt-1 text-sm text-slate-500">Manage registered entities, provision multi-currency digital wallets, and inspect balance ledgers.</p>
        </div>
        
        <div class="flex items-center gap-3">
          <button 
            (click)="loadUsers()" 
            [disabled]="isLoading()"
            class="inline-flex items-center justify-center rounded-lg bg-white border border-slate-300 px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50 hover:border-slate-400 transition-all disabled:opacity-50">
            <svg class="w-3.5 h-3.5 mr-1.5" [class.animate-spin]="isLoading()" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </button>
          
          <button 
            (click)="toggleCreateModal()" 
            class="inline-flex items-center justify-center rounded-lg bg-slate-900 px-4 py-2 text-xs font-semibold text-white shadow-xs hover:bg-slate-800 transition-colors">
            <svg class="w-3.5 h-3.5 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Register Account
          </button>
        </div>
      </div>

      <!-- Toast Feedback -->
      <div *ngIf="errorMessage()" class="p-3.5 bg-rose-50 border border-rose-200 text-rose-800 rounded-lg flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <svg class="w-4 h-4 text-rose-500 shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
          </svg>
          <span class="font-medium">{{ errorMessage() }}</span>
        </div>
        <button (click)="errorMessage.set(null)" class="text-rose-500 hover:text-rose-700 text-sm font-bold ml-2">✕</button>
      </div>

      <div *ngIf="successMessage()" class="p-3.5 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <svg class="w-4 h-4 text-emerald-500 shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
          </svg>
          <span class="font-medium">{{ successMessage() }}</span>
        </div>
        <button (click)="successMessage.set(null)" class="text-emerald-500 hover:text-emerald-700 text-sm font-bold ml-2">✕</button>
      </div>

      <!-- Search & Filter Controls -->
      <div class="flex items-center justify-between gap-4">
        <div class="relative flex-1 max-w-sm">
          <span class="absolute inset-y-0 left-0 flex items-center pl-3 text-slate-400">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </span>
          <input 
            [(ngModel)]="searchQuery" 
            type="text" 
            placeholder="Search by name, email, or principal ID..." 
            class="pl-9 pr-4 py-2 w-full text-xs bg-white border border-slate-200 rounded-lg text-slate-800 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
        </div>
      </div>

      <!-- Account Table (Full Width Enterprise Style) -->
      <div class="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden">
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-left text-xs">
            <thead class="bg-slate-50 text-slate-500 font-semibold uppercase tracking-wider">
              <tr>
                <th scope="col" class="py-3 px-6">Entity / Customer Profile</th>
                <th scope="col" class="py-3 px-6">Principal Identifier</th>
                <th scope="col" class="py-3 px-6">Status</th>
                <th scope="col" class="py-3 px-6">Created At</th>
                <th scope="col" class="py-3 px-6 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 text-slate-700 bg-white">
              <tr *ngFor="let user of filteredUsers()" class="hover:bg-slate-50/75 transition-colors">
                
                <!-- Profile -->
                <td class="py-4 px-6">
                  <div class="flex items-center gap-3">
                    <div class="w-8 h-8 rounded-lg bg-slate-100 border border-slate-200 text-slate-700 font-bold flex items-center justify-center shrink-0">
                      {{ (user.fullName || 'U').charAt(0).toUpperCase() }}
                    </div>
                    <div>
                      <div class="font-bold text-slate-900 text-xs">{{ user.fullName }}</div>
                      <div class="text-[11px] text-slate-500">{{ user.email }}</div>
                    </div>
                  </div>
                </td>

                <!-- ID -->
                <td class="py-4 px-6">
                  <div class="flex items-center gap-2">
                    <span class="font-mono text-xs text-slate-600 bg-slate-100 px-2 py-0.5 rounded border border-slate-200/80">
                      {{ (user.userId || user.id) }}
                    </span>
                    <button 
                      (click)="copyToClipboard(user.userId || user.id)"
                      title="Copy User ID"
                      class="text-slate-400 hover:text-slate-700 transition-colors p-1 rounded">
                      <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7v8a2 2 0 002 2h6M8 7V5a2 2 0 012-2h4.586a1 1 0 01.707.293l4.414 4.414a1 1 0 01.293.707V15a2 2 0 01-2 2h-2M8 7H6a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2v-2" />
                      </svg>
                    </button>
                  </div>
                </td>

                <!-- Status -->
                <td class="py-4 px-6">
                  <span 
                    class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold"
                    [ngClass]="user.status === 'Active' ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-slate-100 text-slate-600 border border-slate-200'">
                    <span class="w-1.5 h-1.5 rounded-full" [ngClass]="user.status === 'Active' ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                    {{ user.status }}
                  </span>
                </td>

                <!-- Created Date -->
                <td class="py-4 px-6 text-slate-500 font-mono text-[11px]">
                  {{ user.createdAt ? (user.createdAt | date:'mediumDate') : 'System Initialized' }}
                </td>

                <!-- Action Button -->
                <td class="py-4 px-6 text-right">
                  <a 
                    [routerLink]="['/users', user.userId || user.id]" 
                    class="inline-flex items-center gap-1 px-3 py-1.5 rounded-md text-xs font-semibold text-indigo-700 bg-indigo-50 border border-indigo-100 hover:bg-indigo-100 transition-colors">
                    <span>Inspect Ledger</span>
                    <span class="text-[10px]">&rarr;</span>
                  </a>
                </td>
              </tr>

              <!-- Empty State -->
              <tr *ngIf="!isLoading() && filteredUsers().length === 0">
                <td colspan="5" class="py-12 text-center text-slate-500">
                  <svg class="w-10 h-10 mx-auto text-slate-300 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                  <p class="font-medium text-slate-700 text-sm">No accounts found</p>
                  <p class="text-xs text-slate-400 mt-0.5">Try adjusting your search criteria or register a new account.</p>
                </td>
              </tr>

              <!-- Loading State -->
              <tr *ngIf="isLoading()">
                <td colspan="5" class="py-12 text-center text-slate-500">
                  <div class="inline-block animate-spin rounded-full h-6 w-6 border-b-2 border-indigo-600 mb-2"></div>
                  <p class="text-xs">Fetching registered users from ledger repository...</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Registration Modal Drawer -->
      <div 
        *ngIf="showCreateModal()" 
        class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
        <div class="bg-white rounded-xl max-w-lg w-full shadow-2xl border border-slate-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50/50">
            <div>
              <h3 class="text-sm font-bold text-slate-900">Register Customer Account</h3>
              <p class="text-xs text-slate-500">Creates an authenticated principal and provisions an initial active digital wallet.</p>
            </div>
            <button (click)="toggleCreateModal()" class="text-slate-400 hover:text-slate-600 text-sm font-bold">✕</button>
          </div>

          <form (ngSubmit)="createUser()" class="p-6 space-y-4">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Full Legal Name</label>
              <input 
                [(ngModel)]="newUserName" 
                name="newUserName"
                type="text" 
                placeholder="e.g. Alice Mercer" 
                required
                class="w-full text-xs px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Email Address</label>
              <input 
                [(ngModel)]="newUserEmail" 
                name="newUserEmail"
                type="email" 
                placeholder="e.g. alice@enterprise.com" 
                required
                class="w-full text-xs px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs">
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Primary Settlement Currency</label>
              <select 
                [(ngModel)]="selectedCurrency" 
                name="selectedCurrency"
                class="w-full text-xs px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 focus:outline-hidden focus:ring-2 focus:ring-slate-900 focus:border-slate-900 shadow-xs font-medium">
                <option value="USD">USD - United States Dollar ($)</option>
                <option value="INR">INR - Indian Rupee (₹)</option>
              </select>
              <p class="text-[11px] text-slate-400 mt-1">Multi-currency wallets can also be provisioned dynamically per user.</p>
            </div>

            <div class="pt-3 border-t border-slate-100 flex items-center justify-end gap-3">
              <button 
                type="button" 
                (click)="toggleCreateModal()" 
                class="px-4 py-2 rounded-lg text-xs font-semibold text-slate-700 hover:bg-slate-100 transition-colors">
                Cancel
              </button>
              <button 
                type="submit" 
                [disabled]="isSubmitting()"
                class="px-4 py-2 rounded-lg text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 transition-colors shadow-xs disabled:opacity-50 flex items-center gap-1.5">
                <svg *ngIf="isSubmitting()" class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                <span>{{ isSubmitting() ? 'Provisioning...' : 'Provision Account' }}</span>
              </button>
            </div>
          </form>
        </div>
      </div>

    </div>
  `
})
export class UsersComponent implements OnInit {
  userService = inject(UserService);
  walletService = inject(WalletService);
  
  users = signal<User[]>([]);
  showCreateModal = signal(false);
  isLoading = signal(false);
  isSubmitting = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  searchQuery = '';
  
  newUserName = '';
  newUserEmail = '';
  selectedCurrency = 'USD';

  filteredUsers = computed(() => {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return this.users();
    return this.users().filter(u => 
      (u.fullName && u.fullName.toLowerCase().includes(q)) ||
      (u.email && u.email.toLowerCase().includes(q)) ||
      (u.userId && u.userId.toLowerCase().includes(q)) ||
      (u.id && u.id.toLowerCase().includes(q))
    );
  });

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.userService.getUsers(1, 50).subscribe({
      next: (res: any) => {
        this.isLoading.set(false);
        if (res && res.success) {
          const list = Array.isArray(res.data) 
            ? res.data 
            : (res.data?.items ?? res.items ?? []);
          this.users.set(list);
        } else {
          this.users.set([]);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = err?.error?.message || 'Failed to connect to the backend service.';
        this.errorMessage.set(msg);
      }
    });
  }

  toggleCreateModal() {
    this.showCreateModal.update(v => !v);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  copyToClipboard(text?: string) {
    if (text) {
      navigator.clipboard.writeText(text);
      this.successMessage.set(`Copied ID to clipboard: ${text}`);
      setTimeout(() => this.successMessage.set(null), 2500);
    }
  }

  createUser() {
    const name = this.newUserName.trim();
    const email = this.newUserEmail.trim();

    if (!name || !email) {
      this.errorMessage.set('Please provide both Full Name and Email Address.');
      return;
    }
    
    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.userService.createUser({ fullName: name, email: email }).subscribe({
      next: (res: any) => {
        if (res && res.success && res.data) {
          const newUserId = res.data.userId || res.data.id;
          // Automatically seed an active wallet in selected currency
          this.walletService.createWallet({ userId: newUserId, currency: this.selectedCurrency }).subscribe({
            next: () => {
              this.isSubmitting.set(false);
              this.successMessage.set(`Account '${name}' and ${this.selectedCurrency} wallet provisioned successfully!`);
              this.newUserName = '';
              this.newUserEmail = '';
              this.showCreateModal.set(false);
              this.loadUsers();
            },
            error: () => {
              this.isSubmitting.set(false);
              this.successMessage.set(`User '${name}' created successfully.`);
              this.newUserName = '';
              this.newUserEmail = '';
              this.showCreateModal.set(false);
              this.loadUsers();
            }
          });
        } else {
          this.isSubmitting.set(false);
          this.loadUsers();
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = err?.error?.message || (err?.error?.errors ? err.error.errors.map((e: any) => e.message).join(', ') : 'Failed to register user.');
        this.errorMessage.set(msg);
      }
    });
  }
}
