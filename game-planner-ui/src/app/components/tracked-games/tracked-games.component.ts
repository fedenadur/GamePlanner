import { Component, OnInit, HostListener } from '@angular/core';
import { Router } from '@angular/router';
import { TrackedGamesService, TrackedGame } from '../../services/tracked-games.service';

interface DateGroup {
  label:    string;
  dateKey:  string;
  isPast:   boolean;
  isToday:  boolean;
  isSoon:   boolean; // within 7 days
  games:    TrackedGame[];
}

@Component({
  selector: 'app-tracked-games',
  templateUrl: './tracked-games.component.html',
  styleUrls: ['./tracked-games.component.css']
})
export class TrackedGamesComponent implements OnInit {
  groups:  DateGroup[] = [];
  loading  = true;
  error    = '';
  today    = new Date();

  untrackingId: number | null = null;

  constructor(
    private service: TrackedGamesService,
    private router:  Router
  ) {}

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.service.getAll().subscribe({
      next:  g  => { this.groups = this.buildGroups(g); this.loading = false; },
      error: () => { this.error = 'Failed to load tracked games.'; this.loading = false; }
    });
  }

  untrack(game: TrackedGame) {
    this.untrackingId = game.igdbId;
    this.service.untrack(game.igdbId).subscribe({
      next: () => {
        this.untrackingId = null;
        // Remove from group, clean up empty groups
        this.groups = this.groups
          .map(g => ({ ...g, games: g.games.filter(x => x.igdbId !== game.igdbId) }))
          .filter(g => g.games.length > 0);
      },
      error: () => { this.untrackingId = null; }
    });
  }

  goToGame(game: TrackedGame) {
    if (game.localGameId) this.router.navigate(['/games', game.localGameId]);
  }

  steamUrl(name: string)  { return `https://store.steampowered.com/search/?term=${encodeURIComponent(name)}`; }
  googleUrl(name: string) { return `https://www.google.com/search?q=${encodeURIComponent(name + ' game')}`; }

  daysUntil(dateStr: string): number {
    const d = new Date(dateStr);
    d.setHours(0, 0, 0, 0);
    const t = new Date(this.today);
    t.setHours(0, 0, 0, 0);
    return Math.round((d.getTime() - t.getTime()) / 86400000);
  }

  get upcomingCount(): number { return this.groups.filter(g => !g.isPast).reduce((s, g) => s + g.games.length, 0); }
  get pastCount():     number { return this.groups.filter(g =>  g.isPast).reduce((s, g) => s + g.games.length, 0); }

  private buildGroups(games: TrackedGame[]): DateGroup[] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const map = new Map<string, TrackedGame[]>();

    for (const g of games) {
      const key = g.releaseDate.substring(0, 10);
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(g);
    }

    return Array.from(map.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, gs]) => {
        const d = new Date(key + 'T00:00:00');
        const diff = Math.round((d.getTime() - today.getTime()) / 86400000);
        return {
          label:   this.formatGroupLabel(d, diff),
          dateKey: key,
          isPast:  diff < 0,
          isToday: diff === 0,
          isSoon:  diff >= 0 && diff <= 7,
          games:   gs
        };
      });
  }

  private formatGroupLabel(d: Date, diff: number): string {
    if (diff === 0)  return 'Today';
    if (diff === 1)  return 'Tomorrow';
    if (diff === -1) return 'Yesterday';
    return d.toLocaleDateString('default', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
  }
}
