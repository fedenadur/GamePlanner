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

  it('shows the load error when settings fail to load', () => {
    settings.getDevices.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    cmp.loadSettings();
    expect(cmp.settingsError).toBe('Could not load your profile settings. Check that the API is running, then reload.');
  });

  it('disables a genre in the other list once chosen', () => {
    cmp.toggleGenre('favorite', 'RPG');
    expect(cmp.isGenreDisabled('avoid', 'RPG')).toBeTrue();
    expect(cmp.isGenreDisabled('favorite', 'RPG')).toBeFalse();
    expect(cmp.isGenreDisabled('avoid', 'Puzzle')).toBeFalse();
  });
});

describe('ProfileComponent reload', () => {
  it('re-fetches the profile on Reload and clears the error only on success', () => {
    const profile = jasmine.createSpyObj<ProfileService>('ProfileService', ['load', 'update']);
    (profile as any).snapshot = null;
    const settings = jasmine.createSpyObj<UserSettingsService>('UserSettingsService',
      ['getDevices', 'getPreferences', 'getGenres']);
    settings.getDevices.and.returnValue(of({ devices: [] }));
    settings.getPreferences.and.returnValue(of({
      favoriteGenres: [], avoidGenres: [], maxGameHours: null,
      singlePlayer: 'Neutral', narrative: 'Neutral', combat: 'Neutral'
    }));
    settings.getGenres.and.returnValue(of([]));
    profile.load.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const cmp = new ProfileComponent(profile, settings);
    cmp.ngOnInit();
    expect(cmp.profileError).toContain('Could not load your profile.');

    cmp.reload();
    expect(profile.load).toHaveBeenCalledTimes(2);
    expect(cmp.profileError).not.toBe('');

    profile.load.and.returnValue(of({ displayName: 'a', email: 'a@b.c', isSteamUser: false } as any));
    cmp.reload();
    expect(profile.load).toHaveBeenCalledTimes(3);
    expect(cmp.profileError).toBe('');
  });

  it('a settings success does not clear a profile failure', () => {
    const profile = jasmine.createSpyObj<ProfileService>('ProfileService', ['load', 'update']);
    (profile as any).snapshot = null;
    profile.load.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const settings = jasmine.createSpyObj<UserSettingsService>('UserSettingsService',
      ['getDevices', 'getPreferences', 'getGenres']);
    settings.getDevices.and.returnValue(of({ devices: [] }));
    settings.getPreferences.and.returnValue(of({
      favoriteGenres: [], avoidGenres: [], maxGameHours: null,
      singlePlayer: 'Neutral', narrative: 'Neutral', combat: 'Neutral'
    }));
    settings.getGenres.and.returnValue(of([]));
    const cmp = new ProfileComponent(profile, settings);
    cmp.ngOnInit();
    cmp.loadSettings();
    expect(cmp.profileError).not.toBe('');
    expect(cmp.settingsError).toBe('');
  });
});
