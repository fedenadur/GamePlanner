import { Component, ChangeDetectionStrategy } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';

@Component({
    selector: 'app-register',
    templateUrl: './register.component.html',
    styleUrls: ['./register.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class RegisterComponent {
  email        = '';
  password     = '';
  username     = '';
  showPassword = false;
  loading      = false;
  error        = '';

  constructor(
    private auth: AuthService,
    private profile: ProfileService,
    private router: Router
  ) {}

  register() {
    if (!this.email || !this.password) return;
    this.loading = true;
    this.error   = '';

    this.auth.register(this.email, this.password, this.username || undefined).subscribe({
      next: () => this.profile.load().subscribe({
        next:  () => this.router.navigate(['/games']),
        error: () => this.router.navigate(['/games'])
      }),
      error: (err) => {
        this.error   = typeof err.error === 'string' ? err.error
                     : err.error?.message ?? 'Could not connect to server.';
        this.loading = false;
      }
    });
  }
}
