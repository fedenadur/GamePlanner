import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Game } from './game.service';
import { environment } from '../../environments/environment';

export interface NowPlayingSlot {
  slot: number;
  game: Game | null;
}

@Injectable({ providedIn: 'root' })
export class NowPlayingService {
  private api = `${environment.apiUrl}/api/now-playing`;

  constructor(private http: HttpClient) {}

  getSlots(): Observable<NowPlayingSlot[]> {
    return this.http.get<NowPlayingSlot[]>(this.api);
  }

  setSlot(slot: number, gameId: number): Observable<void> {
    return this.http.put<void>(`${this.api}/${slot}`, { gameId });
  }

  clearSlot(slot: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/${slot}`);
  }
}
