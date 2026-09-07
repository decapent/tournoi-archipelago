import { describe, expect, it } from 'vitest'
import { calculerApercu, validerLignes, versResultats } from './apercuMatch'
import type { LigneSaisie } from './apercuMatch'

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

describe('calculerApercu', () => {
  it('regroupe par equipe et donne la victoire au plus petit total', () => {
    const apercu = calculerApercu(qualification('1:00:00', '50:00', '1:10:00', '58:00'))

    expect(apercu).toHaveLength(2)
    expect(apercu[0]).toMatchObject({
      equipeId: EQUIPE_A.id,
      position: 1,
      estGagnante: true,
      totalSecs: 6600,
      estAbandon: false,
    })
    expect(apercu[1]).toMatchObject({
      equipeId: EQUIPE_B.id,
      position: 2,
      estGagnante: false,
      totalSecs: 7680,
    })
  })

  it('ignore les membres du roster qui ne participent pas', () => {
    const apercu = calculerApercu(rosterAvecRemplacants())

    expect(apercu).toHaveLength(2)
    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: 1200, estAbandon: false })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 2400, estAbandon: false })
  })

  it('additionne les temps de trois participants en demi-finale', () => {
    const apercu = calculerApercu([
      ...qualification('5:00', '5:00', '10:00', '10:00'),
      ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '5:00'),
      ligne(7, '_annix86', EQUIPE_B.id, EQUIPE_B.nom, '10:00'),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_A.id, totalSecs: 900 })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_B.id, totalSecs: 1800 })
  })

  it('additionne les temps de quatre participants en finale', () => {
    const apercu = calculerApercu(
      rosterAvecRemplacants().map((l) => ({ ...l, participe: true, temps: '1:00' })),
    )

    expect(apercu).toHaveLength(2)
    expect(apercu[0].totalSecs).toBe(240)
    expect(apercu[1].totalSecs).toBe(240)
  })

  it('classe une equipe avec abandon apres une equipe complete plus lente', () => {
    const apercu = calculerApercu(qualification('1:00', '', '5:00:00', '5:00:00'))

    expect(apercu[0].equipeId).toBe(EQUIPE_B.id)
    expect(apercu[1]).toMatchObject({
      equipeId: EQUIPE_A.id,
      estAbandon: true,
      estGagnante: false,
    })
  })

  it('departage deux abandons par le nombre de checks trouves', () => {
    const apercu = calculerApercu([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '', { nbChecks: '30' }),
      ligne(2, 'Moi_Sophia', EQUIPE_A.id, EQUIPE_A.nom, '', { nbChecks: '10' }),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '', { nbChecks: '70' }),
      ligne(4, '_C_La_Sorciere', EQUIPE_B.id, EQUIPE_B.nom, '', { nbChecks: '50' }),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: EQUIPE_B.id, checksTrouves: 120, position: 1 })
    expect(apercu[1]).toMatchObject({ equipeId: EQUIPE_A.id, checksTrouves: 40, position: 2 })
  })

  it('fait partager la premiere place en cas d egalite parfaite', () => {
    const apercu = calculerApercu(qualification('10:00', '20:00', '15:00', '15:00'))

    expect(apercu.map((equipe) => equipe.position)).toEqual([1, 1])
    expect(apercu.every((equipe) => equipe.estGagnante)).toBe(true)
  })

  it('agrege les checks et le pourcentage de completion des participants', () => {
    const apercu = calculerApercu([
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

  it('traite un temps invalide comme absent sans casser l apercu', () => {
    const apercu = calculerApercu(qualification('abc', '10:00', '1:00', '1:00'))

    expect(apercu.find((equipe) => equipe.equipeId === EQUIPE_A.id)).toMatchObject({
      estAbandon: true,
      totalSecs: 600,
    })
  })

  it('ne renvoie aucun pourcentage quand le total de checks est inconnu', () => {
    const apercu = calculerApercu([
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
      },
    ])
  })

  it('exclut les membres du roster qui ne participent pas', () => {
    const resultats = versResultats(rosterAvecRemplacants())

    expect(resultats).toHaveLength(4)
    expect(resultats?.map((r) => r.joueurId)).toEqual([1, 2, 3, 4])
  })

  it('transmet un abandon avec un temps nul', () => {
    const resultats = versResultats([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '', { nbChecks: '12' }),
    ])

    expect(resultats?.[0].tempsFinalSecs).toBeNull()
    expect(resultats?.[0].nbChecks).toBe(12)
  })

  it('refuse de convertir quand un jeu manque', () => {
    expect(
      versResultats([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', { jeuId: null })]),
    ).toBeNull()
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

  it('refuse de convertir quand un temps est mal forme', () => {
    expect(versResultats([ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, 'nope')])).toBeNull()
  })
})

describe('validerLignes', () => {
  it('ne signale rien quand la saisie est valide', () => {
    expect(validerLignes(qualification('1:00', '1:00', '1:00', ''))).toEqual([])
  })

  it('ne signale rien quand des remplacants sont decoches', () => {
    expect(validerLignes(rosterAvecRemplacants())).toEqual([])
  })

  it('signale un effectif inegal entre les deux equipes', () => {
    const lignes = [
      ...qualification('1:00', '1:00', '1:00', '1:00'),
      ligne(5, 'Moi_Flodarien', EQUIPE_A.id, EQUIPE_A.nom, '1:00'),
    ]

    const problemes = validerLignes(lignes)

    expect(problemes.some((p) => p.includes('le meme nombre de joueurs'))).toBe(true)
  })

  it('signale un effectif sous le minimum', () => {
    const lignes = [
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00'),
      ligne(3, '_oli_an22', EQUIPE_B.id, EQUIPE_B.nom, '1:00'),
    ]

    expect(validerLignes(lignes).some((p) => p.includes('entre 2 et 4 joueurs'))).toBe(true)
  })

  it('signale un jeu manquant', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', { jeuId: null }),
    ])

    expect(problemes.some((p) => p.includes('choisir un jeu'))).toBe(true)
  })

  it('signale un temps mal forme', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:2:3:4'),
    ])

    expect(problemes.some((p) => p.includes('temps invalide'))).toBe(true)
  })

  it('signale des checks trouves superieurs au total', () => {
    const problemes = validerLignes([
      ligne(1, 'Moi_Eva', EQUIPE_A.id, EQUIPE_A.nom, '1:00', {
        totalChecks: '100',
        nbChecks: '150',
      }),
    ])

    expect(
      problemes.some((p) => p.includes('150 checks trouves pour un total de 100')),
    ).toBe(true)
  })
})
