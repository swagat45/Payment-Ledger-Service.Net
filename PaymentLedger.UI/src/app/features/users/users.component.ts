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
    <div>
      <div class="sm:flex sm:items-center">
        <div class="sm:flex-auto">
          <h1 class="text-xl font-semibold text-gray-900">Users</h1>
          <p class="mt-2 text-sm text-gray-700">A list of all users registered on the platform.</p>
        </div>
        <div class="mt-4 sm:mt-0 sm:ml-16 sm:flex-none">
          <button (click)="toggleCreateForm()" class="inline-flex items-center justify-center rounded-md border border-transparent bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 sm:w-auto">
            Add user
          </button>
        </div>
      </div>

      <!-- Create Form (Simple Toggle) -->
      <div *ngIf="showCreateForm()" class="mt-4 p-4 bg-gray-50 border border-gray-200 rounded-md">
        <h3 class="text-lg font-medium">Create New User</h3>
        <div class="mt-4 flex space-x-4">
          <input [(ngModel)]="newUserName" type="text" placeholder="Full Name" class="shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3">
          <input [(ngModel)]="newUserEmail" type="email" placeholder="Email Address" class="shadow-sm focus:ring-indigo-500 focus:border-indigo-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3">
          <button (click)="createUser()" class="bg-green-600 text-white px-4 py-2 rounded-md hover:bg-green-700">Submit</button>
        </div>
      </div>

      <div class="mt-8 flex flex-col">
        <div class="-my-2 -mx-4 overflow-x-auto sm:-mx-6 lg:-mx-8">
          <div class="inline-block min-w-full py-2 align-middle md:px-6 lg:px-8">
            <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 md:rounded-lg">
              <table class="min-w-full divide-y divide-gray-300">
                <thead class="bg-gray-50">
                  <tr>
                    <th scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">Name</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Email</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Status</th>
                    <th scope="col" class="relative py-3.5 pl-3 pr-4 sm:pr-6">
                      <span class="sr-only">View Wallet</span>
                    </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 bg-white">
                  <tr *ngFor="let user of users()">
                    <td class="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">{{ user.fullName }}</td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">{{ user.email }}</td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      <span class="inline-flex rounded-full bg-green-100 px-2 text-xs font-semibold leading-5 text-green-800">{{ user.status }}</span>
                    </td>
                    <td class="relative whitespace-nowrap py-4 pl-3 pr-4 text-right text-sm font-medium sm:pr-6">
                      <!-- In a real app we might lookup the wallet ID here, or go to a user detail page -->
                      <a [routerLink]="['/users', user.userId]" class="text-indigo-600 hover:text-indigo-900">View<span class="sr-only">, {{ user.fullName }}</span></a>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
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
