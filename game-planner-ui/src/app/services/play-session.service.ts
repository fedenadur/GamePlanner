import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PlaySession {
  id: number;
  gameId: number;
  device: string;
  playedOn: string;
  hours: number;
  note?: string | null;
}

export interface LogSessionRequest {
  device: string;
  playedOn?: string | null;
  hours: number;
  note?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PlaySessionService {
  private api = `${environment.apiUrl}/api`;

  constructor(private http: HttpClient) {}

  list(gameId: number): Observable<PlaySession[]> {
    return this.http.get<PlaySession[]>(`${this.api}/games/${gameId}/sessions`);
  }

  log(gameId: number, req: LogSessionRequest): Observable<PlaySession> {
    return this.http.post<PlaySession>(`${this.api}/games/${gameId}/sessions`, req);
  }

  update(id: number, req: LogSessionRequest): Observable<any> {
    return this.http.put(`${this.api}/sessions/${id}`, req);
  }

  delete(id: number): Observable<any> {
    return this.http.delete(`${this.api}/sessions/${id}`);
  }
}
