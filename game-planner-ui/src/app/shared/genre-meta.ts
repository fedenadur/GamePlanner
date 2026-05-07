export const GENRE_ICONS: Record<string, string> = {
  'Adventure':                   '🗺️',
  'Arcade':                      '🕹️',
  'Card & Board Game':           '🃏',
  'Fighting':                    '🥊',
  "Hack and slash/Beat 'em up":  '⚔️',
  'Indie':                       '💡',
  'Music':                       '🎵',
  'Pinball':                     '🎱',
  'Platform':                    '🏃',
  'Point-and-click':             '🖱️',
  'Puzzle':                      '🧩',
  'Quiz/Trivia':                 '❓',
  'Racing':                      '🏎️',
  'Real Time Strategy (RTS)':    '⚡',
  'Role-playing (RPG)':          '🐉',
  'Shooter':                     '🔫',
  'Simulator':                   '🏗️',
  'Sport':                       '⚽',
  'Strategy':                    '♟️',
  'Tactical':                    '🎯',
  'Turn-based strategy (TBS)':   '⏳',
  'Visual Novel':                '📖',
};

export function genreIcon(genre: string): string {
  return GENRE_ICONS[genre] ?? '🎮';
}

const GENRE_CLS: Record<string, string> = {
  'visual novel':    'genre-visual-novel',
  'hack and slash':  'genre-hack',
  "beat 'em up":     'genre-beat',
  'point-and-click': 'genre-point',
  'card':            'genre-card',
  'tactical':        'genre-tactical',
  'fighting':        'genre-fighting',
  'racing':          'genre-racing',
  'music':           'genre-music',
  'moba':            'genre-moba',
  'indie':           'genre-indie',
  'rpg':             'genre-rpg',
  'adventure':       'genre-adventure',
  'action':          'genre-action',
  'tbs':             'genre-tbs',
  'rts':             'genre-rts',
  'strategy':        'genre-strategy',
  'simul':           'genre-simulation',
  'sport':           'genre-sports',
  'horror':          'genre-horror',
  'platform':        'genre-platformer',
  'puzzle':          'genre-puzzle',
  'shooter':         'genre-shooter',
  'arcade':          'genre-arcade',
  'pinball':         'genre-pinball',
  'quiz':            'genre-quiz',
};

function genreCls(genre: string): string {
  const key = genre.toLowerCase();
  for (const k of Object.keys(GENRE_CLS)) {
    if (key.includes(k)) return GENRE_CLS[k];
  }
  return 'genre-default';
}

export function genreMeta(genre: string): { icon: string; cls: string } {
  return { icon: genreIcon(genre), cls: genreCls(genre) };
}
