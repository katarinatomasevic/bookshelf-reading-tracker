import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/books/search/search').then((m) => m.Search),
  },
  {
    path: 'shelf',
    loadComponent: () => import('./features/shelf/shelf').then((m) => m.Shelf),
    canActivate: [authGuard],
  },
  {
    path: 'books/:id',
    loadComponent: () =>
      import('./features/books/book-details/book-details').then((m) => m.BookDetailsPage),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
    canActivate: [guestGuard],
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
    canActivate: [guestGuard],
  },
  {
    path: 'profile',
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
    canActivate: [authGuard],
  },
];
