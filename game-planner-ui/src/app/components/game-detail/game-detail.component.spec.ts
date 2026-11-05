import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { NEVER, of } from 'rxjs';
import { GameDetail, GameImage, GameService } from '../../services/game.service';
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
