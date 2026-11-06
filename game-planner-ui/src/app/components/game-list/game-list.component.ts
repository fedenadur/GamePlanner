import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Game, GameService } from '../../services/game.service';
import { loadError } from '../../shared/messages';
import { genreMeta } from '../../shared/genre-meta';
import { JOURNAL_STATUSES, statusColor, SELECTABLE_DEVICES, STORE_LABELS, DEVICE_LABELS } from '../../shared/mvp-constants';

export type LengthBucket = 'Short' | 'Medium' | 'Long' | 'Unknown';

// Same boundaries as the backend GameKnowledge.LengthBucket; lengthHours comes from the API (HLTB, else estimate).
export function lengthBucket(lengthHours: number | null | undefined): LengthBucket {
  if (lengthHours == null || lengthHours <= 0) return 'Unknown';
  return lengthHours < 10 ? 'Short' : lengthHours <= 30 ? 'Medium' : 'Long';
}

@Component({
    selector: 'app-game-list',
    templateUrl: './game-list.component.html',
    styleUrls: ['./game-list.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class GameListComponent implements OnInit {
  games: Game[] = [];
  loading = false;
  error = '';
  loadFailed = false;
  showForm = false;

  filterTitle = '';
  selectedGenres = new Set<string>();
  selectedTags   = new Set<string>();
  selectedStatuses = new Set<string>();
  selectedStores   = new Set<string>();
  selectedDevices  = new Set<string>();
  selectedLengths  = new Set<LengthBucket>();
  favoritesOnly = false;

  readonly statuses = JOURNAL_STATUSES;
  readonly devices = SELECTABLE_DEVICES;
  readonly deviceLabels = DEVICE_LABELS;
  readonly storeLabels = STORE_LABELS;
  readonly lengths: LengthBucket[] = ['Short', 'Medium', 'Long', 'Unknown'];

  editingGame: Game | null = null;
  deletingGame: Game | null = null;

  pageSize = 14;
  currentPage = 1;

  genreMeta = genreMeta;

  constructor(private gameService: GameService) {}

  ngOnInit() { this.loadGames(); }

  loadGames() {
    this.loading = true;
    this.error = '';
    this.loadFailed = false;
    this.gameService.getGames().subscribe({
      next: (data) => { this.games = data; this.loading = false; },
      error: () => { this.error = loadError('your games'); this.loadFailed = true; this.loading = false; }
    });
  }

  get allGenres(): string[] {
    const set = new Set<string>();
    this.games.forEach(g => g.genres.forEach(gen => set.add(gen)));
    return Array.from(set).sort();
  }

  get allTags(): string[] {
    const set = new Set<string>();
    this.games.forEach(g => g.tags.forEach(t => set.add(t)));
    return Array.from(set).sort();
  }

  toggleGenre(genre: string) {
    this.selectedGenres.has(genre) ? this.selectedGenres.delete(genre) : this.selectedGenres.add(genre);
    this.currentPage = 1;
  }

  toggleTag(tag: string) {
    this.selectedTags.has(tag) ? this.selectedTags.delete(tag) : this.selectedTags.add(tag);
    this.currentPage = 1;
  }

  private matches(g: Game, ignoreStatus = false): boolean {
    const title = this.filterTitle.toLowerCase().trim();
    return (!title || g.name.toLowerCase().includes(title))
      && (this.selectedGenres.size === 0 || [...this.selectedGenres].every(gen => g.genres.includes(gen)))
      && (this.selectedTags.size === 0 || [...this.selectedTags].every(t => g.tags.includes(t)))
      && (ignoreStatus || this.selectedStatuses.size === 0 || this.selectedStatuses.has(g.status))
      && (this.selectedStores.size === 0 || this.selectedStores.has(g.store))
      && (this.selectedDevices.size === 0 || g.playableDevices.length === 0
          || g.playableDevices.some(d => this.selectedDevices.has(d)))
      && (this.selectedLengths.size === 0 || this.selectedLengths.has(lengthBucket(g.lengthHours)))
      && (!this.favoritesOnly || g.isFavorite);
  }

  get filtered(): Game[] {
    return this.games.filter(g => this.matches(g));
  }

  get totalPages(): number {
    return Math.ceil(this.filtered.length / this.pageSize) || 1;
  }

  get paginated(): Game[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get pages(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  get hasActiveFilters(): boolean {
    return !!this.filterTitle || this.selectedGenres.size > 0 || this.selectedTags.size > 0
      || this.selectedStatuses.size > 0 || this.selectedStores.size > 0 || this.selectedDevices.size > 0
      || this.selectedLengths.size > 0 || this.favoritesOnly;
  }

  get allStores(): string[] {
    return Array.from(new Set(this.games.map(g => g.store).filter(s => !!s))).sort();
  }

  statusColor = statusColor;

  get allCount(): number { return this.games.filter(g => this.matches(g, true)).length; }

  statusCount(status: string): number {
    return this.games.filter(g => g.status === status && this.matches(g, true)).length;
  }

  toggleIn<T>(set: Set<T>, value: T) {
    set.has(value) ? set.delete(value) : set.add(value);
    this.currentPage = 1;
  }

  trackById(_: number, game: Game) { return game.id; }

  onFilterChange() { this.currentPage = 1; }

  clearFilters() {
    this.filterTitle = '';
    this.selectedGenres.clear();
    this.selectedTags.clear();
    this.selectedStatuses.clear();
    this.selectedStores.clear();
    this.selectedDevices.clear();
    this.selectedLengths.clear();
    this.favoritesOnly = false;
    this.currentPage = 1;
  }

  toggleFavorite(game: Game) {
    const next = !game.isFavorite;
    this.gameService.setFavorite(game.id, next).subscribe({
      next: () => { game.isFavorite = next; },
      error: () => { this.error = 'Could not update favorite. Please try again.'; }
    });
  }

  saved() {
    this.editingGame = null;
    this.loadGames();
  }

  confirmDelete() {
    const game = this.deletingGame;
    this.deletingGame = null;
    if (game) this.deleteGame(game.id);
  }

  deleteGame(id: number) {
    this.error = '';
    this.gameService.deleteGame(id).subscribe({
      next: () => this.loadGames(),
      error: () => { this.error = 'Could not delete game. Please try again.'; }
    });
  }
}
