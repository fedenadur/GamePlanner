import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { StatsDashboard, StatsService } from '../../services/stats.service';

@Component({
    selector: 'app-stats-dashboard',
    templateUrl: './stats-dashboard.component.html',
    styleUrls: ['./stats-dashboard.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class StatsDashboardComponent implements OnInit {
  data: StatsDashboard | null = null;
  loading = true;
  error = false;

  constructor(private statsService: StatsService) {}

  ngOnInit(): void {
    this.statsService.getDashboard().subscribe({
      next: d => { this.data = d; this.loading = false; },
      error: () => { this.error = true; this.loading = false; }
    });
  }

  get completionPercent(): number {
    return this.data?.completion.completionRate ?? 0;
  }

  maxGenreCount(): number {
    const counts = this.data?.genres.topByCount.map(g => g.count) ?? [1];
    return Math.max(...counts, 1);
  }

  maxGenreHours(): number {
    const hours = this.data?.genres.topByHours.map(g => g.hours) ?? [1];
    return Math.max(...hours, 1);
  }

  maxYearCount(): number {
    const counts = this.data?.byYear.map(y => y.count) ?? [1];
    return Math.max(...counts, 1);
  }

  barWidth(value: number, max: number): string {
    return `${Math.round((value / max) * 100)}%`;
  }

  circumference = 2 * Math.PI * 44; // r=44

  get dashOffset(): number {
    return this.circumference * (1 - this.completionPercent / 100);
  }
}
