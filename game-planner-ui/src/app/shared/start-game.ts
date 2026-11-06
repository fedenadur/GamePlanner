import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { NowPlayingService } from '../services/now-playing.service';
import { FinishChoice } from '../components/finish-game-dialog/finish-game-dialog.component';

export type StartResult =
  | { kind: 'started' }
  | { kind: 'finish-needed'; currentName: string }
  | { kind: 'error'; message: string };

const GENERIC_ERROR = 'Could not start the game. Try again.';

// Shared "Start on {device}" flow: a 409 slot_occupied asks the caller to confirm finishing the current game.
export function startOnDevice(
  nowPlaying: NowPlayingService,
  device: string,
  gameId: number,
  choice?: FinishChoice
): Observable<StartResult> {
  const req = choice
    ? { gameId, outgoingOutcome: choice.outcome, outgoingRating: choice.rating }
    : { gameId };
  return nowPlaying.start(device, req).pipe(
    map((): StartResult => ({ kind: 'started' })),
    catchError((err: HttpErrorResponse): Observable<StartResult> => {
      if (!choice && err.status === 409 && err.error?.code === 'slot_occupied') {
        return nowPlaying.getSlots().pipe(
          switchMap(slots => {
            const current = slots.find(s => s.device === device)?.game;
            return current
              ? of<StartResult>({ kind: 'finish-needed', currentName: current.name })
              : startOnDevice(nowPlaying, device, gameId);
          }),
          catchError(() => of<StartResult>({ kind: 'error', message: GENERIC_ERROR }))
        );
      }
      return of<StartResult>({
        kind: 'error',
        message: err.status === 409 && err.error?.code === 'game_on_other_device'
          ? 'That game is already on another device.'
          : GENERIC_ERROR
      });
    })
  );
}
