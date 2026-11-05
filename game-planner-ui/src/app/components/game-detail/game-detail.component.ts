import { Component, OnInit, OnDestroy, ViewChild, ElementRef, HostListener, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { GameService, GameDetail, GameImage } from '../../services/game.service';
import { JournalService, JournalEntry, UpsertJournalRequest } from '../../services/journal.service';
import { PlaySession, PlaySessionService } from '../../services/play-session.service';
import { NowPlayingService } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { LogSessionResult } from '../log-session-dialog/log-session-dialog.component';
import { DEVICE_ICONS, DEVICE_LABELS } from '../../shared/mvp-constants';
import { genreIcon } from '../../shared/genre-meta';

@Component({
    selector: 'app-game-detail',
    templateUrl: './game-detail.component.html',
    styleUrls: ['./game-detail.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class GameDetailComponent implements OnInit, OnDestroy {
  @ViewChild('carouselTrack') carouselTrack!: ElementRef<HTMLElement>;

  game: GameDetail | null = null;
  screenshots: GameImage[] = [];
  lightboxIndex: number | null = null;
  imageFading = false;
  loading = true;
  error   = '';

  journalStatuses = ['Backlog', 'Playing', 'Finished', 'Paused', 'Dropped', 'Replaying'];
  journal: UpsertJournalRequest = this.emptyJournal();
  journalSaved  = false;
  journalSaving = false;

  readonly starPositions = [1, 2, 3, 4, 5];
  hoverRating: number | null = null;

  sessions: PlaySession[] = [];
  nowPlayingDevice: string | null = null;
  removedNotice = '';
  logOpen = false;
  logDevice = '';
  logDevices: string[] | null = null;
  editing: PlaySession | null = null;
  deleting: PlaySession | null = null;
  readonly deviceLabels = DEVICE_LABELS;
  readonly deviceIcons = DEVICE_ICONS;

  private routeSub?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private gameService: GameService,
    private journalService: JournalService,
    private sessionService: PlaySessionService,
    private nowPlayingService: NowPlayingService,
    private settingsService: UserSettingsService
  ) {}

  ngOnInit() {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const id = Number(params.get('id'));
      this.game = null;
      this.loading = true;
      this.error = '';
      this.journal = this.emptyJournal();
      this.journalSaved = false;
      this.hoverRating = null;
      this.sessions = [];
      this.nowPlayingDevice = null;
      this.removedNotice = '';
      this.gameService.getGameDetail(id).subscribe({
        next:  (g) => { this.game = g; this.loading = false; this.loadJournal(id); this.loadSessions(id); this.loadNowPlaying(id); this.loadScreenshots(g.igdbId, g.id); },
        error: ()  => { this.error = 'Game not found.'; this.loading = false; }
      });
    });
  }

  ngOnDestroy() {
    this.routeSub?.unsubscribe();
  }

  private loadScreenshots(igdbId: number | null, gameId: number) {
    this.screenshots = [];
    if (!igdbId) return;
    this.gameService.getGameImages('screenshot', igdbId, gameId).subscribe({
      next: (imgs) => { this.screenshots = imgs; }
    });
  }

  carouselScroll(dir: 1 | -1) {
    const el = this.carouselTrack?.nativeElement;
    if (el) el.scrollBy({ left: dir * 480, behavior: 'smooth' });
  }

  get lightboxImage(): GameImage | null {
    return this.lightboxIndex !== null ? this.screenshots[this.lightboxIndex] : null;
  }

  openLightbox(index: number) { this.setLightboxIndex(index); }
  closeLightbox()             { this.lightboxIndex = null; }

  nextImage() {
    if (this.lightboxIndex === null || this.screenshots.length <= 1) return;
    this.setLightboxIndex((this.lightboxIndex + 1) % this.screenshots.length);
  }

  prevImage() {
    if (this.lightboxIndex === null || this.screenshots.length <= 1) return;
    const len = this.screenshots.length;
    this.setLightboxIndex((this.lightboxIndex - 1 + len) % len);
  }

  @HostListener('window:keydown', ['$event'])
  handleLightboxKeydown(event: KeyboardEvent) {
    if (this.lightboxIndex === null) return;
    if (event.key === 'ArrowRight') this.nextImage();
    else if (event.key === 'ArrowLeft') this.prevImage();
    else if (event.key === 'Escape') this.closeLightbox();
  }

  private setLightboxIndex(index: number) {
    this.lightboxIndex = index;
    this.triggerFade();
    this.preloadNeighbors();
  }

  private triggerFade() {
    this.imageFading = true;
    setTimeout(() => this.imageFading = false, 20);
  }

  private preloadNeighbors() {
    if (this.lightboxIndex === null || this.screenshots.length <= 1) return;
    const len = this.screenshots.length;
    const nextIdx = (this.lightboxIndex + 1) % len;
    const prevIdx = (this.lightboxIndex - 1 + len) % len;
    [nextIdx, prevIdx].forEach(i => {
      const preload = new Image();
      preload.src = this.lightboxUrl(this.screenshots[i].url);
    });
  }

  lightboxUrl(url: string): string {
    return url.replace('t_screenshot_big', 't_1080p');
  }

  private loadJournal(gameId: number) {
    this.journalService.getEntry(gameId).subscribe({
      next: (entry: JournalEntry) => {
        this.journal.status     = entry.status;
        this.journal.userRating = entry.userRating;
      },
      error: () => {}
    });
  }

  saveJournal() {
    if (!this.game) return;
    this.journalSaving = true;
    this.removedNotice = '';
    const gameId = this.game.id;
    const wasNowPlaying = this.nowPlayingDevice !== null;
    this.journalService.upsert(gameId, this.journal).subscribe({
      next: () => {
        this.journalSaved = true; this.journalSaving = false; setTimeout(() => this.journalSaved = false, 2500);
        this.loadNowPlaying(gameId, () => {
          if (wasNowPlaying && this.nowPlayingDevice === null) this.removedNotice = 'Removed from Now Playing.';
        });
      },
      error: () => { this.journalSaving = false; }
    });
  }

  private loadSessions(gameId: number) {
    this.sessionService.list(gameId).subscribe({ next: (list) => { this.sessions = list; } });
  }

  private loadNowPlaying(gameId: number, done?: () => void) {
    this.nowPlayingService.getSlots().subscribe({
      next: (slots) => {
        this.nowPlayingDevice = slots.find(s => s.game?.id === gameId)?.device ?? null;
        done?.();
      }
    });
  }

  get loggedLabel(): string {
    const total = Math.round(this.sessions.reduce((sum, s) => sum + s.hours, 0) * 100) / 100;
    const n = this.sessions.length;
    return `Logged: ${total} h in ${n} ${n === 1 ? 'session' : 'sessions'}`;
  }

  get steamLabel(): string {
    const h = this.game?.playtimeHours ?? 0;
    return h > 0 ? `Steam: ${h} h` : '';
  }

  openLog() {
    if (this.nowPlayingDevice) {
      this.logDevice = this.nowPlayingDevice;
      this.logDevices = null;
      this.logOpen = true;
      return;
    }
    this.settingsService.getDevices().subscribe({
      next: (r) => { this.logDevices = [...r.devices, 'General']; this.logDevice = this.logDevices[0]; this.logOpen = true; },
      error: () => { this.logDevices = ['General']; this.logDevice = 'General'; this.logOpen = true; }
    });
  }

  onLogSaved(r: LogSessionResult) {
    if (!this.game) return;
    const gameId = this.game.id;
    this.sessionService.log(gameId, { device: r.device, hours: r.hours, note: r.note, playedOn: r.playedOn }).subscribe({
      next: () => { this.logOpen = false; this.loadSessions(gameId); },
      error: () => { this.logOpen = false; }
    });
  }

  onEditSaved(r: LogSessionResult) {
    if (!this.game || !this.editing) return;
    const gameId = this.game.id;
    this.sessionService.update(this.editing.id, { device: r.device, hours: r.hours, note: r.note, playedOn: r.playedOn }).subscribe({
      next: () => { this.editing = null; this.loadSessions(gameId); },
      error: () => { this.editing = null; }
    });
  }

  confirmDelete() {
    if (!this.game || !this.deleting) return;
    const gameId = this.game.id;
    this.sessionService.delete(this.deleting.id).subscribe({
      next: () => { this.deleting = null; this.loadSessions(gameId); },
      error: () => { this.deleting = null; }
    });
  }

  formatHours(h: number | null): string {
    if (!h) return 'No data';
    return h < 1 ? `${Math.round(h * 60)}m` : `${h}h`;
  }

  get playedLabel(): string {
    if (!this.game) return '';
    const h = this.game.playtimeHours;
    if (h === 0) return 'Not played';
    return h < 1 ? `${Math.round(h * 60)}m played` : `${h}h played`;
  }

  get ratingScore(): number | null {
    return this.game?.aggregatedRating != null
      ? Math.round(this.game.aggregatedRating)
      : null;
  }

  get ratingColor(): string {
    const r = this.ratingScore;
    if (r == null) return 'muted';
    if (r >= 80)  return 'green';
    if (r >= 60)  return 'yellow';
    return 'red';
  }

  get ratingSentiment(): string {
    const r = this.ratingScore;
    if (r == null) return '';
    if (r >= 90) return 'Overwhelming';
    if (r >= 80) return 'Very Positive';
    if (r >= 70) return 'Mostly Positive';
    if (r >= 60) return 'Mixed';
    if (r >= 40) return 'Mostly Negative';
    return 'Negative';
  }

  get ratingCountLabel(): string {
    const c = this.game?.aggregatedRatingCount;
    if (!c) return '';
    return c.toLocaleString() + ' critic scores';
  }

  setRating(val: number) {
    this.journal.userRating = this.journal.userRating === val ? null : val;
  }

  starFill(star: number): number {
    const v = this.hoverRating ?? this.journal.userRating ?? 0;
    if (v >= star * 2) return 100;
    if (v >= star * 2 - 1) return 50;
    return 0;
  }

  displayRating(rating: number): string {
    const stars = rating / 2;
    return stars % 1 === 0 ? `${stars}` : stars.toFixed(1);
  }

  genreIcon = genreIcon;

  private emptyJournal(): UpsertJournalRequest {
    return { status: 'Backlog', startedAt: null, finishedAt: null, lastPlayedAt: null, userRating: null, notes: null, favoriteMoment: null, replayCount: 0 };
  }
}
