import { apiClient } from './apiClient';
import type { Airport } from '../types/Airport';
import type { AirportDetails } from '../types/AirportDetails';
export const airportService = {
  async getAll(signal?: AbortSignal) { return (await apiClient.get<Airport[]>('/airports', { signal })).data; },
  async getDetails(id: string, signal?: AbortSignal) { return (await apiClient.get<AirportDetails>(`/airports/${encodeURIComponent(id)}`, { signal })).data; },
  async register(airport: { id: string; name: string; latitude: number; longitude: number; regionId: string }) {
    return (await apiClient.post<Airport>('/airports', airport)).data;
  },
};
