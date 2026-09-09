import { describe, expect, it } from 'vitest'
import {
  PENALITE_ABANDON_SECS,
  calculerApercu,
  tempsEffectif,
  validerLignes,
  versResultats,
} from './apercuMatch'
import type { LigneSaisie } from './apercuMatch'

/** Raccourci : seules les equipes classees interessent la plupart des tests. */
function classer(lignes: readonly LigneSaisie[]) {
  return calculerApercu(lignes).equipes
}

function ligne(
  joueurId: number,
  joueurNom: string,
  equipeId: number,
  equipeNom: string,
  temps: string,
  extras: Partial<LigneSaisie> = {},
): LigneSaisie {
  return {
    joueurId,
    joueurNom,
    equipeId,
    equipeNom,
    participe: true,
    estAbandon: false,
    jeuId: 1,
    seed: '',
    totalChecks: '',
    nbChecks: '',
    temps,
    ...extras,
  }
}

const EQUIPE_A = { id: 10, nom: 'Les Nous_' }
const EQUIPE_B = { id: 20, nom: "No M's Land" }

/** Format qualification : deux participants par equipe. */
function qualification(
  tempsA1: string,
  tempsA2: string,
  tempsB1: string,
  tempsB2: string,
): LigneSaisie[] {
  return [
    ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, tempsA1),
    ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, tempsA2),
    ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, tempsB1),
    ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, tempsB2),
  ]
}

/** Roster complet de quatre joueurs, dont seuls les deux premiers participent. */
function rosterAvecRemplacants(): LigneSaisie[] {
  return [
    ...qualification('10:00', '10:00', '20:00', '20:00'),
    ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '', { participe: false }),
    ligne(6, 'Moi_Thunderbuzz', EQUIPE_A.id, EQUIPE_A.nom, '', { participe: false }),
    ligne(7, '_annix86', EQUIPE_B.id, EQUIPE_B.nom, '', { participe: false }),
    ligne(8, '_oo__oony', EQUIPE_B.id, EQUIPE_B.nom, '', { participe: false }),
  ]
}

describe('tempsEffectif', () => {
  it('rend le temps saisi quand le joueur a termine', () => {
    expect(tempsEffectif(ligne(1, 'Moi_Eva', 10, 'Les Nous_', '10:00'))).toBe(600)
  })

  it('majore le temps d une heure en cas d abandon', () => {
    const abandon = ligne(1, 'Moi_Eva', 10, 'Les Nous_', '10:00', { estAbandon: true })

    expect(tempsEffectif(abandon)).toBe(600 + PENALITE_ABANDON_SECS)
  })

  it('ne rend rien quand le temps est absent ou mal forme', () => {
    expect(tempsEffectif(ligne(1, 'Moi_Eva', 10, 'Les Nous_', 'abc'))).toBeNull()
    expect(tempsEffectif(ligne(1, 'Moi_Eva', 10, 'Les Nous_', ''))).toBeNull()
  })
})

describe('calculerApercu', () => {
  it('retient la seed la plus longue de chaque equipe, pas la somme', () => {
    const apercu = classer(qualification('1:00:00', '50:00', '1:10:00', '58:00'))

    expect(apercu).toHaveLength(2)
    expect(apercu[0]).toMatchObject({
      equipeId: EQUIPE_A.id,
      position: 1,
      estGagnante: true,
      totalSecs: 3600,
      penaliteSecs: 0,
      nbAbandons: 0,
    })
    expect(apercu[1]).toMatchObject({
      equipeId: EQUIPE_B.id,
      position: 2,
      estGagnante: false,
      totalSecs: 4200,
    })
  })

  it('majore d une heure la seed abandonnee', () => {
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '10:00'),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '10:00', { estAbandon: true }),
    ])

    // L abandon devient la plus longue : 600 s majorees d une heure.
    expect(apercu[0]).toMatchObject({
      totalBrutSecs: 600,
      penaliteSecs: PENALITE_ABANDON_SECS,
      totalSecs: 600 + PENALITE_ABANDON_SECS,
      nbAbandons: 1,
    })
  })

  it('ne compte que la penalite de la seed determinante', () => {
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '01:00', { estAbandon: true }),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '02:00', { estAbandon: true }),
    ])

    // Les deux ont abandonne, mais seule la plus longue entre dans le score.
    expect(apercu[0]).toMatchObject({
      totalBrutSecs: 120,
      penaliteSecs: PENALITE_ABANDON_SECS,
      totalSecs: 120 + PENALITE_ABANDON_SECS,
      nbAbandons: 2,
    })
  })

  it('classe une equipe avec abandon comme les autres', () => {
    // Equipe A : l abandon donne 60 + 3 600 = 3 660 s, sa plus longue seed.
    // Equipe B : 2:00:00. La penalite etant la sanction, A gagne quand meme.
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '01:00'),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '01:00', { estAbandon: true }),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '2:00:00'),
      ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, '1:00:00'),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: 3660, estGagnante: true })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 7200 })
  })

  it('peut faire perdre une equipe pourtant plus rapide', () => {
    // Equipe A : 100 s bruts, mais un abandon la porte a 3 700 s.
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '50'),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '50', { estAbandon: true }),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
      ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 600 })
    expect(apercu[1]).toMatchObject({
      equipeId: EQUIPE_A.id,
      totalBrutSecs: 50,
      totalSecs: 3650,
    })
  })

  it('ignore les membres du roster qui ne participent pas', () => {
    const apercu = classer(rosterAvecRemplacants())

    expect(apercu).toHaveLength(2)
    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: 600 })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 1200 })
  })

  it('retient la plus longue des trois seeds en demi-finale', () => {
    const apercu = classer([
      ...qualification('5:00', '5:00', '10:00', '10:00'),
      ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '5:00'),
      ligne(7, '_annix86', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: 300 })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 600 })
  })

  it('retient la plus longue des quatre seeds en finale', () => {
    const apercu = classer(
      rosterAvecRemplacants().map((l) => ({ ...l, participe: true, temps: '1:00' })),
    )

    expect(apercu).toHaveLength(2)
    expect(apercu[0].totalSecs).toBe(60)
    expect(apercu[1].totalSecs).toBe(60)
  })

  it('fait partager la premiere place en cas d egalite parfaite', () => {
    const apercu = classer(qualification('10:00', '20:00', '20:00', '15:00'))

    expect(apercu.map((equipe) => equipe.position)).toEqual([1, 1])
    expect(apercu.every((equipe) => equipe.estGagnante)).toBe(true)
  })

  it('peut creer une egalite par la penalite', () => {
    // Equipe A : l abandon donne 400 + 3 600 = 4 000 s, comme la plus longue seed de B.
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '100'),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '400', { estAbandon: true }),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '4000'),
      ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, '1000'),
    ])

    expect(apercu.map((equipe) => equipe.totalSecs)).toEqual([4000, 4000])
    expect(apercu.every((equipe) => equipe.position === 1 && equipe.estGagnante)).toBe(true)
  })

  it('agrege les checks et le pourcentage de completion des participants', () => {
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', {
        nbChecks: '60',
        totalChecks: '100',
      }),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '1:00', {
        nbChecks: '30',
        totalChecks: '200',
      }),
    ])

    expect(apercu[0]).toMatchObject({ checksTrouves: 90, totalChecks: 300 })
    expect(apercu[0].pourcentComplete).toBeCloseTo(0.3, 6)
  })

  it('laisse une equipe sans total quand un temps est invalide', () => {
    const apercu = classer(qualification('abc', '10:00', '1:00', '1:00'))

    expect(apercu.find((equipe) => equipe.equipeId === EQUIPE_A.id)).toMatchObject({
      totalSecs: null,
      nbResultatsEnAttente: 1,
    })
  })

  it('reporte en fin d apercu une equipe dont le total n est pas calculable', () => {
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, ''),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, ''),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
      ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
    ])

    expect(apercu[0].equipeId).toBe(EQUIPE_B.id)
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: null })
  })

  it('ne renvoie aucun pourcentage quand le total de checks est inconnu', () => {
    const apercu = classer([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', { nbChecks: '5' }),
    ])

    expect(apercu[0].totalChecks).toBeNull()
    expect(apercu[0].pourcentComplete).toBeNull()
  })
})

describe('versResultats', () => {
  it('convertit les participants en corps de requete', () => {
    const resultats = versResultats([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00:00', {
        jeuId: 3,
        seed: '  seed-42  ',
        totalChecks: '200',
        nbChecks: '150',
      }),
    ])

    expect(resultats).toEqual([
      {
        joueurId: 1,
        jeuId: 3,
        seed: 'seed-42',
        totalChecks: 200,
        nbChecks: 150,
        tempsFinalSecs: 3600,
        estAbandon: false,
      },
    ])
  })

  it('transmet le temps brut d un abandon, sans la penalite', () => {
    const resultats = versResultats([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '10:00', {
        estAbandon: true,
        nbChecks: '12',
      }),
    ])

    // La penalite est appliquee au classement, pas a la saisie.
    expect(resultats?.[0].tempsFinalSecs).toBe(600)
    expect(resultats?.[0].estAbandon).toBe(true)
    expect(resultats?.[0].nbChecks).toBe(12)
  })

  it('exclut les membres du roster qui ne participent pas', () => {
    const resultats = versResultats(rosterAvecRemplacants())

    expect(resultats).toHaveLength(4)
    expect(resultats?.map((r) => r.joueurId)).toEqual([1, 2, 3, 4])
  })

  it('refuse de convertir quand un jeu manque', () => {
    expect(
      versResultats([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', { jeuId: null })]),
    ).toBeNull()
  })

  it('transmet un temps encore inconnu comme null', () => {
    const resultats = versResultats([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '', { estAbandon: true }),
    ])

    expect(resultats?.[0].tempsFinalSecs).toBeNull()
    expect(resultats?.[0].estAbandon).toBe(true)
  })

  it('refuse de convertir quand un temps est mal forme', () => {
    expect(versResultats([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, 'nope')])).toBeNull()
  })

  it('ignore un jeu manquant sur un non-participant', () => {
    const resultats = versResultats([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00'),
      ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '', {
        participe: false,
        jeuId: null,
      }),
    ])

    expect(resultats).toHaveLength(1)
  })
})

describe('completion du match', () => {
  it('considere un match complet quand tout est saisi', () => {
    const apercu = calculerApercu(qualification('1:00', '1:00', '1:00', '2:00'))

    expect(apercu.estComplet).toBe(true)
    expect(apercu.manquants).toEqual([])
    expect(apercu.equipes.some((equipe) => equipe.estGagnante)).toBe(true)
  })

  it('signale les temps restant a saisir', () => {
    const apercu = calculerApercu(qualification('1:00', '', '1:00', ''))

    expect(apercu.estComplet).toBe(false)
    expect(apercu.manquants.some((m) => m.includes('2 temps'))).toBe(true)
    // Aucun vainqueur tant que le match n est pas termine.
    expect(apercu.equipes.every((equipe) => !equipe.estGagnante)).toBe(true)
  })

  it('signale un effectif inegal entre les deux equipes', () => {
    const apercu = calculerApercu([
      ...qualification('1:00', '1:00', '1:00', '1:00'),
      ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '1:00'),
    ])

    expect(apercu.estComplet).toBe(false)
    expect(apercu.manquants.some((m) => m.includes('le meme nombre de joueurs'))).toBe(true)
  })

  it('signale un effectif sous le minimum', () => {
    const apercu = calculerApercu([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00'),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '1:00'),
    ])

    expect(apercu.estComplet).toBe(false)
    expect(apercu.manquants.some((m) => m.includes('entre 2 et 4 joueurs'))).toBe(true)
  })

  it('signale qu il manque des participants quand une equipe est vide', () => {
    const apercu = calculerApercu([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00')])

    expect(apercu.estComplet).toBe(false)
    expect(apercu.manquants.some((m) => m.includes('participants'))).toBe(true)
  })

  it('rend une equipe sans total quand un de ses temps manque', () => {
    const apercu = calculerApercu(qualification('1:00', '', '1:00', '1:00'))

    const incomplete = apercu.equipes.find((equipe) => equipe.equipeId === EQUIPE_A.id)
    expect(incomplete).toMatchObject({ totalSecs: null, nbResultatsEnAttente: 1 })

    const complete = apercu.equipes.find((equipe) => equipe.equipeId === EQUIPE_B.id)
    expect(complete).toMatchObject({ totalSecs: 60, nbResultatsEnAttente: 0 })
  })
})

describe('validerLignes', () => {
  it('ne signale rien quand la saisie est valide', () => {
    expect(validerLignes(qualification('1:00', '1:00', '1:00', '2:00'))).toEqual([])
  })

  it('ne signale rien quand des remplacants sont decoches', () => {
    expect(validerLignes(rosterAvecRemplacants())).toEqual([])
  })

  it('ne considere pas un temps encore inconnu comme une erreur', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '', { estAbandon: true }),
    ])

    expect(problemes).toEqual([])
  })

  it('signale un jeu manquant', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', { jeuId: null }),
    ])

    expect(problemes.some((p) => p.includes('choisir un jeu'))).toBe(true)
  })

  it('signale un temps mal forme', () => {
    const problemes = validerLignes([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:2:3:4')])

    expect(problemes.some((p) => p.includes('temps invalide'))).toBe(true)
  })

  it('signale des checks trouves superieurs au total', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', {
        totalChecks: '100',
        nbChecks: '150',
      }),
    ])

    expect(problemes.some((p) => p.includes('150 checks trouves pour un total de 100'))).toBe(true)
  })
})
