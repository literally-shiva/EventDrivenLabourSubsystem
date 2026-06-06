import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProjectModel, ProjectSaveRequest, ProjectTimeline, MetricHistory } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly coreBaseUrl = 'http://localhost:5000/api';
  private readonly twinBaseUrl = 'http://localhost:5001';

  constructor(private readonly http: HttpClient) {}

  getTimeline(projectId: string): Observable<ProjectTimeline> {
    return this.http.get<ProjectTimeline>(`${this.coreBaseUrl}/projects/${projectId}/timeline`);
  }

  getProjects(): Observable<ProjectModel[]> {
    return this.http.get<ProjectModel[]>(`${this.twinBaseUrl}/projects`);
  }

  getProject(projectId: string): Observable<ProjectModel> {
    return this.http.get<ProjectModel>(`${this.twinBaseUrl}/projects/${projectId}`);
  }

  createProject(payload: ProjectSaveRequest): Observable<ProjectModel> {
    return this.http.post<ProjectModel>(`${this.twinBaseUrl}/projects`, payload);
  }

  updateProject(projectId: string, payload: ProjectSaveRequest): Observable<ProjectModel> {
    return this.http.put<ProjectModel>(`${this.twinBaseUrl}/projects/${projectId}`, payload);
  }

  deleteProject(projectId: string): Observable<void> {
    return this.http.delete<void>(`${this.twinBaseUrl}/projects/${projectId}`);
  }

  startSimulation(projectId: string): Observable<void> {
    return this.http.post<void>(`${this.twinBaseUrl}/simulation/start/${projectId}`, {});
  }

  stopSimulation(projectId: string): Observable<void> {
    return this.http.post<void>(`${this.twinBaseUrl}/simulation/stop/${projectId}`, {});
  }

  registerUnknownEvent(payload: { workId: string; projectId: string; name: string; vector: number[] }): Observable<void> {
    return this.http.post<void>(`${this.coreBaseUrl}/events/register-unknown`, payload);
  }

  getMetrics(projectId: string): Observable<MetricHistory[]> {
    return this.http.get<MetricHistory[]>(`${this.coreBaseUrl}/projects/${projectId}/metrics`);
  }
}
