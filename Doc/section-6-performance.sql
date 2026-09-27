-- Run only against a dedicated, empty benchmark database/schema after applying EF migrations.
-- Set the connection search_path to that database/schema; the default public schema also works.
-- This script refuses to run if either application table already contains data.
BEGIN;

DO $$
BEGIN
     IF EXISTS (SELECT 1 FROM tickets)
         OR EXISTS (SELECT 1 FROM ticket_audit_logs) THEN
        RAISE EXCEPTION 'Benchmark database must have empty tickets and ticket_audit_logs tables';
    END IF;
END
$$;

INSERT INTO tickets
    ("Id", "Title", "OrganizationName", "Status", "Priority",
     "AssignedTo", "CreatedAt", "UpdatedAt", "Version")
SELECT
    md5(i::text)::uuid,
    'Ticket ' || i,
    'Organization ' || (i % 500),
    (ARRAY['New', 'InProgress', 'Waiting', 'Completed'])[(i % 4) + 1],
    (ARRAY['High', 'Medium', 'Low'])[(i % 3) + 1],
    'agent-' || (i % 50),
    now() - i * interval '1 minute',
    now() - i * interval '30 seconds',
    1
FROM generate_series(1, 100000) AS source(i);

INSERT INTO ticket_audit_logs
    ("TicketId", "OldStatus", "NewStatus", "ChangedBy", "ChangedAt")
SELECT
    "Id",
    'New',
    "Status",
    'section-6-benchmark',
    "UpdatedAt"
 FROM tickets;

ANALYZE tickets;
ANALYZE ticket_audit_logs;

COMMIT;

SELECT count(*) AS ticket_count FROM tickets;
SELECT count(*) AS audit_log_count FROM ticket_audit_logs;

-- GET /api/tickets: count query for status + priority filters.
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)
FROM tickets
WHERE "Status" = 'InProgress'
  AND "Priority" = 'High';

-- GET /api/tickets: one 20-row page, ordered by CreatedAt DESC then Id ASC.
EXPLAIN (ANALYZE, BUFFERS)
SELECT "Id", "Title", "OrganizationName", "Status", "Priority",
       "AssignedTo", "CreatedAt", "UpdatedAt", "Version"
FROM tickets
WHERE "Status" = 'InProgress'
  AND "Priority" = 'High'
ORDER BY "CreatedAt" DESC, "Id" ASC
LIMIT 20 OFFSET 0;

-- GET /api/tickets/{id}/history: existence check followed by history retrieval.
EXPLAIN (ANALYZE, BUFFERS)
SELECT 1
FROM tickets
WHERE "Id" = 'c4ca4238-a0b9-2382-0dcc-509a6f75849b'
LIMIT 1;

EXPLAIN (ANALYZE, BUFFERS)
SELECT "OldStatus", "NewStatus", "ChangedBy", "ChangedAt"
FROM ticket_audit_logs
WHERE "TicketId" = 'c4ca4238-a0b9-2382-0dcc-509a6f75849b'
ORDER BY "ChangedAt" DESC;