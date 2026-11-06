import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { Game, GameService } from '../../services/game.service';
import { DEVICE_ICONS, DEVICE_LABELS, SELECTABLE_DEVICES, STORE_DEFAULT_DEVICES, STORE_LABELS } from '../../shared/mvp-constants';

@Component({
    selector: 'app-game-edit-dialog',
    templateUrl: './game-edit-dialog.component.html',
    styleUrls: ['./game-edit-dialog.component.css'],
    standalone: false
})
export class GameEditDialogComponent implements OnInit {
  @Input() game!: Game;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly devices = SELECTABLE_DEVICES;
  readonly deviceLabels = DEVICE_LABELS;
  readonly deviceIcons = DEVICE_ICONS;
  readonly storeCodes = Object.keys(STORE_LABELS);
  readonly storeLabels = STORE_LABELS;

  name = '';
  coverImageUrl = '';
  store = '';
  private originalStore = '';
  private originalHours: number | null = null;
  selectedDevices = new Set<string>();
  hours: number | null = null;
  genresText = '';
  isFavorite = false;
  saving = false;
  error = '';

  constructor(private gameService: GameService) {}

  ngOnInit() {
    const g = this.game;
    this.name = g.name;
    this.coverImageUrl = g.coverImageUrl ?? '';
    this.store = g.store;
    this.originalStore = g.store;
    this.selectedDevices = new Set(g.playableDevices);
    this.hours = g.estimatedMinutes ? Math.round((g.estimatedMinutes / 60) * 10) / 10 : null;
    this.originalHours = this.hours;
    this.genresText = g.genres.join(', ');
    this.isFavorite = g.isFavorite;
  }

  private sameAsDefault(store: string): boolean {
    const def = STORE_DEFAULT_DEVICES[store] ?? [];
    return def.length === this.selectedDevices.size && def.every(d => this.selectedDevices.has(d));
  }

  get invalidHours(): boolean { return this.hours !== null && this.hours < 0; }

  onStoreChange(newStore: string) {
    if (this.sameAsDefault(this.originalStore)) {
      this.selectedDevices = new Set(STORE_DEFAULT_DEVICES[newStore] ?? []);
    }
    this.originalStore = newStore;
    this.store = newStore;
  }

  toggleDevice(d: string) {
    this.selectedDevices.has(d) ? this.selectedDevices.delete(d) : this.selectedDevices.add(d);
  }

  save() {
    const name = this.name.trim();
    if (this.invalidHours) return;
    if (!name) { this.error = 'Name is required.'; return; }
    this.saving = true;
    this.error = '';
    this.gameService.updateGame(this.game.id, {
      name,
      genres: this.genresText.split(',').map(x => x.trim()).filter(x => !!x),
      estimatedMinutes: this.hours === this.originalHours ? this.game.estimatedMinutes : Math.round((this.hours ?? 0) * 60),
      tags: this.game.tags,
      store: this.store,
      playableDevices: this.sameAsDefault(this.store) ? [] : [...this.selectedDevices],
      isFavorite: this.isFavorite,
      // '' clears a stored cover (sp_UpdateGame keeps the cover only when it gets NULL)
      coverImageUrl: this.coverImageUrl.trim()
    }).subscribe({
      next: () => { this.saving = false; this.saved.emit(); },
      error: () => { this.saving = false; this.error = 'Could not save changes. Please try again.'; }
    });
  }
}
