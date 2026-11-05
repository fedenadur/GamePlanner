import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi, withXhr } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { AuthInterceptor } from './interceptors/auth.interceptor';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { GameListComponent } from './components/game-list/game-list.component';
import { SessionPlannerComponent } from './components/session-planner/session-planner.component';
import { SteamImportComponent } from './components/steam-import/steam-import.component';
import { GameCardComponent } from './components/game-card/game-card.component';
import { LoginComponent } from './components/login/login.component';
import { RegisterComponent } from './components/register/register.component';
import { ProfileComponent } from './components/profile/profile.component';
import { WorkersComponent } from './components/workers/workers.component';
import { GameDetailComponent } from './components/game-detail/game-detail.component';
import { CalendarComponent } from './components/calendar/calendar.component';
import { NowPlayingComponent } from './components/now-playing/now-playing.component';
import { BacklogPrioritizerComponent } from './components/backlog-prioritizer/backlog-prioritizer.component';
import { ClickOutsideDirective } from './directives/click-outside.directive';
import { IgdbSearchModalComponent } from './components/igdb-search-modal/igdb-search-modal.component';
import { JournalComponent } from './components/journal/journal.component';
import { StatsDashboardComponent } from './components/stats-dashboard/stats-dashboard.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { SidebarComponent } from './components/sidebar/sidebar.component';
import { TrackedGamesComponent } from './components/tracked-games/tracked-games.component';
import { FinishGameDialogComponent } from './components/finish-game-dialog/finish-game-dialog.component';
import { LogSessionDialogComponent } from './components/log-session-dialog/log-session-dialog.component';
import { GameEditDialogComponent } from './components/game-edit-dialog/game-edit-dialog.component';
import { ConfirmDialogComponent } from './components/confirm-dialog/confirm-dialog.component';

@NgModule({ declarations: [
        AppComponent,
        SidebarComponent,
        GameListComponent,
        SessionPlannerComponent,
        SteamImportComponent,
        GameCardComponent,
        LoginComponent,
        RegisterComponent,
        ProfileComponent,
        WorkersComponent,
        GameDetailComponent,
        CalendarComponent,
        NowPlayingComponent,
        BacklogPrioritizerComponent,
        ClickOutsideDirective,
        IgdbSearchModalComponent,
        JournalComponent,
        StatsDashboardComponent,
        DashboardComponent,
        TrackedGamesComponent,
        FinishGameDialogComponent,
        LogSessionDialogComponent,
        GameEditDialogComponent,
        ConfirmDialogComponent
    ],
    bootstrap: [AppComponent], imports: [BrowserModule,
        AppRoutingModule,
        FormsModule], providers: [
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
        provideHttpClient(withXhr(), withInterceptorsFromDi())
    ] })
export class AppModule { }
