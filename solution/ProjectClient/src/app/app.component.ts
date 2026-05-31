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

    let projectLoadFailed = false;
    try {
      await this.refreshProjects();
    } catch (error) {
      projectLoadFailed = true;
      this.statusMessage = `Cannot load saved projects: ${this.toErrorMessage(error)}`;
    }

    try {
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
    } catch (error) {
      this.statusMessage = `Realtime unavailable: ${this.toErrorMessage(error)}`;
    }

    if (!projectLoadFailed && !this.projects.length) {
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

  async deleteActiveProject(): Promise<void> {
    if (!this.activeProject?.id) {
      return;
    }

    if (!confirm('Are you sure you want to delete this project?')) {
      return;
    }

    try {
      await firstValueFrom(this.api.deleteProject(this.activeProject.id));
      await this.refreshProjects();
      this.activeProject = undefined;
      this.works = [];
      this.dependencies = [];
      this.timeline = undefined;
      this.projectDialogOpen = true;
      this.statusMessage = 'Project deleted.';
    } catch (error) {
      this.statusMessage = this.toErrorMessage(error);
    }
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

  exportCsv(): void {
    if (!this.activeProject || !this.works.length) {
      return;
    }

    this.syncActiveProject();
    const header = 'id,name,startDate,endDate,plannedDuration,currentDuration,percentComplete,currentState,isCompleted';
    const rows = this.works.map(w =>
      [w.id, `"${w.name}"`, w.startDate, w.endDate, w.plannedDuration, w.currentDuration, w.percentComplete, w.currentState ?? '', w.isCompleted ?? false].join(',')
    );
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const anchor = document.createElement('a');
    anchor.href = URL.createObjectURL(blob);
    anchor.download = `${this.activeProject.name || 'project'}-works.csv`;
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

  importCsv(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.activeProject) {
      return;
    }

    file.text().then(content => {
      const lines = content.trim().split('\n');
      if (lines.length < 2) {
        this.statusMessage = 'CSV file has no data rows.';
        return;
      }

      // Parse header → column index map
      const headers = lines[0].split(',').map(h => h.trim().toLowerCase());
      const col = (name: string) => headers.indexOf(name);

      const parsedWorks: WorkModel[] = [];
      for (const line of lines.slice(1)) {
        if (!line.trim()) continue;
        // Handle quoted fields (e.g. "Work name with comma")
        const cells = line.match(/(".*?"|[^,]+|(?<=,)(?=,)|(?<=,)$|^(?=,))/g)?.map(c => c.replace(/^"|"$/g, '').trim()) ?? line.split(',').map(c => c.trim());

        const name = col('name') >= 0 ? (cells[col('name')] || `Work ${parsedWorks.length + 1}`) : `Work ${parsedWorks.length + 1}`;
        const startDate = col('startdate') >= 0 ? this.toDateOnly(cells[col('startdate')] || this.activeProject!.startDate) : this.activeProject!.startDate;
        const plannedDuration = col('plannedduration') >= 0 ? Math.max(1, Number(cells[col('plannedduration')]) || 1) : 5;
        const endDate = col('enddate') >= 0 ? this.toDateOnly(cells[col('enddate')] || this.toDateOnly(this.addDays(startDate, plannedDuration))) : this.toDateOnly(this.addDays(startDate, plannedDuration));
        const currentDuration = col('currentduration') >= 0 ? Math.max(0, Number(cells[col('currentduration')]) || 0) : 0;
        const percentComplete = col('percentcomplete') >= 0 ? this.clampPercent(Number(cells[col('percentcomplete')]) || 0) : 0;

        parsedWorks.push(this.normalizeWork({
          id: col('id') >= 0 ? (cells[col('id')] || this.createClientId()) : this.createClientId(),
          name,
          startDate,
          endDate,
          plannedDuration,
          currentDuration,
          percentComplete,
          currentState: col('currentstate') >= 0 ? (cells[col('currentstate')] || 'S0Stable') : 'S0Stable',
          isCompleted: col('iscompleted') >= 0 ? cells[col('iscompleted')] === 'true' : percentComplete >= 100
        }, this.activeProject!.id));
      }

      if (!parsedWorks.length) {
        this.statusMessage = 'No valid works found in CSV.';
        return;
      }

      this.works = parsedWorks;
      this.dependencies = [];
      this.syncProjectBoundsFromWorks();
      this.syncActiveProject();
      this.renderGantt();
      this.statusMessage = `Imported ${parsedWorks.length} works from CSV. Save to persist.`;
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
      vector: this.unknownEvent.featureVector ?? []
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
    gantt.config.grid_width = 380;
    gantt.config.open_tree_initially = true;
    gantt.config.drag_move = true;
    gantt.config.drag_resize = true;
    gantt.config.drag_links = true;
    gantt.config.order_branch = true;
    gantt.config.date_format = '%Y-%m-%d';
    gantt.config.columns = [
      { name: 'text', label: 'Work', tree: true, width: 160 },
      { name: 'start_date', label: 'Start', align: 'center', width: 90 },
      { name: 'plannedDuration', label: 'Plan', align: 'center', width: 45 },
      { name: 'currentDuration', label: 'Curr', align: 'center', width: 45 },
      { name: 'stabilityState', label: 'State', align: 'center', width: 40 },
      { name: 'add', label: '', width: 40 }
    ];
    // Colour the Gantt bar by stability state so Markov transitions are immediately visible
    gantt.templates.task_class = (_start: Date, _end: Date, task: any): string => {
      const s: string = task.stabilityState || '';
      if (s.includes('Critical')) return 'stability-critical';
      if (s.includes('High')) return 'stability-high';
      if (s.includes('Medium')) return 'stability-medium';
      if (s.includes('Low')) return 'stability-low';
      return 'stability-stable';
    };
    gantt.templates.task_text = (_start: Date, _end: Date, task: any) => task.text as string;
    // Show truncated stability label inside bar
    gantt.templates.rightside_text = (_start: Date, _end: Date, task: any) => {
      const s: string = task.stabilityState || '';
      if (s === 'S0Stable') return '';
      return `<span class="state-badge">${s.replace('WorkStabilityState.', '').replace('Sensitivity', '')}</span>`;
    };
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
      const duration = Math.max(1, Math.round(task.duration || 1));

      // Calculate endDate from startDate + duration
      const endDate = new Date(startDate);
      endDate.setDate(endDate.getDate() + duration);

      const newWork: WorkModel = {
        id: String(task.id),
        projectId: this.activeProject?.id || undefined,
        name: task.text || `Work ${this.works.length + 1}`,
        startDate: this.toDateOnly(startDate),
        endDate: this.toDateOnly(endDate),
        plannedDuration: duration,
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
      id: work.id || this.createClientId(),
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
    // task.plannedDuration is the user-editable plan field stored on the task;
    // fall back to task.duration only when the task was added without our custom field.
    const planned = Math.max(1, Math.round((task as any).plannedDuration || task.duration || work.plannedDuration));

    work.name = task.text || work.name;
    work.startDate = this.toDateOnly(startDate);
    work.plannedDuration = planned;

    const endDate = new Date(startDate);
    endDate.setDate(endDate.getDate() + planned);
    work.endDate = this.toDateOnly(endDate);

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
        id: work.id || null,
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
      data: this.works.map(work => {
        const startDate = this.parseLocalDate(work.startDate);
        const planned = Math.max(1, Math.round(work.plannedDuration));
        // Use currentDuration for the bar when it exceeds the plan (event impacts);
        // fall back to planned when currentDuration hasn't been set yet.
        const current = work.currentDuration > 0 ? Math.round(work.currentDuration) : planned;
        const barDuration = Math.max(planned, current);

        return {
          id: work.id,
          text: work.name,
          start_date: startDate,
          duration: barDuration,           // drives the visible bar length
          plannedDuration: planned,        // shown in 'Plan' column
          currentDuration: current,        // shown in 'Curr' column
          stabilityState: work.currentState || 'S0Stable',
          progress: Math.max(0, Math.min(1, work.percentComplete / 100))
        };
      }),
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

  private parseLocalDate(dateString: string): Date {
    // Strip time component if present (e.g. "2025-06-01T00:00:00Z" → "2025-06-01")
    const datePart = dateString.split('T')[0];
    // Parse YYYY-MM-DD as local date, not UTC
    const [year, month, day] = datePart.split('-').map(Number);
    return new Date(year, month - 1, day);
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
    // Use local date components to avoid timezone issues
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private addDays(dateValue: string, days: number): Date {
    const date = new Date(dateValue);
    date.setDate(date.getDate() + days);
    return date;
  }

  private createClientId(): string {
    return crypto.randomUUID();
  }

  private toErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error && 'error' in error) {
      const payload = (error as { error?: unknown }).error;
      if (typeof payload === 'string') {
        return payload;
      }
    }

    if (typeof error === 'object' && error) {
      const typedError = error as { status?: number; statusText?: string };
      if (typedError.status && typedError.statusText) {
        return `${typedError.status}: ${typedError.statusText}`;
      }
      if (typedError.statusText) {
        return typedError.statusText;
      }
    }

    return 'Request failed.';
  }
}
