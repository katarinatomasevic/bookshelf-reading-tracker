import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Activity, DashboardPeriod, DashboardSummary } from '../../core/models/dashboard.model';
import { localDate } from '../../core/utils/local-date';

/**
 * The dashboard's two reads. They are separate endpoints on purpose: the activity grid always
 * covers the last 26 weeks, so changing the period selector must not fetch it again.
 *
 * Both send `today` from the browser, because the server's UTC day is not necessarily the day
 * the reader is in — and a streak that resets at the wrong hour is worse than no streak.
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  getDashboard(period: DashboardPeriod): Observable<DashboardSummary> {
    const params = new HttpParams().set('period', period).set('today', localDate());

    return this.http.get<DashboardSummary>(`${environment.apiUrl}/dashboard`, { params });
  }

  getActivity(): Observable<Activity> {
    const params = new HttpParams().set('today', localDate());

    return this.http.get<Activity>(`${environment.apiUrl}/dashboard/activity`, { params });
  }
}
