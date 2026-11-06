import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, of, throwError } from 'rxjs';
import { Game, GameService } from '../../services/game.service';
import { GameListComponent, lengthBucket } from './game-list.component';

describe('GameListComponent delete failure', () => {
  let svc: jasmine.SpyObj<GameService>;
  let cmp: GameListComponent;

  beforeEach(() => {
    svc = jasmine.createSpyObj<GameService>('GameService', ['getGames', 'deleteGame']);
    svc.getGames.and.returnValue(of([]));
    cmp = new GameListComponent(svc);
  });

  it('shows a fixed message and leaks no server text when delete fails', () => {
    svc.deleteGame.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 500,
      statusText: 'Server Error',
      error: 'SqlException: secret'
    })));
    cmp.deleteGame(1);
    expect(cmp.error).toBe('Could not delete game. Please try again.');
    expect(cmp.error).not.toContain('500');
    expect(cmp.error).not.toContain('SqlException');
  });

  it('clears the previous error as soon as a delete starts', () => {
    cmp.error = 'previous';
    svc.deleteGame.and.returnValue(NEVER);
    cmp.deleteGame(1);
    expect(cmp.error).toBe('');
  });
});

function game(over: Partial<Game>): Game {
  return {
    id: 1, name: 'g', genres: [], estimatedMinutes: 600, tags: [], themes: [], playerPerspectives: [], gameModes: [],
    playtimeHours: 0, source: 'Manual', store: 'Steam', isFavorite: false, playableDevices: [], status: 'Backlog',
    loggedHours: 0, sessionCount: 0, totalHours: 0, ...over
  };
}

describe('GameListComponent filters', () => {
  let cmp: GameListComponent;
  const names = () => cmp.filtered.map(g => g.name);

  beforeEach(() => {
    const svc = jasmine.createSpyObj<GameService>('GameService', ['getGames']);
    cmp = new GameListComponent(svc);
    cmp.games = [
      game({ id: 1, name: 'A', status: 'Playing', store: 'Steam', playableDevices: ['PC'], lengthHours: 5 }),
      game({ id: 2, name: 'B', status: 'Finished', store: 'Epic', playableDevices: ['Switch'], lengthHours: 30, isFavorite: true }),
      game({ id: 3, name: 'C', status: 'Backlog', store: 'Steam', playableDevices: [], lengthHours: null }),
      game({ id: 4, name: 'D', status: 'Backlog', store: 'GOG', playableDevices: ['PC', 'Xbox'], lengthHours: 30.02 })
    ];
  });

  it('filters by status', () => {
    cmp.selectedStatuses.add('Backlog');
    expect(names()).toEqual(['C', 'D']);
    cmp.selectedStatuses.add('Playing');
    expect(names()).toEqual(['A', 'C', 'D']);
    expect(cmp.statusCount('Backlog')).toBe(2);
  });

  it('filters by store and device (empty playableDevices matches any device)', () => {
    cmp.selectedStores.add('Steam');
    expect(names()).toEqual(['A', 'C']);
    cmp.selectedDevices.add('Switch');
    expect(names()).toEqual(['C']);
    cmp.selectedStores.clear();
    expect(names()).toEqual(['B', 'C']);
  });

  it('length filter uses lengthHours; unknown length matches only Unknown or no filter', () => {
    cmp.selectedLengths.add('Unknown');
    expect(names()).toEqual(['C']);
    cmp.selectedLengths.clear();
    cmp.selectedLengths.add('Medium');
    expect(names()).toEqual(['B']);
    cmp.selectedLengths.clear();
    cmp.selectedLengths.add('Long');
    expect(names()).toEqual(['D']);
    cmp.selectedLengths.clear();
    cmp.selectedLengths.add('Short');
    expect(names()).toEqual(['A']);
    cmp.selectedLengths.add('Medium');
    cmp.selectedLengths.add('Long');
    expect(names()).toEqual(['A', 'B', 'D']);
  });

  it('length buckets: 10 h and 30 h are Medium; estimatedMinutes alone is ignored', () => {
    expect(lengthBucket(9.9)).toBe('Short');
    expect(lengthBucket(10)).toBe('Medium');
    expect(lengthBucket(30)).toBe('Medium');
    expect(lengthBucket(30.5)).toBe('Long');
    expect(lengthBucket(null)).toBe('Unknown');
    expect(lengthBucket(undefined)).toBe('Unknown');
    cmp.games = [game({ id: 9, name: 'Steam placeholder', estimatedMinutes: 60, lengthHours: null })];
    cmp.selectedLengths.add('Short');
    expect(names()).toEqual([]);
  });

  it('favorites only', () => {
    cmp.favoritesOnly = true;
    expect(names()).toEqual(['B']);
  });

  it('clear all resets every filter', () => {
    cmp.filterTitle = 'x';
    cmp.selectedStatuses.add('Playing');
    cmp.selectedStores.add('Epic');
    cmp.selectedDevices.add('PC');
    cmp.selectedLengths.add('Long');
    cmp.selectedGenres.add('RPG');
    cmp.favoritesOnly = true;
    expect(cmp.hasActiveFilters).toBeTrue();
    cmp.clearFilters();
    expect(cmp.hasActiveFilters).toBeFalse();
    expect(cmp.filtered.length).toBe(4);
  });
});
