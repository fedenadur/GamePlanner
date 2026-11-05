export const DEVICE_LABELS: Record<string, string> = {
  PC: 'PC',
  SteamDeck: 'Steam Deck',
  Switch: 'Switch',
  PlayStation: 'PlayStation',
  Xbox: 'Xbox',
  Mobile: 'Mobile',
  Other: 'Other',
  General: 'General'
};

export const DEVICE_ICONS: Record<string, string> = {
  PC: '🖥️',
  SteamDeck: '🎮',
  Switch: '🕹️',
  PlayStation: '🎮',
  Xbox: '🎮',
  Mobile: '📱',
  Other: '❓',
  General: '⭐'
};

export const SELECTABLE_DEVICES: string[] = ['PC', 'SteamDeck', 'Switch', 'PlayStation', 'Xbox', 'Mobile', 'Other'];

export const STORE_LABELS: Record<string, string> = {
  Steam: 'Steam',
  Epic: 'Epic',
  GOG: 'GOG',
  NintendoEShop: 'Nintendo eShop',
  PlayStationStore: 'PlayStation Store',
  XboxStore: 'Xbox / Microsoft Store',
  Physical: 'Physical',
  Other: 'Other'
};

export const JOURNAL_STATUSES: string[] = ['Backlog', 'Playing', 'Finished', 'Paused', 'Dropped', 'Replaying'];
