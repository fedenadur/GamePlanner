import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface WorkerStatus {
  id:            number;
  name:          string;
  status:        string;
  lastRunAt:     string | null;
  lastSuccessAt: string | null;
  lastError:     string | null;
}

@Injectable({ providedIn: 'root' })
export class WorkerService {
  private api = `${environment.apiUrl}/api/worker`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<WorkerStatus[]> {
    return this.http.get<WorkerStatus[]>(this.api);
  }

  run(name: string, force = false): Observable<any> {
    const url = force ? `${this.api}/${name}/run?force=true` : `${this.api}/${name}/run`;
    return this.http.post(url, {});
  }
}
