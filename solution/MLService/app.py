from __future__ import annotations

import os
import pathlib
from dataclasses import dataclass
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
# Minimum confidence for a classification to be considered "known".
# 0.6 is a reasonable threshold for 6-class SVM with Platt scaling.
CONFIDENCE_THRESHOLD = float(os.getenv("CONFIDENCE_THRESHOLD", "0.6"))


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
class ModelRegistry:
    classifier: object | None = None
    event_types: list[str] | None = None
    training_vectors: list[list[float]] | None = None
    training_labels: list[str] | None = None


app = FastAPI(title="MLService", version="1.0.0")
registry = ModelRegistry(classifier=None, event_types=[], training_vectors=[], training_labels=[])

# Restore previously trained model from disk so state survives restarts.
if MODEL_PATH.exists():
    registry.classifier = joblib.load(MODEL_PATH)


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

    matrix = to_matrix(request.metrics)
    if len(matrix) < 2:
        return ClusterResponse(normalClusterId=0, eventClusters=[], events=[])

    model = DBSCAN(eps=1.2, min_samples=2)
    normalized = StandardScaler().fit_transform(matrix)
    labels = model.fit_predict(normalized)

    unique, counts = np.unique(labels, return_counts=True)
    cluster_sizes = {int(label): int(count) for label, count in zip(unique, counts)}
    filtered = {label: size for label, size in cluster_sizes.items() if label != -1}
    normal_cluster = max(filtered, key=filtered.get) if filtered else 0
    event_clusters = [label for label in cluster_sizes.keys() if label != normal_cluster]

    events: list[EventCandidateDto] = []
    for index, label in enumerate(labels):
        if int(label) == normal_cluster:
            continue
        events.append(
            EventCandidateDto(
                workId=request.metrics[index].workId,
                clusterId=int(label),
                vector=matrix[index].astype(float).tolist(),
            )
        )

    return ClusterResponse(
        normalClusterId=int(normal_cluster),
        eventClusters=[int(cluster) for cluster in event_clusters],
        events=events,
    )


@app.post("/classify", response_model=ClassifyResponse)
def classify(request: ClassifyRequest) -> ClassifyResponse:
    if registry.classifier is None or not registry.training_labels:
        return ClassifyResponse(isKnown=False, eventType="UnknownEvent", confidence=0.0)

    vector = np.array([request.vector], dtype=float)
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
