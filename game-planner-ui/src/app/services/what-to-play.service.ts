import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface WhatToPlayRequest {
  device?: string | null;
  sessionTime: string;
  gameLength: string;
  genres: string[];
  mood?: string | null;
  perspective?: string | null;
  modes: string[];
  strategy: string;
}

export interface WhatToPlayReason {
  kind: string;
  text: string;
}

export interface WhatToPlayResult {
  gameId: number;
  name: string;
  coverImageUrl?: string | null;
  store: string;
  lengthHours?: number | null;
  totalHours: number;
  score: number;
  match: string;
  reasons: WhatToPlayReason[];
}

export interface WhatToPlayResponse {
  device: string;
  results: WhatToPlayResult[];
}

@Injectable({ providedIn: 'root' })
export class WhatToPlayService {
  private api = `${environment.apiUrl}/api/what-to-play`;

  constructor(private http: HttpClient) {}

  recommend(req: WhatToPlayRequest, take = 5): Observable<WhatToPlayResponse> {
    return this.http.post<WhatToPlayResponse>(this.api, req, { params: { take } });
  }
}
