import { Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from '../components/AppLayout';
import { OverviewPage } from '../pages/OverviewPage';
import { LuggagePage } from '../pages/LuggagePage';
import { AlertsPage } from '../pages/AlertsPage';
import { StatisticsPage } from '../pages/StatisticsPage';
import { DevtoolsPage } from '../pages/DevtoolsPage';
import { AirportsPage } from '../pages/AirportsPage';
export function AppRoutes() {
  return <Routes><Route element={<AppLayout/>}><Route index element={<OverviewPage/>}/><Route path="luggage" element={<LuggagePage/>}/><Route path="airports" element={<AirportsPage/>}/><Route path="alerts" element={<AlertsPage/>}/><Route path="statistics" element={<StatisticsPage/>}/><Route path="devtools" element={<DevtoolsPage/>}/><Route path="*" element={<Navigate to="/" replace/>}/></Route></Routes>;
}
