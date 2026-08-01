import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BookDetails, BookSearchPageResult, BookSearchResult } from '../../core/models/book.model';
import { SessionResetService } from '../../core/services/session-reset.service';

const SEARCH_STATE_KEY = 'bookshelf.search';

interface PersistedSearchState {
  query: string;
  results: BookSearchResult[];
  page: number;
  hasMore: boolean;
}

@Injectable({ providedIn: 'root' })
export class BookService {
  private readonly http = inject(HttpClient);
  private readonly sessionReset = inject(SessionResetService);

  /** Search state lives here rather than in the search component for two reasons:
   *  the service outlives navigation to a book's details page and back, and the
   *  snapshot below outlives a full page reload. Either way the user returns to
   *  the results they already had, without a new Open Library round trip. */
  readonly hasSearched = signal(false);
  readonly lastQuery = signal('');
  readonly results = signal<BookSearchResult[]>([]);
  readonly page = signal(1);
  readonly hasMore = signal(false);

  constructor() {
    this.restoreSearchState();
    this.sessionReset.register(() => this.clearSearchState());
  }

  /** Ends the current search: what the previous user was looking at must not stay
   *  on screen (or in storage) once their session is over. */
  clearSearchState(): void {
    this.hasSearched.set(false);
    this.lastQuery.set('');
    this.results.set([]);
    this.page.set(1);
    this.hasMore.set(false);

    try {
      sessionStorage.removeItem(SEARCH_STATE_KEY);
    } catch {
      // Nothing to do — the in-memory signals above are already cleared.
    }
  }

  search(query: string): Observable<BookSearchPageResult> {
    this.hasSearched.set(true);
    this.lastQuery.set(query);

    return this.searchPage(query, 1).pipe(
      tap((result) => {
        this.results.set(result.items);
        this.page.set(result.page);
        this.hasMore.set(result.hasMore);
        this.persistSearchState();
      }),
    );
  }

  loadMore(): Observable<BookSearchPageResult> {
    return this.searchPage(this.lastQuery(), this.page() + 1).pipe(
      tap((result) => {
        this.results.update((current) => [...current, ...result.items]);
        this.page.set(result.page);
        this.hasMore.set(result.hasMore);
        this.persistSearchState();
      }),
    );
  }

  getById(id: string): Observable<BookDetails> {
    return this.http.get<BookDetails>(`${environment.apiUrl}/books/${id}`);
  }

  private searchPage(query: string, page: number): Observable<BookSearchPageResult> {
    const params = new HttpParams().set('q', query).set('page', page);
    return this.http.get<BookSearchPageResult>(`${environment.apiUrl}/books/search`, { params });
  }

  private persistSearchState(): void {
    const state: PersistedSearchState = {
      query: this.lastQuery(),
      results: this.results(),
      page: this.page(),
      hasMore: this.hasMore(),
    };

    try {
      sessionStorage.setItem(SEARCH_STATE_KEY, JSON.stringify(state));
    } catch {
      // Storage can be full or blocked by browser settings; losing the snapshot
      // only costs the user a re-search, so it must never break the page.
    }
  }

  private restoreSearchState(): void {
    let stored: string | null = null;
    try {
      stored = sessionStorage.getItem(SEARCH_STATE_KEY);
    } catch {
      return;
    }

    if (!stored) {
      return;
    }

    try {
      const state = JSON.parse(stored) as PersistedSearchState;
      this.lastQuery.set(state.query);
      this.results.set(state.results);
      this.page.set(state.page);
      this.hasMore.set(state.hasMore);
      this.hasSearched.set(true);
    } catch {
      sessionStorage.removeItem(SEARCH_STATE_KEY);
    }
  }
}
