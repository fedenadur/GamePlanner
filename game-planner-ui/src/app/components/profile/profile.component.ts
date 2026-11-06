import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ProfileService, UserProfile } from '../../services/profile.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { loadError } from '../../shared/messages';
import { DEVICE_ICONS, DEVICE_LABELS, SELECTABLE_DEVICES } from '../../shared/mvp-constants';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class ProfileComponent implements OnInit {
  displayName     = '';
  profileImageUrl = '';
  email           = '';
  isSteamUser     = false;

  loading = false;
  success = false;
  error   = '';

  readonly deviceLabels = DEVICE_LABELS;
  readonly deviceIcons  = DEVICE_ICONS;
  readonly selectableDevices = SELECTABLE_DEVICES;
  readonly preferenceOptions = ['Prefer', 'Neutral', 'Avoid'];
  readonly preferenceGroups: { key: 'singlePlayer' | 'narrative' | 'combat'; label: string }[] = [
    { key: 'singlePlayer', label: 'Single-player' },
    { key: 'narrative',    label: 'Story' },
    { key: 'combat',       label: 'Combat' }
  ];

  devices: string[] = [];
  savedDevices: string[] = [];
  devicesSaving = false;
  devicesSaved  = false;
  devicesError  = '';

  genres: string[] = [];
  favoriteGenres: string[] = [];
  avoidGenres: string[] = [];
  maxGameHours: number | null = null;
  singlePlayer = 'Neutral';
  narrative    = 'Neutral';
  combat       = 'Neutral';
  prefsSaving = false;
  prefsSaved  = false;
  prefsError  = '';
  profileError  = '';
  settingsError = '';

  constructor(private profileService: ProfileService, private settings: UserSettingsService) {}

  ngOnInit() {
    this.loadSettings();

    this.loadProfile();
  }

  loadProfile() {
    this.profileError = '';
    const p = this.profileService.snapshot;
    if (p) this.populate(p);
    else this.profileService.load().subscribe({
      next: p => this.populate(p),
      error: () => this.profileError = loadError('your profile')
    });
  }

  reload() {
    if (this.profileError) this.loadProfile();
    if (this.settingsError) this.loadSettings();
  }

  loadSettings() {
    this.settingsError = '';
    const failed = () => { this.settingsError = loadError('your profile settings'); };
    this.settings.getDevices().subscribe({
      next: r => { this.devices = [...r.devices]; this.savedDevices = [...r.devices]; },
      error: failed
    });
    this.settings.getGenres().subscribe({ next: g => this.genres = g, error: failed });
    this.settings.getPreferences().subscribe({
      next: p => {
        this.favoriteGenres = [...p.favoriteGenres];
        this.avoidGenres    = [...p.avoidGenres];
        this.maxGameHours   = p.maxGameHours;
        this.singlePlayer   = p.singlePlayer;
        this.narrative      = p.narrative;
        this.combat         = p.combat;
      },
      error: failed
    });
  }

  private populate(p: UserProfile) {
    this.displayName     = p.displayName ?? '';
    this.profileImageUrl = p.profileImageUrl ?? '';
    this.email           = p.email ?? '';
    this.isSteamUser     = p.isSteamUser;
  }

  save() {
    this.loading = true;
    this.success = false;
    this.error   = '';

    this.profileService.update({
      displayName:     this.displayName || undefined,
      profileImageUrl: this.profileImageUrl
    }).subscribe({
      next:  () => { this.success = true; this.loading = false; },
      error: (err) => {
        this.error   = err.error ?? 'Failed to save profile.';
        this.loading = false;
      }
    });
  }

  toggleDevice(d: string) {
    this.devicesSaved = false;
    this.devices = this.devices.includes(d) ? this.devices.filter(x => x !== d) : [...this.devices, d];
  }

  moveDevice(i: number, delta: number) {
    const j = i + delta;
    if (j < 0 || j >= this.devices.length) return;
    this.devicesSaved = false;
    const next = [...this.devices];
    [next[i], next[j]] = [next[j], next[i]];
    this.devices = next;
  }

  saveDevices() {
    this.devicesSaving = true;
    this.devicesSaved  = false;
    this.devicesError  = '';
    this.settings.saveDevices(this.devices).subscribe({
      next: () => { this.savedDevices = [...this.devices]; this.devicesSaved = true; this.devicesSaving = false; },
      error: (err) => {
        this.devicesSaving = false;
        if (err.status === 409 && err.error?.code === 'device_in_use') {
          const removed = this.savedDevices.filter(d => !this.devices.includes(d))
            .map(d => DEVICE_LABELS[d] ?? d).join(', ');
          this.devicesError = `Finish or remove the game on ${removed} in Now Playing first.`;
        } else {
          this.devicesError = typeof err.error === 'string' ? err.error : 'Failed to save devices.';
        }
      }
    });
  }

  isGenreDisabled(list: 'favorite' | 'avoid', genre: string): boolean {
    return (list === 'favorite' ? this.avoidGenres : this.favoriteGenres).includes(genre);
  }

  toggleGenre(list: 'favorite' | 'avoid', genre: string) {
    if (this.isGenreDisabled(list, genre)) return;
    this.prefsSaved = false;
    const current = list === 'favorite' ? this.favoriteGenres : this.avoidGenres;
    const next = current.includes(genre) ? current.filter(g => g !== genre) : [...current, genre];
    if (list === 'favorite') this.favoriteGenres = next; else this.avoidGenres = next;
  }

  setPreference(key: 'singlePlayer' | 'narrative' | 'combat', value: string) {
    this.prefsSaved = false;
    this[key] = value;
  }

  savePreferences() {
    this.prefsSaving = true;
    this.prefsSaved  = false;
    this.prefsError  = '';
    this.settings.savePreferences({
      favoriteGenres: this.favoriteGenres,
      avoidGenres:    this.avoidGenres,
      maxGameHours:   this.maxGameHours || null,
      singlePlayer:   this.singlePlayer,
      narrative:      this.narrative,
      combat:         this.combat
    }).subscribe({
      next: () => { this.prefsSaved = true; this.prefsSaving = false; },
      error: (err) => {
        this.prefsSaving = false;
        this.prefsError = typeof err.error === 'string' ? err.error : 'Failed to save preferences.';
      }
    });
  }

  get avatarPreview(): string | null {
    return this.profileImageUrl.trim() || null;
  }

  get initials(): string {
    return (this.displayName || this.email || '?')[0].toUpperCase();
  }
}
