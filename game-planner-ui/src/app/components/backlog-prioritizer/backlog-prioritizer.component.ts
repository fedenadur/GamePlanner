import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Router } from '@angular/router';
import { BacklogGame, BacklogPrioritizerService } from '../../services/backlog-prioritizer.service';

interface Mode {
  value: string;
  label: string;
  icon: string;
  hint: string;
}

@Component({
    selector: 'app-backlog-prioritizer',
    templateUrl: './backlog-prioritizer.component.html',
    styleUrls: ['./backlog-prioritizer.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class BacklogPrioritizerComponent implements OnInit {
  modes: Mode[] = [
    { value: '',            label: 'Smart Pick',   icon: '🧠', hint: 'Balanced scoring' },
    { value: 'QuickWins',   label: 'Quick Wins',   icon: '⚡', hint: 'Short games first' },
    { value: 'Prestige',    label: 'Prestige',     icon: '🏆', hint: 'Highest rated' },
    { value: 'HiddenGems',  label: 'Hidden Gems',  icon: '💎', hint: 'Underrated picks' },
    { value: 'ComfortZone', label: 'Comfort Zone', icon: '🛋️', hint: 'Your favorite genres' },
    { value: 'FreshStart',  label: 'Fresh Start',  icon: '🌱', hint: 'Never played only' },
  ];

  selectedMode = '';
  games: BacklogGame[] = [];
  loading = false;
  error = '';
  loaded = false;

  constructor(
    private service: BacklogPrioritizerService,
    private router: Router
  ) {}

  ngOnInit() { this.load(); }

  selectMode(value: string) {
    this.selectedMode = value;
    this.load();
  }

  load() {
    this.loading = true;
    this.error   = '';

    this.service.prioritize(this.selectedMode).subscribe({
      next: (res) => {
        this.games   = res;
        this.loading = false;
        this.loaded  = true;
      },
      error: () => {
        this.error   = 'Could not reach the server. Is the API running?';
        this.loading = false;
      }
    });
  }

  viewDetail(gameId: number) {
    this.router.navigate(['/games', gameId]);
  }

  scoreColor(score: number): string {
    if (score >= 80) return 'score-high';
    if (score >= 60) return 'score-mid';
    return 'score-low';
  }

  formatHours(h: number | null): string {
    if (h == null) return '—';
    return h < 1 ? `${Math.round(h * 60)}m` : `${h}h`;
  }

  get topPick(): BacklogGame | null {
    return this.games.length ? this.games[0] : null;
  }

  get restPicks(): BacklogGame[] {
    return this.games.slice(1);
  }

  get activeMode(): Mode {
    return this.modes.find(m => m.value === this.selectedMode) ?? this.modes[0];
  }

  reasonIcon(reason: string): string {
    const r = reason.toLowerCase();
    if (r.includes('never'))     return '🎯';
    if (r.includes('barely'))    return '▶️';
    if (r.includes('excellent')) return '⭐';
    if (r.includes('very well')) return '✨';
    if (r.includes('well rev'))  return '👍';
    if (r.includes('short'))     return '⚡';
    if (r.includes('medium len')) return '⏳';
    if (r.includes('moderate'))  return '📏';
    if (r.includes('recent'))    return '🆕';
    if (r.includes('classic'))   return '🏛️';
    if (r.includes('genre'))     return '🎮';
    if (r.includes('long'))      return '⚠️';
    if (r.includes('few rev'))   return '❓';
    return '•';
  }
}
