import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { Dashboard, DashboardService } from '../../services/dashboard.service';
import { NowPlayingService } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';

describe('DashboardComponent', () => {
  const dash = {
    username: 'u', continuePlaying: [], upcomingThisWeek: [],
    tonightRecommendation: { gameId: 1, name: 'Hades', coverImageUrl: null, reason: 'r', mainStoryHours: 20 },
    backlogSummary: { totalBacklog: 0, completed: 0, completionRate: 0, unplayed: 0 },
    quickStats: { totalOwned: 0, totalPlaytimeHours: 0, favoriteGenre: null, mostPlayedGame: null }
  } as Dashboard;

  let fixture: ComponentFixture<DashboardComponent>;
  let devices$: Subject<{ devices: string[] }>;

  function setup(devicesObs?: any) {
    devices$ = new Subject();
    TestBed.configureTestingModule({
      declarations: [DashboardComponent],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        { provide: DashboardService, useValue: { get: () => of(dash) } },
        { provide: NowPlayingService, useValue: { getSlots: () => of([]), start: () => of(undefined) } },
        { provide: UserSettingsService, useValue: { getDevices: () => devicesObs ?? devices$ } },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
      ]
    });
    fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
  }

  const startBtn = () => fixture.nativeElement.querySelector('.action-buttons button') as HTMLButtonElement | null;

  it('disables Start until devices load', () => {
    setup();
    expect(startBtn()!.disabled).toBeTrue();
  });

  it('does not offer Start when devices fail to load', () => {
    setup(throwError(() => new Error('x')));
    fixture.detectChanges();
    expect(startBtn()).toBeNull();
  });

  it('labels Start with the first device', () => {
    setup(of({ devices: ['SteamDeck'] }));
    expect(startBtn()!.disabled).toBeFalse();
    expect(startBtn()!.textContent!.trim()).toBe('▶ Start on Steam Deck');
  });

  it('starts on General when the user has no devices', () => {
    setup(of({ devices: [] }));
    expect(startBtn()!.disabled).toBeFalse();
    expect(startBtn()!.textContent!.trim()).toBe('▶ Start on General');
  });

  it('dayLabel gives Today, Tomorrow and in Nd', () => {
    setup();
    const c = fixture.componentInstance;
    const at = (days: number) => new Date(Date.now() + days * 86400000 - 3600000).toISOString();
    expect(c.dayLabel(new Date(Date.now() - 1000).toISOString())).toBe('Today');
    expect(c.dayLabel(at(1))).toBe('Tomorrow');
    expect(c.dayLabel(at(3))).toBe('in 3d');
  });
});
