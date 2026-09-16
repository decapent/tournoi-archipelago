import { useId } from 'react'
import type { ProgressionJoueur, ResultatIndice } from '../api/types'
import { styleEquipe } from '../lib/couleursEquipes'
import { formaterTemps } from '../lib/format'

/**
 * Frise des demandes d'indice, une voie par joueur.
 *
 * Ce ne sont pas des quantites cumulees mais des evenements ponctuels : la forme est donc une
 * frise, pas une courbe. L'axe du temps est celui de la progression des checks, pour que les
 * deux graphiques se lisent ensemble — un paquet de demandes tombe la ou la courbe plafonne.
 *
 * L'issue est portee par la FORME du marqueur, jamais par la seule couleur : un disque plein
 * pour un indice obtenu, un anneau creux pour un refus faute de points, une croix pour une
 * demande restee sans reponse.
 */

const MARGE = { haut: 10, droite: 16, bas: 34, gauche: 150 }
const LARGEUR = 760
const HAUTEUR_VOIE = 30
const RAYON = 5

const PAS_TEMPS = [60, 300, 600, 900, 1_800, 3_600, 7_200, 10_800, 21_600, 43_200]

export function GraphiqueIndices({ joueurs }: { joueurs: ProgressionJoueur[] }) {
  const identifiant = useId()

  const voies = joueurs.filter((joueur) => joueur.indices.length > 0)
  if (voies.length === 0) {
    return null
  }

  const hauteur = MARGE.haut + voies.length * HAUTEUR_VOIE + MARGE.bas

  // La frise partage l'echelle de la progression : les deux graphiques se superposent du
  // regard. Les checks bornent donc l'axe, meme quand le dernier indice tombe plus tot.
  const tempsMax = Math.max(
    ...joueurs.map((joueur) => joueur.secondes.at(-1) ?? 0),
    ...voies.flatMap((joueur) => joueur.indices.map((indice) => indice.secondes)),
  )

  const pasX = PAS_TEMPS.find((candidat) => tempsMax / candidat <= 8) ?? PAS_TEMPS.at(-1)!
  const limiteX = Math.max(pasX, Math.ceil(tempsMax / pasX) * pasX)

  const x = (secondes: number) =>
    MARGE.gauche + (secondes / limiteX) * (LARGEUR - MARGE.gauche - MARGE.droite)
  const y = (voie: number) => MARGE.haut + voie * HAUTEUR_VOIE + HAUTEUR_VOIE / 2

  const graduations: number[] = []
  for (let valeur = 0; valeur <= limiteX; valeur += pasX) {
    graduations.push(valeur)
  }

  return (
    <div>
      <svg
        viewBox={`0 0 ${LARGEUR} ${hauteur}`}
        className="w-full"
        role="img"
        aria-labelledby={`${identifiant}-titre`}
      >
        <title id={`${identifiant}-titre`}>
          Demandes d indice de chaque joueur, situees dans la course.
        </title>

        {graduations.map((secondes) => (
          <g key={`x-${secondes}`}>
            <line
              x1={x(secondes)}
              x2={x(secondes)}
              y1={MARGE.haut}
              y2={hauteur - MARGE.bas}
              className="stroke-bordure"
              strokeWidth={0.5}
            />
            <text
              x={x(secondes)}
              y={hauteur - MARGE.bas + 16}
              textAnchor="middle"
              className="fill-texte-doux text-[11px] tabular-nums"
            >
              {formaterTemps(secondes)}
            </text>
          </g>
        ))}

        {voies.map((joueur, voie) => (
          <g key={joueur.joueurId}>
            {/* Le fil de la voie relie les marqueurs sans rien mesurer : il sert de guide. */}
            <line
              x1={MARGE.gauche}
              x2={LARGEUR - MARGE.droite}
              y1={y(voie)}
              y2={y(voie)}
              className="stroke-bordure"
              strokeWidth={1}
            />

            <text
              x={MARGE.gauche - 10}
              y={y(voie) - 2}
              textAnchor="end"
              className="fill-texte text-[11px] font-medium"
            >
              {joueur.joueurNom}
            </text>
            <text
              x={MARGE.gauche - 10}
              y={y(voie) + 9}
              textAnchor="end"
              className="text-[10px]"
              style={styleEquipe(joueur.equipeNom)}
            >
              {joueur.equipeNom}
            </text>

            {joueur.indices.map((indice, rang) => (
              <Marqueur
                key={`${indice.secondes}-${rang}`}
                cx={x(indice.secondes)}
                cy={y(voie)}
                resultat={indice.resultat}
                infobulle={
                  `${formaterTemps(indice.secondes)} — ${LIBELLES[indice.resultat]}` +
                  (indice.pointsRestants === null
                    ? ''
                    : `, ${indice.pointsRestants} points en poche`)
                }
              />
            ))}
          </g>
        ))}
      </svg>

      <ul className="mt-2 flex flex-wrap gap-x-5 gap-y-1.5 text-xs">
        {(['Obtenu', 'Refuse', 'SansReponse'] as const).map((resultat) => (
          <li key={resultat} className="flex items-center gap-2">
            <svg width={14} height={14} aria-hidden>
              <Marqueur cx={7} cy={7} resultat={resultat} />
            </svg>
            <span className="text-texte-doux">{LIBELLES[resultat]}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

const LIBELLES: Record<ResultatIndice, string> = {
  Obtenu: 'indice obtenu',
  Refuse: 'refuse, pas assez de points',
  SansReponse: 'sans reponse, objet introuvable',
}

/**
 * L'anneau et la croix portent un liseré de la couleur du fond : deux demandes a quelques
 * secondes d'intervalle se chevauchent, et c'est ce liseré qui les garde lisibles.
 */
function Marqueur({
  cx,
  cy,
  resultat,
  infobulle,
}: {
  cx: number
  cy: number
  resultat: ResultatIndice
  infobulle?: string
}) {
  const titre = infobulle === undefined ? null : <title>{infobulle}</title>

  if (resultat === 'SansReponse') {
    const b = RAYON - 1
    return (
      <g className="stroke-texte-doux" strokeWidth={1.6} strokeLinecap="round">
        {titre}
        <line x1={cx - b} y1={cy - b} x2={cx + b} y2={cy + b} />
        <line x1={cx - b} y1={cy + b} x2={cx + b} y2={cy - b} />
      </g>
    )
  }

  return (
    <circle
      cx={cx}
      cy={cy}
      r={RAYON}
      className={
        resultat === 'Obtenu'
          ? 'fill-accent stroke-panneau'
          : 'fill-panneau stroke-alerte'
      }
      strokeWidth={2}
    >
      {titre}
    </circle>
  )
}
