import { apiClient } from './apiClient';
import type { Scenario } from '../types/Scenario';
import type { SimulationStatus } from '../types/SimulationStatus';
export const devtoolsService = {
  async scenarios(signal?: AbortSignal) {
    return (await apiClient.get<Scenario[]>('/devtools/scenarios', { signal }))
      .data;
  },
  async status(signal?: AbortSignal) {
    return (
      await apiClient.get<SimulationStatus>('/devtools/simulation', { signal })
    ).data;
  },
  async start(scenarioId: string) {
    return (
      await apiClient.post<SimulationStatus>('/devtools/simulation', {
        scenarioId,
      })
    ).data;
  },
  async stop() {
    return (await apiClient.post<SimulationStatus>('/devtools/simulation/stop'))
      .data;
  },
  async clear() {
    await apiClient.post('/devtools/database/clear');
  },
  async seed() {
    await apiClient.post('/devtools/database/seed');
  },
};
