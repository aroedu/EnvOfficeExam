**סעיף 5 ממומש בעיקר בשלושה מקומות**

**חיפוש, סינון, מיון ודפדוף בצד השרת**: הפקדים והטבלה נמצאים ב־ticket-workspace.component.html. הרכיב מרכיב את ה־query ב־buildQuery(), ומגיב לשינויי סינון, מיון ודף ב־ticket-workspace.component.ts. הבקשות נשלחות כפרמטרים לשרת ב־getTickets() שב־ticket-api.service.ts; הדפדוף והמיון עצמם אינם נעשים על כל הנתונים בדפדפן.

**Debounce** וביטול חיפוש קודם: זרם search.valueChanges באותו רכיב ממתין 350ms ומסנן ערכים זהים. הוא מבטל מיד את בקשת הרשימה הפעילה בעת הקלדה, ואז switchMap שולח את הבקשה החדשה.

**מצבי Loading, Empty ו־Error: signals** כמו loading, listError ו־statsLoading מוגדרים ברכיב; ה־template מציג spinner, הודעת רשימה ריקה או הודעת שגיאה בהתאם.

**עדכון סטטוס וטיפול ב־409**: updateStatus() ברכיב קורא ל־updateStatus() בשירות ושולח את version הנוכחי. שגיאת 409 מציגה הודעת conflict ומרעננת את הרשימה.

**Aggregations**: הרכיב טוען נתונים דרך getStatistics() שבשירות ומציג סך פניות, ספירות לפי סטטוס ועדיפות גבוהה ב־template.

**היסטוריית שינויים**: בחירת סמל השעון מפעילה את openHistory() ברכיב; historySelection\$ טוען את ההיסטוריה דרך getHistory() בשירות, וה־template מציג אותה ב־drawer, כולל מצבי טעינה, שגיאה ורשימה ריקה.

**הפרדת אחריות**: TicketWorkspaceComponent מנהל תצוגה ו־state, TicketApiService מרכז קריאות HTTP, ו־ticket.models.ts מכיל את טיפוסי הנתונים. HttpClient מופעל ב־app.config.ts.