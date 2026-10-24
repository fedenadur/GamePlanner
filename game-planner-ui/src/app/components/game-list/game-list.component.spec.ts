import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, of, throwError } from 'rxjs';
import { GameService } from '../../services/game.service';
import { GameListComponent } from './game-list.component';

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
