import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UserProfile {
  email:           string | null;
  displayName:     string | null;
  profileImageUrl: string | null;
  isSteamUser:     boolean;
}

export interface UpdateProfileRequest {
  displayName?:     string;
  profileImageUrl?: string;
}

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private api = `${environment.apiUrl}/api/profile`;

  private _profile = new BehaviorSubject<UserProfile | null>(null);
  profile$ = this._profile.asObservable();

  get snapshot(): UserProfile | null { return this._profile.value; }

  constructor(private http: HttpClient) {}

  load(): Observable<UserProfile> {
    return this.http.get<UserProfile>(this.api)
      .pipe(tap(p => this._profile.next(p)));
  }

  update(request: UpdateProfileRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(this.api, request)
      .pipe(tap(p => this._profile.next(p)));
  }

  clear() { this._profile.next(null); }
}
