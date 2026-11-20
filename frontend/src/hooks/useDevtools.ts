import { useCallback, useEffect, useRef, useState } from 'react';
import { devtoolsService } from '../services/devtoolsService';
import { isCanceled, requestError } from '../services/apiClient';
import type { Scenario } from '../types/Scenario';
import { useDashboard } from '../context/DashboardProvider';

export function useDevtools() {
  const [scenarios, setScenarios] = useState<Scenario[]>([]);
  const {
    simulationStatus: status,
    setSimulationStatus: setStatus,
  } = useDashboard();
  const [error, setError] = useState('');
  const [loadError, setLoadError] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const mounted = useRef(false);
  const actionId = useRef(0);
  const activeActions = useRef(0);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    const currentActionId = actionId.current;
    void Promise.all([
      devtoolsService.scenarios(controller.signal),
      devtoolsService.status(controller.signal),
    ])
      .then(([available, result]) => {
        if (!controller.signal.aborted && currentActionId === actionId.current) {
          setScenarios(available);
          setStatus(result);
          setLoadError('');
        }
      })
      .catch((e) => {
        if (!isCanceled(e) && !controller.signal.aborted)
          setLoadError(requestError(e));
      });
    return () => {
      mounted.current = false;
      controller.abort();
    };
  }, []);
  const execute = useCallback(
    async (action: () => Promise<unknown>, success: string) => {
      const currentActionId = ++actionId.current;
      activeActions.current += 1;
      setBusy(true);
      setError('');
      setMessage('');
      try {
        await action();
        const result = await devtoolsService.status();
        if (mounted.current && currentActionId === actionId.current) {
          setStatus(result);
          setMessage(success);
        }
      } catch (e) {
        if (mounted.current && currentActionId === actionId.current)
          setError(requestError(e));
      } finally {
        activeActions.current -= 1;
        if (mounted.current) setBusy(activeActions.current > 0);
      }
    },
    [],
  );
  return {
    scenarios,
    status,
    error: error || loadError,
    busy,
    message,
    execute,
  };
}
