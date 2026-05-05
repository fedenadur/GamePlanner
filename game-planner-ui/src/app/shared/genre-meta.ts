export const GENRE_META: Record<string, { icon: string; cls: string }> = {
  'visual novel':   { icon: '📖', cls: 'genre-visual-novel' },
  'hack and slash': { icon: '⚔️', cls: 'genre-hack' },
  'beat \'em up':   { icon: '👊', cls: 'genre-beat' },
  'point-and-click':{ icon: '🖱️', cls: 'genre-point' },
  'card':           { icon: '🃏', cls: 'genre-card' },
  'tactical':       { icon: '🎯', cls: 'genre-tactical' },
  'fighting':       { icon: '🥊', cls: 'genre-fighting' },
  'racing':         { icon: '🏎️', cls: 'genre-racing' },
  'music':          { icon: '🎵', cls: 'genre-music' },
  'moba':           { icon: '🏆', cls: 'genre-moba' },
  'indie':          { icon: '💎', cls: 'genre-indie' },
  rpg:              { icon: '🎮', cls: 'genre-rpg' },
  adventure:        { icon: '🧭', cls: 'genre-adventure' },
  action:           { icon: '⚡', cls: 'genre-action' },
  tbs:              { icon: '⏳', cls: 'genre-tbs' },
  rts:              { icon: '🗺️', cls: 'genre-rts' },
  strategy:         { icon: '♟️', cls: 'genre-strategy' },
  simulation:       { icon: '🏗️', cls: 'genre-simulation' },
  sports:           { icon: '⚽', cls: 'genre-sports' },
  horror:           { icon: '💀', cls: 'genre-horror' },
  platformer:       { icon: '🏃', cls: 'genre-platformer' },
  puzzle:           { icon: '🧩', cls: 'genre-puzzle' },
  shooter:          { icon: '🔫', cls: 'genre-shooter' },
};

export function genreMeta(genre: string): { icon: string; cls: string } {
  const key = genre.toLowerCase();
  for (const k of Object.keys(GENRE_META)) {
    if (key.includes(k)) return GENRE_META[k];
  }
  return { icon: '🎲', cls: 'genre-default' };
}
