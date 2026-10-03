# Interview Questions and Answers – Ticket Management

Companion to [Interview-Guide.md](Interview-Guide.md). Answers are written to be said out loud (30–60 seconds each) and match the code in this repo.

## A. General and design

**1. Walk me through the solution.**
A .NET 10 Web API with controllers, a `TicketService` and EF Core on PostgreSQL (Supabase), plus an Angular 20 client. The API supports server-side search, filter, sort and paging, status updates with optimistic concurrency, an audit trail, a bulk update, and a cached statistics endpoint. Four integration tests run against the real HTTP pipeline.

**2. Why this structure and not something heavier (Clean Architecture, CQRS)?**
The domain is small: one aggregate and one audit table. Controller → service → DbContext keeps the code readable and testable. Adding repositories or MediatR would be over-engineering, which the spec explicitly penalizes. If the domain grew, I'd extract query and command handlers first.

**3. Why PostgreSQL and not SQL Server or MongoDB?**
Tickets and audit rows are relational, and I need a real transaction across both. I also need `GROUP BY` aggregations and composite indexes. Supabase gives hosted PostgreSQL, and EF Core with Npgsql works with it as with any Postgres server. MongoDB would make the audit transaction and aggregations more work for no benefit here.

**4. Can you use the Supabase API key instead of a connection string?**
No, not with EF Core. The API key authenticates against Supabase's REST layer. EF Core talks to Postgres directly over its wire protocol with a database user and password. Switching to the REST API would mean losing LINQ, migrations and the `WHERE Version = x` update check.

**5. Why DTOs instead of returning entities?**
It decouples the API contract from the schema. The client can't set `Version` or `UpdatedAt`, because the server controls them. It also avoids exposing navigation properties and over-posting.

**6. How is the code organized for separation of concerns?**
The controller only maps HTTP to service calls. The service holds the business rules (transitions, concurrency, audit, cache). `AppDbContext` holds the mapping and indexes. `GlobalExceptionMiddleware` maps domain exceptions to HTTP codes, so controllers have no try/catch.

## B. API and retrieval (spec 1)

**7. How do you avoid loading all records into memory?**
The filters, ordering, `Skip` and `Take` are all composed on an `IQueryable`, so EF translates them to one SQL statement. Only the requested page is materialized. A separate `COUNT` query gives `totalCount`.

**8. Why do you sort by `Id` after the chosen field?**
A tie-break makes the order deterministic. Without it, rows with equal values can appear on two pages or on none while paging.

**9. What are the drawbacks of offset paging?**
`OFFSET n` makes the database walk past n rows, so deep pages get slower. Keyset (seek) pagination fixes that, but it can't jump to an arbitrary page. For this UI, with numbered pages and filters that cut the data down, offset is acceptable.

**10. How does the search work, and what is its weakness?**
`ILIKE '%term%'` on `Title` and `OrganizationName`. A leading wildcard can't use a B-tree index, so it scans. If measurements show it's a bottleneck, I'd add `pg_trgm` and a GIN index, and verify with `EXPLAIN` before adding it to a migration.

**11. How do you validate paging and sorting parameters?**
`Page` has a `[Range(1, int.MaxValue)]` check, and an invalid value returns 400 (covered by a test). `PageSize` is clamped to 1–100. Sort field is an enum, so an unknown value fails model binding. I'd admit that clamping silently is a debatable choice, and rejecting out-of-range sizes would be stricter.

**12. What aggregations do you provide?**
Counts by status and by priority from `GET /api/tickets/statistics`, each a `GROUP BY` in the database. Missing groups are filled with zero so the UI always gets all values.

**13. Why `AsNoTracking` on reads?**
Reads don't need change tracking, so skipping it saves memory and CPU.

**14. How is `CancellationToken` used?**
It flows from the controller action through the service into every EF async call. If the client disconnects, the query is cancelled. The middleware swallows `OperationCanceledException` when the request was aborted, so it isn't logged as a 500.

## C. Status, concurrency and audit (spec 2–3)

**15. What are the allowed status transitions?**
New → InProgress; InProgress → Waiting or Completed; Waiting → InProgress or Completed; Completed is terminal. Same-status "transitions" are rejected too. It's one dictionary in `TicketStatusTransitions`, so the rule lives in a single place.

**16. Explain optimistic concurrency in your solution.**
`Version` is configured as a concurrency token. The client sends the version it last saw. I set it as the original value, increment it, and EF generates `UPDATE ... WHERE Id = @id AND Version = @orig`. If someone else updated first, zero rows match, EF throws `DbUpdateConcurrencyException`, and I return 409.

**17. Why is that safe if two requests arrive at the same instant?**
The check and the write are one atomic SQL statement, and the database serializes writes to the row. One statement matches and the other doesn't, so there's no lost update. Checking `Version` in C# before saving would not be safe.

**18. Why not handle this in the UI?**
The UI only knows what it loaded earlier. Only the database can decide atomically who wrote first. The UI's job is to show the conflict nicely.

**19. Why an `int Version` and not PostgreSQL `xmin` or a `rowversion`?**
`Version` is portable, visible in the DTO, and works in SQLite for tests. `xmin` needs no app code, but it's PostgreSQL-specific and awkward to expose. Either meets the spec.

**20. Should an invalid transition return 400 or 409?**
My code returns 409, meaning the request conflicts with the ticket's current state. Some teams prefer 400 or 422 for rule violations, reserving 409 for version conflicts. If a reviewer prefers that, it's a one-line change in the middleware, and I'd mention the trade-off: the UI would need to tell the two cases apart.

**21. How do you guarantee the audit row exists for every change?**
I add the audit entity and modify the ticket in the same `SaveChangesAsync`. EF wraps that in a single transaction, so both commit or both roll back.

**22. What does the audit record, and what's missing?**
`OldStatus`, `NewStatus`, `ChangedAt` and `ChangedBy`. The spec also lists `RequestId`, which I haven't added, and `ChangedBy` is null because the API has no authentication. I'd add a correlation id from the request and the user from the auth claims.

## D. Bulk update (spec 4)

**23. Why partial success and not all-or-nothing?**
The items are independent tickets. One missing ticket or stale version shouldn't block the other 99. I return a per-item result with `Success` and an error message, so the client knows exactly what to retry.

**24. When would you choose all-or-nothing?**
When the items form one business unit that must stay consistent, for example closing a group of linked tickets together. Then I'd wrap everything in one transaction and fail the whole request on the first problem.

**25. What limits does the bulk endpoint have?**
At most 100 items. More than that returns 400 before anything is processed. That keeps request time and lock duration bounded.

**26. Any issue with how bulk is implemented?**
Yes, two. It runs items one by one, so it's several round-trips per item. And all items share one `DbContext`, so after a concurrency failure the failed entity can stay in the change tracker and affect later saves. The fix is `ChangeTracker.Clear()` in the catch block, or a new scope per item, with a regression test. If volume grew, I'd also batch-load tickets with one `WHERE Id IN (...)`.

## E. Performance (spec 6)

**27. How did you measure performance?**
I loaded 100,000 tickets and 100,000 audit rows with a repeatable SQL script. For the list and history endpoints I ran `EXPLAIN (ANALYZE, BUFFERS)`, then measured HTTP with warm-up and 30 samples, reporting median and p95.

**28. What were the results?**
SQL time was tiny: about 3 ms for the count and 0.1 ms for the page, using index scans. HTTP medians were about 467 ms, but that's mostly the network trip from my machine to Supabase plus two sequential queries, not database work.

**29. Which indexes did you add and why?**
Single-column indexes on `Status`, `Priority`, `AssignedTo` and `CreatedAt`, a composite `(Status, Priority, CreatedAt)` for the main filter-plus-sort scenario, and `(TicketId, ChangedAt)` on the audit table for history. I deliberately didn't add more without evidence from plans, since each index slows writes.

**30. What's the bottleneck you'd attack first?**
Substring search, then the `COUNT` that runs for every list request, then deep offsets. The fixes are `pg_trgm` with GIN, a cached or approximate count, and keyset pagination.

## F. Cache (spec 7)

**31. What do you cache and why?**
The statistics. They're two aggregation queries, read on every dashboard load, and they change only on create or status change. Individual tickets aren't cached because they change often and are cheap to read by primary key.

**32. How do you avoid stale data?**
The cache key is removed after a successful create or status update, including bulk updates since they use the same method. There's also a 1-minute absolute expiration as a safety net for changes made outside the API.

**33. What breaks with several server instances?**
`IMemoryCache` is per process, so an invalidation on instance A doesn't clear instance B. Until it's moved, B can serve data up to a minute old. The fix is `IDistributedCache` backed by Redis under the same key, deleting the shared key after writes. A local L1 cache would also need Pub/Sub invalidation.

**34. Can two simultaneous requests both miss the cache and both compute?**
Yes, that's a cache stampede, and here the cost is two small queries, so I accepted it. If it mattered, I'd add a lock or use `GetOrCreateAsync` with single-flight behavior.

## G. Angular and RxJS (spec 5)

**35. How does search avoid sending a request per keystroke?**
The search control's `valueChanges` goes through `distinctUntilChanged` and `debounceTime(350)`. Only after the user pauses does it trigger a refresh.

**36. How do you handle out-of-order responses?**
The list stream uses `switchMap`, which unsubscribes from the previous HTTP call when a new query starts, and Angular's `HttpClient` cancels the request. I also cancel in-flight requests as soon as the user types again.

**37. Why `switchMap` here and not `mergeMap` or `exhaustMap`?**
`switchMap` is for "only the latest query matters". `mergeMap` would let old responses overwrite new ones. `exhaustMap` would ignore new queries while one is running, which would show stale results.

**38. How do you avoid memory leaks?**
`takeUntilDestroyed` on all long-lived subscriptions, so they end with the component. Short-lived HTTP calls complete on their own.

**39. How does the UI handle a 409?**
It shows a message that the update conflicted, reloads the list, and the user retries with the fresh `version`. Only transitions allowed by the state machine are offered in the UI, but the server remains the source of truth.

**40. What would you improve in the Angular code?**
Split the single workspace component into smaller presentational components (filters, table, history panel, statistics) driven by a container, as the spec asks for separation of responsibilities. I'd also add a bulk-update screen, and distinguish "stale version" from "invalid transition" in the error message.

**41. How does the UI talk to the API in development?**
`proxy.conf.json` forwards `/api` to `http://localhost:5123`, so the browser sees one origin and I don't need CORS. In production I'd configure CORS or serve both from the same host.

## H. Testing (spec 8)

**42. What tests do you have?**
Four integration tests with `WebApplicationFactory` and in-memory SQLite: filter-before-paging, invalid page returns 400, update writes an audit row and invalidates the cache, and a stale version returns 409.

**43. Why integration tests rather than unit tests?**
The risky behavior is in the interaction of HTTP, middleware, EF and the database, such as status mapping and the concurrency check. A mocked repository would not prove any of that.

**44. What's the limitation of using SQLite?**
It doesn't validate PostgreSQL-specific behavior, such as `ILIKE` and query plans. I'd add Testcontainers with PostgreSQL for that, and a parallel-requests test to prove only one concurrent update wins.

## I. Process, quality and AI

**45. How did you split the work?**
Backend foundation first (model, migration, indexes), then retrieval, then status and concurrency, then audit and bulk. Angular, caching, tests and performance work were done in parallel streams after that, and documentation was last. The critical path was model → status update → audit → UI update.

**46. What would you change with more time?**
Add the missing filters (OrganizationName, date range), `RequestId` in audit, authentication for `ChangedBy`, structured logging of key operations, PostgreSQL integration tests, and Redis for multi-instance cache.

**47. How did you use AI, and how did you verify it?**
I used it for scaffolding, the documentation sections, the cache and the test project. For the core logic I read and ran everything: I checked the concurrency path with a stale-version test, checked query plans with `EXPLAIN`, and corrected build and merge issues by hand. I'm responsible for all submitted code.

**48. How do you handle secrets?**
The connection string lives in user-secrets locally and in an environment variable (`ConnectionStrings__Supabase`) when hosted. `appsettings.json` only holds a placeholder, and nothing sensitive is committed.

**49. What happens if the database is down?**
The unhandled exception goes to the global middleware, which logs it and returns a 500 `ProblemDetails` with a generic message, so no internal details leak. For production I'd add health checks and retry-on-failure for transient errors.

**50. Tell me about a bug or trade-off you hit.**
Good examples: build artifacts (`bin/obj`) were committed before `.gitignore` existed, so I untracked them; and a LINQ `ThenBy` ambiguity on `IQueryable` that I fixed by using `IOrderedQueryable`. For design trade-offs, discuss 409 vs 400 and offset vs keyset paging.

## Questions to ask them

- How do you handle concurrency and auditing in your current systems?
- What's your deployment model, single instance or scaled out? (Ties to the cache answer.)
- How do you balance integration and unit tests?
