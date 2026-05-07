import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TrackedGame {
  igdbId:      number;
  name:        string;
  coverUrl:    string | null;
  releaseDate: string;
  hypes:       number;
  genres:      string[];
  localGameId: number | null;
  trackedAt:   string;
}

@Injectable({ providedIn: 'root' })
export class TrackedGamesService {
  private api = `${environment.apiUrl}/api/tracked-games`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<TrackedGame[]> {
    return this.http.get<TrackedGame[]>(this.api);
  }

  track(igdbId: number): Observable<void> {
    return this.http.post<void>(`${this.api}/${igdbId}`, null);
  }

  untrack(igdbId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/${igdbId}`);
  }
}
