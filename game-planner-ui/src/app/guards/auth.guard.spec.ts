import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AuthGuard } from './auth.guard';

describe('AuthGuard', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let guard: AuthGuard;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['isLoggedIn']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    guard = new AuthGuard(auth, router);
  });

  it('blocks and redirects to /login when not logged in', () => {
    auth.isLoggedIn.and.returnValue(false);
    expect(guard.canActivate()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('allows navigation without redirecting when logged in', () => {
    auth.isLoggedIn.and.returnValue(true);
    expect(guard.canActivate()).toBeTrue();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
