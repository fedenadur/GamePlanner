import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Game } from './game.service';
import { PlaySession } from './play-session.service';
import { environment } from '../../environments/environment';

export interface NowPlayingSlot {
  device: string;
  game: Game | null;
  playingSince: string | null;
  recentSessions: PlaySession[];
}

export interface StartNowPlayingRequest {
  gameId: number;
  outgoingOutcome?: string | null;
  outgoingRating?: number | null;
}

export interface RemoveNowPlayingRequest {
  outcome: string;
  rating?: number | null;
}

@Injectable({ providedIn: 'root' })
export class NowPlayingService {
  private api = `${environment.apiUrl}/api/now-playing`;

  constructor(private http: HttpClient) {}

  getSlots(): Observable<NowPlayingSlot[]> {
    return this.http.get<NowPlayingSlot[]>(this.api);
  }

  start(device: string, req: StartNowPlayingRequest): Observable<void> {
    return this.http.put<void>(`${this.api}/${device}`, req);
  }

  remove(device: string, req: RemoveNowPlayingRequest): Observable<void> {
    return this.http.post<void>(`${this.api}/${device}/remove`, req);
  }
}
