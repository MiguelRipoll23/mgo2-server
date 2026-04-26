import type { CharacterStats } from "../../db/schema.ts";
import { MODE_COLUMN } from "../../core/constants/game-mode-constants.ts";

/**
 * Calculates the animal rank for a character based on their stats.
 * Ported from boiln/echo AnimalRankService.java.
 *
 * Rank IDs (MGO2):
 * 1=Foxhound  2=Fox        3=Doberman    4=Hound       5=Crocodile
 * 6=Eagle     7=Jaws       8=Water Bear  9=Sloth      10=Flying Squirrel
 * 11=Pigeon  12=Owl       13=Tsuchinoko 14=Snake      15=Kerotan
 * 16=GA-KO   17=Chameleon 18=Chicken   19=Bear       20=Tortoise
 * 21=Bee     22=Rat       23=Fighting Fish 24=Komodo Dragon
 * 26=Killer Whale 27=Elephant 28=Cuckoo 29=Hog
 * 40=Octopus 42=Panda 43=Puma 44=Scorpion 46=Mantis
 * 48=Night Owl 50=Hawk 52=Ocelot
 * 0=No rank
 */

interface ModeStats {
  wins: number;
  rounds: number;
  kills: number;
  deaths: number;
  stuns: number;
  stunsRec: number;
  score: number;
  time: number;
}

function parseModeStats(json: string | null | undefined): ModeStats {
  const empty: ModeStats = { wins: 0, rounds: 0, kills: 0, deaths: 0, stuns: 0, stunsRec: 0, score: 0, time: 0 };
  if (!json) return empty;
  try {
    const obj = JSON.parse(json);
    return {
      wins: obj.wins ?? 0,
      rounds: obj.rounds ?? 0,
      kills: obj.kills ?? 0,
      deaths: obj.deaths ?? 0,
      stuns: obj.stuns ?? 0,
      stunsRec: obj.stunsRec ?? 0,
      score: obj.score ?? 0,
      time: obj.time ?? 0,
    };
  } catch {
    return empty;
  }
}

function getModeStats(stats: CharacterStats, mode: number): ModeStats {
  const col = MODE_COLUMN[mode] ?? "stats_dm";
  return parseModeStats(stats[col] as string | null);
}

function calculateKDSRR(dm: ModeStats, tdm: ModeStats, sne: ModeStats): number {
  const totalRounds = dm.rounds + tdm.rounds + sne.rounds;
  if (totalRounds === 0) return 0;
  return (dm.kills + tdm.kills + sne.kills +
          dm.stuns + tdm.stuns + sne.stuns +
          dm.deaths + tdm.deaths + sne.deaths +
          dm.stunsRec + tdm.stunsRec + sne.stunsRec) / totalRounds;
}

function calculateWinRate(...modes: ModeStats[]): number {
  const totalRounds = modes.reduce((s, m) => s + m.rounds, 0);
  if (totalRounds === 0) return 0;
  return modes.reduce((s, m) => s + m.wins, 0) / totalRounds;
}

function checkModeSpecialist(mode: ModeStats, totalRounds: number, minRounds: number): boolean {
  return mode.rounds >= minRounds && totalRounds > 0 && mode.rounds / totalRounds >= 0.60;
}

export function calculateAnimalRank(stats: CharacterStats, daysSinceLastLogin: number): number {
  const totalRounds = stats.rounds === 0 ? 1 : stats.rounds;

  const dm   = getModeStats(stats, 0);
  const tdm  = getModeStats(stats, 1);
  const sne  = getModeStats(stats, 2);
  const cap  = getModeStats(stats, 3);
  const base = getModeStats(stats, 4);
  const bomb = getModeStats(stats, 5);
  const res  = getModeStats(stats, 6);
  const race = getModeStats(stats, 7);
  const tsne = getModeStats(stats, 8);
  const sdm  = getModeStats(stats, 9);

  // ── Foxhound family ──────────────────────────────────────────────────────

  if (stats.rounds >= 100) {
    const kdsrr = calculateKDSRR(dm, tdm, sne);
    const wr    = calculateWinRate(cap, base, bomb, res, tsne);
    const baseCapRate = base.rounds > 0 ? stats.bases_captured / base.rounds : 0;
    const withdrawRate = stats.rounds > 0 ? stats.withdrawals / stats.rounds : 0;
    const raceWr = race.rounds > 0 ? race.wins / race.rounds : 1;

    if (kdsrr >= 1.45 && wr >= 0.525 && raceWr >= 0.50 && baseCapRate >= 1.60 && withdrawRate <= 0.02) return 1; // Foxhound
    if (kdsrr >= 1.40 && wr >= 0.475 && raceWr >= 0.45 && baseCapRate >= 1.40 && withdrawRate <= 0.02 && stats.rounds >= 50) return 2; // Fox
    if (kdsrr >= 1.35 && wr >= 0.45  && raceWr >= 0.425 && baseCapRate >= 1.20 && withdrawRate <= 0.04 && stats.rounds >= 25) return 3; // Doberman
    if (kdsrr >= 1.30 && wr >= 0.425 && raceWr >= 0.40  && baseCapRate >= 1.00 && withdrawRate <= 0.04 && stats.rounds >= 5)  return 4; // Hound
  } else if (stats.rounds >= 50) {
    const kdsrr = calculateKDSRR(dm, tdm, sne);
    const wr    = calculateWinRate(cap, base, bomb, res, tsne);
    const baseCapRate = base.rounds > 0 ? stats.bases_captured / base.rounds : 0;
    const withdrawRate = stats.rounds > 0 ? stats.withdrawals / stats.rounds : 0;
    const raceWr = race.rounds > 0 ? race.wins / race.rounds : 1;

    if (kdsrr >= 1.40 && wr >= 0.475 && raceWr >= 0.45 && baseCapRate >= 1.40 && withdrawRate <= 0.02) return 2;
    if (kdsrr >= 1.35 && wr >= 0.45  && raceWr >= 0.425 && baseCapRate >= 1.20 && withdrawRate <= 0.04 && stats.rounds >= 25) return 3;
    if (kdsrr >= 1.30 && wr >= 0.425 && raceWr >= 0.40  && baseCapRate >= 1.00 && withdrawRate <= 0.04 && stats.rounds >= 5) return 4;
  } else if (stats.rounds >= 25) {
    const kdsrr = calculateKDSRR(dm, tdm, sne);
    const wr    = calculateWinRate(cap, base, bomb, res, tsne);
    const baseCapRate = base.rounds > 0 ? stats.bases_captured / base.rounds : 0;
    const withdrawRate = stats.rounds > 0 ? stats.withdrawals / stats.rounds : 0;
    const raceWr = race.rounds > 0 ? race.wins / race.rounds : 1;

    if (kdsrr >= 1.35 && wr >= 0.45 && raceWr >= 0.425 && baseCapRate >= 1.20 && withdrawRate <= 0.04) return 3;
    if (kdsrr >= 1.30 && wr >= 0.425 && raceWr >= 0.40 && baseCapRate >= 1.00 && withdrawRate <= 0.04 && stats.rounds >= 5) return 4;
  } else if (stats.rounds >= 5) {
    const kdsrr = calculateKDSRR(dm, tdm, sne);
    const wr    = calculateWinRate(cap, base, bomb, res, tsne);
    const baseCapRate = base.rounds > 0 ? stats.bases_captured / base.rounds : 0;
    const withdrawRate = stats.rounds > 0 ? stats.withdrawals / stats.rounds : 0;
    const raceWr = race.rounds > 0 ? race.wins / race.rounds : 1;

    if (kdsrr >= 1.30 && wr >= 0.425 && raceWr >= 0.40 && baseCapRate >= 1.00 && withdrawRate <= 0.04) return 4;
  }

  // ── Combat style ─────────────────────────────────────────────────────────

  const kdsrr = (stats.kills + stats.stuns + stats.deaths + stats.stuns_received) / totalRounds;

  if (kdsrr >= 1.50) return 5; // Crocodile

  if (kdsrr >= 1.30 && stats.kills > 0 && stats.headshot_kills / stats.kills >= 0.50) return 6; // Eagle

  if (kdsrr >= 1.25 && stats.kills > 0 && stats.knife_kills / stats.kills >= 0.075) return 7; // Jaws

  if (stats.kills > 0 && stats.stuns_received > 0) {
    if (stats.stuns / stats.kills >= 1.20 && stats.stuns / stats.stuns_received >= 1.20) return 11; // Pigeon
  }

  if (stats.knife_stuns / totalRounds >= 1.0) return 44; // Scorpion

  if (stats.kills > 0 && stats.lock_kills / stats.kills >= 0.40) return 52; // Ocelot

  // ── Equipment / playstyle ────────────────────────────────────────────────

  if (stats.cqc_given / totalRounds >= 5.0) return 19; // Bear

  if (stats.box_uses / totalRounds >= 15.0) return 20; // Tortoise

  if (stats.scans / totalRounds >= 0.30) return 21; // Bee

  if (stats.trapped / totalRounds >= 0.30) return 22; // Rat

  if (stats.time > 0 && stats.evg_time / stats.time >= 0.05) return 12; // Owl

  const snakeTsneRounds = tsne.rounds + sne.rounds;
  if (snakeTsneRounds >= 15) {
    const spottedRatio = (stats.spotted + stats.snake_spotted) / snakeTsneRounds;
    if (spottedRatio <= 0.15) return 40; // Octopus
    if (spottedRatio <= 0.50) return 48; // Night Owl
  }

  if (tsne.rounds > 0 && stats.spotted / tsne.rounds >= 0.30) return 50; // Hawk

  if (stats.rolls / totalRounds >= 15.0) return 10; // Flying Squirrel

  // ── Game mode specialists ─────────────────────────────────────────────────

  if (checkModeSpecialist(dm,   totalRounds, 30)) return 23; // Fighting Fish
  if (checkModeSpecialist(sdm,  totalRounds, 30)) return 24; // Komodo Dragon
  if (checkModeSpecialist(tdm,  totalRounds, 30)) return 26; // Killer Whale
  if (checkModeSpecialist(base, totalRounds, 30)) return 27; // Elephant
  if (checkModeSpecialist(bomb, totalRounds, 30)) return 28; // Cuckoo
  if (checkModeSpecialist(race, totalRounds, 30)) return 29; // Hog
  if (checkModeSpecialist(sne,  totalRounds, 30)) return 14; // Snake
  if (checkModeSpecialist(cap,  totalRounds, 30)) return 15; // Kerotan
  if (checkModeSpecialist(res,  totalRounds, 30)) return 16; // GA-KO
  if (checkModeSpecialist(tsne, totalRounds, 30)) return 17; // Chameleon

  // ── Snake mode specific ───────────────────────────────────────────────────

  if (sne.rounds >= 15 && stats.snake_holdups / sne.rounds >= 2.0) return 43; // Puma

  if (base.rounds >= 15 && stats.bases_captured / base.rounds >= 2.5) return 42; // Panda

  if (tsne.rounds > 0 && stats.wakeups / tsne.rounds >= 0.30) return 46; // Mantis

  // ── Survival / defense ───────────────────────────────────────────────────

  const resTsneRounds = res.rounds + tsne.rounds;
  if (resTsneRounds > 0) {
    const totalDeaths = res.deaths + tsne.deaths;
    if (totalDeaths / resTsneRounds <= 0.50) return 8; // Water Bear
  }

  // ── Negative / passive ───────────────────────────────────────────────────

  if (stats.deaths > 0 && stats.stuns_received > 0) {
    const kd = stats.kills / stats.deaths;
    const hsDeathRatio = stats.headshot_deaths / stats.deaths;
    const stunRatio = stats.stuns / stats.stuns_received;
    if (kd <= 0.85 && hsDeathRatio >= 0.60 && stunRatio <= 0.85) return 9; // Sloth
  }

  if (
    stats.kills / totalRounds <= 0.30 &&
    stats.stuns / totalRounds <= 0.30 &&
    stats.stuns_received / totalRounds <= 0.50 &&
    stats.deaths / totalRounds <= 0.50
  ) return 18; // Chicken

  if (daysSinceLastLogin >= 30) return 13; // Tsuchinoko

  return 0;
}
