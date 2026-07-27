import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UpdateProfileRequest, UserProfile } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);

  getMe(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${environment.apiUrl}/users/me`);
  }

  updateMe(request: UpdateProfileRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${environment.apiUrl}/users/me`, request);
  }
}
