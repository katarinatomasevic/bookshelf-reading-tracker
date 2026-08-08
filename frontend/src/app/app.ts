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

  /** Account actions only. The pages of the app — Home, My shelf, Dashboard — are in the header
   *  itself, so the dropdown is not half navigation and half account: it holds the two things
   *  that are about *you* rather than about your books. It deliberately holds no forms either
   *  (changing the display name or password lives on the /profile page instead). */
  protected readonly profileMenuItems: MenuItem[] = [
    { label: 'Profile', icon: 'pi pi-user', routerLink: '/profile' },
    { separator: true },
    { label: 'Log out', icon: 'pi pi-sign-out', command: () => this.authService.logout() },
  ];
}
