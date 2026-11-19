import type { ReactNode } from 'react';
import {
  FiActivity,
  FiAlertTriangle,
  FiBriefcase,
  FiCheckCircle,
  FiClipboard,
  FiMail,
  FiMapPin,
  FiSend,
} from 'react-icons/fi';
import { MdFlightTakeoff } from "react-icons/md";
import { useDashboard } from '../context/DashboardProvider';
import { StatisticsCards, type StatTone } from '../components/StatisticsCards';

const STAT_META: Record<string, { icon: ReactNode; tone: StatTone }> = {
  airports: { icon: <FiMapPin />, tone: 'teal' },
  flights: { icon: <MdFlightTakeoff />, tone: 'blue' },
  journeys: { icon: <FiBriefcase />, tone: 'amber' },
  events: { icon: <FiActivity />, tone: 'purple' },
  loadedAssignments: { icon: <FiClipboard />, tone: 'slate' },
  activeAlerts: { icon: <FiAlertTriangle />, tone: 'red' },
  resolvedAlerts: { icon: <FiCheckCircle />, tone: 'green' },
  invalidMessages: { icon: <FiMail />, tone: 'slate' },
};

function formatLabel(key: string) {
  const spaced = key.replace(/([A-Z])/g, ' $1').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export function StatisticsPage() {
  const { statistics } = useDashboard();

  return (
    <section className="panel">
      <h2>Stored system activity</h2>
      <StatisticsCards
        items={Object.entries(statistics ?? {}).map(([key, value]) => ({
          label: formatLabel(key),
          value,
          icon: STAT_META[key]?.icon,
          tone: STAT_META[key]?.tone,
        }))}
      />
      <p className="panel-footnote">
        Counts include all imported runs and persisted history.
      </p>
    </section>
  );
}