# ⚙️ OM General Store — Backend API

This repository hosts the core REST API for **OM General Store (Dukan)** quick-commerce platform.

---

## 📌 Technology Stack
- **Framework:** ASP.NET Core 9.0 Web API (.NET 9 C#)
- **Database:** Entity Framework Core with SQL Server / SQLite (`dukan.db`)
- **Authentication:** JWT Bearer Authentication with Role-Based Access Control (`Admin`, `OrderManager`, `DeliveryBoy`, `Customer`)
- **Cloud Storage:** Google Cloud Storage (GCS) for product images via service account (`gcpkey.json`)
- **Port:** `http://localhost:5149`

---

## 🚀 How to Run
```bash
dotnet restore
dotnet run --launch-profile http
```
Swagger UI will be available at: `http://localhost:5149/swagger`

---

## 🔑 Key Roles & Architecture
1. **Admin (`Admin`):** Full CRUD for products, categories, staff user management, sales analytics. Default: `admin@dukan.com` / `Admin@123`.
2. **Order Manager (`OrderManager`):** Order lifecycle from `Pending` -> `Packed` -> `Dispatched`.
3. **Delivery Partner (`DeliveryBoy`):** Dispatched orders claim via FCFS (`POST /api/orders/{id}/accept-delivery`), turn-by-turn tracking, and delivery completion with 4-digit customer OTP (`POST /api/orders/{id}/verify-delivery`).
4. **Customer (`Customer`):** Browse items, cart operations, checkout, view OTP upon dispatch.

---

## 🐳 CI/CD & Deployment Guidelines
- **Docker:** Can be containerized with `mcr.microsoft.com/dotnet/aspnet:9.0`.
- **Environment Secrets:** Store `Jwt:Key`, `ConnectionStrings:DefaultConnection`, and `GCP:CredentialsJson` in CI/CD Secret Variables (e.g., GitHub Secrets / GitLab CI Variables).
