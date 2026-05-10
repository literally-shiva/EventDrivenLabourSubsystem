import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { DetectedEvent, WorkModel } from './models';

@Injectable({ providedIn: 'root' })
export class SignalrService {
  private readonly connection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5000/project-updates')
    .withAutomaticReconnect()
    .build();

  readonly workUpdated$ = new Subject<WorkModel>();
  readonly eventDetected$ = new Subject<DetectedEvent>();
  readonly durationChanged$ = new Subject<{ workId: string; newDuration: number }>();
  readonly unknownEventDetected$ = new Subject<DetectedEvent>();

  async start(): Promise<void> {
    this.connection.on('workUpdated', (payload: WorkModel) => this.workUpdated$.next(payload));
    this.connection.on('eventDetected', (payload: DetectedEvent) => this.eventDetected$.next(payload));
    this.connection.on('durationChanged', (payload: { workId: string; newDuration: number }) => this.durationChanged$.next(payload));
    this.connection.on('unknownEventDetected', (payload: DetectedEvent) => this.unknownEventDetected$.next(payload));

    if (this.connection.state === signalR.HubConnectionState.Disconnected) {
      await this.connection.start();
    }
  }
}
