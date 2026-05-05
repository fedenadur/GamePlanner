import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface JournalEntry {
  gameId:         number;
  gameName:       string;
  coverImageUrl:  string | null;
  status:         string;
  startedAt:      string | null;
  finishedAt:     string | null;
  lastPlayedAt:   string | null;
  userRating:     number | null;
  notes:          string | null;
  favoriteMoment: string | null;
  replayCount:    number;
  createdAt:      string;
  updatedAt:      string;
  daysToFinish:   number | null;
}

export interface UpsertJournalRequest {
  status:         string;
  startedAt:      string | null;
  finishedAt:     string | null;
  lastPlayedAt:   string | null;
  userRating:     number | null;
  notes:          string | null;
  favoriteMoment: string | null;
  replayCount:    number;
}

@Injectable({ providedIn: 'root' })
export class JournalService {
  private api = `${environment.apiUrl}/api/journal`;

  constructor(private http: HttpClient) {}

  getJournal(): Observable<JournalEntry[]> {
    return this.http.get<JournalEntry[]>(this.api);
  }

  getEntry(gameId: number): Observable<JournalEntry> {
    return this.http.get<JournalEntry>(`${this.api}/${gameId}`);
  }

  upsert(gameId: number, req: UpsertJournalRequest): Observable<void> {
    return this.http.post<void>(`${this.api}/${gameId}`, req);
  }

  delete(gameId: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/${gameId}`);
  }
}
