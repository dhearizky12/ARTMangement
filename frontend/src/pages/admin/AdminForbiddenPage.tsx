import { Link } from "react-router-dom";
import { AdminLayout } from "../../components/admin/AdminLayout";
export function AdminForbiddenPage() {
  return (
    <AdminLayout>
      <h1>Akses terbatas</h1>
      <p>Halaman ini khusus administrator platform.</p>
      <Link className="btn btn-ghost" to="/admin/dashboard">
        Kembali ke dashboard
      </Link>
    </AdminLayout>
  );
}