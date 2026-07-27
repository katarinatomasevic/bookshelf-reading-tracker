export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  token: string;
  expiresAt: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
}

export interface UpdateProfileRequest {
  displayName: string;
  currentPassword: string | null;
  newPassword: string | null;
}
