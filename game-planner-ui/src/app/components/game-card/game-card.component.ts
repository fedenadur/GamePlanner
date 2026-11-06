import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { Game } from '../../services/game.service';
import { genreMeta } from '../../shared/genre-meta';
import { STORE_LABELS, statusColor } from '../../shared/mvp-constants';

const MAX_GENRES = 3;

@Component({
    selector: 'app-game-card',
    templateUrl: './game-card.component.html',
    styleUrls: ['./game-card.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class GameCardComponent {
  @Input() game!: Game;
  @Output() edit = new EventEmitter<Game>();
  @Output() delete = new EventEmitter<Game>();
  @Output() favoriteToggled = new EventEmitter<Game>();

  genreMeta = genreMeta;
  statusColor = statusColor;
  menuOpen = false;

  get shownGenres(): string[] { return this.game.genres.slice(0, MAX_GENRES); }
  get extraGenres(): number { return Math.max(0, this.game.genres.length - MAX_GENRES); }
  get storeLabel(): string { return STORE_LABELS[this.game.store] ?? this.game.store; }

  get hoursLabel(): string {
    const h = this.game.totalHours;
    if (!h || h <= 0) return 'Not played';
    return h < 1 ? `${Math.round(h * 60)}m` : `${Math.round(h * 10) / 10}h`;
  }
}
