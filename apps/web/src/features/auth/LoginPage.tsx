import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { login, setAuth } from '../../api/client';
import { Field } from '../../components/Field';
import { ApiError } from '../../api/client';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: { pathname: string } })?.from?.pathname ?? '/tickets';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | undefined>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(undefined);
    setIsSubmitting(true);

    try {
      const response = await login({ email: email.trim(), password });
      setAuth(response.token, response.agent);
      navigate(from, { replace: true });
    } catch (caught) {
      const message =
        caught instanceof ApiError
          ? caught.message
          : 'An unexpected error occurred. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <main className="login-page__card card">
        <h1>Sign in to Northwind Support</h1>

        <form onSubmit={handleSubmit} noValidate>
          <Field
            id="email"
            label="Email"
            error={error}
          >
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="email"
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value);
                  if (error) setError(undefined);
                }}
                disabled={isSubmitting}
                autoComplete="email"
                autoFocus
              />
            )}
          </Field>

          <Field
            id="password"
            label="Password"
            error={error}
          >
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="password"
                value={password}
                onChange={(event) => {
                  setPassword(event.target.value);
                  if (error) setError(undefined);
                }}
                disabled={isSubmitting}
                autoComplete="current-password"
              />
            )}
          </Field>

          {error && (
            <div className="field__error" role="alert">
              {error}
            </div>
          )}

          <div className="button-row">
            <button
              type="submit"
              className="button button--primary"
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </button>
          </div>
        </form>

        <p className="field__hint">
          Development credentials: any agent email with password <code>Password123!</code>
        </p>
      </main>
    </div>
  );
}