# Backend OD Gudang

Backend ini dibuat dengan ASP.NET Core `net10.0`, PostgreSQL, dan ASP.NET Identity.

## Fitur utama

- Role pengguna: `Admin` dan `User`
- Login aman memakai cookie auth
- Password hashing bawaan ASP.NET Identity
- Proteksi CSRF memakai antiforgery token
- Cookie auth `HttpOnly`, `SameSite=Strict`, dan `Secure` di production
- Lockout setelah percobaan login gagal berulang
- Rate limiting pada endpoint autentikasi
- Security headers dasar seperti CSP, `X-Frame-Options`, dan `nosniff`

## Konfigurasi database

Atur connection string di `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=od_gudang;Username=postgres;Password=postgres"
}
```

Pastikan database PostgreSQL tersedia dan kredensialnya sesuai.

## Akun seed awal

Nilai default ada di `appsettings.json` dan bisa diubah:

- `admin / Admin!23456` dengan role `Admin`
- `operator / User!23456` dengan role `User`

## Menjalankan project

```powershell
dotnet restore backend\backend.csproj --configfile backend\NuGet.Config
dotnet run --project backend\backend.csproj
```

Frontend statis di folder `frontend/` akan diserve langsung oleh backend.

## Endpoint penting

- `GET /api/auth/csrf-token`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/detections`
- `POST /api/detections` (`Admin` only)
