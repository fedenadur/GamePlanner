import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ProfileService } from '../../services/profile.service';
import { UserSettingsService } from '../../services/user-settings.service';
import { ProfileComponent } from './profile.component';

describe('ProfileComponent devices and preferences', () => {
  let settings: jasmine.SpyObj<UserSettingsService>;
  let cmp: ProfileComponent;

  beforeEach(() => {
    const profile = jasmine.createSpyObj<ProfileService>('ProfileService', ['load', 'update']);
    (profile as any).snapshot = { displayName: 'x', email: 'x@y.z', isSteamUser: false };
    settings = jasmine.createSpyObj<UserSettingsService>('UserSettingsService',
      ['getDevices', 'saveDevices', 'getPreferences', 'savePreferences', 'getGenres']);
    settings.getDevices.and.returnValue(of({ devices: ['PC', 'SteamDeck'] }));
    settings.getPreferences.and.returnValue(of({
      favoriteGenres: [], avoidGenres: [], maxGameHours: null,
      singlePlayer: 'Neutral', narrative: 'Neutral', combat: 'Neutral'
    }));
    settings.getGenres.and.returnValue(of(['RPG', 'Puzzle']));
    cmp = new ProfileComponent(profile, settings);
    cmp.ngOnInit();
  });

  it('names the removed device when the API answers device_in_use', () => {
    cmp.toggleDevice('SteamDeck');
    settings.saveDevices.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { code: 'device_in_use' }
    })));
    cmp.saveDevices();
    expect(cmp.devicesError).toBe('Finish or remove the game on Steam Deck in Now Playing first.');
    expect(cmp.devicesSaved).toBeFalse();
  });

  it('disables a genre in the other list once chosen', () => {
    cmp.toggleGenre('favorite', 'RPG');
    expect(cmp.isGenreDisabled('avoid', 'RPG')).toBeTrue();
    expect(cmp.isGenreDisabled('favorite', 'RPG')).toBeFalse();
    expect(cmp.isGenreDisabled('avoid', 'Puzzle')).toBeFalse();
  });
});
