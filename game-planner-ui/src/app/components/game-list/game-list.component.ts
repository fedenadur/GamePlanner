import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Game, GameService } from '../../services/game.service';
import { genreMeta } from '../../shared/genre-meta';

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
  showForm = false;

  filterTitle = '';
  selectedGenres = new Set<string>();
  selectedTags   = new Set<string>();
  playtimeFilter: 'all' | 'played' | 'notPlayed' = 'all';

  pageSize = 14;
  currentPage = 1;

  genreMeta = genreMeta;

  constructor(private gameService: GameService) {}

  ngOnInit() { this.loadGames(); }

  loadGames() {
    this.loading = true;
    this.gameService.getGames().subscribe({
      next: (data) => { this.games = data; this.loading = false; },
      error: () => { this.error = 'Could not load games. Is the API running?'; this.loading = false; }
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

  get filtered(): Game[] {
    const title = this.filterTitle.toLowerCase().trim();

    return this.games.filter(g => {
      const matchTitle    = !title || g.name.toLowerCase().includes(title);
      const matchGenre    = this.selectedGenres.size === 0
        || [...this.selectedGenres].every(gen => g.genres.includes(gen));
      const matchTag      = this.selectedTags.size === 0
        || [...this.selectedTags].every(t => g.tags.includes(t));
      const hours         = g.playtimeHours ?? 0;
      const matchPlaytime =
        this.playtimeFilter === 'all'      ? true :
        this.playtimeFilter === 'played'   ? hours > 0 :
                                             hours === 0;
      return matchTitle && matchGenre && matchTag && matchPlaytime;
    });
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
    return !!this.filterTitle || this.selectedGenres.size > 0
      || this.selectedTags.size > 0 || this.playtimeFilter !== 'all';
  }

  private get filteredBySearch(): Game[] {
    const title = this.filterTitle.toLowerCase().trim();
    return this.games.filter(g => {
      const matchTitle = !title || g.name.toLowerCase().includes(title);
      const matchGenre = this.selectedGenres.size === 0
        || [...this.selectedGenres].every(gen => g.genres.includes(gen));
      const matchTag   = this.selectedTags.size === 0
        || [...this.selectedTags].every(t => g.tags.includes(t));
      return matchTitle && matchGenre && matchTag;
    });
  }

  get allCount(): number       { return this.filteredBySearch.length; }
  get playedCount(): number    { return this.filteredBySearch.filter(g => g.playtimeHours > 0).length; }
  get notPlayedCount(): number { return this.filteredBySearch.filter(g => g.playtimeHours === 0).length; }

  trackById(_: number, game: Game) { return game.id; }

  onFilterChange() { this.currentPage = 1; }

  setPlaytimeFilter(f: 'all' | 'played' | 'notPlayed') {
    this.playtimeFilter = f;
    this.currentPage = 1;
  }

  clearFilters() {
    this.filterTitle = '';
    this.selectedGenres.clear();
    this.selectedTags.clear();
    this.playtimeFilter = 'all';
    this.currentPage = 1;
  }

  deleteGame(id: number) {
    this.gameService.deleteGame(id).subscribe({ next: () => this.loadGames() });
  }
}
