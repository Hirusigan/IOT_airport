import { useEffect, useState } from 'react';
import { airportService } from '../services/airportService';
import { isCanceled, requestError } from '../services/apiClient';
import type { AirportDetails } from '../types/AirportDetails';
export function useAirportDetails(id: string | null, revision: number) {
  const [result, setResult] = useState<{ id: string; data: AirportDetails } | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    setError('');
    if (!id) return;
    const controller = new AbortController();
    void airportService.getDetails(id, controller.signal).then(data => {
      if (!controller.signal.aborted) setResult({ id, data });
    }).catch(e => { if (!isCanceled(e) && !controller.signal.aborted) setError(requestError(e)); });
    return () => controller.abort();
  }, [id, revision]);
  return { details: result?.id === id ? result.data : null, error };
}
