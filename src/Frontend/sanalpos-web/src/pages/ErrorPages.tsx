import { Link } from "react-router-dom";

function ErrorShell({ code, message }: { code: string; message: string }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gray-50">
      <p className="text-6xl font-bold text-brand-600">{code}</p>
      <p className="text-gray-600">{message}</p>
      <Link to="/" className="text-brand-600 hover:underline">
        Ana sayfaya dön
      </Link>
    </div>
  );
}

export function ForbiddenPage() {
  return <ErrorShell code="403" message="Bu sayfaya erişim yetkiniz yok." />;
}

export function NotFoundPage() {
  return <ErrorShell code="404" message="Aradığınız sayfa bulunamadı." />;
}
