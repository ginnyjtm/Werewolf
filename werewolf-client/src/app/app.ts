import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameHubService, PlayerDto } from './game-hub.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
})
export class App implements OnDestroy {
  readonly game = inject(GameHubService);

  name = '';
  code = '';
  chat = '';
  townMsg = '';

  private readonly now = signal(Date.now());
  private readonly ticker = setInterval(() => this.now.set(Date.now()), 250);

  readonly secondsLeft = computed(() =>
    Math.max(0, Math.ceil((this.game.endsAt() - this.now()) / 1000)));

  readonly wolfNames = computed(() =>
    this.game.fellowWolves().map(w => w.name).join(', '));

  readonly phaseTitle = computed(() => ({
    Night: '🌙 Night', Day: '☀️ Day: discuss', Voting: '⚖️ Vote',
  } as Record<string, string>)[this.game.phase()] ?? '');

  readonly buttonLabel = computed(() => {
    if (this.game.phase() === 'Voting') return 'Vote';
    return ({ Werewolf: 'Kill', Seer: 'Inspect', Doctor: 'Protect' } as Record<string, string>)[this.game.myRole() ?? ''] ?? '';
  });

  readonly hint = computed(() => {
    const g = this.game;
    if (!g.amAlive()) return '👻 You are dead. Watch silently.';
    switch (g.phase()) {
      case 'Night':
        return g.myRole() === 'Villager' ? 'Sleep tight… and hope.' : `Choose someone to ${this.buttonLabel().toLowerCase()}.`;
      case 'Day': return 'Discuss who might be a wolf. Voting starts soon.';
      case 'Voting': return 'Vote to eliminate someone.';
      default: return '';
    }
  });

  canTarget(p: PlayerDto): boolean {
    const g = this.game;
    if (!g.amAlive() || !p.isAlive) return false;
    const isMe = p.id === g.myId();

    if (g.phase() === 'Voting') return !isMe;
    if (g.phase() !== 'Night') return false;

    switch (g.myRole()) {
      case 'Werewolf': return !isMe && !g.fellowWolves().some(w => w.id === p.id);
      case 'Seer': return !isMe;
      case 'Doctor': return true; // may protect self
      default: return false;
    }
  }

  act(p: PlayerDto) {
    this.game.phase() === 'Night' ? this.game.nightAction(p.id) : this.game.vote(p.id);
  }

  send() {
    if (!this.chat.trim()) return;
    this.game.sendWolfChat(this.chat.trim());
    this.chat = '';
  }

  sendTown() {
  if (!this.townMsg.trim()) return;
  this.game.sendTownChat(this.townMsg.trim());
  this.townMsg = '';
  }

  ngOnDestroy() {
    clearInterval(this.ticker);
  }
}