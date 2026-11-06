import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Game, GameService } from '../../services/game.service';
import { NowPlayingService, NowPlayingSlot } from '../../services/now-playing.service';
import { PlaySessionService } from '../../services/play-session.service';
import { FinishChoice } from '../finish-game-dialog/finish-game-dialog.component';
import { LogSessionResult } from '../log-session-dialog/log-session-dialog.component';
import { loadError } from '../../shared/messages';
import { DEVICE_ICONS, DEVICE_LABELS, STORE_LABELS } from '../../shared/mvp-constants';

@Component({
    selector: 'app-now-playing',
    templateUrl: './now-playing.component.html',
    styleUrls: ['./now-playing.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class NowPlayingComponent implements OnInit {
  slots: NowPlayingSlot[] = [];
  loadingSlots = true;

  allGames: Game[] = [];
  loadingGames = false;
  gamesError = '';

  pickerDevice: string | null = null;
  pickerSearch = '';
  showFinishedAndDropped = false;

  finishFor: NowPlayingSlot | null = null;
  pendingGame: Game | null = null;
  logFor: NowPlayingSlot | null = null;

  saving = false;
  error = '';

  readonly deviceIcons = DEVICE_ICONS;
  readonly deviceLabels = DEVICE_LABELS;
  readonly storeLabels = STORE_LABELS;

  constructor(
    private gameService: GameService,
    private nowPlayingService: NowPlayingService,
    private playSessionService: PlaySessionService
  ) {}

  ngOnInit() {
    this.loadingGames = true;
    this.gameService.getGames().subscribe({
      next: (data) => { this.allGames = data; this.loadingGames = false; },
      error: () => { this.gamesError = loadError('your library'); this.loadingGames = false; }
    });
    this.loadSlots();
  }

  private loadSlots() {
    this.nowPlayingService.getSlots().subscribe({
      next: (slots) => { this.slots = slots; this.loadingSlots = false; },
      error: () => {
        this.error = loadError('Now Playing');
        this.loadingSlots = false;
      }
    });
  }

  retry() {
    this.error = '';
    this.loadingSlots = true;
    this.loadSlots();
    this.refreshGames();
  }

  get isGeneralOnly(): boolean {
    return this.slots.length === 1 && this.slots[0].device === 'General';
  }

  get pickerGames(): Game[] {
    const q = this.pickerSearch.toLowerCase().trim();
    const onSlots = new Set(this.slots.filter(s => s.game).map(s => s.game!.id));
    return this.allGames
      .filter(g => !onSlots.has(g.id))
      .filter(g => this.showFinishedAndDropped || (g.status !== 'Finished' && g.status !== 'Dropped'))
      .filter(g => !q || g.name.toLowerCase().includes(q));
  }

  openPicker(device: string) {
    this.pickerDevice = device;
    this.pickerSearch = '';
    this.showFinishedAndDropped = false;
  }

  closePicker() {
    this.pickerDevice = null;
    this.pickerSearch = '';
  }

  pickGame(game: Game) {
    const device = this.pickerDevice;
    if (device === null) return;
    this.closePicker();
    const slot = this.slots.find(s => s.device === device)!;
    if (slot.game) {
      this.pendingGame = game;
      this.finishFor = slot;
    } else {
      this.start(device, game.id);
    }
  }

  private start(device: string, gameId: number, choice?: FinishChoice) {
    this.error = '';
    this.saving = true;
    this.nowPlayingService.start(device, {
      gameId,
      outgoingOutcome: choice?.outcome ?? null,
      outgoingRating: choice?.rating ?? null
    }).subscribe({
      next: () => { this.saving = false; this.loadSlots(); this.refreshGames(); },
      error: (err) => this.fail(err)
    });
  }

  openFinish(slot: NowPlayingSlot) {
    this.pendingGame = null;
    this.finishFor = slot;
  }

  onFinishChosen(choice: FinishChoice) {
    const slot = this.finishFor!;
    const next = this.pendingGame;
    this.finishFor = null;
    this.pendingGame = null;
    if (next) {
      this.start(slot.device, next.id, choice);
      return;
    }
    this.error = '';
    this.saving = true;
    this.nowPlayingService.remove(slot.device, { outcome: choice.outcome, rating: choice.rating }).subscribe({
      next: () => { this.saving = false; this.loadSlots(); this.refreshGames(); },
      error: (err) => this.fail(err)
    });
  }

  onFinishCancelled() {
    this.finishFor = null;
    this.pendingGame = null;
  }

  onSessionSaved(result: LogSessionResult) {
    const slot = this.logFor!;
    this.logFor = null;
    this.error = '';
    this.saving = true;
    this.playSessionService.log(slot.game!.id, {
      device: slot.device,
      hours: result.hours,
      note: result.note,
      playedOn: result.playedOn
    }).subscribe({
      next: () => { this.saving = false; this.loadSlots(); this.refreshGames(); },
      error: (err) => this.fail(err)
    });
  }

  refreshGames() {
    this.gameService.getGames().subscribe({
      next: (data) => { this.allGames = data; this.gamesError = ''; },
      error: () => { this.gamesError = loadError('your library'); }
    });
  }

  private fail(err: HttpErrorResponse) {
    this.saving = false;
    this.error = err.status === 409 && err.error?.code === 'game_on_other_device'
      ? 'That game is already on another device.'
      : 'Could not save. Try again.';
  }

  trackById(_: number, game: Game) { return game.id; }
}
