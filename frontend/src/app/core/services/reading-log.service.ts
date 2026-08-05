import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LogProgressRequest, LogProgressResponse } from '../models/reading-log.model';

/**
 * Lives in `core` because the dashboard logs progress as well, from a component that has nothing
 * to do with the shelf page. It deliberately does not touch the shelf signal itself — that would
 * make `core` depend on a feature; the entry component hands the updated item to whoever owns it.
 */
@Injectable({ providedIn: 'root' })
export class ReadingLogService {
  private readonly http = inject(HttpClient);

  logProgress(request: LogProgressRequest): Observable<LogProgressResponse> {
    return this.http.post<LogProgressResponse>(`${environment.apiUrl}/reading-log`, request);
  }
}
