from __future__ import annotations

import os
import pathlib
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime
from typing import List

import joblib
import numpy as np
from fastapi import FastAPI
from pydantic import BaseModel
from sklearn.cluster import DBSCAN
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.svm import SVC

MODEL_PATH = pathlib.Path("./model.joblib")
SCALER_PATH = pathlib.Path("./scaler.joblib")
# Minimum confidence for a classification to be considered "known".
# 0.6 is a reasonable threshold for 6-class SVM with Platt scaling.
CONFIDENCE_THRESHOLD = float(os.getenv("CONFIDENCE_THRESHOLD", "0.6"))
# Sliding window size for DBSCAN clustering
WINDOW_SIZE = int(os.getenv("WINDOW_SIZE", "200"))


class MetricPointDto(BaseModel):
    workId: str
    workersCount: int
    modelDataVolume: float
    changesCount: int
    collisionCount: int
    approvalDelayDays: int
    reworkCount: int


class ClusterRequest(BaseModel):
    metrics: List[MetricPointDto]
    projectId: str = "default"  # Added to support per-project windows


class EventCandidateDto(BaseModel):
    workId: str
    clusterId: int
    vector: List[float]


class ClusterResponse(BaseModel):
    normalClusterId: int
    eventClusters: List[int]
    events: List[EventCandidateDto]


class ClassifyRequest(BaseModel):
    vector: List[float]


class ClassifyResponse(BaseModel):
    isKnown: bool
    eventType: str
    confidence: float


class TrainingEventDto(BaseModel):
    eventType: str
    vector: List[float]


class TrainRequest(BaseModel):
    events: List[TrainingEventDto]


class RegisterEventRequest(BaseModel):
    eventType: str
    vector: List[float]


@dataclass
class HistoryWindow:
    """Sliding window for accumulating metric history."""
    vectors: deque = field(default_factory=lambda: deque(maxlen=WINDOW_SIZE))
    work_ids: deque = field(default_factory=lambda: deque(maxlen=WINDOW_SIZE))
    timestamps: deque = field(default_factory=lambda: deque(maxlen=WINDOW_SIZE))

    def add_batch(self, metrics: List[MetricPointDto], timestamp: datetime):
        """Add a batch of metrics to the sliding window."""
        for metric in metrics:
            vector = [
                metric.workersCount,
                metric.modelDataVolume,
                metric.changesCount,
                metric.collisionCount,
                metric.approvalDelayDays,
                metric.reworkCount,
            ]
            self.vectors.append(vector)
            self.work_ids.append(metric.workId)
            self.timestamps.append(timestamp)

    def get_all(self):
        """Return all accumulated data."""
        return (
            np.array(list(self.vectors), dtype=float),
            list(self.work_ids),
            list(self.timestamps)
        )

    def size(self):
        """Return current window size."""
        return len(self.vectors)


@dataclass
class ModelRegistry:
    classifier: object | None = None
    scaler: StandardScaler | None = None  # Shared scaler for consistency
    event_types: list[str] | None = None
    training_vectors: list[list[float]] | None = None
    training_labels: list[str] | None = None


app = FastAPI(title="MLService", version="1.0.0")
registry = ModelRegistry(
    classifier=None,
    scaler=None,
    event_types=[],
    training_vectors=[],
    training_labels=[]
)

# Per-project sliding windows for DBSCAN
windows: dict[str, HistoryWindow] = {}

# Restore previously trained model and scaler from disk so state survives restarts.
if MODEL_PATH.exists():
    registry.classifier = joblib.load(MODEL_PATH)
if SCALER_PATH.exists():
    registry.scaler = joblib.load(SCALER_PATH)


def to_matrix(metrics: List[MetricPointDto]) -> np.ndarray:
    rows = [
        [
            point.workersCount,
            point.modelDataVolume,
            point.changesCount,
            point.collisionCount,
            point.approvalDelayDays,
            point.reworkCount,
        ]
        for point in metrics
    ]
    return np.array(rows, dtype=float)


@app.post("/cluster", response_model=ClusterResponse)
def cluster(request: ClusterRequest) -> ClusterResponse:
    if not request.metrics:
        return ClusterResponse(normalClusterId=0, eventClusters=[], events=[])

    project_id = request.projectId

    # Get or create sliding window for this project
    if project_id not in windows:
        windows[project_id] = HistoryWindow()

    window = windows[project_id]

    # Add current batch to the sliding window
    current_timestamp = datetime.utcnow()
    window.add_batch(request.metrics, current_timestamp)

    # Need at least 10 points for meaningful clustering
    if window.size() < 10:
        return ClusterResponse(normalClusterId=0, eventClusters=[], events=[])

    # Get all accumulated history from the window
    all_vectors, all_work_ids, all_timestamps = window.get_all()

    # Initialize or reuse the shared scaler
    if registry.scaler is None:
        registry.scaler = StandardScaler()
        registry.scaler.fit(all_vectors)
        joblib.dump(registry.scaler, SCALER_PATH)
    else:
        # Incremental update of scaler statistics (optional - can also refit periodically)
        # For now, we refit on the entire window to maintain accuracy
        if window.size() % 50 == 0:  # Refit every 50 points
            registry.scaler.fit(all_vectors)
            joblib.dump(registry.scaler, SCALER_PATH)

    # Apply DBSCAN to the ENTIRE window (not just current batch)
    normalized = registry.scaler.transform(all_vectors)
    model = DBSCAN(eps=1.2, min_samples=2)
    labels = model.fit_predict(normalized)

    # Find the normal cluster (largest non-noise cluster)
    unique_labels = labels[labels != -1]
    if len(unique_labels) == 0:
        # All points are noise - return no events
        return ClusterResponse(normalClusterId=-1, eventClusters=[], events=[])

    unique, counts = np.unique(unique_labels, return_counts=True)
    normal_cluster = int(unique[np.argmax(counts)])

    # Identify event clusters (all non-normal clusters)
    all_clusters = set(int(label) for label in labels)
    event_clusters = [c for c in all_clusters if c != normal_cluster and c != -1]

    # Detect events ONLY in the current batch (last N points)
    current_batch_size = len(request.metrics)
    current_batch_labels = labels[-current_batch_size:]
    current_batch_work_ids = all_work_ids[-current_batch_size:]
    current_batch_vectors = all_vectors[-current_batch_size:]

    events: list[EventCandidateDto] = []
    for i, label in enumerate(current_batch_labels):
        if int(label) != normal_cluster:  # Outlier or small cluster = event
            events.append(
                EventCandidateDto(
                    workId=current_batch_work_ids[i],
                    clusterId=int(label),
                    vector=current_batch_vectors[i].tolist(),
                )
            )

    return ClusterResponse(
        normalClusterId=normal_cluster,
        eventClusters=event_clusters,
        events=events,
    )


@app.post("/classify", response_model=ClassifyResponse)
def classify(request: ClassifyRequest) -> ClassifyResponse:
    if registry.classifier is None or not registry.training_labels:
        return ClassifyResponse(isKnown=False, eventType="UnknownEvent", confidence=0.0)

    vector = np.array([request.vector], dtype=float)

    # The classifier pipeline includes StandardScaler, so pass raw (unnormalized) vector
    prediction = registry.classifier.predict(vector)[0]
    probabilities = registry.classifier.predict_proba(vector)[0]
    confidence = float(np.max(probabilities))
    is_known = confidence >= CONFIDENCE_THRESHOLD

    return ClassifyResponse(
        isKnown=is_known,
        eventType=str(prediction) if is_known else "UnknownEvent",
        confidence=confidence,
    )


@app.post("/train")
def train(request: TrainRequest) -> dict:
    if len(request.events) < 2 or len({item.eventType for item in request.events}) < 2:
        return {"status": "skipped", "reason": "Need at least two classes for SVM training"}

    x = np.array([item.vector for item in request.events], dtype=float)
    y = np.array([item.eventType for item in request.events])

    # Train SVM with built-in pipeline (StandardScaler + SVC)
    # This ensures training data and classification use the same normalization
    classifier = make_pipeline(StandardScaler(), SVC(kernel="rbf", probability=True))
    classifier.fit(x, y)

    registry.classifier = classifier
    registry.training_vectors = [item.vector for item in request.events]
    registry.training_labels = [item.eventType for item in request.events]
    registry.event_types = sorted(set(registry.training_labels))

    # Persist trained model to disk so it survives process restarts.
    joblib.dump(registry.classifier, MODEL_PATH)
    return {"status": "trained", "samples": len(request.events), "classes": registry.event_types}


@app.post("/register-event")
def register_event(request: RegisterEventRequest) -> dict:
    registry.training_vectors.append(request.vector)
    registry.training_labels.append(request.eventType)
    return {"status": "registered", "eventType": request.eventType}


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "trained": registry.classifier is not None}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="0.0.0.0", port=8000, reload=True)
