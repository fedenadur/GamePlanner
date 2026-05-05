import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Game } from './game.service';
import { environment } from '../../environments/environment';

export interface TonightRecommendation {
  gameId: number;
  name: string;
  coverImageUrl: string | null;
  reason: string | null;
  mainStoryHours: number | null;
  aggregatedRating: number | null;
}

export interface UpcomingGameItem {
  igdbId: number;
  name: string;
  coverUrl: string | null;
  releaseDate: string;
}

export interface BacklogSummary {
  totalBacklog: number;
  completed: number;
  completionRate: number;
  unplayed: number;
}

export interface QuickStats {
  totalOwned: number;
  totalPlaytimeHours: number;
  favoriteGenre: string | null;
  mostPlayedGame: string | null;
}

export interface Dashboard {
  username: string | null;
  continuePlaying: Game[];
  tonightRecommendation: TonightRecommendation | null;
  upcomingThisWeek: UpcomingGameItem[];
  backlogSummary: BacklogSummary;
  quickStats: QuickStats;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private api = `${environment.apiUrl}/api/dashboard`;

  constructor(private http: HttpClient) {}

  get(): Observable<Dashboard> {
    return this.http.get<Dashboard>(this.api);
  }
}
