import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// Sessão OIDC simulada: o login real é do Keycloak e foi testado de ponta a ponta na F2.
vi.mock('react-oidc-context', () => ({
  useAuth: () => ({
    isLoading: false,
    isAuthenticated: true,
    activeNavigator: undefined,
    error: undefined,
    user: { access_token: 'token-de-teste' },
    signinRedirect: vi.fn(),
    signoutRedirect: vi.fn(),
  }),
}));

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
