import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ProfileService, UserProfile } from '../../services/profile.service';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class ProfileComponent implements OnInit {
  displayName     = '';
  profileImageUrl = '';
  email           = '';
  isSteamUser     = false;

  loading = false;
  success = false;
  error   = '';

  constructor(private profileService: ProfileService) {}

  ngOnInit() {
    const p = this.profileService.snapshot;
    if (p) this.populate(p);
    else this.profileService.load().subscribe(p => this.populate(p));
  }

  private populate(p: UserProfile) {
    this.displayName     = p.displayName ?? '';
    this.profileImageUrl = p.profileImageUrl ?? '';
    this.email           = p.email ?? '';
    this.isSteamUser     = p.isSteamUser;
  }

  save() {
    this.loading = true;
    this.success = false;
    this.error   = '';

    this.profileService.update({
      displayName:     this.displayName || undefined,
      profileImageUrl: this.profileImageUrl
    }).subscribe({
      next:  () => { this.success = true; this.loading = false; },
      error: (err) => {
        this.error   = err.error ?? 'Failed to save profile.';
        this.loading = false;
      }
    });
  }

  get avatarPreview(): string | null {
    return this.profileImageUrl.trim() || null;
  }

  get initials(): string {
    return (this.displayName || this.email || '?')[0].toUpperCase();
  }
}
