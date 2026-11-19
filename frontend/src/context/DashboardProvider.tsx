import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react';
import { airportService } from '../services/airportService';
import { alertService } from '../services/alertService';
import { statisticsService } from '../services/statisticsService';
import { startLiveUpdates } from '../services/liveUpdatesService';
import { isCanceled, requestError } from '../services/apiClient';
import type { Airport } from '../types/Airport';
import type { Alert } from '../types/Alert';
import type { Statistics } from '../types/Statistics';
import type { ConnectionStatus } from '../types/ConnectionStatus';

function useDashboardState() {
  const [airports, setAirports] = useState<Airport[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [statistics, setStatistics] = useState<Statistics | null>(null);
  const [error, setError] = useState('');
  const [live, setLive] = useState<ConnectionStatus>('Connecting');
  const [revision, setRevision] = useState(0);
  const [selected, setSelected] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [searched, setSearched] = useState('');
  const [activeOnly, setActiveOnly] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    let refreshing = false;
    const refresh = async () => {
      if (refreshing || controller.signal.aborted) return;
      refreshing = true;
      setRevision((value) => value + 1);
      try {
        const [a, b, c] = await Promise.all([
          airportService.getAll(controller.signal),
          alertService.getAll(controller.signal),
          statisticsService.get(controller.signal),
        ]);
        if (!controller.signal.aborted) {
          setAirports(a);
          setAlerts(b);
          setStatistics(c);
          setError('');
        }
      } catch (e) {
        if (!isCanceled(e) && !controller.signal.aborted)
          setError(requestError(e));
      } finally {
        refreshing = false;
      }
    };
    const stop = startLiveUpdates(() => {
      void refresh();
    }, setLive);
    return () => {
      controller.abort();
      stop();
    };
  }, []);
  return {
    airports,
    alerts,
    statistics,
    error,
    live,
    revision,
    selected,
    setSelected,
    query,
    setQuery,
    searched,
    setSearched,
    activeOnly,
    setActiveOnly,
  };
}
const DashboardContext = createContext<ReturnType<
  typeof useDashboardState
> | null>(null);
export function DashboardProvider({ children }: { children: ReactNode }) {
  return (
    <DashboardContext.Provider value={useDashboardState()}>
      {children}
    </DashboardContext.Provider>
  );
}
export function useDashboard() {
  const value = useContext(DashboardContext);
  if (!value) throw new Error('DashboardProvider is required');
  return value;
}
