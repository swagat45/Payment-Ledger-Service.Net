import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UserService } from '../../core/services/user.service';
import { User } from '../../core/models/user.model';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-8 gap-4">
        <div>
          <h1 class="text-3xl font-extrabold tracking-tight text-slate-900">User Directory</h1>
          <p class="mt-2 text-sm text-slate-500">Manage all registered users and access their financial wallets.</p>
        </div>
        <div>
          <button (click)="toggleCreateForm()" class="inline-flex items-center justify-center rounded-xl bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white shadow-md hover:bg-slate-800 transition-all focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2">
            <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
            Add New User
          </button>
        </div>
      </div>

      <!-- Create Form (Glass Toggle) -->
      <div *ngIf="showCreateForm()" class="mb-8 p-6 bg-white/70 backdrop-blur-xl border border-slate-200/60 rounded-3xl shadow-sm animate-in slide-in-from-top-4 duration-300">
        <div class="flex justify-between items-center mb-4">
           <h3 class="text-lg font-bold text-slate-900">Register User</h3>
           <button (click)="toggleCreateForm()" class="text-slate-400 hover:text-slate-600"><svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg></button>
        </div>
        <div class="flex flex-col sm:flex-row gap-4">
          <input [(ngModel)]="newUserName" type="text" placeholder="Full Name" class="flex-1 bg-white border border-slate-200 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 rounded-xl py-2.5 px-4 text-sm font-medium transition-shadow shadow-sm">
          <input [(ngModel)]="newUserEmail" type="email" placeholder="Email Address" class="flex-1 bg-white border border-slate-200 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 rounded-xl py-2.5 px-4 text-sm font-medium transition-shadow shadow-sm">
          <button (click)="createUser()" class="bg-indigo-600 text-white font-semibold px-6 py-2.5 rounded-xl hover:bg-indigo-500 transition-colors shadow-md">Create &rarr;</button>
        </div>
      </div>

      <div class="bg-white/70 backdrop-blur-xl shadow-sm border border-slate-200/60 rounded-3xl overflow-hidden">
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200/60">
            <thead class="bg-slate-50/50">
              <tr>
                <th scope="col" class="py-4 pl-6 pr-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">User Profile</th>
                <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">System ID</th>
                <th scope="col" class="px-3 py-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                <th scope="col" class="px-6 py-4 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 bg-white/50">
              <tr *ngFor="let user of users()" class="hover:bg-slate-50/80 transition-colors group">
                <td class="whitespace-nowrap py-5 pl-6 pr-3">
                  <div class="flex items-center gap-4">
                    <div class="h-10 w-10 rounded-full bg-gradient-to-br from-indigo-100 to-purple-100 flex items-center justify-center text-indigo-700 font-bold border border-indigo-200 shadow-sm group-hover:scale-105 transition-transform">
                      {{ user.fullName.charAt(0) }}
                    </div>
                    <div>
                      <div class="text-sm font-bold text-slate-900">{{ user.fullName }}</div>
                      <div class="text-xs text-slate-500">{{ user.email }}</div>
                    </div>
                  </div>
                </td>
                <td class="whitespace-nowrap px-3 py-5 text-xs text-slate-400 font-mono">
                  {{ user.userId }}
                </td>
                <td class="whitespace-nowrap px-3 py-5 text-sm">
                  <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset"
                    [ngClass]="user.status === 'Active' ? 'bg-emerald-50 text-emerald-700 ring-emerald-600/20' : 'bg-slate-50 text-slate-600 ring-slate-500/20'">
                    <div class="w-1.5 h-1.5 rounded-full" [ngClass]="user.status === 'Active' ? 'bg-emerald-500' : 'bg-slate-400'"></div>
                    {{ user.status }}
                  </span>
                </td>
                <td class="whitespace-nowrap py-5 pl-3 pr-6 text-right text-sm font-medium">
                  <a [routerLink]="['/users', user.userId]" class="inline-flex items-center text-indigo-600 hover:text-indigo-900 font-bold">
                    View Wallet
                    <svg class="w-4 h-4 ml-1 group-hover:translate-x-1 transition-transform" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" /></svg>
                  </a>
                </td>
              </tr>
              <tr *ngIf="users().length === 0">
                <td colspan="4" class="py-12 text-center text-slate-500">
                  <svg class="w-12 h-12 mx-auto text-slate-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>
                  <p>No users found in the system.</p>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class UsersComponent implements OnInit {
  userService = inject(UserService);
  
  users = signal<User[]>([]);
  showCreateForm = signal(false);
  
  newUserName = '';
  newUserEmail = '';

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.userService.getUsers().subscribe(res => {
      if (res.success) {
        this.users.set(res.data.items);
      }
    });
  }

  toggleCreateForm() {
    this.showCreateForm.update(v => !v);
  }

  createUser() {
    if (!this.newUserName || !this.newUserEmail) return;
    
    this.userService.createUser({ fullName: this.newUserName, email: this.newUserEmail }).subscribe(res => {
      if (res.success) {
        this.newUserName = '';
        this.newUserEmail = '';
        this.showCreateForm.set(false);
        this.loadUsers();
      }
    });
  }
}
