import { useId, useState } from 'react'
import type { ProgressionJoueur } from '../api/types'
import { formaterTemps } from '../lib/format'

/**
 * Courbes de checks cumules dans le temps, une par joueur.
 *
 * Trace en SVG plutot qu'avec une librairie : un escalier monotone n'a besoin ni d'echelles
 * savantes ni d'interactivite lourde, et le projet reste sans dependance de graphiques.
 */

const MARGE = { haut: 12, droite: 16, bas: 34, gauche: 46 }
const LARGEUR = 760
const HAUTEUR = 300

/** Teintes distinctes et lisibles sur le fond sombre, reprises dans la legende. */
const COULEURS = [
  'oklch(0.75 0.16 165)',
  'oklch(0.75 0.16 60)',
  'oklch(0.72 0.17 300)',
  'oklch(0.75 0.15 220)',
  'oklch(0.72 0.18 25)',
  'oklch(0.80 0.15 110)',
  'oklch(0.72 0.15 340)',
  'oklch(0.78 0.12 190)',
]

const PAS_TEMPS = [60, 300, 600, 900, 1_800, 3_600, 7_200, 10_800, 21_600, 43_200]
const PAS_CHECKS = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1_000]

export function GraphiqueProgression({ joueurs }: { joueurs: ProgressionJoueur[] }) {
  const identifiant = useId()
  const [survole, setSurvole] = useState<number | null>(null)

  const traces = joueurs.filter((joueur) => joueur.secondes.length > 0)
  if (traces.length === 0) {
    return null
  }

  // Les deux equipes se distinguent au trait — plein ou pointille — en plus de la couleur.
  const equipes = [...new Set(traces.map((joueur) => joueur.equipeNom))]

  const tempsMax = Math.max(...traces.map((joueur) => joueur.secondes.at(-1) ?? 0))
  const checksMax = Math.max(...traces.map((joueur) => joueur.secondes.length))

  const pasX = choisirPas(PAS_TEMPS, tempsMax)
  const pasY = choisirPas(PAS_CHECKS, checksMax)
  const limiteX = Math.max(pasX, Math.ceil(tempsMax / pasX) * pasX)
  const limiteY = Math.max(pasY, Math.ceil(checksMax / pasY) * pasY)

  const x = (secondes: number) =>
    MARGE.gauche + (secondes / limiteX) * (LARGEUR - MARGE.gauche - MARGE.droite)
  const y = (checks: number) =>
    HAUTEUR - MARGE.bas - (checks / limiteY) * (HAUTEUR - MARGE.haut - MARGE.bas)

  return (
    <div>
      <svg
        viewBox={`0 0 ${LARGEUR} ${HAUTEUR}`}
        className="w-full"
        role="img"
        aria-labelledby={`${identifiant}-titre`}
      >
        <title id={`${identifiant}-titre`}>
          Checks cumules par joueur, de zero a {formaterTemps(tempsMax)} de course.
        </title>

        {graduations(limiteY, pasY).map((checks) => (
          <g key={`y-${checks}`}>
            <line
              x1={MARGE.gauche}
              x2={LARGEUR - MARGE.droite}
              y1={y(checks)}
              y2={y(checks)}
              className="stroke-bordure"
              strokeWidth={checks === 0 ? 1 : 0.5}
            />
            <text
              x={MARGE.gauche - 8}
              y={y(checks) + 4}
              textAnchor="end"
              className="fill-texte-doux text-[11px] tabular-nums"
            >
              {checks}
            </text>
          </g>
        ))}

        {graduations(limiteX, pasX).map((secondes) => (
          <text
            key={`x-${secondes}`}
            x={x(secondes)}
            y={HAUTEUR - MARGE.bas + 16}
            textAnchor="middle"
            className="fill-texte-doux text-[11px] tabular-nums"
          >
            {formaterTemps(secondes)}
          </text>
        ))}

        <text
          x={LARGEUR - MARGE.droite}
          y={HAUTEUR - 6}
          textAnchor="end"
          className="fill-texte-doux text-[11px]"
        >
          temps de course
        </text>

        {traces.map((joueur, rang) => {
          const estEstompe = survole !== null && survole !== joueur.joueurId

          return (
            <path
              key={joueur.joueurId}
              d={escalier(joueur.secondes, x, y)}
              fill="none"
              stroke={COULEURS[rang % COULEURS.length]}
              strokeWidth={survole === joueur.joueurId ? 2.5 : 1.6}
              strokeDasharray={equipes.indexOf(joueur.equipeNom) === 1 ? '5 3' : undefined}
              strokeLinejoin="round"
              opacity={estEstompe ? 0.25 : 1}
            />
          )
        })}
      </svg>

      <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 text-xs">
        {traces.map((joueur, rang) => (
          <li
            key={joueur.joueurId}
            className="flex cursor-default items-center gap-2"
            onMouseEnter={() => setSurvole(joueur.joueurId)}
            onMouseLeave={() => setSurvole(null)}
          >
            <span
              aria-hidden
              className="inline-block h-0.5 w-5 rounded"
              style={{ backgroundColor: COULEURS[rang % COULEURS.length] }}
            />
            <span className="font-medium">{joueur.joueurNom}</span>
            <span className="text-texte-doux">
              {joueur.equipeNom}
              {joueur.jeuNom !== '' && ` · ${joueur.jeuNom}`} · {joueur.secondes.length} checks
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

/**
 * Escalier des checks cumules : le compte ne monte qu'a l'instant d'un check, et le palier
 * se prolonge jusqu'au suivant.
 */
function escalier(
  secondes: readonly number[],
  x: (secondes: number) => number,
  y: (checks: number) => number,
): string {
  const morceaux = [`M ${x(0)} ${y(0)}`]

  secondes.forEach((instant, index) => {
    morceaux.push(`L ${x(instant)} ${y(index)}`, `L ${x(instant)} ${y(index + 1)}`)
  })

  return morceaux.join(' ')
}

/** Plus petit pas de la liste donnant au plus huit graduations. */
function choisirPas(pas: readonly number[], maximum: number): number {
  return pas.find((candidat) => maximum / candidat <= 8) ?? pas[pas.length - 1]
}

function graduations(limite: number, pas: number): number[] {
  const valeurs: number[] = []
  for (let valeur = 0; valeur <= limite; valeur += pas) {
    valeurs.push(valeur)
  }
  return valeurs
}
