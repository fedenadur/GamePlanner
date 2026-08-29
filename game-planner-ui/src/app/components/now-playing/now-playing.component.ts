import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Game, GameService } from '../../services/game.service';
import { NowPlayingService } from '../../services/now-playing.service';

@Component({
    selector: 'app-now-playing',
    templateUrl: './now-playing.component.html',
    styleUrls: ['./now-playing.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class NowPlayingComponent implements OnInit {
  readonly MAX_SLOTS = 3;

  nowPlaying: (Game | null)[] = [null, null, null];
  loadingSlots = true;

  allGames: Game[] = [];
  loadingGames = false;

  pickerOpenForSlot: number | null = null;
  pickerSearch = '';
  saving = false;
  error = '';

  constructor(
    private gameService: GameService,
    private nowPlayingService: NowPlayingService
  ) {}

  ngOnInit() {
    this.loadingGames = true;
    this.gameService.getGames().subscribe({
      next: (data) => { this.allGames = data; this.loadingGames = false; },
      error: () => { this.loadingGames = false; }
    });

    this.nowPlayingService.getSlots().subscribe({
      next: (slots) => {
        slots.forEach(s => { this.nowPlaying[s.slot] = s.game; });
        this.loadingSlots = false;
      },
      error: () => {
        this.error = 'Could not load Now Playing. Is the API running?';
        this.loadingSlots = false;
      }
    });
  }

  get pickerGames(): Game[] {
    const q = this.pickerSearch.toLowerCase().trim();
    const pickedIds = new Set(this.nowPlaying.filter(g => g !== null).map(g => g!.id));
    return this.allGames
      .filter(g => !pickedIds.has(g.id))
      .filter(g => !q || g.name.toLowerCase().includes(q));
  }

  openPicker(slot: number) {
    this.pickerOpenForSlot = slot;
    this.pickerSearch = '';
  }

  closePicker() {
    this.pickerOpenForSlot = null;
    this.pickerSearch = '';
  }

  pickGame(game: Game) {
    const slot = this.pickerOpenForSlot;
    if (slot === null) return;
    this.closePicker();

    this.saving = true;
    this.nowPlayingService.setSlot(slot, game.id).subscribe({
      next: () => {
        this.nowPlaying[slot] = game;
        this.saving = false;
      },
      error: () => {
        this.error = 'Failed to save. Try again.';
        this.saving = false;
      }
    });
  }

  removeGame(slot: number) {
    this.nowPlayingService.clearSlot(slot).subscribe({
      next: () => { this.nowPlaying[slot] = null; },
      error: () => { this.error = 'Failed to remove. Try again.'; }
    });
  }

  get filledCount(): number {
    return this.nowPlaying.filter(g => g !== null).length;
  }

  trackById(_: number, game: Game) { return game.id; }
}
