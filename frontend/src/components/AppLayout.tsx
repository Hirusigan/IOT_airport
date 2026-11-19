import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useDashboard } from '../context/DashboardProvider';
import luggageIcon from '../assets/luggage.svg';
import {
  FaSuitcase,
  FaPlane,
  FaBell,
  FaChartBar,
  FaHeadSideVirus,
} from 'react-icons/fa';
import { PiHandEyeFill } from "react-icons/pi";
import './styles/AppLayout.css';


const navigation = [
  { path: '/', title: 'Overview', icon: PiHandEyeFill },
  { path: '/luggage', title: 'Luggage', icon: FaSuitcase },
  { path: '/airports', title: 'Airports', icon: FaPlane },
  { path: '/alerts', title: 'Alerts', icon: FaBell },
  { path: '/statistics', title: 'Statistics', icon: FaChartBar },
  { path: '/devtools', title: 'Devtools', icon: FaHeadSideVirus },
];
export function AppLayout() {
  const { live, error, statistics } = useDashboard();
  const { pathname } = useLocation();
  const title =
    navigation.find((item) => item.path === pathname)?.title ?? 'Overview';
  return (
    <div className="shell">
      <aside>
        <div className="brand">
          <img
            src={luggageIcon}
            alt=""
            aria-hidden="true"
            className="brand-icon"
          />
          <div className="brand-text">
            <span className="brand-title">Luggage</span>
            <span className="brand-sub">Airport operations</span>
          </div>
      </div>
        <nav>
          {navigation.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              end
              className={({ isActive }) => (isActive ? 'selected' : '')}
            >
              <item.icon />
              <span>{item.title}</span>
            </NavLink>
          ))}
        </nav>
      </aside>
      <main>
        <header>
          <div>
            <p className="eyebrow">AIRPORT LUGGAGE TRACKING</p>
            <h1>{title}</h1>
          </div>
          <span className={`connection ${live === 'Live' ? 'online' : ''}`}>
              <span className="connection-dot" aria-hidden="true" />
              {live}
          </span>
        </header>
        {error && (
          <div role="alert" className="error">
            {error} Retrying automatically.
          </div>
        )}
        {!statistics && !error && <p>Loading current airport data…</p>}
        <Outlet />
      </main>
    </div>
  );
}
