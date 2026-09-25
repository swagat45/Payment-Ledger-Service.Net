import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResponse } from '../models/api-response.model';
import { Transaction } from '../models/transaction.model';

@Injectable({
  providedIn: 'root'
})
export class TransactionService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getWalletTransactions(walletId: string, pageNumber: number = 1, pageSize: number = 20): Observable<ApiResponse<PagedResponse<Transaction>>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());
      
    return this.http.get<ApiResponse<PagedResponse<Transaction>>>(`${this.baseUrl}/wallets/${walletId}/transactions`, { params });
  }

  getTransactionDetails(transactionId: string): Observable<ApiResponse<Transaction>> {
    return this.http.get<ApiResponse<Transaction>>(`${this.baseUrl}/transactions/${transactionId}`);
  }

  refundTransaction(transactionId: string, amount: number, currency: string, reason: string, idempotencyKey: string): Observable<ApiResponse<Transaction>> {
    const headers = new HttpHeaders().set('Idempotency-Key', idempotencyKey);
    const body = { amount, currency, reason };
    return this.http.post<ApiResponse<Transaction>>(`${this.baseUrl}/transactions/${transactionId}/refund`, body, { headers });
  }

  verifyTransactionConsistency(transactionId: string): Observable<ApiResponse<any>> {
    return this.http.get<ApiResponse<any>>(`${this.baseUrl}/transactions/${transactionId}/verify`);
  }
}
