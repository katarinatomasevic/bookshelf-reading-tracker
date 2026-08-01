import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AddToShelfRequest, ManualBookRequest, ShelfItem } from '../../core/models/shelf.model';
import { SessionResetService } from '../../core/services/session-reset.service';

@Injectable({ providedIn: 'root' })
export class ShelfService {
  private readonly http = inject(HttpClient);
  private readonly sessionReset = inject(SessionResetService);

  /** The whole shelf, loaded in one request; the page splits it into tabs client-side. */
  readonly items = signal<ShelfItem[]>([]);
  readonly loaded = signal(false);

  constructor() {
    this.sessionReset.register(() => this.clear());
  }

  /** One user's shelf must never stay in memory for the next one. */
  clear(): void {
    this.items.set([]);
    this.loaded.set(false);
  }

  getShelf(): Observable<ShelfItem[]> {
    return this.http.get<ShelfItem[]>(`${environment.apiUrl}/shelf`).pipe(
      tap((items) => {
        this.items.set(items);
        this.loaded.set(true);
      }),
    );
  }

  addToShelf(request: AddToShelfRequest): Observable<ShelfItem> {
    return this.http
      .post<ShelfItem>(`${environment.apiUrl}/shelf`, request)
      .pipe(tap((item) => this.upsert(item)));
  }

  addManual(request: ManualBookRequest): Observable<ShelfItem> {
    return this.http
      .post<ShelfItem>(`${environment.apiUrl}/books/manual`, request)
      .pipe(tap((item) => this.upsert(item)));
  }

  /** Adding is idempotent server-side, so the same entry can come back twice. */
  private upsert(item: ShelfItem): void {
    this.items.update((current) => {
      const index = current.findIndex((existing) => existing.id === item.id);
      if (index === -1) {
        return [item, ...current];
      }

      const updated = [...current];
      updated[index] = item;
      return updated;
    });
  }
}
