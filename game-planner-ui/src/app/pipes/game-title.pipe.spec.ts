import { GameTitlePipe } from './game-title.pipe';

describe('GameTitlePipe', () => {
  const pipe = new GameTitlePipe();

  it('appends the release year', () => {
    expect(pipe.transform({ name: 'Hades', releaseYear: 2020 })).toBe('Hades (2020)');
  });

  it('renders just the name when there is no year', () => {
    expect(pipe.transform({ name: 'Hades', releaseYear: null })).toBe('Hades');
    expect(pipe.transform({ name: 'Hades' })).toBe('Hades');
  });
});
