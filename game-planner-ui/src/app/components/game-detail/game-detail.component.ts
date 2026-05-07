import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { GameService, GameDetail, GameImage } from '../../services/game.service';
import { JournalService, JournalEntry, UpsertJournalRequest } from '../../services/journal.service';
import { genreIcon } from '../../shared/genre-meta';

@Component({
  selector: 'app-game-detail',
  templateUrl: './game-detail.component.html',
  styleUrls: ['./game-detail.component.css']
})
export class GameDetailComponent implements OnInit, OnDestroy {
  @ViewChild('carouselTrack') carouselTrack!: ElementRef<HTMLElement>;

  game: GameDetail | null = null;
  screenshots: GameImage[] = [];
  lightboxImage: GameImage | null = null;
  loading = true;
  error   = '';

  journalStatuses = ['Backlog', 'Playing', 'Finished', 'Paused', 'Dropped', 'Replaying'];
  journal: UpsertJournalRequest = this.emptyJournal();
  journalSaved  = false;
  journalSaving = false;

  readonly starPositions = [1, 2, 3, 4, 5];
  hoverRating: number | null = null;

  private routeSub?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private gameService: GameService,
    private journalService: JournalService
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
      this.gameService.getGameDetail(id).subscribe({
        next:  (g) => { this.game = g; this.loading = false; this.loadJournal(id); this.loadScreenshots(g.igdbId); },
        error: ()  => { this.error = 'Game not found.'; this.loading = false; }
      });
    });
  }

  ngOnDestroy() {
    this.routeSub?.unsubscribe();
  }

  private loadScreenshots(igdbId: number | null) {
    this.screenshots = [];
    if (!igdbId) return;
    this.gameService.getGameImages('screenshot', igdbId).subscribe({
      next: (imgs) => { this.screenshots = imgs; }
    });
  }

  carouselScroll(dir: 1 | -1) {
    const el = this.carouselTrack?.nativeElement;
    if (el) el.scrollBy({ left: dir * 480, behavior: 'smooth' });
  }

  openLightbox(img: GameImage)  { this.lightboxImage = img; }
  closeLightbox()               { this.lightboxImage = null; }

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
    this.journalService.upsert(this.game.id, this.journal).subscribe({
      next: () => { this.journalSaved = true; this.journalSaving = false; setTimeout(() => this.journalSaved = false, 2500); },
      error: () => { this.journalSaving = false; }
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
