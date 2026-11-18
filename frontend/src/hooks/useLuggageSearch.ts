import { useEffect, useState } from 'react';
import { luggageService } from '../services/luggageService';
import { isCanceled, requestError } from '../services/apiClient';
import type { LuggageDetails } from '../types/LuggageDetails';
export function useLuggageSearch(id: string, revision: number) {
  const [result, setResult] = useState<{ id: string; data: LuggageDetails } | null>(null);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    setError('');
    if (!id) return;
    const controller = new AbortController();
    void luggageService.getDetails(id, controller.signal).then(data => {
      if (!controller.signal.aborted) setResult({ id, data });
    }).catch(e => { if (!isCanceled(e) && !controller.signal.aborted) setError(requestError(e)); });
    return () => controller.abort();
  }, [id, revision, attempt]);
  return { bag: result?.id === id ? result.data : null, error, retry: () => setAttempt(value => value + 1) };
}
