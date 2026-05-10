CREATE TABLE IF NOT EXISTS "Projects" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "Works" (
    "Id" uuid PRIMARY KEY,
    "ProjectId" uuid NOT NULL REFERENCES "Projects"("Id"),
    "Name" varchar(200) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "PlannedDuration" integer NOT NULL,
    "CurrentDuration" double precision NOT NULL,
    "PercentComplete" double precision NOT NULL,
    "CurrentState" integer NOT NULL
);

CREATE TABLE IF NOT EXISTS "MetricsHistory" (
    "Id" uuid PRIMARY KEY,
    "ProjectId" uuid NOT NULL,
    "WorkId" uuid NOT NULL,
    "WorkName" varchar(200) NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL,
    "WorkersCount" integer NOT NULL,
    "ModelDataVolume" double precision NOT NULL,
    "ChangesCount" integer NOT NULL,
    "CollisionCount" integer NOT NULL,
    "ApprovalCount" integer NOT NULL,
    "ApprovalDelayDays" integer NOT NULL,
    "DocumentationVersionCount" integer NOT NULL,
    "ReworkCount" integer NOT NULL,
    "ProgressPercent" double precision NOT NULL,
    "SimulatedEventType" varchar(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS "DetectedEvents" (
    "Id" uuid PRIMARY KEY,
    "ProjectId" uuid NOT NULL,
    "WorkId" uuid NOT NULL,
    "Name" varchar(200) NOT NULL,
    "EventType" integer NOT NULL,
    "IsKnown" boolean NOT NULL,
    "Confidence" double precision NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "EventPatterns" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "Vector" text NOT NULL,
    "EventType" integer NOT NULL,
    "AverageDelayImpact" double precision NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "WorkMarkovStates" (
    "WorkId" uuid PRIMARY KEY,
    "CurrentState" integer NOT NULL,
    "LastUpdated" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS "DurationHistory" (
    "Id" uuid PRIMARY KEY,
    "WorkId" uuid NOT NULL,
    "PreviousDuration" double precision NOT NULL,
    "NewDuration" double precision NOT NULL,
    "EventId" uuid NOT NULL,
    "Timestamp" timestamp with time zone NOT NULL
);
