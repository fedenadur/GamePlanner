import { Component, Input, Output, EventEmitter } from '@angular/core';
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
  styleUrls: ['./sidebar.component.css']
})
export class SidebarComponent {
  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();

  mainNavItems: NavItem[] = [
    { label: 'My Games',        icon: '🎮', route: '/games' },
    { label: 'Now Playing',     icon: '🕹️', route: '/now-playing' },
    { label: 'Session Planner', icon: '📅', route: '/planner' },
    { label: 'Calendar',        icon: '🗓️', route: '/calendar' },
    { label: 'Tracked Games',   icon: '🔔', route: '/tracked-games' },
    { label: 'Backlog',         icon: '📋', route: '/backlog-prioritizer' },
    { label: 'Journal',         icon: '📖', route: '/journal' },
    { label: 'Stats',           icon: '📊', route: '/stats' },
  ];

  utilNavItems: NavItem[] = [
    { label: 'Steam Import', icon: '📥', route: '/steam' },
    { label: 'Workers',      icon: '⚙️', route: '/workers' },
  ];

  constructor(public auth: AuthService, public profile: ProfileService) {}

  close() { this.closed.emit(); }

  get avatarUrl(): string | null { return this.profile.snapshot?.profileImageUrl ?? null; }
  get displayName(): string { return this.profile.snapshot?.displayName ?? 'Account'; }

  logout() {
    this.profile.clear();
    this.auth.logout();
    this.close();
  }
}
