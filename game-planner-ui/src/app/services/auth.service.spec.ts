import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';

const ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function fakeToken(payload: object): string {
  return `h.${btoa(JSON.stringify(payload))}.s`;
}

describe('AuthService.isAdmin', () => {
  let service: AuthService;

  beforeEach(() => {
    localStorage.clear();
    service = new AuthService({} as HttpClient, {} as Router);
  });

  afterEach(() => localStorage.clear());

  it('is true when the role claim is Admin', () => {
    service.saveToken(fakeToken({ [ROLE]: 'Admin' }));
    expect(service.isAdmin()).toBeTrue();
  });

  it('is true when the role claim is an array including Admin', () => {
    service.saveToken(fakeToken({ [ROLE]: ['User', 'Admin'] }));
    expect(service.isAdmin()).toBeTrue();
  });

  it('is false without the role or without a token', () => {
    expect(service.isAdmin()).toBeFalse();
    service.saveToken(fakeToken({ sub: '1' }));
    expect(service.isAdmin()).toBeFalse();
  });
});
