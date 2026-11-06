import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { WhatToPlayComponent } from './components/what-to-play/what-to-play.component';
import { GameListComponent } from './components/game-list/game-list.component';
import { SteamImportComponent } from './components/steam-import/steam-import.component';
import { LoginComponent } from './components/login/login.component';
import { RegisterComponent } from './components/register/register.component';
import { ProfileComponent } from './components/profile/profile.component';
import { WorkersComponent } from './components/workers/workers.component';
import { GameDetailComponent } from './components/game-detail/game-detail.component';
import { CalendarComponent } from './components/calendar/calendar.component';
import { NowPlayingComponent } from './components/now-playing/now-playing.component';
import { JournalComponent } from './components/journal/journal.component';
import { StatsDashboardComponent } from './components/stats-dashboard/stats-dashboard.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { AuthGuard } from './guards/auth.guard';
import { AdminGuard } from './guards/admin.guard';
import { TrackedRedirectGuard } from './guards/tracked-redirect.guard';

const routes: Routes = [
  { path: 'login',    component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: '',          component: DashboardComponent,        canActivate: [AuthGuard] },
  { path: 'dashboard', component: DashboardComponent,        canActivate: [AuthGuard] },
  { path: 'games',   component: GameListComponent,       canActivate: [AuthGuard] },
  { path: 'what-to-play', component: WhatToPlayComponent, canActivate: [AuthGuard] },
  { path: 'steam',   component: SteamImportComponent,    canActivate: [AuthGuard] },
  { path: 'profile', component: ProfileComponent,        canActivate: [AuthGuard] },
  { path: 'workers',    component: WorkersComponent,     canActivate: [AuthGuard, AdminGuard] },
  { path: 'games/:id', component: GameDetailComponent,  canActivate: [AuthGuard] },
  { path: 'calendar',    component: CalendarComponent,    canActivate: [AuthGuard] },
  { path: 'now-playing',          component: NowPlayingComponent,          canActivate: [AuthGuard] },
  { path: 'journal',              component: JournalComponent,             canActivate: [AuthGuard] },
  { path: 'stats',                component: StatsDashboardComponent,      canActivate: [AuthGuard] },
  { path: 'planner',             redirectTo: 'what-to-play', pathMatch: 'full' },
  { path: 'backlog-prioritizer', redirectTo: 'what-to-play', pathMatch: 'full' },
  { path: 'tracked-games',       canActivate: [AuthGuard, TrackedRedirectGuard], children: [] },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
