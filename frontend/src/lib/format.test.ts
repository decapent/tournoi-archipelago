import { describe, expect, it } from 'vitest'
import {
  ABSENT,
  analyserTemps,
  formaterDate,
  formaterNombre,
  formaterPourcent,
  formaterTemps,
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

  it('affiche le marqueur d absence quand le temps manque', () => {
    expect(formaterTemps(null)).toBe(ABSENT)
    expect(formaterTemps(undefined)).toBe(ABSENT)
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

  // Un abandon porte l'instant ou le joueur a arrete : un temps reste requis.
  it('rejette une saisie vide', () => {
    expect(analyserTemps('')).toBeUndefined()
    expect(analyserTemps('   ')).toBeUndefined()
  })

  it('rejette les saisies invalides', () => {
    expect(analyserTemps('abc')).toBeUndefined()
    expect(analyserTemps('1:2:3:4')).toBeUndefined()
    expect(analyserTemps('1:99')).toBeUndefined()
    expect(analyserTemps('1:30:99')).toBeUndefined()
    expect(analyserTemps('-5')).toBeUndefined()
    expect(analyserTemps('1,5')).toBeUndefined()
  })

  it('rejette un temps nul', () => {
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

  it('affiche le marqueur d absence quand la part manque', () => {
    expect(formaterPourcent(null)).toBe(ABSENT)
    expect(formaterPourcent(undefined)).toBe(ABSENT)
  })
})

describe('formaterNombre', () => {
  // La locale fr-CA utilise la virgule comme separateur decimal.
  it('affiche le marqueur d absence quand la valeur manque', () => {
    expect(formaterNombre(null)).toBe(ABSENT)
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
