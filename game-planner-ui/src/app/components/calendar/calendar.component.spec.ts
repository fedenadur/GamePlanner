import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { CalendarComponent } from './calendar.component';
import { CalendarService } from '../../services/calendar.service';
import { TrackedGamesService } from '../../services/tracked-games.service';

describe('CalendarComponent tab', () => {
  it('follows the tab query param, back to Releases when it is removed', () => {
    const q$ = new BehaviorSubject<ParamMap>(convertToParamMap({ tab: 'tracked' }));
    const cal = jasmine.createSpyObj<CalendarService>('CalendarService', ['getMonth', 'getWeek']);
    cal.getMonth.and.returnValue(of({}));
    const cmp = new CalendarComponent(cal, {} as TrackedGamesService, {} as Router, { queryParamMap: q$ } as unknown as ActivatedRoute);
    cmp.ngOnInit();
    expect(cmp.tab).toBe('tracked');
    q$.next(convertToParamMap({}));
    expect(cmp.tab).toBe('releases');
    cmp.ngOnDestroy();
  });
});
