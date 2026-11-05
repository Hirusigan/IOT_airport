import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useDashboard } from '../context/DashboardProvider';
const navigation = [{ path: '/', title: 'Overview' }, { path: '/luggage', title: 'Luggage' }, { path: '/alerts', title: 'Alerts' }, { path: '/statistics', title: 'Statistics' }, { path: '/devtools', title: 'Devtools' }];
export function AppLayout() {
  const { live, error, statistics } = useDashboard();
  const { pathname } = useLocation();
  const title = navigation.find(item => item.path === pathname)?.title ?? 'Overview';
  return <div className="shell"><aside><div className="brand"><span>↗</span> Luggage<span className="brand-sub">Airport operations</span></div>
    <nav>{navigation.map(item => <NavLink key={item.path} to={item.path} end className={({ isActive }) => isActive ? 'selected' : ''}>{item.title}</NavLink>)}</nav>
    <div className="sidebar-bottom">EUROPE REGION<small>Initial course project</small></div></aside>
    <main><header><div><p className="eyebrow">AIRPORT LUGGAGE TRACKING</p><h1>{title}</h1></div><span className={`connection ${live === 'Live' ? 'online' : ''}`}>● {live}</span></header>
      {error && <div role="alert" className="error">{error} Retrying automatically.</div>}
      {!statistics && !error && <p>Loading current airport data…</p>}<Outlet/>
    </main></div>;
}
