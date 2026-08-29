import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.css'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class LoginComponent implements OnInit {
  email        = '';
  password     = '';
  showPassword = false;
  loading      = false;
  error        = '';

  constructor(
    private auth: AuthService,
    private profile: ProfileService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      if (params['code']) {
        this.loading = true;
        this.auth.exchangeSteamCode(params['code']).subscribe({
          next:  () => this.afterLogin(),
          error: () => { this.error = 'Steam login failed. Please try again.'; this.loading = false; }
        });
      }
      if (params['error']) {
        this.error = 'Steam login failed. Please try again.';
      }
    });
  }

  login() {
    if (!this.email || !this.password) return;
    this.loading = true;
    this.error   = '';

    this.auth.login(this.email, this.password).subscribe({
      next:  () => this.afterLogin(),
      error: (err) => {
        this.error   = err.status === 401 ? 'Invalid email or password.'
                     : typeof err.error === 'string' ? err.error
                     : err.error?.message ?? 'Could not connect to server.';
        this.loading = false;
      }
    });
  }

  private afterLogin() {
    this.profile.load().subscribe({
      next:  () => this.router.navigate(['/games']),
      error: () => this.router.navigate(['/games']) // navigate anyway
    });
  }

  steamLogin() {
    window.location.href = this.auth.getSteamLoginUrl();
  }
}
