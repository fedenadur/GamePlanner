import { Component, EventEmitter, Output } from '@angular/core';
import { GameService, IgdbSearchResult } from '../../services/game.service';

@Component({
    selector: 'app-igdb-search-modal',
    templateUrl: './igdb-search-modal.component.html',
    styleUrls: ['./igdb-search-modal.component.css'],
    standalone: false
})
export class IgdbSearchModalComponent {
  @Output() closed = new EventEmitter<void>();
  @Output() gameAdded = new EventEmitter<void>();

  query = '';
  searching = false;
  searched = false;
  results: IgdbSearchResult[] = [];
  selected: IgdbSearchResult | null = null;
  estimatedMinutes = 60;
  saving = false;
  error = '';

  timeOptions = [30, 60, 120];

  constructor(private gameService: GameService) {}

  search() {
    if (!this.query.trim()) return;
    this.searching = true;
    this.searched = false;
    this.results = [];
    this.selected = null;
    this.error = '';

    this.gameService.searchIgdb(this.query.trim()).subscribe({
      next: (data) => {
        this.results = data;
        this.searched = true;
        this.searching = false;
      },
      error: () => {
        this.error = 'Search failed. Is the API running?';
        this.searched = true;
        this.searching = false;
      }
    });
  }

  select(result: IgdbSearchResult) {
    this.selected = this.selected?.igdbId === result.igdbId ? null : result;
  }

  addGame() {
    if (!this.selected) return;
    this.saving = true;
    this.error = '';

    this.gameService.createGameFromIgdb({
      igdbId: this.selected.igdbId,
      name: this.selected.name,
      estimatedMinutes: this.estimatedMinutes,
      coverUrl: this.selected.coverUrl,
      genres: this.selected.genres
    }).subscribe({
      next: () => {
        this.saving = false;
        this.gameAdded.emit();
        this.closed.emit();
      },
      error: () => {
        this.error = 'Failed to add game.';
        this.saving = false;
      }
    });
  }

  close() {
    this.closed.emit();
  }
}
