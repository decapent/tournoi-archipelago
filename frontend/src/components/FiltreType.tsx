import { TYPES_MATCH } from '../api/types'
import type { TypeMatch } from '../api/types'

interface Props {
  valeur: TypeMatch | undefined
  onChange: (type: TypeMatch | undefined) => void
}

/** Selecteur QUALIFICATION / TOURNOI, avec une option « tous ». */
export function FiltreType({ valeur, onChange }: Props) {
  return (
    <label className="flex items-center gap-2">
      <span className="etiquette">Type</span>
      <select
        className="champ"
        value={valeur ?? ''}
        onChange={(evenement) =>
          onChange(evenement.target.value === '' ? undefined : (evenement.target.value as TypeMatch))
        }
      >
        <option value="">Tous</option>
        {TYPES_MATCH.map((type) => (
          <option key={type} value={type}>
            {type}
          </option>
        ))}
      </select>
    </label>
  )
}
