import { useCallback, useEffect, useRef, useState } from 'react';
import { devtoolsService } from '../services/devtoolsService';
import { isCanceled, requestError } from '../services/apiClient';
import type { Scenario } from '../types/Scenario';
import type { SimulationStatus } from '../types/SimulationStatus';

export function useDevtools() {
  const [scenarios, setScenarios] = useState<Scenario[]>([]);
  const [status, setStatus] = useState<SimulationStatus | null>(null);
  const [error, setError] = useState('');
  const [pollError, setPollError] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const mounted = useRef(false);
  const actionId = useRef(0);
  const activeActions = useRef(0);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    let polling = false;
    const poll = async () => {
      if (polling) return;
      polling = true;
      const currentActionId = actionId.current;
      try {
        const [available, result] = await Promise.all([
          devtoolsService.scenarios(controller.signal),
          devtoolsService.status(controller.signal),
        ]);
        if (!controller.signal.aborted && currentActionId === actionId.current) {
          setScenarios(available);
          setStatus(result);
          setPollError('');
        }
      } catch (e) {
        if (!isCanceled(e) && !controller.signal.aborted)
          setPollError(requestError(e));
      } finally {
        polling = false;
      }
    };
    void poll();
    const timer = window.setInterval(() => {
      void poll();
    }, 1000);
    return () => {
      mounted.current = false;
      controller.abort();
      clearInterval(timer);
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
    error: error || pollError,
    busy,
    message,
    execute,
  };
}
