import type { ProblemDetails, LoginRequest, LoginResponse, AuthenticatedAgent } from '../types/api';

/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

const TOKEN_KEY = 'supportdesk_token';
const AGENT_KEY = 'supportdesk_agent';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

/**
 * Get the stored JWT token, if any.
 */
export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

/**
 * Get the stored authenticated agent, if any.
 */
export function getAgent(): AuthenticatedAgent | null {
  const stored = localStorage.getItem(AGENT_KEY);
  if (!stored) return null;
  try {
    return JSON.parse(stored) as AuthenticatedAgent;
  } catch {
    return null;
  }
}

/**
 * Store the JWT token and agent info after successful login.
 */
export function setAuth(token: string, agent: AuthenticatedAgent): void {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(AGENT_KEY, JSON.stringify(agent));
}

/**
 * Clear the stored authentication data.
 */
export function clearAuth(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(AGENT_KEY);
}

/**
 * Check if the user is currently authenticated.
 */
export function isAuthenticated(): boolean {
  return getToken() !== null;
}

/**
 * The single place that talks to the API. Returns parsed JSON, or throws an
 * {@link ApiError} that callers can show to the user.
 */
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(init?.headers ?? {}),
  };

  if (token) {
    (headers as Record<string, string>)['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers,
  });

  if (response.status === 401) {
    clearAuth();
    // If we're not already on the login page, redirect to it
    if (window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
    throw new ApiError(401, 'Your session has expired. Please log in again.');
  }

  if (!response.ok) {
    const problem = await readProblem(response);

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Authenticate with email and password.
 */
export async function login(request: LoginRequest): Promise<LoginResponse> {
  const response = await fetch(`${baseUrl}/auth/login`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  return (await response.json()) as LoginResponse;
}

/**
 * Get the current authenticated agent.
 */
export async function getMe(): Promise<AuthenticatedAgent> {
  const response = await fetch(`${baseUrl}/auth/me`, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${getToken()}`,
    },
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  return (await response.json()) as AuthenticatedAgent;
}

/** Turns anything thrown by a fetch into a message worth showing a user. */
export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}