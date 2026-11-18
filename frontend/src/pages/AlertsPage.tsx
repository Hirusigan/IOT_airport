import { useDashboard } from '../context/DashboardProvider';
import { AlertList } from '../components/AlertList';
export function AlertsPage() {
  const { alerts, activeOnly, setActiveOnly } = useDashboard();
  return (
    <section className="panel">
      <div className="panel-heading">
        <h2>Handling alerts</h2>
        <label>
          <input
            type="checkbox"
            checked={activeOnly}
            onChange={(e) => setActiveOnly(e.target.checked)}
          />{' '}
          Active only
        </label>
      </div>
      <AlertList alerts={alerts.filter((a) => !activeOnly || a.active)} />
    </section>
  );
}
