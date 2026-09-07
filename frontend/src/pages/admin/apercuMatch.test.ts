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
    jeuId: 1,
    seed: '',
    totalChecks: '',
    nbChecks: '',
    temps,
    ...extras,
  }
}

const QUATRE_LIGNES = (tempsA1: string, tempsA2: string, tempsB1: string, tempsB2: string) => [
  ligne(1, 'Alice', 10, 'Alice & Bob', tempsA1),
  ligne(2, 'Bob', 10, 'Alice & Bob', tempsA2),
  ligne(3, 'Chloe', 20, 'Chloe & David', tempsB1),
  ligne(4, 'David', 20, 'Chloe & David', tempsB2),
]

describe('calculerApercu', () => {
  it('regroupe par equipe et donne la victoire au plus petit total', () => {
    const apercu = calculerApercu(QUATRE_LIGNES('1:00:00', '50:00', '1:10:00', '58:00'))

    expect(apercu).toHaveLength(2)
    expect(apercu[0]).toMatchObject({
      equipeId: 10,
      position: 1,
      estGagnante: true,
      totalSecs: 6600,
      estAbandon: false,
    })
    expect(apercu[1]).toMatchObject({ equipeId: 20, position: 2, estGagnante: false, totalSecs: 7680 })
  })

  it('classe une equipe avec abandon apres une equipe complete plus lente', () => {
    const apercu = calculerApercu(QUATRE_LIGNES('1:00', '', '5:00:00', '5:00:00'))

    expect(apercu[0].equipeId).toBe(20)
    expect(apercu[1]).toMatchObject({ equipeId: 10, estAbandon: true, estGagnante: false })
  })

  it('departage deux abandons par le nombre de checks trouves', () => {
    const apercu = calculerApercu([
      ligne(1, 'Alice', 10, 'Alice & Bob', '', { nbChecks: '30' }),
      ligne(2, 'Bob', 10, 'Alice & Bob', '', { nbChecks: '10' }),
      ligne(3, 'Chloe', 20, 'Chloe & David', '', { nbChecks: '70' }),
      ligne(4, 'David', 20, 'Chloe & David', '', { nbChecks: '50' }),
    ])

    expect(apercu[0]).toMatchObject({ equipeId: 20, checksTrouves: 120, position: 1 })
    expect(apercu[1]).toMatchObject({ equipeId: 10, checksTrouves: 40, position: 2 })
  })

  it('fait partager la premiere place en cas d egalite parfaite', () => {
    const apercu = calculerApercu(QUATRE_LIGNES('10:00', '20:00', '15:00', '15:00'))

    expect(apercu.map((equipe) => equipe.position)).toEqual([1, 1])
    expect(apercu.every((equipe) => equipe.estGagnante)).toBe(true)
  })

  it('agrege les checks et le pourcentage de completion du duo', () => {
    const apercu = calculerApercu([
      ligne(1, 'Alice', 10, 'Alice & Bob', '1:00', { nbChecks: '60', totalChecks: '100' }),
      ligne(2, 'Bob', 10, 'Alice & Bob', '1:00', { nbChecks: '30', totalChecks: '200' }),
    ])

    expect(apercu[0]).toMatchObject({ checksTrouves: 90, totalChecks: 300 })
    expect(apercu[0].pourcentComplete).toBeCloseTo(0.3, 6)
  })

  it('traite un temps invalide comme absent sans casser l apercu', () => {
    const apercu = calculerApercu(QUATRE_LIGNES('abc', '10:00', '1:00', '1:00'))

    expect(apercu.find((equipe) => equipe.equipeId === 10)).toMatchObject({
      estAbandon: true,
      totalSecs: 600,
    })
  })

  it('ne renvoie aucun pourcentage quand le total de checks est inconnu', () => {
    const apercu = calculerApercu([ligne(1, 'Alice', 10, 'Alice & Bob', '1:00', { nbChecks: '5' })])

    expect(apercu[0].totalChecks).toBeNull()
    expect(apercu[0].pourcentComplete).toBeNull()
  })
})

describe('versResultats', () => {
  it('convertit les lignes completes en corps de requete', () => {
    const resultats = versResultats([
      ligne(1, 'Alice', 10, 'Alice & Bob', '1:00:00', {
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

  it('transmet un abandon avec un temps nul', () => {
    const resultats = versResultats([ligne(1, 'Alice', 10, 'Alice & Bob', '', { nbChecks: '12' })])

    expect(resultats?.[0].tempsFinalSecs).toBeNull()
    expect(resultats?.[0].nbChecks).toBe(12)
  })

  it('refuse de convertir quand un jeu manque', () => {
    expect(versResultats([ligne(1, 'Alice', 10, 'Alice & Bob', '1:00', { jeuId: null })])).toBeNull()
  })

  it('refuse de convertir quand un temps est mal forme', () => {
    expect(versResultats([ligne(1, 'Alice', 10, 'Alice & Bob', 'nope')])).toBeNull()
  })
})

describe('validerLignes', () => {
  it('ne signale rien quand la saisie est valide', () => {
    expect(validerLignes(QUATRE_LIGNES('1:00', '1:00', '1:00', ''))).toEqual([])
  })

  it('signale un jeu manquant', () => {
    const problemes = validerLignes([ligne(1, 'Alice', 10, 'Alice & Bob', '1:00', { jeuId: null })])

    expect(problemes).toHaveLength(1)
    expect(problemes[0]).toContain('choisir un jeu')
  })

  it('signale un temps mal forme', () => {
    const problemes = validerLignes([ligne(1, 'Alice', 10, 'Alice & Bob', '1:2:3:4')])

    expect(problemes[0]).toContain('temps invalide')
  })

  it('signale des checks trouves superieurs au total', () => {
    const problemes = validerLignes([
      ligne(1, 'Alice', 10, 'Alice & Bob', '1:00', { totalChecks: '100', nbChecks: '150' }),
    ])

    expect(problemes[0]).toContain('150 checks trouves pour un total de 100')
  })
})
