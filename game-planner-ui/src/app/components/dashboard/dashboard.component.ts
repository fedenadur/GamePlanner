import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { Dashboard, DashboardService } from '../../services/dashboard.service';
import { NowPlayingService } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { FinishChoice } from '../finish-game-dialog/finish-game-dialog.component';
import { DEVICE_LABELS } from '../../shared/mvp-constants';
import { startOnDevice } from '../../shared/start-game';

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class DashboardComponent implements OnInit {
  data: Dashboard | null = null;
  loading = true;
  error = false;

  readonly deviceLabels = DEVICE_LABELS;
  devicesState: 'loading' | 'ready' | 'error' = 'loading';
  firstDevice = 'General';
  deviceByGameId = new Map<number, string>();

  startError = '';
  starting = false;
  pendingStart = false;
  finishGameName = '';

  constructor(
    private dashboardService: DashboardService,
    private nowPlaying: NowPlayingService,
    private settings: UserSettingsService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.dashboardService.get().subscribe({
      next: d => { this.data = d; this.loading = false; },
      error: () => { this.error = true; this.loading = false; }
    });
    forkJoin({
      devices: this.settings.getDevices().pipe(catchError(() => of(null))),
      slots: this.nowPlaying.getSlots().pipe(catchError(() => of([])))
    }).subscribe(({ devices, slots }) => {
      if (devices) {
        this.firstDevice = devices.devices[0] ?? 'General';
        this.devicesState = 'ready';
      } else {
        this.devicesState = 'error';
      }
      this.deviceByGameId = new Map(slots.filter(s => s.game).map(s => [s.game!.id, s.device]));
    });
  }

  deviceLabel(d: string): string { return this.deviceLabels[d] ?? d; }

  startTonight(choice?: FinishChoice) {
    const t = this.data?.tonightRecommendation;
    if (!t) return;
    this.startError = '';
    this.starting = true;
    startOnDevice(this.nowPlaying, this.firstDevice, t.gameId, choice).subscribe(res => {
      this.starting = false;
      if (res.kind === 'started') this.router.navigate(['/now-playing']);
      else if (res.kind === 'finish-needed') { this.pendingStart = true; this.finishGameName = res.currentName; }
      else this.startError = res.message;
    });
  }

  onFinishChosen(choice: FinishChoice) {
    this.pendingStart = false;
    this.startTonight(choice);
  }

  onFinishCancelled() { this.pendingStart = false; }

  dayLabel(date: string): string {
    const d = this.daysUntil(date);
    return d === 0 ? 'Today' : d === 1 ? 'Tomorrow' : `in ${d}d`;
  }

  daysUntil(date: string): number {
    const ms = new Date(date).getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / (1000 * 60 * 60 * 24)));
  }

  groupUpcomingByDay(games: any[]) {
    const groups = new Map<string, any[]>();
    games.forEach(game => {
      const date = new Date(game.releaseDate).toDateString();
      if (!groups.has(date)) groups.set(date, []);
      groups.get(date)!.push(game);
    });
    return Array.from(groups.entries()).map(([date, games]) => ({
      date: new Date(date),
      games
    }));
  }
}
