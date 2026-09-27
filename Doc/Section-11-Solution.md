# סעיף 11 – תיעוד הפתרון

## הפעלה

### דרישות מקדימות

- .NET SDK 10.0; הפרויקט נבנה ונבדק עם SDK 10.0.401.
- Node.js ו־npm להתקנת והרצת Angular CLI 20.
- פרויקט PostgreSQL ב־Supabase או שרת PostgreSQL נגיש.
- כלי EF CLI `dotnet-ef` בגרסה 10.0.12.

### הגדרת מסד הנתונים והפעלת ה־API

1. צרו/בחרו מסד PostgreSQL ב־Supabase והעתיקו connection string מתאים לשרת המארח.
2. בסביבת פיתוח הגדירו את הסוד באמצעות User Secrets:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Supabase" "<connection-string>" --project src/TicketManagement.Api
   ```

   בסביבת hosting הגדירו משתנה סביבה בשם `ConnectionStrings__Supabase`. אין לשמור credentials בקובצי `appsettings.json`, בתיעוד או ב־Git.

3. התקינו את כלי המיגרציות אם חסר:

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.0.12
   ```

4. החילו את המיגרציות על מסד היעד:

   ```powershell
   dotnet ef database update --project src/TicketManagement.Api
   ```

5. הפעילו את ה־API:

   ```powershell
   dotnet run --project src/TicketManagement.Api
   ```

   פרופיל הפיתוח מאזין ב־`http://localhost:5123`.

### הפעלת Angular

```powershell
npm install --prefix src/TicketManagement.Web
npm start --prefix src/TicketManagement.Web
```

פתחו `http://localhost:4200`. ה־proxy של Angular מעביר קריאות `/api` ל־API המקומי בפורט 5123.

### בדיקות

הרצת בדיקות ה־API:

```powershell
dotnet test tests/TicketManagement.Api.IntegrationTests/TicketManagement.Api.IntegrationTests.csproj --configuration Release
```

הפרויקט כולל ארבע בדיקות אינטגרציה עם `WebApplicationFactory` ו־SQLite זמני בזיכרון. הן מכסות סינון, קלט לא תקין, עדכון סטטוס ו־audit יחד עם cache invalidation, וגרסה מיושנת שמחזירה 409. SQLite מאפשר להריץ את הבדיקות בלי Docker, אך אינו מחליף בדיקות PostgreSQL לתכונות ספק ייחודיות כמו `ILIKE`.

בניית הממשק:

```powershell
npm run build --prefix src/TicketManagement.Web
```

## טכנולוגיות וגרסאות

| רכיב | טכנולוגיה / גרסה |
| --- | --- |
| Backend | ASP.NET Core Web API, ‏.NET 10 (`net10.0`) |
| ORM ומיגרציות | Entity Framework Core 10.0.12 |
| PostgreSQL provider | Npgsql EF Core 10.0.3 |
| Database | PostgreSQL מנוהל ב־Supabase |
| Frontend | Angular 20.3; Angular CLI/Build 20.3.37 |
| UI state/forms | Angular Signals, Reactive Forms ו־RxJS 7.8 |
| Icons | `@lucide/angular` 1.48 |
| בדיקות API | xUnit 2.9, ASP.NET Core MVC Testing 10.0.11, SQLite EF Core 10.0.12 |

## מבנה הפתרון

- `src/TicketManagement.Api/Controllers` – endpoints עבור רשימה, פרטים, סטטיסטיקות, עדכוני סטטוס, bulk והיסטוריה.
- `src/TicketManagement.Api/Services` – שאילתות, מעברי סטטוס, concurrency, audit ועדכוני bulk.
- `src/TicketManagement.Api/Models` ו־`Dtos` – ישויות המסד וחוזי הבקשות/תשובות.
- `src/TicketManagement.Api/Data` – `AppDbContext` ומיגרציות EF Core.
- `src/TicketManagement.Api/Middleware` – מיפוי חריגות לתגובות `ProblemDetails`.
- `src/TicketManagement.Web/src/app` – רכיב סביבת העבודה, מודלים ושירות Angular מול ה־API.
- `tests/TicketManagement.Api.IntegrationTests` – בדיקות HTTP/API עם מסד SQLite מבודד לכל בדיקה.
- `Doc` – מסמכי ביצועים, Cache, בדיקות ותיעוד הפתרון.

## בסיס הנתונים והאינדקסים

נבחר PostgreSQL ב־Supabase, וניגשים אליו דרך Npgsql ו־EF Core. המודל יחסי: פניות ויומן audit, מפתחות זרים וטרנזקציות. EF Core מספק מיגרציות, ושאילתות הרשימה נשארות ב־DB עד לאחר סינון, מיון ודפדוף. אין שימוש ב־Supabase SDK ייעודי, משום שהפעולות הנדרשות הן SQL יחסיות רגילות.

תרשים קשרים ורשימת עמודות מלאה מופיעים ב־[תיעוד מבנה מסד הנתונים](Database-Schema.md).

האינדקסים מוגדרים ב־`AppDbContext` ונוצרים במיגרציה:

- `tickets` – מפתח ראשי על `Id`; אינדקסים על `Status`,‏ `Priority`,‏ `AssignedTo` ו־`CreatedAt`.
- `tickets(Status, Priority, CreatedAt)` – נועד לסינון המשולב הנפוץ ולמיון לפי תאריך יצירה.
- `ticket_audit_logs(TicketId, ChangedAt)` – נועד לשליפת היסטוריה לפי פנייה ובסדר כרונולוגי.

חיפוש substring מבוסס `ILIKE '%term%'`; אינדקסי B-tree הקיימים אינם מאיצים אותו היטב. הרחבת `pg_trgm` ואינדקסי GIN הם שיפור אפשרי אם המדידה מצדיקה אותם.

## Bulk ו־Concurrency

`POST /api/tickets/bulk-status` מגביל בקשה ל־100 פריטים; חריגה מהגודל נדחית כולה ב־HTTP 400 לפני עיבוד. הפריטים מעובדים בזה אחר זה. לכל פריט נשמרת תוצאה נפרדת: הצלחה, או כשל עקב פנייה חסרה, מעבר סטטוס אסור או conflict. כשל בפריט אחד אינו עוצר את האחרים. כל פריט מצליח מעדכן את הפנייה ואת רשומת ה־audit באותה פעולת `SaveChanges`/טרנזקציה; כל הבקשה אינה טרנזקציה אטומית אחת.

Concurrency אופטימי ממומש באמצעות `Ticket.Version`, שמוגדר ב־EF Core כ־`IsConcurrencyToken`. הלקוח שולח את הגרסה האחרונה שקרא. בעדכון, EF כולל את הגרסה בתנאי ה־`UPDATE`; אם הרשומה השתנתה, מספר השורות שהושפעו אינו תואם ו־`DbUpdateConcurrencyException` מומר ל־HTTP 409. השרת מגדיל את הגרסה ומעדכן את `UpdatedAt`; ערכים אלה אינם מתקבלים מהלקוח. אותו מנגנון חל גם על כל פריט bulk.

## Cache ו־Invalidation

`GET /api/tickets/statistics` שומר את הסיכומים לפי סטטוס ועדיפות ב־`IMemoryCache` תחת `ticket-statistics:v1`, עם תפוגה מוחלטת של דקה. הנתון נבחר משום שהוא מסכם שתי שאילתות aggregation ועלול להיקרא שוב ושוב, בעוד ששינויים בו מתרחשים רק בעת יצירת פנייה או שינוי סטטוס.

אחרי יצירה או עדכון סטטוס מוצלח נמחק המפתח. עדכון שנכשל אינו מבטל cache. עדכוני bulk עוברים באותו מסלול עדכון ומפעילים invalidation. ה־TTL מגביל התיישנות אם שינוי נעשה ממקור חיצוני שלא ביצע invalidation.

`IMemoryCache` הוא מקומי לתהליך. בפריסה מרובת מופעים יש להחליף אותו ב־`IDistributedCache` עם Redis משותף ולבטל את אותו מפתח אחרי כתיבה. אם בנוסף משתמשים ב־L1 מקומי בכל מופע, נדרש ערוץ הפצת invalidation, למשל Redis Pub/Sub.

## החלטות טכנולוגיות

1. **PostgreSQL/Supabase עם EF Core/Npgsql:** נבחר מסד יחסי בגלל סינון, מיון, pagination, קשר בין פנייה ל־audit וטרנזקציות. חלופות שנשקלו: Supabase SDK (שאינו נחוץ עבור CRUD/SQL רגיל) או מסד NoSQL, שהיה מוסיף מורכבות עבור הקשרים והשאילתות.
2. **Concurrency אופטימי עם `Version`:** נבחר כדי לזהות עדכונים מתנגשים בלי לנעול שורות בזמן שהמשתמש עורך. חלופות: last-write-wins, שעלול לדרוס עדכון של משתמש אחר, או נעילות pessimistic שמחזיקות משאבי DB לאורך זמן.

## מגבלה ושיפור אפשרי

המטמון הנוכחי מבוסס זיכרון מקומי ולכן אינו משותף בין מופעי שרת; ב־deployment מרובה מופעים, שינוי שעובד במופע אחד לא מוחק מיד עותק במופע אחר. השיפור המתאים הוא Redis משותף עם invalidation לכל המופעים, כמתואר לעיל.

## שימוש בכלי AI

במהלך העבודה על סעיפים 6–8 ועל מסמך זה נעשה שימוש ב־GitHub Copilot לסיוע בכתיבת סקריפט/תיעוד מדידת ביצועים, במימוש Cache ובבניית בדיקות אינטגרציה. לוגיקת הליבה של סעיפים 1–4 כבר הייתה קיימת לפני עבודת התיעוד הזו. ההצעות נבדקו מול מבנה הפרויקט; ה־API נבנה ב־Release, ארבע בדיקות האינטגרציה הורצו בהצלחה, ומדידת סעיף 6 הורצה מול סכמה זמנית נפרדת. נדרש review אנושי לפני deployment, ובפרט לוודא הגדרות secrets, הרשאות מסד והתאמה של התנהגות PostgreSQL לסביבת ה־hosting.

תוכנית חזרה ממוקדת לראיון, המבוססת על משקלי המבדק, מופיעה ב־[תוכנית הלימודים לראיון](Interview-Study-Plan.md).
