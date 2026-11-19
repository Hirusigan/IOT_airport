import { useDashboard } from '../context/DashboardProvider';
import { AlertList } from '../components/AlertList';
import './styles/AlertsPage.css';

export function AlertsPage() {
  const { alerts, activeOnly, setActiveOnly } = useDashboard();
  const visibleAlerts = alerts.filter((a) => !activeOnly || a.active);

  return (
    <section className="panel">
      <div className="panel-heading alerts-header">
        <div className="alerts-title">
          <h2>Handling alerts</h2>
          <span className="alerts-count">{visibleAlerts.length}</span>
        </div>

        <label className={`alerts-filter ${activeOnly ? 'is-checked' : ''}`}>
          <input
            type="checkbox"
            checked={activeOnly}
            onChange={(e) => setActiveOnly(e.target.checked)}
          />
          <span>Active only</span>
        </label>
      </div>

      <AlertList alerts={visibleAlerts} />
    </section>
  );
}