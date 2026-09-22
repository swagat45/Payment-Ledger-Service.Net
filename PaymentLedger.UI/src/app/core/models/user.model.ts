export interface User {
  userId: string;
  fullName: string;
  email: string;
  status: 'Active' | 'Inactive' | 'Suspended';
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
}
