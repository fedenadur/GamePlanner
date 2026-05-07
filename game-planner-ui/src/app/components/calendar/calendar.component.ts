import { Component, OnInit, HostListener } from '@angular/core';
import { Router } from '@angular/router';
import { CalendarService, CalendarData, CalendarGame, CalendarFilters } from '../../services/calendar.service';
import { TrackedGamesService } from '../../services/tracked-games.service';

interface CalendarCell {
  date:  Date | null;
  key:   string;
  games: CalendarGame[];
}

const HOT_THRESHOLD = 100;

@Component({
  selector: 'app-calendar',
  templateUrl: './calendar.component.html',
  styleUrls: ['./calendar.component.css']
})
export class CalendarComponent implements OnInit {
  mode: 'month' | 'week' = 'month';

  today         = new Date();
  year          = this.today.getFullYear();
  month         = this.today.getMonth() + 1;
  weekStartDate = this.getWeekStart(this.today);

  data: CalendarData = {};
  cells: CalendarCell[] = [];
  loading = false;
  error   = '';

  // Filters
  titleFilter         = '';
  genreFilter         = '';
  hypeMinFilter       = 0;
  favoriteGenresOnly  = false;
  ownedOnly           = false;

  availableGenres: string[] = [];

  showFilters = true;
  collapsed = { anticipated: false, taste: false, thisWeek: false };
  showAllThisWeek = false;

  modal: { game: CalendarGame; date: Date | null } | null = null;

  readonly DAY_LABELS    = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  readonly HOT_THRESHOLD = HOT_THRESHOLD;
  readonly HYPE_OPTIONS  = [
    { label: 'Any Hype', value: 0   },
    { label: '10+ Hypes', value: 10  },
    { label: '50+ Hypes', value: 50  },
    { label: '100+ Hypes', value: 100 },
  ];

  trackingInProgress = false;

  constructor(
    private calendarService: CalendarService,
    private trackedGamesService: TrackedGamesService,
    private router: Router
  ) {}

  ngOnInit() { this.load(); }

  get isViewingToday(): boolean {
    if (this.mode === 'month')
      return this.year === this.today.getFullYear() && this.month === this.today.getMonth() + 1;
    const weekEnd = new Date(this.weekStartDate);
    weekEnd.setDate(this.weekStartDate.getDate() + 7);
    return this.today >= this.weekStartDate && this.today < weekEnd;
  }

  get label(): string {
    if (this.mode === 'month') {
      const d = new Date(this.year, this.month - 1, 1);
      return `${d.toLocaleString('default', { month: 'long' })} ${this.year}`;
    }
    const weekEnd = new Date(this.weekStartDate);
    weekEnd.setDate(this.weekStartDate.getDate() + 6);
    const startStr = this.weekStartDate.toLocaleDateString('default', { month: 'short', day: 'numeric' });
    const endStr   = weekEnd.toLocaleDateString('default', { month: 'short', day: 'numeric', year: 'numeric' });
    return `${startStr} – ${endStr}`;
  }

  get topAnticipated(): { game: CalendarGame; date: Date }[] {
    return this.gamesWithDates()
      .sort((a, b) => b.game.hypes - a.game.hypes)
      .slice(0, 5);
  }

  get genreMatches(): { game: CalendarGame; date: Date }[] {
    if (this.favoriteGenresOnly) return [];
    return this.gamesWithDates()
      .filter(e => e.game.genreMatch)
      .sort((a, b) => b.game.hypes - a.game.hypes)
      .slice(0, 8);
  }

  get thisWeekGames(): { game: CalendarGame; date: Date }[] {
    if (this.mode === 'week') return [];
    const weekStart = this.getWeekStart(this.today);
    const weekEnd   = new Date(weekStart);
    weekEnd.setDate(weekStart.getDate() + 7);
    return this.gamesWithDates()
      .filter(e => e.date >= weekStart && e.date < weekEnd)
      .sort((a, b) => b.game.hypes - a.game.hypes);
  }

  visibleGames(games: CalendarGame[]): CalendarGame[] {
    if (!this.titleFilter) return games;
    const q = this.titleFilter.toLowerCase();
    return games.filter(g => g.name.toLowerCase().includes(q));
  }

  isHot(g: CalendarGame): boolean { return g.hypes >= HOT_THRESHOLD; }

  weekSlottedGames(games: CalendarGame[]): { game: CalendarGame; featured: boolean }[] {
    const visible = this.visibleGames(games).slice().sort((a, b) => b.hypes - a.hypes);
    const result: { game: CalendarGame; featured: boolean }[] = [];
    let slots = 0;
    for (const g of visible) {
      const featured = this.isHot(g);
      const cost = featured ? 2 : 1;
      if (slots + cost > 4) break;
      result.push({ game: g, featured });
      slots += cost;
    }
    return result;
  }

  monthSlottedGames(games: CalendarGame[]): { game: CalendarGame; size: 'full' | 'half' | 'small' }[] {
    const visible = this.visibleGames(games).slice().sort((a, b) => b.hypes - a.hypes);
    const result: { game: CalendarGame; size: 'full' | 'half' | 'small' }[] = [];
    let slotsUsed = 0;
    for (const g of visible) {
      if (slotsUsed >= 4) break;
      let size: 'full' | 'half' | 'small';
      let cost: number;
      if (g.hypes >= 100)     { size = 'full';  cost = 4; }
      else if (g.hypes >= 50) { size = 'half';  cost = 2; }
      else                    { size = 'small'; cost = 1; }
      if (slotsUsed + cost > 4) continue;
      result.push({ game: g, size });
      slotsUsed += cost;
    }
    return result;
  }

  hypeFlame(hypes: number): string {
    if (hypes >= 100) return '🔥🔥🔥';
    if (hypes >= 50)  return '🔥🔥';
    if (hypes >= 10)  return '🔥';
    return '';
  }

  setMode(m: 'month' | 'week') {
    this.mode = m;
    if (m === 'week') this.weekStartDate = this.getWeekStart(this.today);
    this.load();
  }

  prev() {
    if (this.mode === 'month') {
      if (--this.month < 1) { this.month = 12; this.year--; }
    } else {
      const d = new Date(this.weekStartDate);
      d.setDate(d.getDate() - 7);
      this.weekStartDate = d;
    }
    this.load();
  }

  next() {
    if (this.mode === 'month') {
      if (++this.month > 12) { this.month = 1; this.year++; }
    } else {
      const d = new Date(this.weekStartDate);
      d.setDate(d.getDate() + 7);
      this.weekStartDate = d;
    }
    this.load();
  }

  onServerFilterChange() { this.load(); }

  goToday() {
    this.year          = this.today.getFullYear();
    this.month         = this.today.getMonth() + 1;
    this.weekStartDate = this.getWeekStart(this.today);
    this.load();
  }

  @HostListener('document:keydown.escape')
  onEscape() { this.modal = null; }

  load() {
    this.loading = true;
    this.error   = '';
    this.modal   = null;

    const filters: CalendarFilters = {
      genre:              this.genreFilter   || undefined,
      hypeMin:            this.hypeMinFilter || undefined,
      ownedOnly:          this.ownedOnly     || undefined,
      favoriteGenresOnly: this.favoriteGenresOnly || undefined,
    };

    const obs = this.mode === 'month'
      ? this.calendarService.getMonth(this.year, this.month, filters)
      : (() => { const p = this.weekParams(); return this.calendarService.getWeek(p.year, p.month, p.week, filters); })();

    obs.subscribe({
      next: (d) => {
        this.data = d;
        this.buildCells();
        if (!this.genreFilter) this.refreshGenreList();
        this.showAllThisWeek = false;
        this.loading = false;
      },
      error: () => { this.error = 'Failed to load calendar.'; this.loading = false; }
    });
  }

  buildCells() {
    this.cells = [];

    if (this.mode === 'month') {
      const firstDay = new Date(this.year, this.month - 1, 1);
      const lastDay  = new Date(this.year, this.month, 0);

      for (let i = 0; i < firstDay.getDay(); i++)
        this.cells.push({ date: null, key: '', games: [] });

      for (let d = 1; d <= lastDay.getDate(); d++) {
        const date = new Date(this.year, this.month - 1, d);
        const key  = this.toKey(date);
        this.cells.push({ date, key, games: this.data[key] ?? [] });
      }
    } else {
      for (let i = 0; i < 7; i++) {
        const date = new Date(this.weekStartDate);
        date.setDate(this.weekStartDate.getDate() + i);
        const key = this.toKey(date);
        this.cells.push({ date, key, games: this.data[key] ?? [] });
      }
    }
  }

  onGameClick(_event: MouseEvent, game: CalendarGame, date: Date | null = null) {
    if (game.localGameId) {
      this.router.navigate(['/games', game.localGameId]);
    } else {
      this.modal = { game, date };
    }
  }

  closeModal() { this.modal = null; }

  toggleTrack(game: CalendarGame) {
    if (this.trackingInProgress) return;
    this.trackingInProgress = true;
    const obs = game.isTracked
      ? this.trackedGamesService.untrack(game.igdbId)
      : this.trackedGamesService.track(game.igdbId);
    obs.subscribe({
      next: () => {
        game.isTracked = !game.isTracked;
        this.trackingInProgress = false;
      },
      error: () => { this.trackingInProgress = false; }
    });
  }

  steamUrl(name: string): string {
    return `https://store.steampowered.com/search/?term=${encodeURIComponent(name)}`;
  }

  googleUrl(name: string): string {
    return `https://www.google.com/search?q=${encodeURIComponent(name + ' game')}`;
  }

  private gamesWithDates(): { game: CalendarGame; date: Date }[] {
    const result: { game: CalendarGame; date: Date }[] = [];
    for (const cell of this.cells) {
      if (!cell.date) continue;
      for (const g of cell.games) result.push({ game: g, date: cell.date });
    }
    return result;
  }

  private refreshGenreList() {
    const all = Object.values(this.data).flat().flatMap(g => g.genres);
    this.availableGenres = [...new Set(all)].sort();
  }

  private weekParams(): { year: number; month: number; week: number } {
    const mid = new Date(this.weekStartDate);
    mid.setDate(this.weekStartDate.getDate() + 3);

    const year  = mid.getFullYear();
    const month = mid.getMonth() + 1;

    const firstOfMonth = new Date(year, month - 1, 1);
    const startOfWeek1 = new Date(firstOfMonth);
    startOfWeek1.setDate(firstOfMonth.getDate() - firstOfMonth.getDay());
    const week = Math.round((this.weekStartDate.getTime() - startOfWeek1.getTime()) / (7 * 24 * 60 * 60 * 1000)) + 1;

    return { year, month, week };
  }

  private getWeekStart(d: Date): Date {
    const s = new Date(d);
    s.setDate(d.getDate() - d.getDay());
    s.setHours(0, 0, 0, 0);
    return s;
  }

  private toKey(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
