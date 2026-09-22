export interface Wallet {
  walletId: string;
  userId: string;
  currency: string;
  balance: number;
  availableBalance?: number; // Depending on API response
  status: 'Active' | 'Blocked' | 'Closed';
  createdAt: string;
  updatedAt?: string;
}

export interface CreateWalletRequest {
  userId: string;
  currency: string;
}
