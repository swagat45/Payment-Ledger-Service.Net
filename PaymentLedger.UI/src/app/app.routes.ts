import { Routes } from '@angular/router';
import { LayoutComponent } from './shared/components/layout/layout.component';

export const routes: Routes = [
  {
    path: '',
    component: LayoutComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent)
      },
      {
        path: 'users/:id', // Assuming wallet ID is mapped or fetched via user ID, or we just pass wallet ID
        loadComponent: () => import('./features/wallets/wallet-details.component').then(m => m.WalletDetailsComponent)
      }
    ]
  }
];
