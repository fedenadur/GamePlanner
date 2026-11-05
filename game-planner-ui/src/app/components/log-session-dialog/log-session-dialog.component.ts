import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { parseHours } from '../../shared/hours';
import { DEVICE_ICONS, DEVICE_LABELS } from '../../shared/mvp-constants';

export interface LogSessionResult {
  hours: number;
  note: string | null;
  playedOn: string;
  device: string;
}

export interface InitialSession {
  hours: number;
  note?: string | null;
  playedOn: string;
  device: string;
}

@Component({
  selector: 'app-log-session-dialog',
  templateUrl: './log-session-dialog.component.html',
  styleUrls: ['./log-session-dialog.component.css'],
  standalone: false
})
export class LogSessionDialogComponent implements OnInit {
  @Input() gameName = '';
  @Input() device = '';
  @Input() devices: string[] | null = null;
  @Input() initial: InitialSession | null = null;
  @Output() saved = new EventEmitter<LogSessionResult>();
  @Output() cancelled = new EventEmitter<void>();

  selectedDevice = '';
  hoursText = '';
  note = '';
  playedOn = LogSessionDialogComponent.toIsoDate(new Date());
  readonly today = LogSessionDialogComponent.toIsoDate(new Date());

  ngOnInit() {
    if (this.initial) {
      this.hoursText = String(this.initial.hours);
      this.note = this.initial.note ?? '';
      this.playedOn = this.initial.playedOn;
      this.selectedDevice = this.initial.device;
    } else {
      this.selectedDevice = this.device || this.devices?.[0] || '';
    }
  }

  static toIsoDate(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  readonly deviceLabels = DEVICE_LABELS;
  readonly deviceIcons = DEVICE_ICONS;

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
    this.saved.emit({ hours, note: this.note.trim() || null, playedOn: this.playedOn,
      device: this.devices ? this.selectedDevice : this.device });
  }
}
