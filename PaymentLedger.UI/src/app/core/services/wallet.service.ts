import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { Wallet, CreateWalletRequest } from '../models/wallet.model';
import { CreditRequest, DebitRequest, Transaction } from '../models/transaction.model';

@Injectable({
  providedIn: 'root'
})
export class WalletService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/wallets`;

  createWallet(request: CreateWalletRequest): Observable<ApiResponse<Wallet>> {
    return this.http.post<ApiResponse<Wallet>>(this.baseUrl, request);
  }

  getWallet(walletId: string): Observable<ApiResponse<Wallet>> {
    return this.http.get<ApiResponse<Wallet>>(`${this.baseUrl}/${walletId}`);
  }

  getWalletBalance(walletId: string): Observable<ApiResponse<Wallet>> {
    return this.http.get<ApiResponse<Wallet>>(`${this.baseUrl}/${walletId}/balance`);
  }

  creditWallet(walletId: string, request: CreditRequest, idempotencyKey: string): Observable<ApiResponse<Transaction>> {
    const headers = new HttpHeaders().set('Idempotency-Key', idempotencyKey);
    return this.http.post<ApiResponse<Transaction>>(`${this.baseUrl}/${walletId}/credit`, request, { headers });
  }

  debitWallet(walletId: string, request: DebitRequest, idempotencyKey: string): Observable<ApiResponse<Transaction>> {
    const headers = new HttpHeaders().set('Idempotency-Key', idempotencyKey);
    return this.http.post<ApiResponse<Transaction>>(`${this.baseUrl}/${walletId}/debit`, request, { headers });
  }

  blockWallet(walletId: string): Observable<ApiResponse<Wallet>> {
    return this.http.patch<ApiResponse<Wallet>>(`${this.baseUrl}/${walletId}/block`, {});
  }

  unblockWallet(walletId: string): Observable<ApiResponse<Wallet>> {
    return this.http.patch<ApiResponse<Wallet>>(`${this.baseUrl}/${walletId}/unblock`, {});
  }

  closeWallet(walletId: string): Observable<ApiResponse<Wallet>> {
    return this.http.patch<ApiResponse<Wallet>>(`${this.baseUrl}/${walletId}/close`, {});
  }

  verifyWalletConsistency(walletId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${this.baseUrl}/${walletId}/verify`);
  }
}
