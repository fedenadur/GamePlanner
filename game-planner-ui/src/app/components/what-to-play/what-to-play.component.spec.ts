import { fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Game, GameService } from '../../services/game.service';
import { NowPlayingService, NowPlayingSlot } from '../../services/now-playing.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { WhatToPlayResponse, WhatToPlayResult, WhatToPlayService } from '../../services/what-to-play.service';
import { WhatToPlayComponent } from './what-to-play.component';

function result(over: Partial<WhatToPlayResult>): WhatToPlayResult {
  return {
    gameId: 1, name: 'Hades', store: 'Steam', totalHours: 0, score: 86, match: 'High',
    reasons: [
      { kind: 'Positive', text: 'Action, as requested' },
      { kind: 'Neutral', text: 'Never played' },
      { kind: 'Negative', text: '~40 h, longer than requested' }
    ],
    ...over
  };
}

describe('WhatToPlayComponent', () => {
  let wtp: jasmine.SpyObj<WhatToPlayService>;
  let np: jasmine.SpyObj<NowPlayingService>;
  let settings: jasmine.SpyObj<UserSettingsService>;
  let games: jasmine.SpyObj<GameService>;
  let router: jasmine.SpyObj<Router>;
  let query: Record<string, string>;
  let library: Game[];

  const response = (results: WhatToPlayResult[]): WhatToPlayResponse => ({ device: 'SteamDeck', results });

  function create() {
    const route = { snapshot: { queryParamMap: { get: (k: string) => query[k] ?? null } } } as unknown as ActivatedRoute;
    const cmp = new WhatToPlayComponent(wtp, np, settings, games, route, router);
    cmp.ngOnInit();
    return cmp;
  }

  beforeEach(() => {
    query = {};
    library = [{ id: 1 } as Game];
    wtp = jasmine.createSpyObj<WhatToPlayService>('WhatToPlayService', ['recommend']);
    wtp.recommend.and.returnValue(of(response([result({})])));
    np = jasmine.createSpyObj<NowPlayingService>('NowPlayingService', ['start', 'getSlots']);
    np.start.and.returnValue(of(undefined));
    settings = jasmine.createSpyObj<UserSettingsService>('UserSettingsService', ['getDevices', 'getGenres']);
    settings.getDevices.and.returnValue(of({ devices: ['PC', 'SteamDeck'] }));
    settings.getGenres.and.returnValue(of(['Action', 'RPG']));
    games = jasmine.createSpyObj<GameService>('GameService', ['getGames']);
    games.getGames.and.callFake(() => of(library));
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
  });

  it('requests with defaults on init', fakeAsync(() => {
    const cmp = create();
    tick();
    expect(wtp.recommend).toHaveBeenCalledTimes(1);
    expect(wtp.recommend).toHaveBeenCalledWith({
      device: 'PC', sessionTime: 'Any', gameLength: 'Any', genres: [],
      mood: null, perspective: null, modes: [], strategy: 'Default'
    }, 5);
    expect(cmp.top?.name).toBe('Hades');
  }));

  it('uses ?device= query param', fakeAsync(() => {
    query['device'] = 'SteamDeck';
    const cmp = create();
    tick();
    expect(cmp.device).toBe('SteamDeck');
    expect(wtp.recommend.calls.mostRecent().args[0].device).toBe('SteamDeck');
  }));

  it('falls back to General when the user has no devices', fakeAsync(() => {
    settings.getDevices.and.returnValue(of({ devices: [] }));
    const cmp = create();
    tick();
    expect(cmp.devices).toEqual(['General']);
    expect(wtp.recommend.calls.mostRecent().args[0].device).toBe('General');
  }));

  it('debounces option changes by 300 ms', fakeAsync(() => {
    const cmp = create();
    tick();
    cmp.setSessionTime('Short');
    cmp.toggleGenre('Action');
    tick(299);
    expect(wtp.recommend).toHaveBeenCalledTimes(1);
    tick(1);
    expect(wtp.recommend).toHaveBeenCalledTimes(2);
    const req = wtp.recommend.calls.mostRecent().args[0];
    expect(req.sessionTime).toBe('Short');
    expect(req.genres).toEqual(['Action']);
  }));

  it('renders reasons with kind classes', () => {
    const cmp = new WhatToPlayComponent(wtp, np, settings, games, {} as ActivatedRoute, router);
    expect(cmp.reasonClass('Positive')).toBe('ok');
    expect(cmp.reasonClass('Neutral')).toBe('meh');
    expect(cmp.reasonClass('Negative')).toBe('bad');
    expect(cmp.reasonIcon('Positive')).toBe('✓');
    expect(cmp.reasonIcon('Negative')).toBe('•');
  });

  it('start on 409 opens finish dialog and retries with the outcome', fakeAsync(() => {
    const cmp = create();
    tick();
    np.start.and.returnValues(
      throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'slot_occupied' } })),
      of(undefined));
    np.getSlots.and.returnValue(of([{ device: 'PC', game: { id: 9, name: 'Old' } as Game, playingSince: null, recentSessions: [] } as NowPlayingSlot]));

    cmp.start(cmp.top!);
    tick();
    expect(cmp.finishGameName).toBe('Old');

    cmp.onFinishChosen({ outcome: 'Finished', rating: 8 });
    tick();
    expect(np.start).toHaveBeenCalledTimes(2);
    expect(np.start.calls.mostRecent().args).toEqual(['PC', { gameId: 1, outgoingOutcome: 'Finished', outgoingRating: 8 }]);
    expect(router.navigate).toHaveBeenCalledWith(['/now-playing']);
  }));

  it('start navigates to now playing when the slot is free', fakeAsync(() => {
    const cmp = create();
    tick();
    cmp.start(cmp.top!);
    tick();
    expect(np.start).toHaveBeenCalledWith('PC', { gameId: 1 });
    expect(router.navigate).toHaveBeenCalledWith(['/now-playing']);
  }));

  it('shows empty state when results empty', fakeAsync(() => {
    wtp.recommend.and.returnValue(of(response([])));
    const cmp = create();
    tick();
    expect(cmp.top).toBeNull();
    expect(cmp.libraryEmpty).toBeFalse();
    expect(cmp.noResults).toBeTrue();
  }));

  it('flags an empty library', fakeAsync(() => {
    library = [];
    wtp.recommend.and.returnValue(of(response([])));
    const cmp = create();
    tick();
    expect(cmp.libraryEmpty).toBeTrue();
    expect(cmp.noResults).toBeFalse();
  }));

  it('clears stale results and shows the load error when recommend fails', fakeAsync(() => {
    const cmp = create();
    tick();
    expect(cmp.top).not.toBeNull();
    wtp.recommend.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    cmp.setDevice('SteamDeck');
    tick(400);
    expect(cmp.results).toEqual([]);
    expect(cmp.top).toBeNull();
    expect(cmp.error).toBe('Could not load recommendations. Check that the API is running, then reload.');
  }));
});
