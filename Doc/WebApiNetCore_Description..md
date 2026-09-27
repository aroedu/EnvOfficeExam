**תיעוד API ומימוש מערכת פניות**

דוגמאות API, Dependency Injection, Validation, Concurrency, חיפוש, מיון, Pagination, Aggregations, Audit ו־Bulk Operations

**דוגמה לפנייה לשירות**

```
@baseUrl = http://localhost:5123
@ticketId = 11111111-1111-1111-1111-111111111111
@ticketId2 = 22222222-2222-2222-2222-222222222222

### רשימת פניות עם סינון, חיפוש, מיון ודפדוף
GET {{baseUrl}}/api/tickets?page=1&pageSize=20&status=New&priority=High&assignedTo=Alice&search=City&sortBy=CreatedAt&sortDescending=true

### אגרגציות: ספירה לפי סטטוס ועדיפות
GET {{baseUrl}}/api/tickets/statistics

### שליפת פנייה לפי מזהה
GET {{baseUrl}}/api/tickets/{{ticketId}}

### יצירת פנייה
POST {{baseUrl}}/api/tickets
Content-Type: application/json

{
  "title": "Cannot access account",
  "organizationName": "Example City",
  "priority": "High",
  "assignedTo": "Alice"
}

### עדכון סטטוס
PATCH {{baseUrl}}/api/tickets/{{ticketId}}/status
Content-Type: application/json

{
  "newStatus": "InProgress",
  "version": 1
}

### עדכון סטטוס למספר פניות, עד 100 פריטים
POST {{baseUrl}}/api/tickets/bulk-status
Content-Type: application/json

[
  {
    "ticketId": "{{ticketId}}",
    "newStatus": "InProgress",
    "version": 1
  },
  {
    "ticketId": "{{ticketId2}}",
    "newStatus": "Waiting",
    "version": 2
  }
]

### היסטוריית שינויי סטטוס
GET {{baseUrl}}/api/tickets/{{ticketId}}/history
```

**Dependency Injection**

ב־Program.cs נרשמת ההתאמה ITicketService ← TicketService. המשמעות היא שכאשר מחלקה מבקשת ITicketService, מנגנון ה־Dependency Injection מספק לה מופע של TicketService.

ה־TicketsController מקבל ITicketService בבנאי שלו. לכן, כאשר ASP.NET Core יוצר את ה־Controller עבור בקשת API, הוא מספק לו את השירות הרשום.

גם TicketService מקבל AppDbContext בבנאי. AppDbContext נרשם באמצעות AddDbContext, יחד עם הגדרות החיבור ל־PostgreSQL.

ניתן לראות את שרשרת ה־Dependency Injection בקבצים TicketsController.cs, TicketService.cs ו־AppDbContext.cs.

שני השירותים נרשמים כ־Scoped. כלומר, נוצר מופע אחד לכל בקשת HTTP, והוא משותף בתוך אותה בקשה. כך AppDbContext והשירות מטפלים בעבודה של הבקשה בלי ליצור אותם ידנית בכל פעולה.

**Sorting – מיון**

```
GET http://localhost:5123/api/tickets?sortBy=Title&sortDescending=false
```

הפרמטר sortDescending=false מבצע מיון עולה (ASC). כאשר sortDescending=true, מתבצע מיון יורד (DESC). ברירת המחדל היא מיון לפי CreatedAt בסדר יורד.

השדות הנתמכים ב־sortBy הם CreatedAt, UpdatedAt, Priority, Title ו־Status.

**Pagination – דפדוף**

```
GET http://localhost:5123/api/tickets?page=2&pageSize=10
```

הפרמטר page מייצג את מספר העמוד, והפרמטר pageSize מייצג את מספר הרשומות בעמוד. ברירת המחדל של pageSize היא 20, והערך מוגבל לטווח 1–100.

אם נשלח ערך קטן מ־1, הוא מתוקן ל־1. אם נשלח ערך גדול מ־100, הוא מתוקן ל־100.

**Validation**

ב־TicketQueryParameters.cs, Page מתחיל כברירת מחדל ב־1, אך אין בדיקה מפורשת שמונעת ערך 0 או ערך שלילי.

PageSize מוגבל אוטומטית לטווח 1–100. בקריאת API, ערך enum או פרמטר שאינו תקין עשוי להחזיר 400 Bad Request בזכות \[ApiController\] ב־TicketsController.cs.

מעברי סטטוס לא חוקיים וקונפליקטים מחזירים 409 Conflict. כלומר, קיימות מספר בדיקות מובנות, אך כרגע אין Validation מפורש לכל כללי הקלט.

**Concurrency – מניעת עדכון לפי מידע ישן**

מדובר בהגנה מפני מצב שבו לקוח מנסה לעדכן פנייה על בסיס מידע ישן. לכל Ticket יש שדה Version שמתחיל ב־1 ועולה לאחר כל עדכון. השדה מוגדר כ־Concurrency Token ב־AppDbContext.cs.

בתהליך עדכון הסטטוס, הלקוח שולח את הגרסה שקיבל קודם.

```
PATCH /api/tickets/{id}/status
Content-Type: application/json

{
  "newStatus": "InProgress",
  "version": 1
}
```

אם העדכון מצליח, הגרסה עולה ל־2 ומוחזרת בתשובה. אם מישהו כבר עדכן את ה־Ticket, שליחת version ישנה גורמת לתשובה 409 Conflict במקום לדרוס את השינוי הקודם. הבדיקה מתבצעת ב־TicketService.cs, ומבנה הבקשה מוגדר ב־TicketDtos.cs.

**דוגמת סינון**

```
GET /api/tickets?status=New&priority=High&assignedTo=Alice&search=City
```

ניתן לסנן לפי סטטוס, עדיפות, משתמש שהוקצתה אליו הפנייה וטקסט לחיפוש.

**חיפוש לפי OrganizationName ו־Title**

החיפוש הטקסטואלי נמצא בתוך GetTicketsAsync שב־TicketService.cs. הוא משתמש בפרמטר search ומחפש התאמה חלקית גם ב־Title וגם ב־OrganizationName, ללא תלות באותיות גדולות או קטנות, באמצעות PostgreSQL ILIKE.

```
GET /api/tickets?search=City
```

הפרמטר Search מוגדר ב־TicketQueryParameters.cs.

**Asc / Desc – כיוון המיון**

```
GET /api/tickets?sortBy=CreatedAt&sortDescending=true
```

true = DESC – מיון יורד. false = ASC – מיון עולה. ברירת המחדל היא CreatedAt DESC.

**שתי Aggregations**

שתי האגרגציות נמצאות ב־GetStatisticsAsync שב־TicketService.cs.

```
GroupBy(ticket => ticket.Status)
Count()

GroupBy(ticket => ticket.Priority)
Count()
```

האגרגציה הראשונה מחזירה את מספר הפניות עבור כל סטטוס. האגרגציה השנייה מחזירה את מספר הפניות עבור כל עדיפות.

```
GET /api/tickets/statistics
```

ה־Endpoint מוגדר ב־TicketsController.cs.

**הגבלת Page Size ו־Validation לפרמטרים**

pageSize: ברירת המחדל היא 20 והוא מוגבל לטווח 1–100. ערך מחוץ לטווח מתוקן לגבול הקרוב ולא נדחה בשגיאה.

ערך שלא ניתן להמיר לסוג המתאים, כגון page=abc או ערך enum לא מוכר ל־status, priority או sortBy, מחזיר 400.

ל־search ול־assignedTo אין כרגע הגבלות אורך או כללי Validation נוספים.

**CancellationToken בפעולות I/O**

פעולות ה־API מקבלות CancellationToken מ־ASP.NET Core ומעבירות אותו לשירות, כפי שמוגדר ב־TicketsController.cs.

בשירות הוא מועבר לכל פעולות ה־I/O מול EF Core: ToListAsync, CountAsync, FirstOrDefaultAsync, AnyAsync ו־SaveChangesAsync, כולל שתי שאילתות האגרגציה ופעולות ה־Bulk.

כאשר בקשת HTTP מתבטלת, למשל כאשר הלקוח מתנתק, הביטול יכול להתפשט גם לשאילתת מסד הנתונים ולפעולת השמירה. גם ה־Middleware מתעלם מ־OperationCanceledException כאשר הבקשה בוטלה, במקום לנסות לכתוב תגובת שגיאה ללקוח שכבר התנתק.

**החזרת DTOs ולא ישויות DB ישירות**

ה־API מחזיר DTOs ולא ישויות EF/DB ישירות. לדוגמה, ביצירה ובשליפה ממירים Ticket ל־TicketDto באמצעות TicketDto.FromEntity.

היסטוריה, Bulk וסטטיסטיקות מחזירים DTOs ייעודיים. גם רשימת הכרטיסים ממופה ל־TicketDto בתוך שאילתת EF לפני החזרה מה־API.

המיפוי מתבצע ב־TicketService.cs וה־DTOs מוגדרים ב־TicketDtos.cs.

**מניעת טעינת כלל הנתונים לזיכרון**

השאילתה נשארת IQueryable עד לביצוע מול PostgreSQL. הסינון, המיון, Count, הדפדוף באמצעות Skip ו־Take וה־Projection ל־DTO מבוצעים במסד הנתונים.

לזיכרון נטענים רק פריטי העמוד הנוכחית, ו־AsNoTracking מונע מ־EF לבצע Tracking של הישויות שנשלפו.

**אינדקסים ותמיכה בחיפוש ובסינון**

מוגדרים אינדקסים על Status, Priority, AssignedTo ו־CreatedAt. בנוסף קיים אינדקס משולב על (Status, Priority, CreatedAt).

האינדקסים מיועדים לתמוך בסינון השוויון הנפוץ ובשילובים של סטטוס, עדיפות ותאריך. PostgreSQL מחליט אם להשתמש בהם בהתאם לתוכנית השאילתה ולנתונים בפועל.

**ביצועי חיפוש טקסטואלי**

החיפוש משתמש ב־ILIKE '%...%' על השדות Title ו־OrganizationName.

אינדקסי B-tree רגילים אינם מתאימים בדרך כלל לחיפוש שבו קיים wildcard בתחילת הביטוי. לכן, אם החיפוש יהפוך לצוואר בקבוק מבחינת ביצועים, ניתן לשקול שימוש באינדקסי pg_trgm.

**פנייה שאינה קיימת – קוד HTTP מתאים**

בפעולות על פנייה בודדת, אם ה־ID לא קיים, נזרקת TicketNotFoundException וממופה ל־404 Not Found יחד עם תשובת ProblemDetails. המיפוי מתבצע ב־GlobalExceptionMiddleware.cs.

ב־Bulk, פנייה חסרה מסומנת ככישלון של אותו פריט בתוצאה, בעוד ששאר הבקשה ממשיכה.

**סטטוס שאינו חוקי או מעבר סטטוס שאינו מותר**

ערך סטטוס לא מוכר בבקשת PATCH נדחה בשלב ה־Model Binding. בגלל \[ApiController\] מוחזר 400 Bad Request.

אם הסטטוס מוכר אך המעבר אינו מותר, מוחזר 409 Conflict עם ProblemDetails, לדוגמה: Transition from 'New' to 'Completed' is not allowed.

המעברים המותרים מוגדרים ב־TicketStatusTransitions.cs, והחריגה ממופה ל־409 ב־GlobalExceptionMiddleware.cs.

בבקשת Bulk, פריט עם מעבר אסור מסומן ככישלון בתוצאה שלו, בעוד ששאר הפריטים ממשיכים.

**Optimistic Concurrency – עדכון מתחרה**

המימוש משתמש ב־Optimistic Concurrency באמצעות Version. Version מוגדר כ־Concurrency Token ב־AppDbContext.cs, וכל עדכון סטטוס חייב לשלוח את הגרסה שהלקוח קרא קודם.

אם שתי בקשות מנסות לעדכן במקביל עם אותה גרסה, הבקשה הראשונה מצליחה ומגדילה את Version. הבקשה השנייה תיתקל ב־DbUpdateConcurrencyException, שתמופה ל־409 Conflict.

המנגנון נמצא ב־TicketService.cs, והמיפוי ל־HTTP 409 נמצא ב־GlobalExceptionMiddleware.cs.

**UpdatedAt ו־Version – עדכון עקבי**

בעת יצירת פנייה, CreatedAt ו־UpdatedAt נקבעים לאותו זמן UTC, ו־Version מתחיל ב־1.

בעדכון סטטוס מוצלח, UpdatedAt מתעדכן לזמן UTC חדש ו־Version גדל באותו עדכון. זמן הרשומה ביומן הביקורת נקבע לאותו UpdatedAt.

עדכון שלא מצליח בגלל מעבר סטטוס אסור או בגלל Concurrency Conflict אינו נשמר במסד הנתונים.

**מניעת Lost Update**

כדי למנוע Lost Update, כל בקשת שינוי סטטוס שולחת את Version שהלקוח קרא קודם.

העדכון במסד מותנה בכך שהגרסה עדיין זהה. אם בקשה אחרת כבר עדכנה את הפנייה, העדכון השני לא דורס את השינוי ומוחזר 409 Conflict.

הלוגיקה נמצאת ב־TicketService.cs, ו־Version מוגדר כ־Concurrency Token ב־AppDbContext.cs.

לאחר קבלת 409, הלקוח צריך לקרוא מחדש את הפנייה ולקבל את ה־Version העדכני לפני ניסיון נוסף.

**היסטוריית שינויים**

היסטוריית השינויים מתעדת שינויי סטטוס של פנייה, ולא כל שינוי בשדות שלה.

בעת עדכון סטטוס מוצלח, השירות יוצר רשומת Audit הכוללת את הסטטוס הקודם, הסטטוס החדש וזמן העדכון. הרשומה נשמרת יחד עם עדכון הפנייה.

אם העדכון נכשל בגלל מעבר סטטוס אסור או בגלל קונפליקט Version, הוא לא אמור להופיע בהיסטוריה.

השדה ChangedBy קיים, אך כרגע אינו מוגדר ולכן יישאר null.

```
GET /api/tickets/{id}/history
```

התוצאות ממוינות מהחדשה לישנה. אם מזהה הפנייה אינו קיים, מוחזר 404. אם הפנייה קיימת אך אין לה שינויי סטטוס, מוחזרת רשימה ריקה.

הכתיבה והשליפה ממומשות ב־TicketService.cs, וה־Endpoint מוגדר ב־TicketsController.cs.

**פעולת Bulk**

```
POST /api/tickets/bulk-status
```

ה־Endpoint מקבל עד 100 פריטים. אם נשלחים יותר מ־100 פריטים, מוחזר 400 Bad Request והבקשה אינה מתחילה לעבד את הפריטים.

פנייה חסרה או Version מיושן מסומנים ככישלון של הפריט הרלוונטי. שאר הפריטים ממשיכים להיות מעובדים. תגובת ה־Bulk היא 200 ומכילה תוצאה עבור כל פריט.

העדכונים מתבצעים בזה אחר זה ולא כפעולה אטומית אחת. לכן הצלחות נשמרות גם כאשר פריטים אחרים נכשלים.

הבדיקה נמצאת ב־TicketsController.cs, הטיפול בכשלים נמצא ב־TicketService.cs, והתיעוד נמצא ב־README.md.