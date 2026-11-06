import { Component, OnDestroy, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, Subscription, catchError, debounce, filter, forkJoin, of, switchMap, timer } from 'rxjs';
import { GameService } from '../../services/game.service';
import { NowPlayingService } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { WhatToPlayResponse, WhatToPlayResult, WhatToPlayService } from '../../services/what-to-play.service';
import { FinishChoice } from '../finish-game-dialog/finish-game-dialog.component';
import { startOnDevice } from '../../shared/start-game';
import { loadError } from '../../shared/messages';
import { DEVICE_ICONS, DEVICE_LABELS } from '../../shared/mvp-constants';

const DEBOUNCE_MS = 300;

@Component({
    selector: 'app-what-to-play',
    templateUrl: './what-to-play.component.html',
    styleUrls: ['./what-to-play.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class WhatToPlayComponent implements OnInit, OnDestroy {
  readonly sessionTimeOptions = [
    { label: '< 1 h', value: 'Short' },
    { label: '1–2 h', value: 'Medium' },
    { label: '2 h+', value: 'Long' },
    { label: 'Any', value: 'Any' },
  ];

  readonly gameLengthOptions = [
    { label: 'Short < 10 h', value: 'Short' },
    { label: 'Medium 10–30 h', value: 'Medium' },
    { label: 'Long 30 h+', value: 'Long' },
    { label: 'Any', value: 'Any' },
  ];

  readonly moodOptions = [
    { label: 'Any', value: '' },
    { label: 'Chill', value: 'chill' },
    { label: 'Focused', value: 'focused' },
    { label: 'Story', value: 'story' },
    { label: 'Coop', value: 'coop' },
  ];

  readonly perspectiveOptions = [
    { label: 'Any', value: '' },
    { label: 'First person', value: 'First person' },
    { label: 'Third person', value: 'Third person' },
    { label: 'Side view', value: 'Side view' },
    { label: 'VR', value: 'Virtual Reality' },
  ];

  readonly modeOptions = [
    { label: 'Single player', value: 'Single player' },
    { label: 'Co-op', value: 'Co-operative' },
    { label: 'Multiplayer', value: 'Multiplayer' },
  ];

  readonly strategyOptions = [
    { label: 'Default', value: 'Default' },
    { label: 'Resume started games', value: 'Resume' },
    { label: 'Discover new genres', value: 'Discover' },
  ];

  readonly deviceIcons = DEVICE_ICONS;
  readonly deviceLabels = DEVICE_LABELS;

  devices: string[] = [];
  genres: string[] = [];

  device = 'General';
  sessionTime = 'Any';
  gameLength = 'Any';
  selectedGenres = new Set<string>();
  mood = '';
  perspective = '';
  modes = new Set<string>();
  strategy = 'Default';
  showMore = false;

  results: WhatToPlayResult[] = [];
  libraryEmpty = false;
  loading = true;
  loaded = false;
  error = '';
  devicesError = '';

  pendingStart: WhatToPlayResult | null = null;
  finishGameName = '';
  starting = false;

  private run$ = new Subject<number>();
  private sub = new Subscription();

  constructor(
    private whatToPlay: WhatToPlayService,
    private nowPlaying: NowPlayingService,
    private settings: UserSettingsService,
    private gameService: GameService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit() {
    this.sub.add(this.run$.pipe(
      debounce(delay => timer(delay)),
      filter(() => this.devices.length > 0),
      switchMap(() => {
        this.loading = true;
        this.error = '';
        return this.whatToPlay.recommend(this.buildRequest(), 5).pipe(
          catchError(() => {
            this.error = loadError('recommendations');
            this.results = [];
            return of<WhatToPlayResponse | null>(null);
          })
        );
      })
    ).subscribe(res => {
      this.loading = false;
      this.loaded = true;
      if (res) this.results = res.results;
    }));

    this.loadOptions();
  }

  // A devices error must not fall back to General: the API rejects General for a user who has devices.
  loadOptions() {
    this.devicesError = '';
    this.loading = true;
    forkJoin({
      devices: this.settings.getDevices().pipe(catchError(() => of(null))),
      genres: this.settings.getGenres().pipe(catchError(() => of([] as string[]))),
      games: this.gameService.getGames().pipe(catchError(() => of(null)))
    }).subscribe(({ devices, genres, games }) => {
      if (!devices) {
        this.devicesError = loadError('your devices');
        this.loading = false;
        return;
      }
      this.devices = devices.devices.length ? devices.devices : ['General'];
      this.genres = genres;
      this.libraryEmpty = games !== null && games.length === 0;
      const wanted = this.route.snapshot.queryParamMap.get('device');
      this.device = wanted && this.devices.includes(wanted) ? wanted : this.devices[0];
      this.run$.next(0);
    });
  }

  retry() { this.run$.next(0); }

  resetOptions() {
    this.sessionTime = 'Any';
    this.gameLength = 'Any';
    this.selectedGenres.clear();
    this.mood = '';
    this.perspective = '';
    this.modes.clear();
    this.strategy = 'Default';
    this.run$.next(0);
  }

  ngOnDestroy() { this.sub.unsubscribe(); }

  private buildRequest() {
    return {
      device: this.device,
      sessionTime: this.sessionTime,
      gameLength: this.gameLength,
      genres: [...this.selectedGenres],
      mood: this.mood || null,
      perspective: this.perspective || null,
      modes: [...this.modes],
      strategy: this.strategy
    };
  }

  private changed() { this.run$.next(DEBOUNCE_MS); }

  setDevice(v: string)      { this.device = v; this.changed(); }
  setSessionTime(v: string) { this.sessionTime = v; this.changed(); }
  setGameLength(v: string)  { this.gameLength = v; this.changed(); }
  setMood(v: string)        { this.mood = v; this.changed(); }
  setPerspective(v: string) { this.perspective = v; this.changed(); }
  setStrategy(v: string)    { this.strategy = v; this.changed(); }

  toggleGenre(g: string) {
    this.selectedGenres.has(g) ? this.selectedGenres.delete(g) : this.selectedGenres.add(g);
    this.changed();
  }

  clearGenres() { this.selectedGenres.clear(); this.changed(); }

  toggleMode(m: string) {
    this.modes.has(m) ? this.modes.delete(m) : this.modes.add(m);
    this.changed();
  }

  get top(): WhatToPlayResult | null { return this.results[0] ?? null; }
  get alternatives(): WhatToPlayResult[] { return this.results.slice(1, 5); }
  get noResults(): boolean { return this.loaded && !this.loading && !this.error && !this.libraryEmpty && !this.top; }

  reasonClass(kind: string): string {
    return kind === 'Positive' ? 'ok' : kind === 'Negative' ? 'bad' : 'meh';
  }

  reasonIcon(kind: string): string { return kind === 'Positive' ? '✓' : '•'; }

  altSummary(r: WhatToPlayResult): string {
    return r.reasons.slice(0, 2).map(x => x.text).join(' · ');
  }

  start(r: WhatToPlayResult, choice?: FinishChoice) {
    this.error = '';
    this.starting = true;
    startOnDevice(this.nowPlaying, this.device, r.gameId, choice).subscribe(res => {
      this.starting = false;
      if (res.kind === 'started') this.router.navigate(['/now-playing']);
      else if (res.kind === 'finish-needed') { this.pendingStart = r; this.finishGameName = res.currentName; }
      else this.error = res.message;
    });
  }

  onFinishChosen(choice: FinishChoice) {
    const next = this.pendingStart!;
    this.pendingStart = null;
    this.start(next, choice);
  }

  onFinishCancelled() { this.pendingStart = null; }
}
