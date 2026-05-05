import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface BacklogGame {
  gameId: number;
  name: string;
  coverImageUrl: string | null;
  score: number;
  reasons: string[];
  mainStoryHours: number | null;
  aggregatedRating: number | null;
  playtimeHours: number;
  genres: string[];
}

@Injectable({ providedIn: 'root' })
export class BacklogPrioritizerService {
  private api = `${environment.apiUrl}/api/backlog`;

  constructor(private http: HttpClient) {}

  prioritize(mode: string, limit = 10): Observable<BacklogGame[]> {
    const params = new HttpParams()
      .set('mode', mode)
      .set('limit', limit);
    return this.http.get<BacklogGame[]>(`${this.api}/prioritize`, { params });
  }
}
