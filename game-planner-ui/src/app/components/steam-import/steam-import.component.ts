import { Component, OnInit, OnDestroy } from '@angular/core';
import { GameService } from '../../services/game.service';
import { AuthService } from '../../services/auth.service';

const JOB_KEY = 'gp_import_job';
const POLL_INTERVAL_MS = 3000;

@Component({
  selector: 'app-steam-import',
  templateUrl: './steam-import.component.html',
  styleUrls: ['./steam-import.component.css']
})
export class SteamImportComponent implements OnInit, OnDestroy {
  steamId     = '';
  steamLocked = false;

  jobId:     number | null = null;
  status     = '';
  progress   = 0;
  processed  = 0;
  total      = 0;
  error      = '';

  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  get isRunning(): boolean {
    return this.status === 'Pending' || this.status === 'Running';
  }

  get isDone(): boolean {
    return this.status === 'Completed' || this.status === 'Failed';
  }

  constructor(private gameService: GameService, private auth: AuthService) {}

  ngOnInit() {
    const id = this.auth.getSteamId();
    if (id) {
      this.steamId     = id;
      this.steamLocked = true;
    }

    // Resume polling if a job was running before page reload
    const saved = localStorage.getItem(JOB_KEY);
    if (saved) {
      this.jobId = +saved;
      this.poll();
    }
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  import() {
    if (!this.steamId.trim() || this.isRunning) return;
    this.error    = '';
    this.status   = '';
    this.progress = 0;
    this.processed = 0;
    this.total    = 0;

    this.gameService.startSteamImport(this.steamId.trim()).subscribe({
      next: (res) => {
        this.jobId = res.jobId;
        localStorage.setItem(JOB_KEY, String(res.jobId));
        this.status = 'Pending';
        this.schedulePoll();
      },
      error: () => { this.error = 'Could not start import. Is the API running?'; }
    });
  }

  private schedulePoll() {
    this.pollTimer = setTimeout(() => this.poll(), POLL_INTERVAL_MS);
  }

  private poll() {
    if (!this.jobId) return;

    this.gameService.getImportProgress(this.jobId).subscribe({
      next: (res) => {
        this.status    = res.status;
        this.progress  = res.progress;
        this.processed = res.processedGames;
        this.total     = res.totalGames;

        if (res.status === 'Failed') {
          this.error = res.errorMessage ?? 'Import failed.';
          this.clearJob();
        } else if (res.status === 'Completed') {
          this.clearJob();
        } else {
          this.schedulePoll();
        }
      },
      error: () => this.schedulePoll() // keep polling on transient errors
    });
  }

  private stopPolling() {
    if (this.pollTimer) { clearTimeout(this.pollTimer); this.pollTimer = null; }
  }

  private clearJob() {
    this.stopPolling();
    localStorage.removeItem(JOB_KEY);
  }
}
