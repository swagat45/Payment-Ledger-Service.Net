export interface ErrorDetail {
  code: string;
  field?: string;
  message: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors: ErrorDetail[] | null;
  correlationId: string;
}

export interface PagedResponse<T> extends ApiResponse<T[]> {
  items?: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}
