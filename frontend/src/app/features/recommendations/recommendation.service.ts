import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Recommendations } from '../../core/models/recommendation.model';

/**
 * Fetches recommendations. Deliberately thin, and deliberately holding no state.
 *
 * Every other feature service in this project caches what it loaded — the shelf keeps its books,
 * the search keeps its results. This one must not. Recommendations are a function of the shelf,
 * and the shelf changes underneath them; a cached list would show a book the reader has just
 * added, or keep showing the old suggestions after they rated something. Each of the three
 * triggers asks for a fresh list, and the backend computes one in a few dozen milliseconds.
 *
 * Holding no state also means there is nothing to clear when a session ends, which is why this
 * service is the only feature service that does not register with SessionResetService.
 */
@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private readonly http = inject(HttpClient);

  /**
   * @param offset how far into the ranked list to start; the refresh button advances it, and the
   *   backend wraps around at the end rather than running out.
   */
  getRecommendations(limit: number, offset = 0): Observable<Recommendations> {
    const params = new HttpParams().set('limit', limit).set('offset', offset);
    return this.http.get<Recommendations>(`${environment.apiUrl}/recommendations`, { params });
  }
}
