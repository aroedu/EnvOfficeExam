# סעיף 8 – בדיקות אוטומטיות

הבדיקות נמצאות ב־`tests/TicketManagement.Api.IntegrationTests` ומפעילות את ה־API האמיתי דרך `WebApplicationFactory`, כולל controllers, middleware, service ו־EF Core מול בסיס נתונים יחסי זמני בזיכרון (SQLite). כל בדיקה מקבלת מסד נפרד; אין חיבור ל־Supabase ואין תלות ב־Docker.

להרצה משורש המאגר:

```powershell
dotnet test tests/TicketManagement.Api.IntegrationTests/TicketManagement.Api.IntegrationTests.csproj --configuration Release
```

## תרחישים מכוסים

1. `GetTickets_FiltersBeforePaging` – יוצר נתונים במסד, שולח בקשת HTTP עם מסנני סטטוס ועדיפות, ומוודא שרק הרשומה המתאימה מוחזרת.
2. `GetTickets_ReturnsBadRequestForInvalidPage` – שולח `page=0` ומוודא שוולידציית ה־API מחזירה HTTP 400.
3. `UpdateStatus_WritesAuditAndInvalidatesStatisticsCache` – קורא סטטיסטיקות כדי לאכלס את המטמון, משנה סטטוס דרך ה־API, ומוודא שהגרסה וה־audit נשמרו ושהסטטיסטיקות שנקראו שוב משקפות את השינוי.
4. `UpdateStatus_ReturnsConflictForStaleVersion` – מבצע עדכון מוצלח, מנסה עדכון נוסף עם `Version` מיושן, ומוודא HTTP 409.

`SqliteAppDbContext` קיים רק בפרויקט הבדיקות ומשתמש בהמרת `DateTimeOffset` שנדרשת למיון ב־SQLite. זהו תחליף מתאים לבדיקות אינטגרציה של HTTP ו־EF Core ללא Docker, אך הוא אינו מאמת התנהגות ייחודית ל־PostgreSQL, כגון `ILIKE` או תוכניות ביצוע. בדיקות כאלה דורשות סביבת PostgreSQL נפרדת.
