import type { ReactNode } from 'react';
import './styles/Dashboard.css';

export type StatTone = 'teal' | 'blue' | 'purple' | 'amber' | 'green' | 'red' | 'slate';

export interface StatItem {
  label: string;
  value: number | string;
  icon?: ReactNode;
  tone?: StatTone;
}

export function StatisticsCards({
  items,
  overview = false,
}: {
  items: StatItem[];
  overview?: boolean;
}) {
  return (
    <div className={overview ? 'metrics' : 'statistics'}>
      {items.map((item) => (
        <div key={item.label} className="stat-card">
          <div className="stat-content">
            <strong>{item.value}</strong>
            <span>{item.label}</span>
          </div>
          {item.icon && (
            <div
              className={`stat-icon ${item.tone ?? 'slate'}`}
              aria-hidden="true"
            >
              {item.icon}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}