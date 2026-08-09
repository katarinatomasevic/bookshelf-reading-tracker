import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ShelfItem } from '../models/shelf.model';
import {
  LogProgressRequest,
  LogProgressResponse,
  ReadingLogEntry,
  UpdateReadingLogRequest,
} from '../models/reading-log.model';

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

  /** Called when the history section is opened, never when the modal is. */
  getHistory(userBookId: string): Observable<ReadingLogEntry[]> {
    return this.http.get<ReadingLogEntry[]>(`${environment.apiUrl}/reading-log`, {
      params: { userBookId },
    });
  }

  /**
   * Both corrections return the updated shelf entry rather than the log: changing the past moves
   * the reader's current page, and that is what the shelf and the progress bar are showing.
   */
  update(logId: string, request: UpdateReadingLogRequest): Observable<ShelfItem> {
    return this.http.patch<ShelfItem>(`${environment.apiUrl}/reading-log/${logId}`, request);
  }

  remove(logId: string): Observable<ShelfItem> {
    return this.http.delete<ShelfItem>(`${environment.apiUrl}/reading-log/${logId}`);
  }
}
