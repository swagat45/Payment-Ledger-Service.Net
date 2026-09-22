import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div>
      <h1 class="text-2xl font-semibold text-gray-900 mb-6">Platform Operations</h1>
      
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-3">
        <!-- Card 1 -->
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Total Users</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">0</dd>
          </div>
        </div>
        
        <!-- Card 2 -->
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Platform Wallets</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">0</dd>
          </div>
        </div>

        <!-- Card 3 -->
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Treasury Balance</dt>
            <dd class="mt-1 text-3xl font-semibold text-green-600">--</dd>
          </div>
        </div>
      </div>

      <div class="mt-8">
        <h2 class="text-lg leading-6 font-medium text-gray-900 mb-4">Quick Links</h2>
        <div class="flex space-x-4">
          <a routerLink="/users" class="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md shadow-sm text-white bg-indigo-600 hover:bg-indigo-700">
            Manage Users
          </a>
        </div>
      </div>
    </div>
  `
})
export class DashboardComponent {
  // We can hook this up to a stats endpoint later if it exists
}
