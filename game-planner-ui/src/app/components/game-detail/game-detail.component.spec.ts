import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NEVER, of, throwError } from 'rxjs';
import { GameDetail, GameImage, GameService } from '../../services/game.service';
import { JournalEntry, JournalService } from '../../services/journal.service';
import { PlaySession, PlaySessionService } from '../../services/play-session.service';
import { NowPlayingService, NowPlayingSlot } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { GameDetailComponent } from './game-detail.component';

function buildComponent() {
  const svc = jasmine.createSpyObj<GameService>('GameService', ['getGameDetail', 'getGameImages']);
  const journal = jasmine.createSpyObj<JournalService>('JournalService', ['getEntry']);
  journal.getEntry.and.returnValue(throwError(() => ({ status: 404 })));
  const route = { paramMap: of(convertToParamMap({ id: '42' })) } as unknown as ActivatedRoute;
  svc.getGameDetail.and.returnValue(of({
    id: 42, igdbId: 7, name: 'x', genres: [], tags: [], themes: [], playerPerspectives: [],
    gameModes: [], estimatedMinutes: 60, playtimeHours: 0, source: 'Manual'
  } as unknown as GameDetail));
  svc.getGameImages.and.returnValue(of([]));
  const sessions = jasmine.createSpyObj<PlaySessionService>('PlaySessionService', ['list', 'log', 'update', 'delete']);
  sessions.list.and.returnValue(of([]));
  const nowPlaying = jasmine.createSpyObj<NowPlayingService>('NowPlayingService', ['getSlots']);
  nowPlaying.getSlots.and.returnValue(of([]));
  const settings = jasmine.createSpyObj<UserSettingsService>('UserSettingsService', ['getDevices']);
  settings.getDevices.and.returnValue(of({ devices: ['PC', 'Switch'] }));
  const cmp = new GameDetailComponent(route, svc, journal, sessions, nowPlaying, settings);
  return { svc, cmp, journal, sessions, nowPlaying, settings };
}

describe('GameDetailComponent screenshots', () => {
  it('requests screenshots for this game id only', () => {
    const { svc, cmp } = buildComponent();
    cmp.ngOnInit();
    expect(svc.getGameImages).toHaveBeenCalledWith('screenshot', 7, 42);
  });
});

describe('GameDetailComponent lightbox keys', () => {
  const key = (k: string) => new KeyboardEvent('keydown', { key: k });
  let cmp: GameDetailComponent;

  const shots = (n: number): GameImage[] =>
    Array.from({ length: n }, (_, i) => ({
      id: i + 1, gameId: 42, gameName: 'x', imageType: 'screenshot',
      url: `https://images.igdb.com/t_screenshot_big/s${i}.jpg`
    }));

  beforeEach(() => {
    cmp = buildComponent().cmp;
  });

  it('wraps ArrowLeft on the first and ArrowRight on the last screenshot', () => {
    cmp.screenshots = shots(3);
    cmp.openLightbox(0);
    cmp.handleLightboxKeydown(key('ArrowLeft'));
    expect(cmp.lightboxIndex).toBe(2);
    cmp.handleLightboxKeydown(key('ArrowRight'));
    expect(cmp.lightboxIndex).toBe(0);
  });

  it('closes the lightbox on Escape', () => {
    cmp.screenshots = shots(3);
    cmp.openLightbox(1);
    cmp.handleLightboxKeydown(key('Escape'));
    expect(cmp.lightboxIndex).toBeNull();
  });

  it('ignores keys while the lightbox is closed', () => {
    cmp.screenshots = shots(3);
    expect(() => {
      cmp.handleLightboxKeydown(key('ArrowRight'));
      cmp.handleLightboxKeydown(key('ArrowLeft'));
      cmp.handleLightboxKeydown(key('Escape'));
    }).not.toThrow();
    expect(cmp.lightboxIndex).toBeNull();
  });

  it('keeps index 0 with a single screenshot', () => {
    cmp.screenshots = shots(1);
    cmp.openLightbox(0);
    cmp.nextImage();
    expect(cmp.lightboxIndex).toBe(0);
    cmp.prevImage();
    expect(cmp.lightboxIndex).toBe(0);
  });
});

const sess = (id: number, hours: number): PlaySession =>
  ({ id, gameId: 42, device: 'PC', playedOn: '2026-01-01', hours, note: null });
const slot = (device: string, gameId: number | null) =>
  ({ device, game: gameId ? { id: gameId } : null, playingSince: null, recentSessions: [] } as unknown as NowPlayingSlot);

describe('GameDetailComponent play sessions', () => {
  it('totals logged hours and count from the session list', () => {
    const { cmp, sessions } = buildComponent();
    sessions.list.and.returnValue(of([sess(1, 1.5), sess(2, 2)]));
    cmp.ngOnInit();
    expect(cmp.loggedLabel).toBe('Logged: 3.5 h in 2 sessions');
  });

  it('recomputes totals after a delete', () => {
    const { cmp, sessions } = buildComponent();
    sessions.list.and.returnValues(of([sess(1, 1.5), sess(2, 2)]), of([sess(1, 1.5)]));
    sessions.delete.and.returnValue(of({}));
    cmp.ngOnInit();
    cmp.deleting = cmp.sessions[1];
    cmp.confirmDelete();
    expect(sessions.delete).toHaveBeenCalledWith(2);
    expect(cmp.deleting).toBeNull();
    expect(cmp.loggedLabel).toBe('Logged: 1.5 h in 1 session');
  });

  it('shows Steam hours only when playtime > 0', () => {
    const { cmp } = buildComponent();
    cmp.ngOnInit();
    expect(cmp.steamLabel).toBe('');
    cmp.game!.playtimeHours = 12;
    expect(cmp.steamLabel).toBe('Steam: 12 h');
  });

  it('+ Log session uses the Now Playing device when the game is there', () => {
    const { cmp, nowPlaying } = buildComponent();
    nowPlaying.getSlots.and.returnValue(of([slot('Switch', 42)]));
    cmp.ngOnInit();
    cmp.openLog();
    expect(cmp.logDevice).toBe('Switch');
    expect(cmp.logDevices).toBeNull();
  });

  it('+ Log session offers the user devices otherwise', () => {
    const { cmp } = buildComponent();
    cmp.ngOnInit();
    cmp.openLog();
    expect(cmp.logDevices).toEqual(['PC', 'Switch']);
    expect(cmp.logDevice).toBe('PC');
    expect(cmp.logOpen).toBeTrue();
  });

  it('+ Log session offers only General when the user has no devices', () => {
    const { cmp, settings } = buildComponent();
    settings.getDevices.and.returnValue(of({ devices: [] }));
    cmp.ngOnInit();
    cmp.openLog();
    expect(cmp.logDevices).toEqual(['General']);
  });

  it('+ Log session shows the load error and stays closed when devices fail to load', () => {
    const { cmp, settings } = buildComponent();
    settings.getDevices.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    cmp.openLog();
    expect(cmp.logOpen).toBeFalse();
    expect(cmp.sessionsError).toBe('Could not load your devices. Check that the API is running, then reload.');
  });

  it('logs a session with the chosen device then refreshes', () => {
    const { cmp, sessions } = buildComponent();
    sessions.log.and.returnValue(of(sess(3, 1)));
    cmp.ngOnInit();
    cmp.openLog();
    cmp.onLogSaved({ hours: 1, note: null, playedOn: '2026-01-01', device: 'General' });
    expect(sessions.log).toHaveBeenCalledWith(42, { device: 'General', hours: 1, note: null, playedOn: '2026-01-01' });
    expect(cmp.logOpen).toBeFalse();
    expect(sessions.list).toHaveBeenCalledTimes(2);
  });

  it('updates the edited session', () => {
    const { cmp, sessions } = buildComponent();
    sessions.update.and.returnValue(of({}));
    cmp.ngOnInit();
    cmp.editing = sess(5, 1);
    cmp.onEditSaved({ hours: 2, note: 'x', playedOn: '2026-01-02', device: 'PC' });
    expect(sessions.update).toHaveBeenCalledWith(5, { device: 'PC', hours: 2, note: 'x', playedOn: '2026-01-02' });
    expect(cmp.editing).toBeNull();
    expect(sessions.list).toHaveBeenCalledTimes(2);
  });

  it('ignores a second save while one is in flight', () => {
    const { cmp, sessions } = buildComponent();
    sessions.log.and.returnValue(NEVER);
    cmp.ngOnInit();
    cmp.openLog();
    const r = { hours: 1, note: null, playedOn: '2026-01-01', device: 'PC' };
    cmp.onLogSaved(r);
    cmp.onLogSaved(r);
    expect(sessions.log).toHaveBeenCalledTimes(1);
    expect(cmp.saving).toBeTrue();
  });

  it('keeps the dialog open and shows the error when save fails', () => {
    const { cmp, sessions } = buildComponent();
    sessions.log.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    cmp.openLog();
    cmp.onLogSaved({ hours: 1, note: null, playedOn: '2026-01-01', device: 'PC' });
    expect(cmp.logOpen).toBeTrue();
    expect(cmp.saveError).toBe('Could not save. Try again.');
    expect(cmp.saving).toBeFalse();
  });

  it('keeps the edit dialog open when update fails', () => {
    const { cmp, sessions } = buildComponent();
    sessions.update.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    cmp.editing = sess(5, 1);
    cmp.onEditSaved({ hours: 2, note: null, playedOn: '2026-01-02', device: 'PC' });
    expect(cmp.editing).not.toBeNull();
    expect(cmp.saveError).toBe('Could not save. Try again.');
  });

  it('keeps the confirm open and shows the error when delete fails', () => {
    const { cmp, sessions } = buildComponent();
    sessions.delete.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    cmp.deleting = sess(5, 1);
    cmp.confirmDelete();
    expect(cmp.deleting).not.toBeNull();
    expect(cmp.deleteError).toBe('Could not delete. Try again.');
  });

  it('shows a load error when the session list fails', () => {
    const { cmp, sessions } = buildComponent();
    sessions.list.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    expect(cmp.sessionsError).toBe('Could not load sessions. Check that the API is running, then reload.');
  });

  it('still saves the journal when Now Playing fails to load', () => {
    const { cmp, journal, nowPlaying } = buildComponent();
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    nowPlaying.getSlots.and.returnValue(throwError(() => new Error('x')));
    cmp.ngOnInit();
    expect(() => cmp.saveJournal()).not.toThrow();
    expect(cmp.removedNotice).toBe('');
  });
});

describe('GameDetailComponent journal save and Now Playing', () => {
  it('announces removal when the game left Now Playing', () => {
    const { cmp, journal, nowPlaying } = buildComponent();
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    nowPlaying.getSlots.and.returnValues(of([slot('PC', 42)]), of([slot('PC', null)]));
    cmp.ngOnInit();
    cmp.journal.status = 'Finished';
    cmp.saveJournal();
    expect(cmp.removedNotice).toBe('Removed from Now Playing.');
  });

  it('stays silent when the game was never in Now Playing', () => {
    const { cmp, journal } = buildComponent();
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    cmp.ngOnInit();
    cmp.saveJournal();
    expect(cmp.removedNotice).toBe('');
  });

  it('stays silent when still Playing', () => {
    const { cmp, journal, nowPlaying } = buildComponent();
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    nowPlaying.getSlots.and.returnValue(of([slot('PC', 42)]));
    cmp.ngOnInit();
    cmp.saveJournal();
    expect(cmp.removedNotice).toBe('');
  });
});

describe('GameDetailComponent journal load', () => {
  const entry: JournalEntry = {
    gameId: 42, gameName: 'x', coverImageUrl: null, status: 'Playing',
    startedAt: '2026-01-01T00:00:00', finishedAt: null, lastPlayedAt: '2026-02-01T00:00:00',
    userRating: 6, notes: 'my notes', favoriteMoment: 'the boss', replayCount: 2,
    createdAt: '2026-01-01T00:00:00', updatedAt: '2026-02-01T00:00:00', daysToFinish: null
  };

  it('saves the full loaded entry with only the edited fields changed', () => {
    const { cmp, journal } = buildComponent();
    journal.getEntry.and.returnValue(of(entry));
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    cmp.ngOnInit();
    cmp.journal.status = 'Finished';
    cmp.setRating(8);
    cmp.saveJournal();
    expect(journal.upsert).toHaveBeenCalledWith(42, {
      status: 'Finished', startedAt: '2026-01-01T00:00:00', finishedAt: null,
      lastPlayedAt: '2026-02-01T00:00:00', userRating: 8, notes: 'my notes',
      favoriteMoment: 'the boss', replayCount: 2
    });
  });

  it('shows the load error and blocks Save when the entry fails to load', () => {
    const { cmp, journal } = buildComponent();
    journal.getEntry.and.returnValue(throwError(() => ({ status: 500 })));
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    cmp.ngOnInit();
    expect(cmp.journalError).toBe('Could not load the journal entry. Check that the API is running, then reload.');
    expect(cmp.journalReady).toBeFalse();
    cmp.saveJournal();
    expect(journal.upsert).not.toHaveBeenCalled();
  });

  it('blocks Save while the entry is still loading', () => {
    const { cmp, journal } = buildComponent();
    journal.getEntry.and.returnValue(NEVER);
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    cmp.ngOnInit();
    cmp.saveJournal();
    expect(journal.upsert).not.toHaveBeenCalled();
  });

  it('a 404 means no entry yet and still allows creating one', () => {
    const { cmp, journal } = buildComponent();
    journal.upsert = jasmine.createSpy().and.returnValue(of({}));
    cmp.ngOnInit();
    expect(cmp.journalError).toBe('');
    expect(cmp.journalReady).toBeTrue();
    cmp.journal.status = 'Playing';
    cmp.saveJournal();
    expect(journal.upsert).toHaveBeenCalledWith(42, jasmine.objectContaining({ status: 'Playing', replayCount: 0, notes: null }));
  });
});
