import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { AuthService } from './core/services/auth.service';
import { SessionExpiredModal } from './core/components/session-expired-modal/session-expired-modal';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MenuModule, SessionExpiredModal],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly authService = inject(AuthService);

  /** Navigation entries only — the profile dropdown deliberately holds no forms
   *  (changing the display name or password lives on the /profile page instead).
   *  A Dashboard entry joins these once that page exists. */
  protected readonly profileMenuItems: MenuItem[] = [
    { label: 'Profile', icon: 'pi pi-user', routerLink: '/profile' },
    { separator: true },
    { label: 'Log out', icon: 'pi pi-sign-out', command: () => this.authService.logout() },
  ];
}
