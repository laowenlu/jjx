import { BrowserRouter as Router, Route, Routes } from 'react-router-dom';
import ExampleView from '@/views/ExampleView';
import NotFoundView from '@/views/NotFoundView';

/**
 * Minimal starter route table.
 * Replace ExampleView with the app's real first route as soon as one exists.
 * ExampleView is not a style reference or product-screen layout template.
 */
const AppRoutes = () => {
  return (
    <Router basename={import.meta.env.BASE_URL}>
      <Routes>
        <Route index element={<ExampleView />} />
        <Route path="*" element={<NotFoundView />} />
      </Routes>
    </Router>
  );
};

export default AppRoutes;
