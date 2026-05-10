CREATE TABLE IF NOT EXISTS "Projects" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "CurrentSimulationTime" timestamp with time zone NOT NULL,
    "IsSimulationRunning" boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS "Works" (
    "Id" uuid PRIMARY KEY,
    "ProjectId" uuid NOT NULL REFERENCES "Projects"("Id"),
    "Name" varchar(200) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "PlannedDuration" integer NOT NULL,
    "CurrentDuration" integer NOT NULL,
    "PercentComplete" double precision NOT NULL,
    "IsCompleted" boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS "WorkDependencies" (
    "ParentWorkId" uuid NOT NULL,
    "ChildWorkId" uuid NOT NULL,
    PRIMARY KEY ("ParentWorkId", "ChildWorkId")
);

CREATE TABLE IF NOT EXISTS "WorkMetricSnapshots" (
    "Id" uuid PRIMARY KEY,
    "WorkId" uuid NOT NULL REFERENCES "Works"("Id"),
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
    "SimulatedEventType" integer NOT NULL
);
