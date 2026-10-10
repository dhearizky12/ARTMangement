import { Link } from "react-router-dom";
import { AdminLayout } from "../../components/admin/AdminLayout";
export function AdminNotFoundPage() {
  return (
    <AdminLayout>
      <h1>Halaman tidak ditemukan</h1>
      <p>Alamat yang Anda tuju tidak ada di ruang kelola.</p>
      <Link className="btn btn-ghost" to="/admin/dashboard">
        Kembali ke dashboard
      </Link>
    </AdminLayout>
  );
}