import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { Game } from '../../services/game.service';
import { genreMeta } from '../../shared/genre-meta';

@Component({
    selector: 'app-game-card',
    templateUrl: './game-card.component.html',
    styleUrls: ['./game-card.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class GameCardComponent {
  @Input() game!: Game;
  @Output() delete = new EventEmitter<number>();

  genreMeta = genreMeta;

  get played(): boolean { return this.game.playtimeHours > 0; }

  get playtimeLabel(): string {
    if (!this.played) return 'Not played';
    const h = this.game.playtimeHours;
    return h < 1 ? `${Math.round(h * 60)}m played` : `${h}h played`;
  }
}
