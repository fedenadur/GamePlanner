import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AdminGuard } from './admin.guard';

describe('AdminGuard', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let guard: AdminGuard;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['isAdmin']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    guard = new AdminGuard(auth, router);
  });

  it('redirects to /dashboard when the user is not an admin', () => {
    auth.isAdmin.and.returnValue(false);
    expect(guard.canActivate()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('lets an admin through', () => {
    auth.isAdmin.and.returnValue(true);
    expect(guard.canActivate()).toBeTrue();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
