import { Router, UrlTree } from '@angular/router';
import { TrackedRedirectGuard } from './tracked-redirect.guard';

describe('TrackedRedirectGuard', () => {
  it('redirects to the calendar Tracked tab', () => {
    const tree = {} as UrlTree;
    const router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(tree);
    expect(new TrackedRedirectGuard(router).canActivate()).toBe(tree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/calendar'], { queryParams: { tab: 'tracked' } });
  });
});
