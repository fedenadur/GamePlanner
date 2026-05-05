import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LibraryStats {
  totalOwned: number;
  neverPlayed: number;
  finished: number;
  currentlyPlaying: number;
  backlog: number;
}

export interface TimeStats {
  totalPlaytimeHours: number;
  averagePlaytimeHours: number;
  mostPlayedGame: string | null;
  mostPlayedGameHours: number;
}

export interface CompletionStats {
  completionRate: number;
  averageCompletionDays: number | null;
}

export interface RatingStats {
  averageUserRating: number | null;
  highestRatedFinishedGame: string | null;
  highestRating: number | null;
}

export interface GenreCount { genre: string; count: number; }
export interface GenreHours { genre: string; hours: number; }

export interface GenreStats {
  topByCount: GenreCount[];
  topByHours: GenreHours[];
}

export interface YearBucket { year: number; count: number; }

export interface Highlights {
  mostPlayedGame: string | null;
  favoriteGenre: string | null;
  hiddenGem: string | null;
  oldestUnplayedGame: string | null;
  oldestUnplayedYear: number | null;
}

export interface StatsDashboard {
  library: LibraryStats;
  time: TimeStats;
  completion: CompletionStats;
  ratings: RatingStats;
  genres: GenreStats;
  byYear: YearBucket[];
  highlights: Highlights;
}

@Injectable({ providedIn: 'root' })
export class StatsService {
  private base = `${environment.apiUrl}/api`;

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<StatsDashboard> {
    return this.http.get<StatsDashboard>(`${this.base}/stats/dashboard`);
  }
}
