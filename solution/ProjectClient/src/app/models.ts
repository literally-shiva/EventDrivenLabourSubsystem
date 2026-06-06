export interface WorkModel {
  id: string;
  projectId?: string;
  name: string;
  startDate: string;
  endDate: string;
  plannedDuration: number;
  currentDuration: number;
  percentComplete: number;
  currentState?: string;
  isCompleted?: boolean;
}

export interface WorkDependency {
  sourceWorkId: string;
  targetWorkId: string;
}

export interface ProjectModel {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  currentSimulationTime: string;
  isSimulationRunning: boolean;
  works: WorkModel[];
  dependencies: WorkDependency[];
}

export interface ProjectSaveRequest {
  name: string;
  startDate: string;
  endDate: string;
  works: Array<{
    id?: string | null;
    name: string;
    startDate: string;
    endDate: string;
    plannedDuration: number;
    currentDuration: number;
    percentComplete: number;
  }>;
  dependencies: WorkDependency[];
}

export interface ProjectTimeline {
  projectId: string;
  works: WorkModel[];
  events: DetectedEvent[];
}

export interface DetectedEvent {
  id: string;
  projectId: string;
  workId: string;
  name: string;
  eventType: string;
  isKnown: boolean;
  confidence: number;
  timestamp: string;
  featureVector?: number[];
}

export interface MetricHistory {
  id: string;
  projectId: string;
  workId: string;
  workName: string;
  timestamp: string;
  workersCount: number;
  modelDataVolume: number;
  changesCount: number;
  collisionCount: number;
  approvalCount: number;
  approvalDelayDays: number;
  documentationVersionCount: number;
  reworkCount: number;
  progressPercent: number;
  simulatedEventType: string;
}
