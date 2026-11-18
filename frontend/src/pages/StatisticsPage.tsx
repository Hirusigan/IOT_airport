import { useDashboard } from '../context/DashboardProvider';
import { StatisticsCards } from '../components/StatisticsCards';
export function StatisticsPage() {
  const { statistics } = useDashboard();
  return (
    <section className="panel">
      <h2>Stored system activity</h2>
      <StatisticsCards
        items={Object.entries(statistics ?? {}).map(([key, value]) => ({
          label: key.replace(/([A-Z])/g, ' $1'),
          value,
        }))}
      />
      <p>Counts include all imported runs and persisted history.</p>
    </section>
  );
}
