# Akun Bawaan (Seeding)

Saat aplikasi mulai, `Data/DataSeeder.cs` membuat peran `Admin` dan `User`, lalu akun awal.

## Mode Development

Jika `ASPNETCORE_ENVIRONMENT=Development`, akun berikut dibuat otomatis **hanya untuk pengembangan lokal**:

| Peran | Email | Password |
|---|---|---|
| Admin | `admin@blazorquiz.com` | `Admin123!` |
| User | `user@blazorquiz.com` | `User123!` |

## Produksi

Di luar Development **tidak ada akun yang dibuat** kecuali Anda mengaturnya lewat konfigurasi:

| Kunci | Fungsi |
|---|---|
| `Seed:AdminEmail` | Email akun admin |
| `Seed:AdminPassword` | Password akun admin |
| `Seed:UserEmail` | (opsional) Email akun user |
| `Seed:UserPassword` | (opsional) Password akun user |

Contoh dengan variabel lingkungan:

```bash
export Seed__AdminEmail="admin@contoh.id"
export Seed__AdminPassword="<password-kuat-anda>"
```

Atau dengan user-secrets saat pengembangan:

```bash
dotnet user-secrets set "Seed:AdminEmail" "admin@contoh.id"
dotnet user-secrets set "Seed:AdminPassword" "<password-kuat-anda>"
```

Akun hanya dibuat jika belum ada, dan password tidak ditulis ke log. Setelah login pertama, ganti password lewat halaman akun.


---

# Default Accounts (Seeding) — English

On startup, `Data/DataSeeder.cs` creates the `Admin` and `User` roles, then the initial accounts.

## Development mode

When `ASPNETCORE_ENVIRONMENT=Development`, the following accounts are created automatically **for local development only**:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@blazorquiz.com` | `Admin123!` |
| User | `user@blazorquiz.com` | `User123!` |

## Production

Outside Development **no accounts are created** unless you configure them:

| Key | Purpose |
|---|---|
| `Seed:AdminEmail` | Admin account email |
| `Seed:AdminPassword` | Admin account password |
| `Seed:UserEmail` | (optional) User account email |
| `Seed:UserPassword` | (optional) User account password |

Example with environment variables:

```bash
export Seed__AdminEmail="admin@example.com"
export Seed__AdminPassword="<your-strong-password>"
```

Or with user-secrets during development:

```bash
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "<your-strong-password>"
```

Accounts are only created if they do not exist yet, and passwords are never written to the log. After the first login, change the password on the account page.
