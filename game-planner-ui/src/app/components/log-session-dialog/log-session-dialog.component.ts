import { Component, EventEmitter, Input, Output } from '@angular/core';
import { parseHours } from '../../shared/hours';
import { DEVICE_ICONS, DEVICE_LABELS } from '../../shared/mvp-constants';

export interface LogSessionResult {
  hours: number;
  note: string | null;
  playedOn: string;
}

@Component({
  selector: 'app-log-session-dialog',
  templateUrl: './log-session-dialog.component.html',
  styleUrls: ['./log-session-dialog.component.css'],
  standalone: false
})
export class LogSessionDialogComponent {
  @Input() gameName = '';
  @Input() device = '';
  @Output() saved = new EventEmitter<LogSessionResult>();
  @Output() cancelled = new EventEmitter<void>();

  hoursText = '';
  note = '';
  playedOn = LogSessionDialogComponent.toIsoDate(new Date());
  readonly today = LogSessionDialogComponent.toIsoDate(new Date());

  static toIsoDate(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  get deviceLabel(): string { return `${DEVICE_ICONS[this.device] ?? ''} ${DEVICE_LABELS[this.device] ?? this.device}`; }

  get hoursError(): string {
    return this.hoursText.trim() !== '' && parseHours(this.hoursText) === null
      ? 'Enter hours between 0.1 and 24.' : '';
  }

  get dateValid(): boolean { return !!this.playedOn && this.playedOn <= this.today; }

  get canSave(): boolean { return parseHours(this.hoursText) !== null && this.dateValid; }

  save() {
    const hours = parseHours(this.hoursText);
    if (hours === null || !this.dateValid) return;
    this.saved.emit({ hours, note: this.note.trim() || null, playedOn: this.playedOn });
  }
}
