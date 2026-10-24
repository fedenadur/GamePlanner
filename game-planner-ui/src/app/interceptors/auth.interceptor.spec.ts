import { HTTP_INTERCEPTORS, HttpClient, HttpErrorResponse, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../services/auth.service';
import { AuthInterceptor } from './auth.interceptor';

describe('AuthInterceptor', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['getToken', 'logout']);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches the bearer token when one is stored', () => {
    auth.getToken.and.returnValue('abc');
    http.get('/x').subscribe();
    const req = httpMock.expectOne('/x');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc');
    req.flush({});
  });

  it('sends no Authorization header when there is no token', () => {
    auth.getToken.and.returnValue(null);
    http.get('/x').subscribe();
    const req = httpMock.expectOne('/x');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('logs out and re-throws on a 401 response', () => {
    auth.getToken.and.returnValue('abc');
    let status: number | undefined;
    http.get('/x').subscribe({ error: (e: HttpErrorResponse) => { status = e.status; } });
    httpMock.expectOne('/x').flush('nope', { status: 401, statusText: 'Unauthorized' });
    expect(auth.logout).toHaveBeenCalledTimes(1);
    expect(status).toBe(401);
  });

  it('does not log out on a non-401 error but still re-throws', () => {
    auth.getToken.and.returnValue('abc');
    let status: number | undefined;
    http.get('/x').subscribe({ error: (e: HttpErrorResponse) => { status = e.status; } });
    httpMock.expectOne('/x').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(auth.logout).not.toHaveBeenCalled();
    expect(status).toBe(500);
  });
});
