import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthResponse {
  token: string;
  email: string;
  username?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = `${environment.apiUrl}/api/auth`;
  private readonly TOKEN_KEY = 'gp_token';

  constructor(private http: HttpClient, private router: Router) {}

  register(email: string, password: string, username?: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/register`, { email, password, username })
      .pipe(tap(res => this.saveToken(res.token)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/login`, { email, password })
      .pipe(tap(res => this.saveToken(res.token)));
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  saveToken(token: string) {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  getSteamId(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['steamId'] ?? null;
    } catch {
      return null;
    }
  }

  exchangeSteamCode(code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/steam/exchange`, { code })
      .pipe(tap(res => this.saveToken(res.token)));
  }

  getSteamLoginUrl(): string {
    return `${environment.apiUrl}/api/auth/steam`;
  }
}
