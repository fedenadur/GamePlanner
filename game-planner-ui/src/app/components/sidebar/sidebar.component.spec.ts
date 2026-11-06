import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  function make(isAdmin: boolean) {
    const auth = jasmine.createSpyObj<AuthService>('AuthService', ['isAdmin', 'logout']);
    auth.isAdmin.and.returnValue(isAdmin);
    return new SidebarComponent(auth, {} as ProfileService);
  }

  it('main menu is in the final order', () => {
    expect(make(false).mainNavItems.map(i => i.label))
      .toEqual(['Home', 'Now Playing', 'What to Play', 'My Games', 'Journal', 'Calendar', 'Stats']);
  });

  it('hides the Administration section for non-admins', () => {
    const c = make(false);
    expect(c.showAdmin).toBeFalse();
  });

  it('shows Workers under Administration for admins', () => {
    const c = make(true);
    expect(c.showAdmin).toBeTrue();
    expect(c.adminNavItems.map(i => i.label)).toEqual(['Workers']);
  });
});
