import { describe, expect, it } from 'vitest'
import {
  ABANDON,
  analyserTemps,
  formaterDate,
  formaterNombre,
  formaterPourcent,
  formaterTemps,
  totaliserEquipe,
} from './format'

describe('formaterTemps', () => {
  it('affiche mm:ss sous une heure', () => {
    expect(formaterTemps(0)).toBe('00:00')
    expect(formaterTemps(59)).toBe('00:59')
    expect(formaterTemps(90)).toBe('01:30')
    expect(formaterTemps(3599)).toBe('59:59')
  })

  it('affiche h:mm:ss a partir d une heure', () => {
    expect(formaterTemps(3600)).toBe('1:00:00')
    expect(formaterTemps(3661)).toBe('1:01:01')
    expect(formaterTemps(45296)).toBe('12:34:56')
  })

  it('signale un abandon quand le temps est absent', () => {
    expect(formaterTemps(null)).toBe(ABANDON)
    expect(formaterTemps(undefined)).toBe(ABANDON)
  })
})

describe('analyserTemps', () => {
  it('lit les trois formats acceptes', () => {
    expect(analyserTemps('90')).toBe(90)
    expect(analyserTemps('1:30')).toBe(90)
    expect(analyserTemps('01:30')).toBe(90)
    expect(analyserTemps('1:01:01')).toBe(3661)
    expect(analyserTemps('12:34:56')).toBe(45296)
  })

  it('tolere les espaces autour de la saisie', () => {
    expect(analyserTemps('  2:00  ')).toBe(120)
  })

  it('interprete une saisie vide comme un abandon', () => {
    expect(analyserTemps('')).toBeNull()
    expect(analyserTemps('   ')).toBeNull()
  })

  it('rejette les saisies invalides', () => {
    expect(analyserTemps('abc')).toBeUndefined()
    expect(analyserTemps('1:2:3:4')).toBeUndefined()
    expect(analyserTemps('1:99')).toBeUndefined()
    expect(analyserTemps('1:30:99')).toBeUndefined()
    expect(analyserTemps('-5')).toBeUndefined()
    expect(analyserTemps('1,5')).toBeUndefined()
  })

  it('rejette un temps nul, qui ne distingue pas un abandon', () => {
    expect(analyserTemps('0')).toBeUndefined()
    expect(analyserTemps('00:00')).toBeUndefined()
  })

  it('fait l aller-retour avec formaterTemps', () => {
    for (const secondes of [1, 59, 60, 90, 3600, 3661, 45296]) {
      expect(analyserTemps(formaterTemps(secondes))).toBe(secondes)
    }
  })
})

describe('formaterPourcent', () => {
  it('convertit une part en pourcentage', () => {
    expect(formaterPourcent(0.5)).toBe('50,0 %')
    expect(formaterPourcent(1)).toBe('100,0 %')
    expect(formaterPourcent(0.1234, 2)).toBe('12,34 %')
  })

  it('affiche un tiret quand la part est absente', () => {
    expect(formaterPourcent(null)).toBe('—')
    expect(formaterPourcent(undefined)).toBe('—')
  })
})

describe('formaterNombre', () => {
  // La locale fr-CA utilise la virgule comme separateur decimal.
  it('affiche un tiret quand la valeur est absente', () => {
    expect(formaterNombre(null)).toBe('—')
  })

  it('respecte le nombre de decimales demande', () => {
    expect(formaterNombre(12.345, 1)).toBe('12,3')
    expect(formaterNombre(12, 0)).toBe('12')
  })
})

describe('formaterDate', () => {
  it('presente une date ISO en jour-mois-annee', () => {
    expect(formaterDate('2026-09-05')).toBe('05-09-2026')
  })

  it('ignore la partie heure quand elle est presente', () => {
    expect(formaterDate('2026-09-05T00:00:00')).toBe('05-09-2026')
  })
})

describe('totaliserEquipe', () => {
  it('additionne les temps des deux membres', () => {
    expect(totaliserEquipe([3600, 3000])).toEqual({ totalSecs: 6600, estAbandon: false })
  })

  it('marque un abandon des qu un membre n a pas de temps', () => {
    expect(totaliserEquipe([3600, null])).toEqual({ totalSecs: 3600, estAbandon: true })
  })

  it('ne renvoie aucun total quand personne n a de temps', () => {
    expect(totaliserEquipe([null, null])).toEqual({ totalSecs: null, estAbandon: true })
  })
})
