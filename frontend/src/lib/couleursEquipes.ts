/**
 * Charte de couleurs des equipes du tournoi.
 *
 * La charte d'origine est faite de couleurs saturees sur fond noir pur. L'application pose ses
 * tableaux sur des panneaux gris-bleu (#1b1f29), ou le bleu et le rouge purs deviennent
 * illisibles. Chaque teinte est donc conservee mais reglee pour passer 4,5:1 de contraste
 * texte sur ce fond — le ratio mesure figure en commentaire.
 *
 * La paire la plus serree reste le vert et le cyan (13,2 de Delta E en vision normale, sous la
 * cible de 15) : la charte prescrit un vert pur et un cyan purs, voisins par nature. Ce n'est
 * pas genant ici, le nom de l'equipe etant toujours ecrit en clair a cote de sa couleur — la
 * couleur ne porte jamais seule l'identite.
 */
const CHARTE: Record<string, string> = {
  // nom normalise      couleur     charte    contraste
  ojmi: '#EDD84F', //                #FFFF00   11,40:1
  lesnous: '#E86FC4', //             #FF00FF    5,89:1
  nomsland: '#6E9BF0', //            #0000FF    5,97:1
  mynameispending: '#F59B2E', //     #FF8000    7,54:1
  whatisaname: '#E85E62', //         #FF0000    4,87:1
  agreatteam: '#D6DAE8', //          #FFFFFF   11,81:1
  elsaipasgg: '#4FD4D4', //          #00FFFF    9,18:1
  '4g0l': '#6FD46F', //              #00FF00    8,88:1
}

/**
 * Couleur d'une equipe, ou <c>undefined</c> si elle n'est pas dans la charte : le texte garde
 * alors la couleur par defaut plutot qu'une teinte inventee.
 *
 * Le rapprochement ignore casse, accents et ponctuation, ce qui laisse passer les variations
 * d'ecriture d'un meme nom (« No M's Land » et « No Ms Land », « O.J.M.I. » et « OJMI »).
 */
export function couleurEquipe(nom: string | null | undefined): string | undefined {
  return nom === null || nom === undefined ? undefined : CHARTE[normaliser(nom)]
}

/** Style pret a poser sur un element. Vide hors charte, pour ne rien surcharger. */
export function styleEquipe(nom: string | null | undefined): { color: string } | undefined {
  const couleur = couleurEquipe(nom)
  return couleur === undefined ? undefined : { color: couleur }
}

function normaliser(valeur: string): string {
  return valeur
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
}
