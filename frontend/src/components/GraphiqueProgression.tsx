import { useId, useState } from 'react'
import type { ProgressionJoueur } from '../api/types'
import { couleurEquipe, styleEquipe } from '../lib/couleursEquipes'
import { formaterTemps } from '../lib/format'

/**
 * Courbes de checks cumules dans le temps, une par joueur.
 *
 * La courbe trace TOUTES les lignes du journal, rafales de collecte et de liberation
 * comprises. Elles ne sont pas des checks trouves — le tableau au-dessus ne les compte pas —
 * mais ce sont elles qui menent la courbe jusqu'a la taille du monde, a l'instant exact de la
 * completion. Sans elles, la courbe s'arretait au dernier check et paraissait tronquee face
 * au temps affiche a cote.
 *
 * Trace en SVG plutot qu'avec une librairie : un escalier monotone n'a besoin ni d'echelles
 * savantes ni d'interactivite lourde, et le projet reste sans dependance de graphiques.
 */

const MARGE = { haut: 12, droite: 16, bas: 34, gauche: 46 }
const LARGEUR = 760
const HAUTEUR = 300

/**
 * Teintes de repli, pour une equipe absente de la charte du tournoi. Lisibles sur le fond
 * sombre, comme celles de la charte.
 */
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

/**
 * La couleur portant l'equipe, c'est le trait qui separe ses joueurs entre eux. Quatre motifs
 * suffisent : c'est l'effectif maximal d'une equipe, en finale.
 */
const TRAITS: (string | undefined)[] = [undefined, '7 4', '2 3', '11 3 2 3']

const PAS_TEMPS = [60, 300, 600, 900, 1_800, 3_600, 7_200, 10_800, 21_600, 43_200]
const PAS_CHECKS = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1_000]

export function GraphiqueProgression({ joueurs }: { joueurs: ProgressionJoueur[] }) {
  const identifiant = useId()
  const [survole, setSurvole] = useState<number | null>(null)

  const traces = joueurs
    .filter((joueur) => joueur.secondes.length + joueur.secondesRafale.length > 0)
    .map((joueur) => ({
      ...joueur,
      // Un seul escalier, du premier check jusqu'au vidage du monde.
      points: [...joueur.secondes, ...joueur.secondesRafale].sort((a, b) => a - b),
    }))

  if (traces.length === 0) {
    return null
  }

  // Chaque equipe porte sa couleur de la charte, et ses joueurs se separent au trait. Une
  // equipe hors charte retombe sur une teinte de repli, pour rester distincte des autres.
  const equipes = [...new Set(traces.map((joueur) => joueur.equipeNom))]

  const couleurDe = (equipeNom: string) =>
    couleurEquipe(equipeNom) ?? COULEURS[equipes.indexOf(equipeNom) % COULEURS.length]

  const traitDe = (joueur: (typeof traces)[number]) =>
    TRAITS[
      traces.filter((autre) => autre.equipeNom === joueur.equipeNom).indexOf(joueur) %
        TRAITS.length
    ]

  // Un seuil par jeu : la taille de son monde. Les deux equipes courent les memes seeds,
  // les joueurs d'un meme jeu partagent donc le meme total.
  const arrivees = [
    ...new Map(
      traces
        .filter((joueur) => joueur.totalChecks !== null && joueur.jeuNom !== '')
        .map((joueur) => [joueur.jeuNom, joueur.totalChecks!]),
    ),
  ].sort((a, b) => b[1] - a[1])

  const tempsMax = Math.max(...traces.map((joueur) => joueur.points.at(-1) ?? 0))
  const checksMax = Math.max(
    ...traces.map((joueur) => joueur.points.length),
    ...arrivees.map(([, total]) => total),
  )

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
          Checks cumules par joueur, de zero a {formaterTemps(tempsMax)} de course. Un seuil
          en pointille marque la taille du monde de chaque jeu.
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

        {arrivees.map(([jeu, total]) => (
          <g key={`arrivee-${jeu}`}>
            {/*
              Pointille assume : sur une grille ce serait du bruit, mais ici c'est justement
              un seuil, et le trait discontinu est ce qui le distingue d'une graduation.
            */}
            <line
              x1={MARGE.gauche}
              x2={LARGEUR - MARGE.droite}
              y1={y(total)}
              y2={y(total)}
              className="stroke-texte-doux"
              strokeWidth={1}
              strokeDasharray="6 4"
            />
            <text
              x={LARGEUR - MARGE.droite}
              y={y(total) - 5}
              textAnchor="end"
              className="fill-texte-doux text-[11px]"
            >
              {jeu} · {total} checks
            </text>
          </g>
        ))}

        {traces.map((joueur) => {
          const estEstompe = survole !== null && survole !== joueur.joueurId

          return (
            <path
              key={joueur.joueurId}
              d={escalier(joueur.points, x, y)}
              fill="none"
              stroke={couleurDe(joueur.equipeNom)}
              strokeWidth={survole === joueur.joueurId ? 2.5 : 1.6}
              strokeDasharray={traitDe(joueur)}
              strokeLinejoin="round"
              opacity={estEstompe ? 0.25 : 1}
            />
          )
        })}
      </svg>

      <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 text-xs">
        {traces.map((joueur) => (
          <li
            key={joueur.joueurId}
            className="flex cursor-default items-center gap-2"
            onMouseEnter={() => setSurvole(joueur.joueurId)}
            onMouseLeave={() => setSurvole(null)}
          >
            <svg width={22} height={6} aria-hidden className="shrink-0">
              <line
                x1={0}
                y1={3}
                x2={22}
                y2={3}
                stroke={couleurDe(joueur.equipeNom)}
                strokeWidth={2}
                strokeDasharray={traitDe(joueur)}
              />
            </svg>
            <span className="font-medium">{joueur.joueurNom}</span>
            <span style={styleEquipe(joueur.equipeNom)}>{joueur.equipeNom}</span>
            <span className="text-texte-doux">
              {joueur.jeuNom !== '' && `${joueur.jeuNom} · `}
              {joueur.secondes.length} checks trouves
              {joueur.secondesRafale.length > 0 && ` sur ${joueur.points.length}`}
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
