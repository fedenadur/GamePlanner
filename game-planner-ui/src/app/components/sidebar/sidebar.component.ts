import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
    selector: 'app-sidebar',
    templateUrl: './sidebar.component.html',
    styleUrls: ['./sidebar.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class SidebarComponent {
  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();

  mainNavItems: NavItem[] = [
    { label: 'Home',         icon: '🏠', route: '/dashboard' },
    { label: 'Now Playing',  icon: '🕹️', route: '/now-playing' },
    { label: 'What to Play', icon: '🎲', route: '/what-to-play' },
    { label: 'My Games',     icon: '🎮', route: '/games' },
    { label: 'Journal',      icon: '📖', route: '/journal' },
    { label: 'Calendar',     icon: '🗓️', route: '/calendar' },
    { label: 'Stats',        icon: '📊', route: '/stats' },
  ];

  adminNavItems: NavItem[] = [
    { label: 'Workers', icon: '⚙️', route: '/workers' },
  ];

  constructor(public auth: AuthService, public profile: ProfileService) {}

  get showAdmin(): boolean { return this.auth.isAdmin(); }

  close() { this.closed.emit(); }

  get avatarUrl(): string | null { return this.profile.snapshot?.profileImageUrl ?? null; }
  get displayName(): string { return this.profile.snapshot?.displayName ?? 'Account'; }

  logout() {
    this.profile.clear();
    this.auth.logout();
    this.close();
  }
}
