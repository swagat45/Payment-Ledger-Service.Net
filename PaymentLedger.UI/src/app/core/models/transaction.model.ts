export interface LedgerEntry {
  ledgerEntryId: number;
  transactionId: string;
  walletId: string;
  entryType: 'Credit' | 'Debit';
  amount: number;
  currency: string;
  balanceAfterTransaction?: number;
  createdAt: string;
}

export interface Transaction {
  transactionId: string;
  transactionCode: string;
  type: 'Credit' | 'Debit' | 'Refund';
  status: 'Pending' | 'Posted' | 'Failed' | 'Refunded' | 'PartiallyRefunded' | 'Reversed';
  sourceWalletId: string;
  destinationWalletId: string;
  amount: number;
  currency: string;
  idempotencyKey: string;
  referenceTransactionId?: string;
  description?: string;
  metadata?: any;
  createdAt: string;
  updatedAt?: string;
  ledgerEntries?: LedgerEntry[];
}

export interface CreditRequest {
  amount: number;
  currency: string;
  description: string;
  metadata?: any;
}

export interface DebitRequest {
  amount: number;
  currency: string;
  description: string;
  metadata?: any;
}
