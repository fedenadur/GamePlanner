import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './services/auth.service';
import { ProfileService } from './services/profile.service';
import { GameService, Game } from './services/game.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  private _sidebarOpen = false;
  dropdownOpen = false;

  searchQuery = '';
  searchResults: Game[] = [];
  searchOpen = false;
  private allGames: Game[] | null = null;

  get sidebarOpen() { return this._sidebarOpen; }
  set sidebarOpen(val: boolean) {
    this._sidebarOpen = val;
    document.body.classList.toggle('no-scroll', val);
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.sidebarOpen = false;
    this.closeSearch();
  }

  constructor(
    public auth: AuthService,
    public profile: ProfileService,
    private gameService: GameService,
    private router: Router
  ) {}

  ngOnInit() {
    if (this.auth.isLoggedIn()) this.profile.load().subscribe();
  }

  ngOnDestroy() {
    document.body.classList.remove('no-scroll');
  }

  logout() {
    this.sidebarOpen = false;
    this.profile.clear();
    this.auth.logout();
  }

  onSearchInput() {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) { this.searchResults = []; this.searchOpen = false; return; }

    if (this.allGames) {
      this.filterResults(q);
    } else {
      this.gameService.getGames().subscribe(games => {
        this.allGames = games;
        this.filterResults(q);
      });
    }
  }

  private filterResults(q: string) {
    this.searchResults = (this.allGames ?? [])
      .filter(g => g.name.toLowerCase().includes(q))
      .slice(0, 8);
    this.searchOpen = this.searchResults.length > 0;
  }

  goToGame(id: number) {
    this.closeSearch();
    this.router.navigate(['/games', id]);
  }

  closeSearch() {
    this.searchOpen = false;
    this.searchQuery = '';
    this.searchResults = [];
  }

  get avatarUrl(): string | null { return this.profile.snapshot?.profileImageUrl ?? null; }
  get displayName(): string { return this.profile.snapshot?.displayName ?? 'Account'; }
}
