import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { GameService } from '../../services/game.service';
import { NowPlayingService } from '../../services/now-playing.service';
import { PlaySessionService } from '../../services/play-session.service';
import { GameTitlePipe } from '../../pipes/game-title.pipe';
import { NowPlayingComponent } from './now-playing.component';

describe('NowPlayingComponent library load error', () => {
  let fixture: ComponentFixture<NowPlayingComponent>;
  let games: jasmine.SpyObj<GameService>;

  beforeEach(() => {
    games = jasmine.createSpyObj<GameService>('GameService', ['getGames']);
    games.getGames.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const np = jasmine.createSpyObj<NowPlayingService>('NowPlayingService', ['getSlots']);
    np.getSlots.and.returnValue(of([{ device: 'General', game: null, playingSince: null, recentSessions: [] }]));
    TestBed.configureTestingModule({
      declarations: [NowPlayingComponent, GameTitlePipe],
      imports: [RouterTestingModule],
      providers: [
        { provide: GameService, useValue: games },
        { provide: NowPlayingService, useValue: np },
        { provide: PlaySessionService, useValue: {} }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    });
    fixture = TestBed.createComponent(NowPlayingComponent);
    fixture.detectChanges();
  });

  it('shows a Reload button in the picker that fetches games again', () => {
    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('.empty button') as HTMLButtonElement).click();
    fixture.detectChanges();
    const btn = Array.from(el.querySelectorAll('.picker-empty button')).find(b => b.textContent?.trim() === 'Reload') as HTMLButtonElement;
    expect(btn).toBeTruthy();
    const before = games.getGames.calls.count();
    btn.click();
    expect(games.getGames.calls.count()).toBe(before + 1);
  });

  it('shows the page-level retry when the library failed to load', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.error-msg')?.textContent).toContain('Could not load your library');
    expect(Array.from(el.querySelectorAll('button')).some(b => b.textContent?.trim() === 'Reload')).toBeTrue();
  });
});
