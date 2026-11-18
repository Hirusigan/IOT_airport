import { apiClient } from './apiClient';
import type { Alert } from '../types/Alert';
export const alertService = {
  async getAll(signal?: AbortSignal) {
    return (await apiClient.get<Alert[]>('/alerts', { signal })).data;
  },
};
