import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, UrlTree } from '@angular/router';
import { ParamMap } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.model';

const STORAGE_KEY = 'bookshelf_auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly authState = signal<AuthResponse | null>(this.readStoredAuth());

  readonly isLoggedIn = computed(() => this.authState() !== null);
  readonly currentUser = computed(() => this.authState());

  readonly sessionExpired = signal(false);
  private sessionExpiredReturnUrl = '/';

  constructor() {
    this.clearIfExpired();
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((response) => this.setAuth(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, request)
      .pipe(tap((response) => this.setAuth(response)));
  }

  /**
   * Explicit, user-facing logout. Guards only re-run on navigation, so clearing the auth
   * state alone would leave a currently-open protected page (e.g. /profile) rendered —
   * this always navigates away too.
   */
  logout(): void {
    this.clearAuth();
    this.router.navigateByUrl('/');
  }

  getToken(): string | null {
    return this.authState()?.token ?? null;
  }

  /**
   * Keeps the locally-stored auth snapshot (and therefore the navbar, which reads
   * `currentUser()`) in sync after the profile page changes the display name — the
   * JWT itself still carries the old name until the next login, but the app's view
   * of "who am I" shouldn't wait for that.
   */
  updateDisplayName(displayName: string): void {
    const current = this.authState();
    if (!current) {
      return;
    }
    const updated: AuthResponse = { ...current, displayName };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
    this.authState.set(updated);
  }

  /** Called by the auth interceptor on a 401. Guarded against bursts of parallel failing requests. */
  handleSessionExpired(): void {
    if (this.sessionExpired()) {
      return;
    }
    this.sessionExpiredReturnUrl = this.router.url;
    this.clearAuth();
    this.sessionExpired.set(true);
  }

  /** Called by the session-expired modal's OK button. */
  acknowledgeSessionExpired(): void {
    const returnUrl = this.sessionExpiredReturnUrl;
    this.sessionExpired.set(false);
    this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  /** Single place that knows how the returnUrl query param is named and read. */
  buildLoginRedirect(returnUrl: string): UrlTree {
    return this.router.createUrlTree(['/login'], { queryParams: { returnUrl } });
  }

  getReturnUrl(queryParamMap: ParamMap, fallback = '/'): string {
    return queryParamMap.get('returnUrl') ?? fallback;
  }

  private setAuth(response: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this.authState.set(response);
  }

  private clearAuth(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.authState.set(null);
  }

  private readStoredAuth(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as AuthResponse;
    } catch {
      return null;
    }
  }

  private clearIfExpired(): void {
    const auth = this.authState();
    if (auth && new Date(auth.expiresAt).getTime() <= Date.now()) {
      this.clearAuth();
    }
  }
}
