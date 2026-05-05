import { Component, OnInit, OnDestroy } from '@angular/core';
import { WorkerService, WorkerStatus } from '../../services/worker.service';

@Component({
  selector: 'app-workers',
  templateUrl: './workers.component.html',
  styleUrls: ['./workers.component.css']
})
export class WorkersComponent implements OnInit, OnDestroy {
  workers: WorkerStatus[] = [];
  triggering = new Set<string>();
  error = '';

  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private workerService: WorkerService) {}

  ngOnInit() { this.load(); }
  ngOnDestroy() { this.stopPolling(); }

  load() {
    this.workerService.getAll().subscribe({
      next:  (w) => { this.workers = w; this.scheduleRefreshIfRunning(); },
      error: () => { this.error = 'Could not load worker status.'; }
    });
  }

  run(name: string) {
    this.triggering.add(name);
    this.error = '';

    this.workerService.run(name).subscribe({
      next: () => {
        this.triggering.delete(name);
        setTimeout(() => this.load(), 800); // brief delay for status to update
        this.startPolling();
      },
      error: () => {
        this.triggering.delete(name);
        this.error = `Failed to trigger ${name}.`;
      }
    });
  }

  private scheduleRefreshIfRunning() {
    const anyRunning = this.workers.some(w => w.status === 'Running');
    if (anyRunning) this.startPolling();
    else            this.stopPolling();
  }

  private startPolling() {
    if (this.pollTimer) return;
    this.pollTimer = setTimeout(() => {
      this.pollTimer = null;
      this.load();
    }, 3000);
  }

  private stopPolling() {
    if (this.pollTimer) { clearTimeout(this.pollTimer); this.pollTimer = null; }
  }

  formatDate(val: string | null): string {
    if (!val) return '—';
    return new Date(val).toLocaleString();
  }
}
