import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Game {
  id: number;
  name: string;
  genres: string[];
  estimatedMinutes: number;
  tags: string[];
  themes: string[];
  playerPerspectives: string[];
  gameModes: string[];
  coverImageUrl?: string;
  playtimeHours: number;
  source: string;
}

export interface GameDetail extends Game {
  igdbId:                number | null;
  steamAppId:            number | null;
  steamUrl:              string | null;
  hltbMainStory:         number | null;
  hltbMainPlus:          number | null;
  hltbCompletionist:     number | null;
  aggregatedRating:      number | null;
  aggregatedRatingCount: number | null;
}

export interface CreateGameRequest {
  name: string;
  genres: string[];
  estimatedMinutes: number;
  tags: string[];
}

export interface IgdbSearchResult {
  igdbId: number;
  name: string;
  coverUrl?: string;
  genres: string[];
  releaseYear?: number;
}

export interface CreateGameFromIgdbRequest {
  igdbId: number;
  name: string;
  estimatedMinutes: number;
  coverUrl?: string;
  genres: string[];
}

export interface SessionRequest {
  availableMinutes: number;
  mood: string;
}

export interface SessionRecommendation {
  game: Game | null;
  isExactMatch: boolean;
  message: string;
}

export interface SessionPlannerRequest {
  playedStatus: string;
  mood:         string;
  duration:     string;
  perspective:  string;
  gameModes:    string[];
  gameLength:   string;
}

export interface SessionPlannerResult {
  games: Game[];
  message: string;
}

@Injectable({ providedIn: 'root' })
export class GameService {
  private api = `${environment.apiUrl}/api`;

  constructor(private http: HttpClient) {}

  getGameDetail(id: number): Observable<GameDetail> {
    return this.http.get<GameDetail>(`${this.api}/games/${id}/detail`);
  }

  getGames(): Observable<Game[]> {
    return this.http.get<Game[]>(`${this.api}/games`);
  }

  createGame(request: CreateGameRequest): Observable<any> {
    return this.http.post(`${this.api}/games`, request);
  }

  deleteGame(id: number): Observable<any> {
    return this.http.delete(`${this.api}/games/${id}`);
  }

  searchIgdb(name: string): Observable<IgdbSearchResult[]> {
    return this.http.get<IgdbSearchResult[]>(`${this.api}/games/igdb/search`, { params: { name } });
  }

  createGameFromIgdb(request: CreateGameFromIgdbRequest): Observable<any> {
    return this.http.post(`${this.api}/games/from-igdb`, request);
  }

  getRecommendation(request: SessionRequest): Observable<SessionRecommendation> {
    return this.http.post<SessionRecommendation>(`${this.api}/session/recommend`, request);
  }

  planSession(request: SessionPlannerRequest): Observable<SessionPlannerResult> {
    return this.http.post<SessionPlannerResult>(`${this.api}/session/plan`, request);
  }

  getGameImages(type = 'screenshot', igdbId?: number | null): Observable<GameImage[]> {
    const params: Record<string, string | number> = { type };
    if (igdbId != null) params['igdbId'] = igdbId;
    return this.http.get<GameImage[]>(`${this.api}/gameimages`, { params });
  }

  startSteamImport(steamId: string): Observable<{ jobId: number; alreadyRunning?: boolean }> {
    return this.http.post<{ jobId: number; alreadyRunning?: boolean }>(`${this.api}/steam/import`, { steamId });
  }

  getImportProgress(jobId: number): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/steam/import/${jobId}`);
  }
}

export interface ImportJobStatus {
  status: string;
  progress: number;
  processedGames: number;
  totalGames: number;
  errorMessage?: string;
}

export interface GameImage {
  id:        number;
  gameId:    number;
  gameName:  string;
  url:       string;
  imageType: string;
}
