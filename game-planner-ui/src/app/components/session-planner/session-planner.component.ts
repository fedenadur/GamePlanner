import { Component } from '@angular/core';
import { GameService, SessionPlannerResult } from '../../services/game.service';

@Component({
  selector: 'app-session-planner',
  templateUrl: './session-planner.component.html',
  styleUrls: ['./session-planner.component.css']
})
export class SessionPlannerComponent {
  situationOptions = [
    { label: 'Long day',   icon: '😮‍💨', hint: 'Need to turn off my brain',   value: 'longday',    mood: 'chill',   duration: 'short',  gameLength: '',      modes: [] as string[] },
    { label: 'Quick fix',  icon: '⚡',   hint: 'I only have 30 minutes',      value: 'quickfix',   mood: '',        duration: 'short',  gameLength: 'short', modes: ['solo'] },
    { label: 'Date night', icon: '🥂',   hint: 'Playing with someone special', value: 'datenight',  mood: 'coop',    duration: 'medium', gameLength: '',      modes: ['coop'] },
    { label: 'Deep dive',  icon: '🛋️',   hint: 'Nowhere to be, going deep',   value: 'deepdive',   mood: 'story',   duration: 'long',   gameLength: 'long',  modes: [] as string[] },
    { label: 'Decompress', icon: '🌊',   hint: 'Gaming as therapy',           value: 'decompress', mood: 'chill',   duration: 'medium', gameLength: '',      modes: [] as string[] },
    { label: 'Celebrate',  icon: '🎉',   hint: 'Good mood, want to share it', value: 'celebrate',  mood: 'coop',    duration: '',       gameLength: '',      modes: ['coop', 'multiplayer'] },
  ];

  statusOptions = [
    { label: 'All',        value: 'all' },
    { label: '▶ Played',   value: 'played' },
    { label: '⬜ Backlog', value: 'notPlayed' },
  ];

  moodOptions = [
    { label: 'Chill',   icon: '😌', hint: 'Indie, Puzzle',    value: 'chill' },
    { label: 'Focused', icon: '🎯', hint: 'Action, Platform', value: 'focused' },
    { label: 'Story',   icon: '📖', hint: 'RPG, Adventure',   value: 'story' },
    { label: 'Coop',    icon: '🤝', hint: 'Co-op',            value: 'coop' },
  ];

  durationOptions = [
    { label: '⚡ Short  ≤30 min', value: 'short' },
    { label: '⏱ Medium ~1 hour', value: 'medium' },
    { label: '🕐 Long   >90 min', value: 'long' },
  ];

  perspectiveOptions = [
    { label: '👁 First Person', value: 'firstperson' },
    { label: '🧍 Third Person', value: 'thirdperson' },
    { label: '🥽 VR',           value: 'vr' },
    { label: '✦ Other',         value: 'other' },
  ];

  gameModeOptions = [
    { label: '🎮 Solo',        value: 'solo' },
    { label: '🤝 Co-op',       value: 'coop' },
    { label: '🌐 Multiplayer', value: 'multiplayer' },
  ];

  gameLengthOptions = [
    { label: '🕐 Short  ≤15h', value: 'short' },
    { label: '⏳ Medium ≤30h', value: 'medium' },
    { label: '📖 Long   30h+', value: 'long' },
  ];

  selectedSituation   = '';
  selectedStatus      = 'all';
  selectedMood        = '';
  selectedDuration    = '';
  selectedPerspective = '';
  selectedModes       = new Set<string>();
  selectedGameLength  = '';

  result:  SessionPlannerResult | null = null;
  loading  = false;
  error    = '';

  constructor(private gameService: GameService) {}

  selectSituation(s: typeof this.situationOptions[0]) {
    if (this.selectedSituation === s.value) {
      this.selectedSituation = '';
      return;
    }
    this.selectedSituation  = s.value;
    this.selectedMood       = s.mood;
    this.selectedDuration   = s.duration;
    this.selectedGameLength = s.gameLength;
    this.selectedModes      = new Set(s.modes);
  }

  toggleMode(value: string) {
    this.selectedModes.has(value) ? this.selectedModes.delete(value) : this.selectedModes.add(value);
  }

  suggest() {
    this.loading = true;
    this.error   = '';
    this.result  = null;

    this.gameService.planSession({
      playedStatus: this.selectedStatus,
      mood:         this.selectedMood,
      duration:     this.selectedDuration,
      perspective:  this.selectedPerspective,
      gameModes:    [...this.selectedModes],
      gameLength:   this.selectedGameLength,
    }).subscribe({
      next:  (res) => { this.result = res; this.loading = false; },
      error: ()    => { this.error = 'Could not reach the server.'; this.loading = false; }
    });
  }

  tryAgain() { this.suggest(); }

  get hasFilters(): boolean {
    return !!this.selectedSituation || this.selectedStatus !== 'all' || !!this.selectedMood
      || !!this.selectedDuration || !!this.selectedPerspective
      || this.selectedModes.size > 0 || !!this.selectedGameLength;
  }

  clearFilters() {
    this.selectedSituation   = '';
    this.selectedStatus      = 'all';
    this.selectedMood        = '';
    this.selectedDuration    = '';
    this.selectedPerspective = '';
    this.selectedModes.clear();
    this.selectedGameLength  = '';
    this.result              = null;
  }
}
