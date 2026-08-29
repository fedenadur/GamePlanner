import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Dashboard, DashboardService } from '../../services/dashboard.service';

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class DashboardComponent implements OnInit {
  data: Dashboard | null = null;
  loading = true;
  error = false;

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.get().subscribe({
      next: d => { this.data = d; this.loading = false; },
      error: () => { this.error = true; this.loading = false; }
    });
  }

  daysUntil(date: string): number {
    const ms = new Date(date).getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / (1000 * 60 * 60 * 24)));
  }

  groupUpcomingByDay(games: any[]) {
    const groups = new Map<string, any[]>();
    games.forEach(game => {
      const date = new Date(game.releaseDate).toDateString();
      if (!groups.has(date)) groups.set(date, []);
      groups.get(date)!.push(game);
    });
    return Array.from(groups.entries()).map(([date, games]) => ({
      date: new Date(date),
      games
    }));
  }
}
