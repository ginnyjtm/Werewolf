import { Injectable, computed, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

const HUB_URL = 'http://localhost:5124/hubs/game'; // ← your port here

export type Role = 'Villager' | 'Werewolf' | 'Seer' | 'Doctor';
export type Phase = 'Lobby' | 'Night' | 'Day' | 'Voting' | 'GameOver';
export interface PlayerDto { id: string; name: string; isAlive: boolean; isBot: boolean; }
export interface LobbyDto { roomCode: string; hostId: string; players: PlayerDto[]; }
export interface PhaseDto { phase: Phase; round: number; endsAt: string; players: PlayerDto[]; }
export interface RoleDto { role: Role; fellowWolves: PlayerDto[]; }
export interface RevealDto { id: string; name: string; role: Role; isAlive: boolean; }
export interface GameOverDto { winner: 'Village' | 'Wolves'; players: RevealDto[]; }
interface JoinResult { roomCode: string; playerId: string; }

@Injectable({ providedIn: 'root' })
export class GameHubService {
  private readonly hub = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // ---------- State ----------
  readonly myId = signal<string | null>(null);
  readonly roomCode = signal<string | null>(null);
  readonly hostId = signal<string | null>(null);
  readonly players = signal<PlayerDto[]>([]);
  readonly phase = signal<Phase>('Lobby');
  readonly round = signal(0);
  readonly endsAt = signal(0);
  readonly myRole = signal<Role | null>(null);
  readonly fellowWolves = signal<PlayerDto[]>([]);
  readonly tally = signal<Record<string, number>>({});
  readonly selected = signal<string | null>(null);
  readonly log = signal<string[]>([]);
  readonly wolfChat = signal<{ from: string; message: string }[]>([]);
  readonly townChat = signal<{ from: string; message: string }[]>([]);
  readonly gameOver = signal<GameOverDto | null>(null);
  readonly error = signal<string | null>(null);

  readonly me = computed(() => this.players().find(p => p.id === this.myId()));
  readonly isHost = computed(() => !!this.myId() && this.myId() === this.hostId());
  readonly amAlive = computed(() => this.me()?.isAlive ?? false);

  constructor() {
    this.hub.on('LobbyUpdated', (l: LobbyDto) => {
      this.roomCode.set(l.roomCode);
      this.hostId.set(l.hostId);
      this.players.set(l.players);
    });

    this.hub.on('RoleAssigned', (r: RoleDto) => {
      this.myRole.set(r.role);
      this.fellowWolves.set(r.fellowWolves);
      this.addLog(`🎭 You are a ${r.role}.`);
    });

    this.hub.on('PhaseChanged', (p: PhaseDto) => {
      this.phase.set(p.phase);
      this.round.set(p.round);
      this.endsAt.set(new Date(p.endsAt).getTime());
      this.players.set(p.players);
      this.tally.set({});
      this.selected.set(null);
    });

    this.hub.on('NightResolved', (killed: string | null) =>
      this.addLog(killed ? `🌅 Dawn breaks… ${killed} was found dead.` : '🌅 Dawn breaks… everyone survived.'));

    this.hub.on('SeerResult', (name: string, isWolf: boolean) =>
      this.addLog(`🔮 ${name} is ${isWolf ? 'a WEREWOLF!' : 'not a werewolf.'}`));

    this.hub.on('VoteTally', (t: Record<string, number>) => this.tally.set(t));

    this.hub.on('DayResolved', (name: string | null) =>
      this.addLog(name ? `⚖️ The village eliminated ${name}.` : '⚖️ Tied vote. Nobody was eliminated.'));

    this.hub.on('WolfChat', (from: string, message: string) =>
      this.wolfChat.update(c => [...c, { from, message }]));
  
    this.hub.on('TownChat', (from: string, message: string) =>
      this.townChat.update(c => [...c, { from, message }]));

    this.hub.on('GameOver', (r: GameOverDto) => {
      this.gameOver.set(r);
      this.phase.set('GameOver');
    });
  }

  // ---------- Commands ----------
  createRoom(name: string) {
    return this.call(async () => {
      await this.ensureConnected();
      const r = await this.hub.invoke<JoinResult>('CreateRoom', name);
      this.myId.set(r.playerId);
    });
  }

  joinRoom(code: string, name: string) {
    return this.call(async () => {
      await this.ensureConnected();
      const r = await this.hub.invoke<JoinResult>('JoinRoom', code, name);
      this.myId.set(r.playerId);
    });
  }

  addBots(count: number) { return this.call(() => this.hub.invoke('AddBots', count)); }
  startGame() { return this.call(() => this.hub.invoke('StartGame')); }
  sendWolfChat(message: string) { return this.call(() => this.hub.invoke('SendWolfChat', message)); }
  sendTownChat(message: string) { return this.call(() => this.hub.invoke('SendTownChat', message)); }

  nightAction(targetId: string) {
    this.selected.set(targetId);
    return this.call(() => this.hub.invoke('NightAction', targetId));
  }

  vote(targetId: string) {
    this.selected.set(targetId);
    return this.call(() => this.hub.invoke('Vote', targetId));
  }

  // ---------- Helpers ----------
  private async ensureConnected() {
    if (this.hub.state === signalR.HubConnectionState.Disconnected) await this.hub.start();
  }

  private async call(fn: () => Promise<unknown>) {
    this.error.set(null);
    try {
      await fn();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      this.error.set(msg.split('HubException: ').pop() ?? msg); // show the server's own message
    }
  }

  private addLog(line: string) {
    this.log.update(l => [line, ...l]);
  }
}