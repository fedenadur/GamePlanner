import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NEVER, of } from 'rxjs';
import { GameDetail, GameService } from '../../services/game.service';
import { JournalService } from '../../services/journal.service';
import { GameDetailComponent } from './game-detail.component';

function buildComponent() {
  const svc = jasmine.createSpyObj<GameService>('GameService', ['getGameDetail', 'getGameImages']);
  const journal = jasmine.createSpyObj<JournalService>('JournalService', ['getEntry']);
  journal.getEntry.and.returnValue(NEVER);
  const route = { paramMap: of(convertToParamMap({ id: '42' })) } as unknown as ActivatedRoute;
  svc.getGameDetail.and.returnValue(of({
    id: 42, igdbId: 7, name: 'x', genres: [], tags: [], themes: [], playerPerspectives: [],
    gameModes: [], estimatedMinutes: 60, playtimeHours: 0, source: 'Manual'
  } as unknown as GameDetail));
  svc.getGameImages.and.returnValue(of([]));
  const cmp = new GameDetailComponent(route, svc, journal);
  return { svc, cmp };
}

describe('GameDetailComponent screenshots', () => {
  it('requests screenshots for this game id only', () => {
    const { svc, cmp } = buildComponent();
    cmp.ngOnInit();
    expect(svc.getGameImages).toHaveBeenCalledWith('screenshot', 7, 42);
  });
});
