import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { JournalService, JournalEntry, UpsertJournalRequest } from '../../services/journal.service';
import { GameService, Game } from '../../services/game.service';

@Component({
    selector: 'app-journal',
    templateUrl: './journal.component.html',
    styleUrls: ['./journal.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class JournalComponent implements OnInit {
  entries:  JournalEntry[] = [];
  loading = true;
  error   = '';

  filterQuery  = '';
  filterStatus = 'All';
  readonly statuses = ['All', 'Playing', 'Replaying', 'Finished', 'Paused', 'Backlog', 'Dropped'];

  selected:     JournalEntry | null = null;
  editForm:     UpsertJournalRequest = this.emptyForm();
  editSaving  = false;
  editSaved   = false;
  editDeleting = false;

  readonly starPositions = [1, 2, 3, 4, 5];
  hoverRating: number | null = null;

  pickerOpen    = false;
  pickerQuery   = '';
  pickerGames:  Game[] = [];
  pickerLoading = false;

  constructor(private journalService: JournalService, private gameService: GameService) {}

  ngOnInit() {
    this.load();
  }

  private load() {
    this.loading = true;
    this.journalService.getJournal().subscribe({
      next:  (data) => { this.entries = data; this.loading = false; },
      error: ()     => { this.error = 'Could not load journal.'; this.loading = false; }
    });
  }

  get filtered(): JournalEntry[] {
    const q = this.filterQuery.toLowerCase().trim();
    return this.entries.filter(e => {
      const matchStatus = this.filterStatus === 'All' || e.status === this.filterStatus;
      const matchQuery  = !q || e.gameName.toLowerCase().includes(q);
      return matchStatus && matchQuery;
    });
  }

  get playing():  number { return this.entries.filter(e => e.status === 'Playing' || e.status === 'Replaying').length; }
  get finished(): number { return this.entries.filter(e => e.status === 'Finished').length; }
  get backlog():  number { return this.entries.filter(e => e.status === 'Backlog' || e.status === 'Paused').length; }
  get dropped():  number { return this.entries.filter(e => e.status === 'Dropped').length; }

  openEdit(entry: JournalEntry) {
    this.selected  = entry;
    this.editSaved = false;
    this.editForm  = {
      status:         entry.status,
      startedAt:      entry.startedAt    ? entry.startedAt.substring(0, 10)    : null,
      finishedAt:     entry.finishedAt   ? entry.finishedAt.substring(0, 10)   : null,
      lastPlayedAt:   entry.lastPlayedAt ? entry.lastPlayedAt.substring(0, 10) : null,
      userRating:     entry.userRating,
      notes:          entry.notes,
      favoriteMoment: entry.favoriteMoment,
      replayCount:    entry.replayCount
    };
  }

  closeEdit() { this.selected = null; }

  openPicker() {
    this.pickerOpen    = true;
    this.pickerQuery   = '';
    this.pickerLoading = true;
    this.gameService.getGames().subscribe({
      next:  (games) => { this.pickerGames = games; this.pickerLoading = false; },
      error: ()      => { this.pickerLoading = false; }
    });
  }

  closePicker() { this.pickerOpen = false; }

  get pickerFiltered(): Game[] {
    const q   = this.pickerQuery.toLowerCase().trim();
    const ids = new Set(this.entries.map(e => e.gameId));
    return this.pickerGames.filter(g =>
      !ids.has(g.id) && (!q || g.name.toLowerCase().includes(q))
    );
  }

  pickGame(game: Game) {
    this.pickerOpen = false;
    const stub: JournalEntry = {
      gameId: game.id, gameName: game.name, coverImageUrl: game.coverImageUrl ?? null,
      status: 'Backlog', startedAt: null, finishedAt: null, lastPlayedAt: null,
      userRating: null, notes: null, favoriteMoment: null, replayCount: 0,
      createdAt: '', updatedAt: '', daysToFinish: null
    };
    this.openEdit(stub);
  }

  saveEdit() {
    if (!this.selected) return;
    this.editSaving = true;
    this.journalService.upsert(this.selected.gameId, this.editForm).subscribe({
      next: () => {
        this.editSaving = false;
        this.editSaved  = true;
        const idx = this.entries.findIndex(e => e.gameId === this.selected!.gameId);
        if (idx !== -1) {
          this.entries[idx] = { ...this.entries[idx], ...this.editForm };
          this.selected = this.entries[idx];
        } else {
          const newEntry = { ...this.selected!, ...this.editForm };
          this.entries.push(newEntry);
          this.selected = newEntry;
        }
        setTimeout(() => this.editSaved = false, 2000);
      },
      error: () => { this.editSaving = false; }
    });
  }

  deleteEdit() {
    if (!this.selected) return;
    this.editDeleting = true;
    this.journalService.delete(this.selected.gameId).subscribe({
      next: () => {
        this.entries = this.entries.filter(e => e.gameId !== this.selected!.gameId);
        this.selected    = null;
        this.editDeleting = false;
      },
      error: () => { this.editDeleting = false; }
    });
  }

  get daysToFinish(): number | null {
    if (!this.editForm.startedAt || !this.editForm.finishedAt) return null;
    const diff = new Date(this.editForm.finishedAt).getTime() - new Date(this.editForm.startedAt).getTime();
    return diff >= 0 ? Math.round(diff / 86400000) : null;
  }

  setRating(val: number) {
    this.editForm.userRating = this.editForm.userRating === val ? null : val;
  }

  starFill(star: number): number {
    const v = this.hoverRating ?? this.editForm.userRating ?? 0;
    if (v >= star * 2) return 100;
    if (v >= star * 2 - 1) return 50;
    return 0;
  }

  displayRating(rating: number): string {
    const stars = rating / 2;
    return stars % 1 === 0 ? `${stars}` : stars.toFixed(1);
  }

  statusColor(status: string): string {
    const map: Record<string, string> = {
      Playing: 'green', Replaying: 'blue', Finished: 'purple',
      Paused: 'yellow', Backlog: 'muted', Dropped: 'red'
    };
    return map[status] ?? 'muted';
  }

  private emptyForm(): UpsertJournalRequest {
    return { status: 'Backlog', startedAt: null, finishedAt: null, lastPlayedAt: null, userRating: null, notes: null, favoriteMoment: null, replayCount: 0 };
  }
}
