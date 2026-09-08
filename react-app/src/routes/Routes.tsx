import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import ExampleView from '@/views/ExampleView';
import NotFoundView from '@/views/NotFoundView';
import ProtectedRoute from '@/routes/ProtectedRoute';
import UnauthorizedView from '@/views/UnauthorizedView';

/**
 * Minimal starter route table.
 * Replace ExampleView with the app's real first route as soon as one exists.
 * ExampleView is not a style reference or product-screen layout template.
 */
const AppRoutes = () => {
  return (
    <Router>
      <Routes>
        <Route index element={<ExampleView />} />
        <Route path="/unauthorized" element={<UnauthorizedView />} />

        {/**
          Example of protecting a route:
          <Route element={<ProtectedRoute />}>
            <Route path="account" element={<AccountView />} />
          </Route>

          Example of restricting a route by role:
          <Route element={<ProtectedRoute roles={[ADMIN_ROLE]} />}>
            <Route path="admin-stuff" element={<AdminStuffView />} />
          </Route>
        */}
        <Route path="*" element={<NotFoundView />} />
      </Routes>
    </Router>
  );
};

export default AppRoutes;
