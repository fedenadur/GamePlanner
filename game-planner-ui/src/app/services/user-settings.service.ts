import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UserPreferences {
  favoriteGenres: string[];
  avoidGenres: string[];
  maxGameHours: number | null;
  singlePlayer: string;
  narrative: string;
  combat: string;
}

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private api = `${environment.apiUrl}/api`;

  constructor(private http: HttpClient) {}

  getDevices(): Observable<{ devices: string[] }> {
    return this.http.get<{ devices: string[] }>(`${this.api}/profile/devices`);
  }

  saveDevices(devices: string[]): Observable<any> {
    return this.http.put(`${this.api}/profile/devices`, { devices });
  }

  getPreferences(): Observable<UserPreferences> {
    return this.http.get<UserPreferences>(`${this.api}/profile/preferences`);
  }

  savePreferences(p: UserPreferences): Observable<any> {
    return this.http.put(`${this.api}/profile/preferences`, p);
  }

  getGenres(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/genres`);
  }
}
