import { Injectable } from '@angular/core';
import { Router, UrlTree } from '@angular/router';

// redirectTo cannot carry query params, so /tracked-games redirects through a guard.
@Injectable({ providedIn: 'root' })
export class TrackedRedirectGuard {
  constructor(private router: Router) {}

  canActivate(): UrlTree {
    return this.router.createUrlTree(['/calendar'], { queryParams: { tab: 'tracked' } });
  }
}
