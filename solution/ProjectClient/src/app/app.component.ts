import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { gantt } from 'dhtmlx-gantt';
import { ApiService } from './api.service';
import { DetectedEvent, ProjectModel, ProjectSaveRequest, ProjectTimeline, WorkDependency, WorkModel } from './models';
import { SignalrService } from './signalr.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  @ViewChild('ganttHost', { static: true }) ganttHost!: ElementRef<HTMLDivElement>;

  private readonly api = inject(ApiService);
  private readonly signalr = inject(SignalrService);

  projects: ProjectModel[] = [];
  activeProject?: ProjectModel;
  timeline?: ProjectTimeline;
  works: WorkModel[] = [];
  dependencies: WorkDependency[] = [];
  selectedWorkId?: string;
  projectDialogOpen = true;
  unknownEvent?: DetectedEvent;
  unknownEventName = '';
  statusMessage = '';
  isSaving = false;

  private suppressGanttEvents = false;

  async ngOnInit(): Promise<void> {
    this.configureGantt();
    gantt.init(this.ganttHost.nativeElement);
    this.attachGanttEvents();

    await this.signalr.start();
    this.signalr.workUpdated$.subscribe(work => this.applyWorkUpdate(work));
    this.signalr.eventDetected$.subscribe(eventItem => {
      if (this.timeline && this.activeProject?.id === eventItem.projectId) {
        this.timeline.events = [eventItem, ...this.timeline.events];
      }
    });
    this.signalr.durationChanged$.subscribe(change => {
      const item = this.works.find(work => work.id === change.workId);
      if (item) {
        item.currentDuration = change.newDuration;
        this.renderGantt();
      }
    });
    this.signalr.unknownEventDetected$.subscribe(eventItem => {
      if (this.activeProject?.id !== eventItem.projectId) {
        return;
      }
      this.unknownEvent = eventItem;
      this.unknownEventName = '';
    });

    await this.refreshProjects();
    if (!this.projects.length) {
      this.createNewProject();
    }
  }

  async refreshProjects(): Promise<void> {
    this.projects = await firstValueFrom(this.api.getProjects());
  }

  openProjectDialog(): void {
    this.projectDialogOpen = true;
    this.statusMessage = '';
  }

  closeProjectDialog(): void {
    this.projectDialogOpen = false;
  }

  async chooseProject(projectId: string): Promise<void> {
    const project = await firstValueFrom(this.api.getProject(projectId));
    this.applyProject(project);
    await this.refreshTimeline(project.id);
    this.projectDialogOpen = false;
  }

  createNewProject(): void {
    const today = this.toDateOnly(new Date());
    this.applyProject({
      id: '',
      name: 'New project',
      startDate: today,
      endDate: this.toDateOnly(this.addDays(today, 14)),
      currentSimulationTime: today,
      isSimulationRunning: false,
      works: [],
      dependencies: []
    });
    this.timeline = undefined;
    this.projectDialogOpen = false;
  }

  async saveProject(): Promise<void> {
    if (!this.activeProject) {
      return;
    }

    this.syncActiveProject();
    this.syncProjectBoundsFromWorks();
    this.isSaving = true;
    this.statusMessage = '';

    try {
      const payload = this.buildSaveRequest();
      const saved = this.activeProject.id
        ? await firstValueFrom(this.api.updateProject(this.activeProject.id, payload))
        : await firstValueFrom(this.api.createProject(payload));

      this.applyProject(saved);
      await this.refreshProjects();
      await this.refreshTimeline(saved.id);
      this.statusMessage = 'Project saved.';
    } catch (error) {
      this.statusMessage = this.toErrorMessage(error);
    } finally {
      this.isSaving = false;
    }
  }

  async toggleSimulation(): Promise<void> {
    if (!this.activeProject?.id) {
      return;
    }

    try {
      if (this.activeProject.isSimulationRunning) {
        await firstValueFrom(this.api.stopSimulation(this.activeProject.id));
        this.activeProject.isSimulationRunning = false;
        this.statusMessage = 'Simulation stopped.';
      } else {
        await firstValueFrom(this.api.startSimulation(this.activeProject.id));
        this.activeProject.isSimulationRunning = true;
        this.statusMessage = 'Simulation started.';
      }
      await this.refreshProjects();
    } catch (error) {
      this.statusMessage = this.toErrorMessage(error);
    }
  }

  async reloadFromServices(): Promise<void> {
    if (!this.activeProject?.id) {
      return;
    }

    await this.chooseProject(this.activeProject.id);
    this.statusMessage = 'Project reloaded.';
  }

  addWork(): void {
    if (!this.activeProject) {
      return;
    }

    const baseline = this.selectedWork ?? this.works[this.works.length - 1];
    const startDate = baseline ? this.toDateOnly(baseline.startDate) : this.activeProject.startDate;
    const plannedDuration = baseline ? baseline.plannedDuration : 5;
    const endDate = this.toDateOnly(this.addDays(startDate, plannedDuration));
    const work: WorkModel = {
      id: this.createClientId(),
      projectId: this.activeProject.id || undefined,
      name: `Work ${this.works.length + 1}`,
      startDate,
      endDate,
      plannedDuration,
      currentDuration: 0,
      percentComplete: 0,
      currentState: 'Planned',
      isCompleted: false
    };

    this.works = [...this.works, work];
    this.selectedWorkId = work.id;
    this.syncProjectBoundsFromWorks();
    this.syncActiveProject();
    this.renderGantt();
  }

  removeSelectedWork(): void {
    if (!this.selectedWorkId) {
      return;
    }

    this.works = this.works.filter(work => work.id !== this.selectedWorkId);
    this.dependencies = this.dependencies.filter(
      link => link.sourceWorkId !== this.selectedWorkId && link.targetWorkId !== this.selectedWorkId
    );
    this.selectedWorkId = this.works[0]?.id;
    this.syncProjectBoundsFromWorks();
    this.syncActiveProject();
    this.renderGantt();
  }

  onSelectedWorkFormChange(): void {
    const work = this.selectedWork;
    if (!work) {
      return;
    }

    work.plannedDuration = this.calculateDuration(work.startDate, work.endDate, work.plannedDuration);
    work.currentDuration = Math.max(0, Math.round(work.currentDuration));
    work.percentComplete = this.clampPercent(work.percentComplete);
    work.isCompleted = work.percentComplete >= 100;
    this.syncProjectBoundsFromWorks();
    this.syncActiveProject();
    this.renderGantt();
  }

  exportJson(): void {
    if (!this.activeProject) {
      return;
    }

    this.syncActiveProject();
    const blob = new Blob([JSON.stringify(this.activeProject, null, 2)], { type: 'application/json' });
    const anchor = document.createElement('a');
    anchor.href = URL.createObjectURL(blob);
    anchor.download = `${this.activeProject.name || 'project'}.json`;
    anchor.click();
  }

  importJson(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    file.text().then(content => {
      const parsed = JSON.parse(content) as ProjectModel;
      this.applyProject({
        ...parsed,
        id: parsed.id || '',
        works: (parsed.works ?? []).map(work => this.normalizeWork(work, parsed.id || '')),
        dependencies: (parsed.dependencies ?? []).map(link => ({ ...link }))
      });
      this.timeline = undefined;
      this.statusMessage = 'Project imported locally. Save it to persist.';
      input.value = '';
    });
  }

  saveUnknownEvent(): void {
    if (!this.unknownEvent || !this.unknownEventName.trim()) {
      return;
    }

    this.api.registerUnknownEvent({
      workId: this.unknownEvent.workId,
      projectId: this.unknownEvent.projectId,
      name: this.unknownEventName,
      vector: [4, 220, 2, 1, 4, 2]
    }).subscribe(() => {
      this.unknownEvent = undefined;
      this.unknownEventName = '';
    });
  }

  cancelUnknownEvent(): void {
    this.unknownEvent = undefined;
    this.unknownEventName = '';
  }

  trackProject(_: number, project: ProjectModel): string {
    return project.id;
  }

  get selectedWork(): WorkModel | undefined {
    return this.works.find(work => work.id === this.selectedWorkId);
  }

  private configureGantt(): void {
    gantt.config.grid_width = 360;
    gantt.config.open_tree_initially = true;
    gantt.config.drag_move = true;
    gantt.config.drag_resize = true;
    gantt.config.drag_links = true;
    gantt.config.order_branch = true;
    gantt.config.date_format = '%Y-%m-%d';
    gantt.config.columns = [
      { name: 'text', label: 'Work', tree: true, width: 180 },
      { name: 'start_date', label: 'Start', align: 'center', width: 90 },
      { name: 'duration', label: 'Plan', align: 'center', width: 70 },
      { name: 'currentDuration', label: 'Current', align: 'center', width: 70 },
      { name: 'add', label: '', width: 44 }
    ];
    gantt.templates.task_text = (_start, _end, task) => `${task.text}`;
  }

  private attachGanttEvents(): void {
    gantt.attachEvent('onTaskSelected', id => {
      this.selectedWorkId = String(id);
      return true;
    });

    gantt.attachEvent('onAfterTaskAdd', id => {
      if (this.suppressGanttEvents) {
        return;
      }

      const task = gantt.getTask(id);
      const startDate = task.start_date ?? new Date();
      const endDate = task.end_date ?? startDate;
      const newWork: WorkModel = {
        id: String(task.id),
        projectId: this.activeProject?.id || undefined,
        name: task.text || `Work ${this.works.length + 1}`,
        startDate: this.toDateOnly(startDate),
        endDate: this.toDateOnly(endDate),
        plannedDuration: Math.max(1, Math.round(task.duration || 1)),
        currentDuration: 0,
        percentComplete: 0,
        currentState: 'Planned',
        isCompleted: false
      };

      this.works = [...this.works, newWork];
      this.selectedWorkId = newWork.id;
      this.syncProjectBoundsFromWorks();
      this.syncActiveProject();
      this.renderGantt();
    });

    gantt.attachEvent('onAfterTaskUpdate', id => {
      if (!this.suppressGanttEvents) {
        this.updateWorkFromTask(String(id));
      }
    });

    gantt.attachEvent('onAfterTaskDrag', id => {
      if (!this.suppressGanttEvents) {
        this.updateWorkFromTask(String(id));
      }
    });

    gantt.attachEvent('onAfterTaskDelete', id => {
      if (this.suppressGanttEvents) {
        return;
      }

      this.works = this.works.filter(work => work.id !== String(id));
      this.dependencies = this.dependencies.filter(link => link.sourceWorkId !== String(id) && link.targetWorkId !== String(id));
      this.selectedWorkId = this.works[0]?.id;
      this.syncProjectBoundsFromWorks();
      this.syncActiveProject();
    });

    gantt.attachEvent('onBeforeLinkAdd', (_id, link) => link.source !== link.target);

    gantt.attachEvent('onAfterLinkAdd', (_id, link) => {
      if (this.suppressGanttEvents) {
        return;
      }

      const dependency = {
        sourceWorkId: String(link.source),
        targetWorkId: String(link.target)
      };

      if (!this.dependencies.some(item => item.sourceWorkId === dependency.sourceWorkId && item.targetWorkId === dependency.targetWorkId)) {
        this.dependencies = [...this.dependencies, dependency];
        this.syncActiveProject();
      }
    });

    gantt.attachEvent('onAfterLinkDelete', (_id, link) => {
      if (this.suppressGanttEvents) {
        return;
      }

      this.dependencies = this.dependencies.filter(
        item => !(item.sourceWorkId === String(link.source) && item.targetWorkId === String(link.target))
      );
      this.syncActiveProject();
    });
  }

  private async refreshTimeline(projectId: string): Promise<void> {
    try {
      this.timeline = await firstValueFrom(this.api.getTimeline(projectId));
      this.mergeTimelineIntoWorks();
    } catch {
      this.timeline = undefined;
      this.renderGantt();
    }
  }

  private applyProject(project: ProjectModel): void {
    this.activeProject = {
      ...project,
      startDate: this.toDateOnly(project.startDate),
      endDate: this.toDateOnly(project.endDate),
      currentSimulationTime: this.toDateOnly(project.currentSimulationTime),
      works: (project.works ?? []).map(work => this.normalizeWork(work, project.id)),
      dependencies: (project.dependencies ?? []).map(link => ({ ...link }))
    };
    this.works = [...this.activeProject.works];
    this.dependencies = [...this.activeProject.dependencies];
    this.selectedWorkId = this.works[0]?.id;
    this.renderGantt();
  }

  private normalizeWork(work: WorkModel, projectId: string): WorkModel {
    return {
      ...work,
      projectId: work.projectId || projectId || undefined,
      startDate: this.toDateOnly(work.startDate),
      endDate: this.toDateOnly(work.endDate),
      plannedDuration: Math.max(1, Math.round(work.plannedDuration || this.calculateDuration(work.startDate, work.endDate, 1))),
      currentDuration: Math.max(0, Math.round(work.currentDuration || 0)),
      percentComplete: this.clampPercent(work.percentComplete || 0),
      currentState: work.currentState || 'Planned',
      isCompleted: work.isCompleted ?? (work.percentComplete || 0) >= 100
    };
  }

  private mergeTimelineIntoWorks(): void {
    if (!this.timeline) {
      this.renderGantt();
      return;
    }

    const timelineById = new Map(this.timeline.works.map(work => [work.id, work]));
    this.works = this.works.map(work => {
      const live = timelineById.get(work.id);
      return live
        ? {
            ...work,
            currentDuration: Math.max(0, Math.round(live.currentDuration || work.currentDuration)),
            percentComplete: this.clampPercent(live.percentComplete ?? work.percentComplete),
            currentState: live.currentState || work.currentState
          }
        : work;
    });

    this.syncActiveProject();
    this.renderGantt();
  }

  private applyWorkUpdate(work: WorkModel): void {
    const index = this.works.findIndex(item => item.id === work.id);
    if (index < 0) {
      return;
    }

    this.works[index] = {
      ...this.works[index],
      currentDuration: Math.max(0, Math.round(work.currentDuration ?? this.works[index].currentDuration)),
      percentComplete: this.clampPercent(work.percentComplete ?? this.works[index].percentComplete),
      currentState: work.currentState || this.works[index].currentState
    };
    this.syncActiveProject();
    this.renderGantt();
  }

  private updateWorkFromTask(taskId: string): void {
    const task = gantt.getTask(taskId);
    const work = this.works.find(item => item.id === taskId);
    if (!work) {
      return;
    }

    const startDate = task.start_date ?? new Date(work.startDate);
    const endDate = task.end_date ?? startDate;
    work.name = task.text || work.name;
    work.startDate = this.toDateOnly(startDate);
    work.endDate = this.toDateOnly(endDate);
    work.plannedDuration = Math.max(1, Math.round(task.duration || work.plannedDuration));
    this.selectedWorkId = work.id;
    this.syncProjectBoundsFromWorks();
    this.syncActiveProject();
  }

  private syncProjectBoundsFromWorks(): void {
    if (!this.activeProject || !this.works.length) {
      return;
    }

    const starts = this.works.map(work => new Date(work.startDate).getTime());
    const ends = this.works.map(work => new Date(work.endDate).getTime());
    this.activeProject.startDate = this.toDateOnly(new Date(Math.min(...starts)));
    this.activeProject.endDate = this.toDateOnly(new Date(Math.max(...ends)));
  }

  private syncActiveProject(): void {
    if (!this.activeProject) {
      return;
    }

    this.activeProject = {
      ...this.activeProject,
      works: this.works.map(work => ({ ...work })),
      dependencies: this.dependencies.map(link => ({ ...link }))
    };
  }

  private buildSaveRequest(): ProjectSaveRequest {
    return {
      name: this.activeProject?.name?.trim() || 'Untitled project',
      startDate: this.activeProject?.startDate || this.toDateOnly(new Date()),
      endDate: this.activeProject?.endDate || this.toDateOnly(new Date()),
      works: this.works.map(work => ({
        id: this.isClientOnlyId(work.id) ? null : work.id,
        name: work.name,
        startDate: work.startDate,
        endDate: work.endDate,
        plannedDuration: Math.max(1, Math.round(work.plannedDuration)),
        currentDuration: Math.max(0, Math.round(work.currentDuration)),
        percentComplete: this.clampPercent(work.percentComplete)
      })),
      dependencies: this.dependencies
    };
  }

  private renderGantt(): void {
    this.suppressGanttEvents = true;
    gantt.clearAll();
    gantt.parse({
      data: this.works.map(work => ({
        id: work.id,
        text: work.name,
        start_date: new Date(work.startDate),
        end_date: new Date(work.endDate),
        duration: Math.max(1, Math.round(work.plannedDuration)),
        currentDuration: Math.max(0, Math.round(work.currentDuration)),
        progress: Math.max(0, Math.min(1, work.percentComplete / 100))
      })),
      links: this.dependencies.map(link => ({
        id: `${link.sourceWorkId}-${link.targetWorkId}`,
        source: link.sourceWorkId,
        target: link.targetWorkId,
        type: '0'
      }))
    });

    if (this.selectedWorkId && gantt.isTaskExists(this.selectedWorkId)) {
      gantt.selectTask(this.selectedWorkId);
    }

    this.suppressGanttEvents = false;
  }

  private calculateDuration(startDate: string, endDate: string, fallback: number): number {
    const start = new Date(startDate);
    const end = new Date(endDate);
    const diff = Math.round((end.getTime() - start.getTime()) / 86400000);
    return Math.max(1, diff || fallback);
  }

  private clampPercent(value: number): number {
    return Math.max(0, Math.min(100, Math.round(value || 0)));
  }

  private toDateOnly(value: string | Date): string {
    const date = value instanceof Date ? value : new Date(value);
    return date.toISOString().slice(0, 10);
  }

  private addDays(dateValue: string, days: number): Date {
    const date = new Date(dateValue);
    date.setDate(date.getDate() + days);
    return date;
  }

  private createClientId(): string {
    return `tmp-${crypto.randomUUID()}`;
  }

  private isClientOnlyId(id: string): boolean {
    return id.startsWith('tmp-');
  }

  private toErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error && 'error' in error) {
      const payload = (error as { error?: unknown }).error;
      if (typeof payload === 'string') {
        return payload;
      }
    }

    return 'Request failed.';
  }
}
