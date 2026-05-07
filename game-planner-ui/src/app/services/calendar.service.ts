import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface CalendarGame {
  igdbId:      number;
  name:        string;
  coverUrl:    string | null;
  hypes:       number;
  localGameId: number | null;
  genreMatch:  boolean;
  isTracked:   boolean;
  genres:      string[];
}

export interface CalendarFilters {
  genre?:             string;
  hypeMin?:           number;
  ownedOnly?:         boolean;
  favoriteGenresOnly?: boolean;
}

export type CalendarDay  = CalendarGame[];
export type CalendarData = Record<string, CalendarDay>;

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private api = `${environment.apiUrl}/api`;

  constructor(private http: HttpClient) {}

  getMonth(year: number, month: number, filters: CalendarFilters = {}): Observable<CalendarData> {
    const params = this.buildParams({ year, month }, filters);
    return this.http.get<CalendarData>(`${this.api}/calendar`, { params });
  }

  getWeek(year: number, month: number, week: number, filters: CalendarFilters = {}): Observable<CalendarData> {
    const params = this.buildParams({ year, month, week }, filters);
    return this.http.get<CalendarData>(`${this.api}/calendar`, { params });
  }

  private buildParams(base: Record<string, number>, filters: CalendarFilters): HttpParams {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(base)) p = p.set(k, v);
    if (filters.genre)              p = p.set('genre',             filters.genre);
    if (filters.hypeMin)            p = p.set('hypeMin',           filters.hypeMin);
    if (filters.ownedOnly)          p = p.set('ownedOnly',         'true');
    if (filters.favoriteGenresOnly) p = p.set('favoriteGenresOnly','true');
    return p;
  }
}
