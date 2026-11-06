import { of } from 'rxjs';
import { Game, GameService } from '../../services/game.service';
import { GameEditDialogComponent } from './game-edit-dialog.component';

function make(over: Partial<Game> = {}) {
  const svc = jasmine.createSpyObj<GameService>('GameService', ['updateGame']);
  svc.updateGame.and.returnValue(of({}));
  const cmp = new GameEditDialogComponent(svc);
  cmp.game = {
    id: 5, name: 'G', genres: ['RPG'], estimatedMinutes: 600, tags: ['t'], themes: [], playerPerspectives: [], gameModes: [],
    playtimeHours: 0, source: 'Manual', store: 'Steam', isFavorite: false, playableDevices: ['PC', 'SteamDeck'],
    status: 'Backlog', loggedHours: 0, sessionCount: 0, totalHours: 0, ...over
  };
  cmp.ngOnInit();
  return { svc, cmp };
}

describe('GameEditDialogComponent', () => {
  it('sends an empty cover when the user clears a stored cover', () => {
    const { svc, cmp } = make({ coverImageUrl: 'https://img/x.jpg' });
    cmp.coverImageUrl = '   ';
    cmp.save();
    expect(svc.updateGame.calls.mostRecent().args[1].coverImageUrl).toBe('');
  });

  it('sends the trimmed cover URL', () => {
    const { svc, cmp } = make();
    cmp.coverImageUrl = ' https://img/y.jpg ';
    cmp.save();
    expect(svc.updateGame.calls.mostRecent().args[1].coverImageUrl).toBe('https://img/y.jpg');
  });

  it('maps hours to minutes and genres from comma text', () => {
    const { svc, cmp } = make();
    cmp.hours = 2.5;
    cmp.genresText = 'A, B ,, C';
    cmp.save();
    const req = svc.updateGame.calls.mostRecent().args[1];
    expect(req.estimatedMinutes).toBe(150);
    expect(req.genres).toEqual(['A', 'B', 'C']);
  });

  it('keeps original minutes when hours are untouched', () => {
    const { svc, cmp } = make({ estimatedMinutes: 619 });
    cmp.save();
    expect(svc.updateGame.calls.mostRecent().args[1].estimatedMinutes).toBe(619);
  });

  it('sends [] when the selection equals the store default', () => {
    const { svc, cmp } = make({ playableDevices: ['SteamDeck', 'PC'] });
    cmp.save();
    expect(svc.updateGame.calls.mostRecent().args[1].playableDevices).toEqual([]);
  });

  it('store change Steam to NintendoEShop with default selection resets to Switch and saves []', () => {
    const { svc, cmp } = make();
    cmp.onStoreChange('NintendoEShop');
    expect([...cmp.selectedDevices]).toEqual(['Switch']);
    cmp.save();
    const req = svc.updateGame.calls.mostRecent().args[1];
    expect(req.store).toBe('NintendoEShop');
    expect(req.playableDevices).toEqual([]);
  });

  it('preserves and sends a custom selection', () => {
    const { svc, cmp } = make({ playableDevices: ['PC', 'Mobile'] });
    cmp.onStoreChange('NintendoEShop');
    expect([...cmp.selectedDevices].sort()).toEqual(['Mobile', 'PC']);
    cmp.save();
    expect(svc.updateGame.calls.mostRecent().args[1].playableDevices.sort()).toEqual(['Mobile', 'PC']);
  });

  it('does not save negative hours', () => {
    const { svc, cmp } = make();
    cmp.hours = -1;
    expect(cmp.invalidHours).toBeTrue();
    cmp.save();
    expect(svc.updateGame).not.toHaveBeenCalled();
  });
});
