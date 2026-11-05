import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface FinishChoice {
  outcome: 'Finished' | 'Dropped' | 'Paused' | 'None';
  rating: number | null;
}

@Component({
  selector: 'app-finish-game-dialog',
  templateUrl: './finish-game-dialog.component.html',
  styleUrls: ['./finish-game-dialog.component.css'],
  standalone: false
})
export class FinishGameDialogComponent {
  @Input() gameName = '';
  @Output() chosen = new EventEmitter<FinishChoice>();
  @Output() cancelled = new EventEmitter<void>();

  readonly starPositions = [1, 2, 3, 4, 5];
  stars = 0;

  setStars(n: number, event: Event) {
    event.stopPropagation();
    this.stars = this.stars === n ? 0 : n;
  }

  choose(outcome: FinishChoice['outcome']) {
    // Journal ratings are 1..10 (half stars): 4 whole stars -> 8.
    const rating = outcome === 'Finished' && this.stars > 0 ? this.stars * 2 : null;
    this.chosen.emit({ outcome, rating });
  }
}
