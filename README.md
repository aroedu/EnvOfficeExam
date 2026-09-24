# Ticket Management API

מימוש API עבור ניהול פניות (Tickets), על בסיס .NET 10 Web API + EF Core + PostgreSQL (Supabase),
המכסה את סעיפים 1–4 במסמך המבחן.

## הרצה מול Supabase

1. ב-Supabase: Project Settings → Database → Connection string (Session / Transaction pooler).
2. עדכנו את `ConnectionStrings:Supabase` ב-[appsettings.json](src/TicketManagement.Api/appsettings.json)
   (או עדיף: `dotnet user-secrets set "ConnectionStrings:Supabase" "..."`).
3. הרצת המיגרציה ליצירת הטבלאות: `dotnet ef database update` בתוך `src/TicketManagement.Api`.
4. הרצת השרת: `dotnet run --project src/TicketManagement.Api`.

## הרצת Angular

1. הפעילו את ה-API לפי השלבים למעלה.
2. הריצו `npm start --prefix src/TicketManagement.Web`.
3. פתחו `http://localhost:4200`. Proxy של Angular מעביר את `/api` ל-`http://localhost:5123`.

## מבנה

- `Models` – ישות `Ticket` (Id, Title, OrganizationName, Status, Priority, AssignedTo, CreatedAt,
  UpdatedAt, Version) ו-`TicketAuditLog`.
- `Data/AppDbContext` – מיפוי EF Core + אינדקסים על Status/Priority/AssignedTo/CreatedAt לתמיכה בסינון ומיון.
- `Dtos` – פרמטרי שאילתה (paging/filter/sort/search), DTOs לתשובות, בקשות עדכון.
- `Services/TicketService` – לוגיקת השאילתות, מעברי סטטוס, concurrency, audit, bulk update.
- `Controllers/TicketsController` – ה-endpoints.
- `Middleware/GlobalExceptionMiddleware` – טיפול גלובלי בשגיאות → `ProblemDetails`.

## כיסוי הסעיפים

**סעיף 1 – שליפה, סינון, מיון, דפדוף**
`GET /api/tickets?page=&pageSize=&status=&priority=&assignedTo=&search=&sortBy=&sortDescending=`
- Paging עם הגבלת `pageSize` (עד 100).
- סינון משולב לפי Status, Priority, AssignedTo.
- חיפוש טקסטואלי (`ILIKE`) ב-Title וב-OrganizationName.
- מיון לפי 5 שדות אפשריים (CreatedAt, UpdatedAt, Priority, Title, Status), כולל tie-break יציב לפי Id.
- כל השאילתה נבנית כ-`IQueryable` (EF Core מתרגם ל-SQL) כך שלא נטענים כלל הנתונים לזיכרון; ה-DB
  מבצע את הסינון/המיון/ה-Skip+Take עם האינדקסים שהוגדרו ב-`AppDbContext`.
- תמיכה מלאה ב-`CancellationToken` לאורך כל השרשרת האסינכרונית.

**סעיף 2 – טיפול בשגיאות ומעברי סטטוס**
- פנייה שאינה קיימת → `TicketNotFoundException` → HTTP 404.
- מעבר סטטוס לא חוקי (`TicketStatusTransitions`, state machine) → HTTP 409.
- כל השגיאות ממופות ל-`ProblemDetails` דרך `GlobalExceptionMiddleware`.

**סעיף 3 – Concurrency אופטימי**
- עמודת `Version` מסומנת כ-`IsConcurrencyToken()`.
- `PATCH /api/tickets/{id}/status` מקבל `Version` נוכחי מהלקוח; אם הרשומה השתנתה בינתיים
  EF Core זורק `DbUpdateConcurrencyException` שממופה ל-409 (`ConcurrencyConflictException`).
- `UpdatedAt` מתעדכן אך ורק דרך השרת (לא מתקבל מהלקוח) ומונע מצב לא עקבי.

**סעיף 4 – Audit + פעולת Bulk**
- כל שינוי סטטוס נכתב ל-`TicketAuditLog` (OldStatus, NewStatus, ChangedAt) בתוך אותה טרנזקציה.
- `GET /api/tickets/{id}/history` מציג את היסטוריית השינויים.
- `POST /api/tickets/bulk-status` מקבל עד 100 פניות בבקשה. בקשה גדולה יותר נדחית כולה עם HTTP 400
  לפני עיבוד הפריטים. עדכוני ה-bulk מתבצעים בזה אחר זה; פנייה שאינה קיימת, מעבר סטטוס לא חוקי או
  קונפליקט concurrency (גרסת `Version` מיושנת) מסומנים ככישלון של אותו פריט, ושאר הפריטים ממשיכים.
  מוחזרת תוצאה לכל פריט (`BulkStatusUpdateItemResult`) עם `Success` ו-`Error`; כשלים ברמת פריט
  אינם משנים את HTTP 200 של תגובת ה-bulk.

## הערה

Supabase הוא PostgreSQL, לכן הגישה נעשית דרך ספריית EF Core הרגילה `Npgsql.EntityFrameworkCore.PostgreSQL`
מול connection string רגיל של Postgres – ללא צורך ב-SDK ייעודי של Supabase.
