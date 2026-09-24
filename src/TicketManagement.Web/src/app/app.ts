import { Component } from '@angular/core';
import { TicketWorkspaceComponent } from './components/ticket-workspace/ticket-workspace.component';

@Component({
  selector: 'app-root',
  imports: [TicketWorkspaceComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
