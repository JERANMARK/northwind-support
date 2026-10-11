import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { getAgent, clearAuth, isAuthenticated } from './api/client';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const navigate = useNavigate();
  const agent = getAgent();

  const handleLogout = () => {
    clearAuth();
    navigate('/login', { replace: true });
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          {isAuthenticated() && (
            <>
              <NavLink to="/tickets">Tickets</NavLink>
              <NavLink to="/customers">Customers</NavLink>
            </>
          )}
        </nav>

        {isAuthenticated() && agent && (
          <div className="app__user">
            <span className="app__user-name">{agent.fullName}</span>
            <button className="button button--ghost app__logout" onClick={handleLogout}>
              Log out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
